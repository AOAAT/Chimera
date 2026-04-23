using System.Collections.Generic;
using UnityEngine;

public class ChimeraAIController : MonoBehaviour
{
    private RuntimeChimeraData runtimeData;
    private Transform currentTarget;
    private Rigidbody2D rb;
    private BuffManager myBuffMgr;
    private DamageReceiver myReceiver; // 缓存自身受击组件，用于判定阵营

    [Header("=== 性能优化设定 ===")]
    [Tooltip("AI 重新搜寻目标的间隔时间 (秒)，推荐 0.1~0.2")]
    public float SearchInterval = 0.2f;
    private float searchTimer = 0f;

    [Header("=== 动态物理计算结果 ===")]
    public float CurrentSpeed;

    [Header("=== 物理状态 ===")]
    public bool isStaggered = false;
    private float staggerTimer = 0f;
    public bool isDashing = false;
    private float dashTimer = 0f;

    // AI 走位核心阈值
    private float maxWeaponRange = 0f;
    private float minWeaponRange = 0f;
    private float optimalFireRange = 0f;

    public void Initialize(RuntimeChimeraData data)
    {
        runtimeData = data;
        rb = GetComponent<Rigidbody2D>();
        myReceiver = GetComponent<DamageReceiver>(); // 缓存自身阵营

        if (rb != null) rb.drag = 5f;

        myBuffMgr = GetComponent<BuffManager>();
        if (myBuffMgr != null) myBuffMgr.OnBuffsChanged += RecalculateSpeedAndRanges;

        RecalculateSpeedAndRanges();
    }

    private void OnDestroy()
    {
        if (myBuffMgr != null)
        {
            myBuffMgr.OnBuffsChanged -= RecalculateSpeedAndRanges;
        }
    }

    /// <summary>
    /// 核心计算：基于当前装备和 Buff 算出机甲的物理性能上限
    /// </summary>
    public void RecalculateSpeedAndRanges()
    {
        if (runtimeData == null) return;

        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;

        float currentEnginePower = runtimeData.TotalEnginePower;
        if (myBuffMgr != null && myBuffMgr.BuffStatModifiers.ContainsKey(StatType.EnginePower))
        {
            currentEnginePower += myBuffMgr.BuffStatModifiers[StatType.EnginePower];
        }

        CurrentSpeed = GameFormulas.CalcMoveSpeed(currentEnginePower, runtimeData.TotalMass, speedMult);

        // --- 重新计算射程阈值 ---
        maxWeaponRange = 0f;
        minWeaponRange = 0f;
        optimalFireRange = float.MaxValue;

        if (runtimeData.EquippedWeapons.Count == 0)
        {
            optimalFireRange = 1.5f * distMult;
        }
        else
        {
            for (int i = 0; i < runtimeData.EquippedWeapons.Count; i++)
            {
                var wpn = runtimeData.EquippedWeapons[i];
                float rawMax = wpn.GetStat(StatType.MaxRange);
                float rawMin = wpn.GetStat(StatType.MinRange);

                // 叠加 Buff 影响
                if (myBuffMgr != null)
                {
                    if (myBuffMgr.BuffStatModifiers.ContainsKey(StatType.MaxRange)) rawMax += myBuffMgr.BuffStatModifiers[StatType.MaxRange];
                    if (myBuffMgr.BuffStatModifiers.ContainsKey(StatType.MinRange)) rawMin += myBuffMgr.BuffStatModifiers[StatType.MinRange];
                }

                if (rawMax * distMult > maxWeaponRange) maxWeaponRange = rawMax * distMult;
                if (rawMin * distMult > minWeaponRange) minWeaponRange = rawMin * distMult;
                if (rawMax * distMult < optimalFireRange) optimalFireRange = rawMax * distMult;
            }
        }

        if (optimalFireRange < minWeaponRange) optimalFireRange = minWeaponRange + 0.5f;
        if (optimalFireRange == float.MaxValue) optimalFireRange = 5f * distMult;
    }


