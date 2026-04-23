using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChimeraAIController : MonoBehaviour
{
    private RuntimeChimeraData runtimeData;
    private Transform currentTarget;
    private Rigidbody2D rb;
    private BuffManager myBuffMgr;

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
            foreach (var wpn in runtimeData.EquippedWeapons)
            {
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
        if (runtimeData == null) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

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

        // 👇【核心修复】：每帧执行索敌，确保 currentTarget 不为空
        FindTarget();
        HandleMovement();
    }

    private void FindTarget()
    {
        bool IAmEnemy = GetComponent<DamageReceiver>().isEnemy;

        // 如果我是敌人，我就找玩家；如果我是玩家，我就找敌人
        var potentialTargets = IAmEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;

        // 1. 扫描场上所有存活敌人
        var allEnemies = potentialTargets.Where(e => e != null && e.CurrentHP > 0).ToList();
        if (allEnemies.Count == 0) { currentTarget = null; return; }

        
      

        // 2. 获取核心大脑的策略
        TargetingStrategy strategy = runtimeData.TargetingLogic;

        // 👇【关键修正】：如果大脑设为了 Follow (0)，强制修正为 Nearest (1)
        // 核心大脑本身不能“跟随核心”，它必须有一个具体的策略
        if (strategy == TargetingStrategy.FollowCoreAI) strategy = TargetingStrategy.Nearest;

        // 3. 执行排序逻辑
        IOrderedEnumerable<DamageReceiver> sorted;
        switch (strategy)
        {
            case TargetingStrategy.MaxHPHighest: sorted = allEnemies.OrderByDescending(e => e.MaxHP); break;
            case TargetingStrategy.MaxHPLowest: sorted = allEnemies.OrderBy(e => e.MaxHP); break;
            case TargetingStrategy.CurrentHPHighest: sorted = allEnemies.OrderByDescending(e => e.CurrentHP); break;
            case TargetingStrategy.CurrentHPLowest: sorted = allEnemies.OrderBy(e => e.CurrentHP); break;
            case TargetingStrategy.Furthest: sorted = allEnemies.OrderByDescending(e => Vector3.Distance(transform.position, e.transform.position)); break;
            case TargetingStrategy.Nearest:
            default: sorted = allEnemies.OrderBy(e => Vector3.Distance(transform.position, e.transform.position)); break;
        }

        currentTarget = sorted.First().transform;

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

        // 考虑碰撞盒表面的真实距离
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
            float engagementBuffer = runtimeData.EquippedWeapons.Any(w => w.DeliveryType == WeaponDeliveryType.Melee) ? 0.8f : 0f;

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
                targetVelocity = Vector2.zero; // 完美就位
            }
        }

        if (rb != null) rb.velocity = targetVelocity;
    }

    // ==========================================
    // 物理引擎交互接口 (由碰撞和积木调用)
    // ==========================================

    /// <summary>
    /// 常规物理打击（如爆炸击退、怪物殴打）
    /// </summary>
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

    /// <summary>
    /// 开火后坐力专用：大推力，但只产生极短僵直
    /// </summary>
    public void ApplyRecoil(Vector2 dir, float impulse, float manualStunTime)
    {
        if (rb == null) return;

        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;

        // 1. 设置人工僵直，拦截 AI 控制
        isStaggered = true;
        staggerTimer = manualStunTime;

        // 2. 物理调优：降低阻力让它滑得更远
        rb.drag = 1.0f;

        // 3. 计算并施加即时速度
        float deltaV = (impulse / mass) * speedMult;
        rb.velocity = dir * deltaV;
        rb.AddForce(dir * impulse * speedMult, ForceMode2D.Impulse);
    }

    /// <summary>
    /// 执行战术冲刺（如马头、大象腿）
    /// </summary>
    public void ExecuteDash(Vector2 direction, float speedMultiplier, float duration)
    {
        if (rb == null) return;

        isDashing = true;
        dashTimer = duration;

        float dashSpeed = CurrentSpeed * speedMultiplier;
        Vector2 velocity = direction.normalized * dashSpeed;

        // --- 零距离爆破判定 (解决贴脸没伤害问题) ---
        float scanDist = 1.2f * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);
        int mask = LayerMask.GetMask("Enemy_Hitbox");
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scanDist, mask);

        foreach (var hit in hits)
        {
            DamageReceiver victim = hit.GetComponentInParent<DamageReceiver>();
            if (victim != null && victim.isEnemy)
            {
                float eMass = hit.GetComponentInParent<EnemyBrain>()?.MyData.GetStat(StatType.Mass) ?? 5f;
                float rawDamage = GameFormulas.CalcKineticRamDamage(runtimeData.TotalMass, eMass, dashSpeed, 2.0f);

                if (rawDamage > 10f)
                {
                    Debug.Log($"<color=#FFD700>【零距离冲撞】</color> 对 {victim.name} 造成 {rawDamage:F0} 瞬发伤害");
                    float enemyShare = runtimeData.TotalMass / (runtimeData.TotalMass + eMass);
                    victim.TakeDamage(rawDamage * enemyShare, runtimeData.UnitName + " (零距离爆破)");

                    if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(0.08f);
                    if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(0.3f, 0.15f);
                }
            }
        }

        // 执行物理滑行
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

        // 撞击速度阈值
        if (relVelocity > 5.0f * speedMult)
        {
            DamageReceiver victim = col.gameObject.GetComponentInParent<DamageReceiver>();
            if (victim != null && victim.isEnemy)
            {
                EnemyBrain enemyAI = victim.GetComponent<EnemyBrain>();
                float enemyMass = enemyAI != null && enemyAI.MyData != null ? enemyAI.MyData.GetStat(StatType.Mass) : 5f;

                float rawDamage = GameFormulas.CalcKineticRamDamage(runtimeData.TotalMass, enemyMass, relVelocity, 2.0f);

                if (rawDamage > 5f)
                {
                    float myShare = enemyMass / (runtimeData.TotalMass + enemyMass);
                    float enemyShare = runtimeData.TotalMass / (runtimeData.TotalMass + enemyMass);

                    victim.TakeDamage(rawDamage * enemyShare, runtimeData.UnitName + " (泥头车碾压)");

                    // 只有够重的撞击才会卡肉
                    if (rawDamage > 30f)
                    {
                        if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(0.08f, 0.01f);
                        if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(rawDamage / 250f, 0.15f);
                    }

                    // 自身反噬伤害
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

        // 黄色：最优火力停车线
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
        Gizmos.DrawWireSphere(logicCenter, optimalFireRange);

        // 蓝色：生存模式保持线
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
        Gizmos.DrawWireSphere(logicCenter, maxWeaponRange);

        // 红色：死角警告线
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(logicCenter, minWeaponRange);
    }
}