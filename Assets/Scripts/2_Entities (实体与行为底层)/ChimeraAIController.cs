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
    public float staggerTimer = 0f;
    public bool isDashing = false;
    private float dashTimer = 0f;

    [Header("=== AI 走位核心阈值 (只读) ===")]
    [SerializeField] private float maxWeaponRange = 0f;      // 所有武器中最远的射程
    [SerializeField] private float minWeaponRange = 0f;      // 所有武器中最大的最小射程 (死角)
    [SerializeField] private float optimalFireRange = 0f;    // 最短的那把武器的最大射程 (火力窗口)

    public void Initialize(RuntimeChimeraData data)
    {
        runtimeData = data;
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.drag = 5f; // 默认地面阻力
        }

        myBuffMgr = GetComponent<BuffManager>();
        if (myBuffMgr != null)
        {
            // 每次 Buff 变化，重新计算移速和射程窗口
            myBuffMgr.OnBuffsChanged += RecalculateSpeedAndRanges;
        }

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

        // 1. 计算移速
        float currentEnginePower = runtimeData.TotalEnginePower;
        if (myBuffMgr != null && myBuffMgr.BuffStatModifiers.ContainsKey(StatType.EnginePower))
        {
            currentEnginePower += myBuffMgr.BuffStatModifiers[StatType.EnginePower];
        }

        CurrentSpeed = GameFormulas.CalcMoveSpeed(currentEnginePower, runtimeData.TotalMass, speedMult);

        // 2. 计算火力窗口 (Optimal Fire Window)
        maxWeaponRange = 0f;
        minWeaponRange = 0f;
        optimalFireRange = float.MaxValue;

        float bonusMaxRange = (myBuffMgr != null && myBuffMgr.BuffStatModifiers.ContainsKey(StatType.MaxRange)) ? myBuffMgr.BuffStatModifiers[StatType.MaxRange] : 0f;
        float bonusMinRange = (myBuffMgr != null && myBuffMgr.BuffStatModifiers.ContainsKey(StatType.MinRange)) ? myBuffMgr.BuffStatModifiers[StatType.MinRange] : 0f;

        if (runtimeData.EquippedWeapons.Count == 0)
        {
            optimalFireRange = 1.5f;
        }
        else
        {
            foreach (var wpn in runtimeData.EquippedWeapons)
            {
                float rawMax = wpn.GetStat(StatType.MaxRange) + bonusMaxRange;
                float rawMin = wpn.GetStat(StatType.MinRange) + bonusMinRange;

                // 记录全机最远射程
                if (rawMax > maxWeaponRange) maxWeaponRange = rawMax;

                // 记录最大的射击死角
                if (rawMin > minWeaponRange) minWeaponRange = rawMin;

                // 最短的最大射程，即为最优火力线
                if (rawMax < optimalFireRange) optimalFireRange = rawMax;
            }
        }

        // 应用度量衡缩放
        maxWeaponRange *= distMult;
        minWeaponRange *= distMult;
        optimalFireRange *= distMult;

        // 安全兜底：火力线不能卡在死角里
        if (optimalFireRange < minWeaponRange)
        {
            optimalFireRange = minWeaponRange + 0.5f;
        }

        Debug.Log($"<color=#00FFFF>[AI属性同步]</color> {runtimeData.UnitName} | 移速: {CurrentSpeed:F1} | 最优窗口: {optimalFireRange:F1}m");
    }

    private void Update()
    {
        if (runtimeData == null) return;

        // 战斗未开启时，强行刹车
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        // 处理受击硬直
        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0)
            {
                isStaggered = false;
                if (rb != null) rb.drag = 5f; // 恢复正常阻力
            }
            return; // 硬直期间 AI 无法控制位移
        }

        // 处理冲刺状态
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
                if (rb != null) rb.drag = 5f;
            }
            return; // 冲刺期间由物理惯性掌控
        }

        FindTarget();
        HandleMovement();
    }

    private void FindTarget()
    {
        var allEnemies = CombatDirector.ActiveEnemies.Where(e => e != null && e.CurrentHP > 0).ToList();
        if (allEnemies.Count == 0)
        {
            currentTarget = null;
            return;
        }

        // 根据核心设定的索敌逻辑进行排序
        switch (runtimeData.TargetingLogic)
        {
            case TargetingStrategy.Nearest:
                currentTarget = allEnemies.OrderBy(e => Vector3.Distance(transform.position, e.transform.position)).First().transform;
                break;
            case TargetingStrategy.MaxHPHighest:
                currentTarget = allEnemies.OrderByDescending(e => e.MaxHP).First().transform;
                break;
            case TargetingStrategy.MaxHPLowest:
                currentTarget = allEnemies.OrderBy(e => e.MaxHP).First().transform;
                break;
            case TargetingStrategy.CurrentHPHighest:
                currentTarget = allEnemies.OrderByDescending(e => e.CurrentHP).First().transform;
                break;
            case TargetingStrategy.CurrentHPLowest:
                currentTarget = allEnemies.OrderBy(e => e.CurrentHP).First().transform;
                break;
        }
    }

    private void HandleMovement()
    {
        if (currentTarget == null)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        // 判断是否有来自 Buff 的 AI 覆写
        MovementStrategy activeLogic = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideMovement : runtimeData.MovementLogic;
        float activeDodgeDist = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideDodgeDist : runtimeData.SafeDodgeDistance;

        Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);
        Vector3 dirToTarget = (currentTarget.position - logicCenter).normalized;
        float dist = Vector3.Distance(logicCenter, currentTarget.position);

        // 考虑对方碰撞盒边缘的精确距离判定
        Collider2D targetCol = currentTarget.GetComponentInChildren<Collider2D>();
        if (targetCol != null)
        {
            dist = Vector2.Distance(logicCenter, targetCol.ClosestPoint(logicCenter));
        }

        Vector2 targetVelocity = Vector2.zero;

        // --- 执行决策树 ---
        if (activeLogic == MovementStrategy.Dodge && dist < activeDodgeDist)
        {
            // 撤离模式：离得太近就后退
            targetVelocity = -dirToTarget * CurrentSpeed;
        }
        else if (activeLogic == MovementStrategy.Active_Survival && dist > maxWeaponRange)
        {
            // 生存模式：保持在最远武器的射程线
            targetVelocity = dirToTarget * CurrentSpeed;
        }
        else if (activeLogic == MovementStrategy.Active_Firepower)
        {
            // 【核心重构】：最优火力窗口模式
            float engagementBuffer = 0f;
            // 如果身上有近战武器，允许额外深入 0.5 米防止哑火
            if (runtimeData.EquippedWeapons.Any(w => w.DeliveryType == WeaponDeliveryType.Melee))
            {
                engagementBuffer = 0.5f * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);
            }

            if (dist > (optimalFireRange - engagementBuffer))
            {
                // 离得太远：向前推进
                targetVelocity = dirToTarget * CurrentSpeed;
            }
            else if (dist < minWeaponRange)
            {
                // 进了死角：尝试后退
                targetVelocity = -dirToTarget * (CurrentSpeed * 0.5f);
            }
            else
            {
                // 完美射程窗口：强制停车，最大化火力
                targetVelocity = Vector2.zero;
            }
        }

        if (rb != null)
        {
            rb.velocity = targetVelocity;
        }
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