    private void Update()
    {
        // 1. 死亡检查
        if (myReceiver == null || myReceiver.CurrentHP <= 0)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        if (runtimeData == null) return;

        // 2. 战斗状态检查
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        // 3. 物理硬直计时
        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0) { isStaggered = false; rb.drag = 5f; }
            return;
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) { isDashing = false; rb.drag = 5f; }
            return;
        }

        // 4. 【核心性能优化】：不再每帧暴力搜索，采用计时器+目标存活校验
        searchTimer -= Time.deltaTime;

        // 判定条件：当前没目标 OR 目标死了 OR 搜索冷却好了
        if (currentTarget == null || !IsTargetValid(currentTarget) || searchTimer <= 0)
        {
            FindTargetOptimized();
            searchTimer = SearchInterval;
        }

        HandleMovement();
    }

    // 高性能目标有效性校验
    private bool IsTargetValid(Transform target)
    {
        if (target == null) return false;
        DamageReceiver dr = target.GetComponentInParent<DamageReceiver>();
        return dr != null && dr.CurrentHP > 0 && target.gameObject.activeInHierarchy;
    }

    private void FindTargetOptimized()
    {
        // 1. 获取阵营及对应的列表
        bool iAmEnemy = myReceiver != null && myReceiver.isEnemy;
        var targetList = iAmEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;

        if (targetList.Count == 0) { currentTarget = null; return; }

        // 2. 准备策略
        TargetingStrategy strategy = runtimeData.TargetingLogic;
        if (strategy == TargetingStrategy.FollowCoreAI) strategy = TargetingStrategy.Nearest;

        // 3. 【核心循环】：不使用 LINQ，通过单次遍历寻找最佳分值
        DamageReceiver bestCandidate = null;
        float bestScore = -float.MaxValue;

        for (int i = 0; i < targetList.Count; i++)
        {
            DamageReceiver potential = targetList[i];

            // 基础过滤
            if (potential == null || potential.CurrentHP <= 0) continue;

            float dist = Vector3.Distance(transform.position, potential.transform.position);
            float currentScore = 0f;

            // 策略打分系统
            switch (strategy)
            {
                case TargetingStrategy.Nearest:
                    currentScore = -dist; // 距离越短分数越高
                    break;
                case TargetingStrategy.Furthest:
                    currentScore = dist; // 距离越长分数越高
                    break;
                case TargetingStrategy.MaxHPHighest:
                    currentScore = potential.MaxHP;
                    break;
                case TargetingStrategy.MaxHPLowest:
                    currentScore = -potential.MaxHP;
                    break;
                case TargetingStrategy.CurrentHPHighest:
                    currentScore = potential.CurrentHP;
                    break;
                case TargetingStrategy.CurrentHPLowest:
                    currentScore = -potential.CurrentHP;
                    break;
                default:
                    currentScore = -dist;
                    break;
            }

            if (currentScore > bestScore)
            {
                bestScore = currentScore;
                bestCandidate = potential;
            }
        }

        if (bestCandidate != null)
        {
            currentTarget = bestCandidate.transform;
        }
        else
        {
            currentTarget = null;
        }
    }

    private void HandleMovement()
    {
        if (currentTarget == null)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        // 判定 AI 覆写
        MovementStrategy activeLogic = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideMovement : runtimeData.MovementLogic;
        float activeDodgeDist = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideDodgeDist : runtimeData.SafeDodgeDistance;

        Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);

        // 考虑碰撞盒表面的真实距离 (ClosestPoint 性能尚可)
        float dist = Vector3.Distance(logicCenter, currentTarget.position);
        Collider2D targetCol = currentTarget.GetComponentInChildren<Collider2D>();
        if (targetCol != null) dist = Vector2.Distance(logicCenter, targetCol.ClosestPoint(logicCenter));

        Vector2 dirToTarget = (currentTarget.position - logicCenter).normalized;
        Vector2 targetVelocity = Vector2.zero;

        // --- 基于策略的位移决策 ---
        if (activeLogic == MovementStrategy.Dodge && dist < activeDodgeDist)
        {
            targetVelocity = -dirToTarget * CurrentSpeed;
        }
        else if (activeLogic == MovementStrategy.Active_Survival && dist > maxWeaponRange)
        {
            targetVelocity = dirToTarget * CurrentSpeed;
        }
        else if (activeLogic == MovementStrategy.Active_Firepower)
        {
            // 为近战单位增加侵入缓冲区
            bool hasMelee = false;
            for (int i = 0; i < runtimeData.EquippedWeapons.Count; i++)
            {
                if (runtimeData.EquippedWeapons[i].DeliveryType == WeaponDeliveryType.Melee)
                {
                    hasMelee = true;
                    break;
                }
            }
            float engagementBuffer = hasMelee ? 0.8f : 0f;

            if (dist > (optimalFireRange - engagementBuffer))
            {
                targetVelocity = dirToTarget * CurrentSpeed;
            }
            else if (dist < minWeaponRange)
            {
                targetVelocity = -dirToTarget * (CurrentSpeed * 0.5f);
            }
            else
            {
                targetVelocity = Vector2.zero; // 到达射程
            }
        }

        if (rb != null) rb.velocity = targetVelocity;
    }

    // ==========================================
    // 物理引擎交互接口 (由碰撞和积木调用)
    // ==========================================

    public void ApplyImpulse(Vector2 dir, float impulse, bool ignoreStun = false)
    {
        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;

        if (!ignoreStun)
        {
            float stunTime = GameFormulas.CalcStaggerTime(impulse, mass);
            if (stunTime > 0f)
            {
                isStaggered = true;
                staggerTimer = stunTime;
            }
        }

        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float clampedDeltaV = Mathf.Clamp((impulse / mass) * speedMult, 0f, 25f);

        if (rb != null)
        {
            rb.drag = 5f;
            if (!ignoreStun) rb.velocity = Vector2.zero;
            rb.AddForce(dir * clampedDeltaV * mass, ForceMode2D.Impulse);
        }
    }

    public void ApplyRecoil(Vector2 dir, float impulse, float manualStunTime)
    {
        if (rb == null) return;

        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;

        isStaggered = true;
        staggerTimer = manualStunTime;
        rb.drag = 1.0f;

        float deltaV = (impulse / mass) * speedMult;
        rb.velocity = dir * deltaV;
        rb.AddForce(dir * impulse * speedMult, ForceMode2D.Impulse);
    }

    public void ExecuteDash(Vector2 direction, float speedMultiplier, float duration)
    {
        if (rb == null) return;

        isDashing = true;
        dashTimer = duration;

        float dashSpeed = CurrentSpeed * speedMultiplier;
        Vector2 velocity = direction.normalized * dashSpeed;
        bool iAmEnemy = myReceiver != null && myReceiver.isEnemy;
        int targetMask = iAmEnemy ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");

        // 扫描碰撞
        float scanDist = 1.2f * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scanDist, targetMask);

        for (int i = 0; i < hits.Length; i++)
        {
            DamageReceiver victim = hits[i].GetComponentInParent<DamageReceiver>();
            if (victim != null && victim.isEnemy != iAmEnemy)
            {
                float eMass = 5f;
                EnemyBrain eb = hits[i].GetComponentInParent<EnemyBrain>();
                if (eb != null && eb.MyData != null) eMass = eb.MyData.GetStat(StatType.Mass);

                float rawDamage = GameFormulas.CalcKineticRamDamage(runtimeData.TotalMass, eMass, dashSpeed, 2.0f);

                if (rawDamage > 10f)
                {
                    float enemyShare = runtimeData.TotalMass / (runtimeData.TotalMass + eMass);
                    victim.TakeDamage(rawDamage * enemyShare, runtimeData.UnitName + " (零距离爆破)");

                    if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(0.08f);
                    if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(0.3f, 0.15f);
                }
            }
        }

        rb.drag = 0.5f;
        rb.velocity = velocity;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (runtimeData == null || rb == null) return;

        Rigidbody2D targetRb = col.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        float relVelocity = col.relativeVelocity.magnitude;
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;

        if (relVelocity > 5.0f * speedMult)
        {
            DamageReceiver victim = col.gameObject.GetComponentInParent<DamageReceiver>();
            if (victim != null && victim.isEnemy)
            {
                float enemyMass = 5f;
                EnemyBrain enemyAI = victim.GetComponent<EnemyBrain>();
                if (enemyAI != null && enemyAI.MyData != null) enemyMass = enemyAI.MyData.GetStat(StatType.Mass);

                float rawDamage = GameFormulas.CalcKineticRamDamage(runtimeData.TotalMass, enemyMass, relVelocity, 2.0f);

                if (rawDamage > 5f)
                {
                    float myShare = enemyMass / (runtimeData.TotalMass + enemyMass);
                    float enemyShare = runtimeData.TotalMass / (runtimeData.TotalMass + enemyMass);

                    victim.TakeDamage(rawDamage * enemyShare, runtimeData.UnitName + " (泥头车碾压)");

                    if (rawDamage > 30f)
                    {
                        if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(0.08f, 0.01f);
                        if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(rawDamage / 250f, 0.15f);
                    }

                    DamageReceiver self = GetComponent<DamageReceiver>();
                    if (self != null) self.TakeDamage(rawDamage * myShare, "撞击反弹");
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (runtimeData == null) return;
        Vector3 logicCenter = Application.isPlaying ? transform.TransformPoint(runtimeData.LogicCenterOffset) : transform.position;

        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
        Gizmos.DrawWireSphere(logicCenter, optimalFireRange);

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
        Gizmos.DrawWireSphere(logicCenter, maxWeaponRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(logicCenter, minWeaponRange);
    }
}