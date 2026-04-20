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
    private float maxWeaponRange = 0f;      // 所有武器中最远的射程 (用于生存模式)
    private float minWeaponRange = 0f;      // 所有武器中最大的最小射程 (死角判定)
    private float optimalFireRange = 0f;    // 最短的那把武器的最大射程 (用于火力模式)

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
        if (myBuffMgr != null) myBuffMgr.OnBuffsChanged -= RecalculateSpeedAndRanges;
    }

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

        // --- 核心逻辑：计算 AI 控距阈值 ---
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

                if (rawMax > maxWeaponRange) maxWeaponRange = rawMax;
                if (rawMin > minWeaponRange) minWeaponRange = rawMin;
                if (rawMax < optimalFireRange) optimalFireRange = rawMax;
            }
        }

        // 应用度量衡缩放
        maxWeaponRange *= distMult;
        minWeaponRange *= distMult;
        optimalFireRange *= distMult;

        if (optimalFireRange < minWeaponRange) optimalFireRange = minWeaponRange + 0.5f;
    }

    private void Update()
    {
        if (runtimeData == null) return;
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
                rb.drag = 5f; // 【关键】：硬直结束，恢复阻力，AI 重新接管
            }
            return; // 硬直期间不执行 FindTarget 和 HandleMovement
        }

        // 处理冲刺状态
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
                rb.drag = 5f;
            }
            return;
        }

        FindTarget();
        HandleMovement();
    }

    private void FindTarget()
    {
        var allEnemies = FindObjectsOfType<DamageReceiver>().Where(e => e.isEnemy && e.CurrentHP > 0).ToList();
        if (allEnemies.Count == 0) { currentTarget = null; return; }

        switch (runtimeData.TargetingLogic)
        {
            case TargetingStrategy.Nearest: currentTarget = allEnemies.OrderBy(e => Vector3.Distance(transform.position, e.transform.position)).First().transform; break;
            case TargetingStrategy.MaxHPHighest: currentTarget = allEnemies.OrderByDescending(e => e.MaxHP).First().transform; break;
            case TargetingStrategy.MaxHPLowest: currentTarget = allEnemies.OrderBy(e => e.MaxHP).First().transform; break;
            case TargetingStrategy.CurrentHPHighest: currentTarget = allEnemies.OrderByDescending(e => e.CurrentHP).First().transform; break;
            case TargetingStrategy.CurrentHPLowest: currentTarget = allEnemies.OrderBy(e => e.CurrentHP).First().transform; break;
        }
    }

    private void HandleMovement()
    {
        if (currentTarget == null)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        MovementStrategy activeLogic = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideMovement : runtimeData.MovementLogic;
        float activeDodgeDist = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideDodgeDist : runtimeData.SafeDodgeDistance;

        Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);
        Vector3 dirToTarget = (currentTarget.position - logicCenter).normalized;
        float dist = Vector3.Distance(logicCenter, currentTarget.position);

        Collider2D targetCol = currentTarget.GetComponentInChildren<Collider2D>();
        if (targetCol != null) dist = Vector2.Distance(logicCenter, targetCol.ClosestPoint(logicCenter));

        Vector2 targetVelocity = Vector2.zero;

        // --- 基于“最优火力窗口”的决策逻辑 ---
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
            if (dist > optimalFireRange)
            {
                targetVelocity = dirToTarget * CurrentSpeed;
            }
            else if (dist < minWeaponRange)
            {
                targetVelocity = -dirToTarget * (CurrentSpeed * 0.5f);
            }
            else
            {
                // 已进入所有武器的最大射程覆盖区，且不在死角，原地开火
                targetVelocity = Vector2.zero;
            }
        }

        if (rb != null) rb.velocity = targetVelocity;
    }

    // ==========================================
    // 物理接口区 (ECA 积木和碰撞引擎调用)
    // ==========================================

    // --- 修改 ChimeraAIController.cs ---
    // 增加 ignoreStun 参数，默认不忽略
    public void ApplyImpulse(Vector2 dir, float impulse, bool ignoreStun = false)
    {
        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;

        // 如果不忽略硬直，才去计算晕眩时间
        if (!ignoreStun)
        {
            float stunTime = GameFormulas.CalcStaggerTime(impulse, mass);
            if (stunTime > 0f)
            {
                isStaggered = true;
                staggerTimer = stunTime;
            }
        }

        // 无论晕不晕，物理上的推挤力是一定要给的，这才是后坐力的浪漫
        float clampedDeltaV = Mathf.Clamp(impulse / mass, 0f, 20f);
        rb.drag = 5f;
        // 注意：如果是后坐力，我们不强制清空原有速度，让它滑得更自然
        if (!ignoreStun) rb.velocity = Vector2.zero;

        rb.AddForce(dir * clampedDeltaV * mass, ForceMode2D.Impulse);
    }


    public void ApplyRecoil(Vector2 dir, float impulse, float manualStunTime)
    {
        if (rb == null) return;

        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;

        // 1. 立即进入硬直状态，拦截 AI 走位覆盖
        isStaggered = true;
        staggerTimer = manualStunTime;

        // 2. 物理手感调校：瞬间降低阻力，让它能滑出去
        // 等到 Update 里的 staggerTimer 结束时，会自动恢复到 5f
        rb.drag = 1.0f;

        // 3. 施加冲量
        // 计算速度增量 deltaV = I / m
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float deltaV = (impulse / mass) * speedMult;

        // 强制赋予一个初始速度，确保第一帧就有位移
        rb.velocity = dir * deltaV;

        // 补一发冲量确保物理模拟连贯
        rb.AddForce(dir * impulse * speedMult, ForceMode2D.Impulse);

        // Debug.Log($"<color=white>【后坐力】</color> 冲量:{impulse} 导致位移速度:{rb.velocity.magnitude:F2}");
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
                EnemyBrain enemyAI = victim.GetComponent<EnemyBrain>();
                float enemyMass = enemyAI != null && enemyAI.MyData != null ? enemyAI.MyData.GetStat(StatType.Mass) : 5f;
                float rawDamage = GameFormulas.CalcKineticRamDamage(runtimeData.TotalMass, enemyMass, relVelocity, 2.0f);

                if (rawDamage > 0)
                {
                    float myDamageShare = enemyMass / (runtimeData.TotalMass + enemyMass);
                    float enemyDamageShare = runtimeData.TotalMass / (runtimeData.TotalMass + enemyMass);

                    victim.TakeDamage(rawDamage * enemyDamageShare, runtimeData.UnitName + " (泥头车碾压)");

                    // 触发顿挫感 (Hit Stop)
                    if (rawDamage > 30f || relVelocity > 10f * speedMult)
                    {
                        float freezeTime = Mathf.Clamp(rawDamage / 1000f + 0.05f, 0.05f, 0.12f);
                        if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(freezeTime, 0.01f);
                        if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(rawDamage / 200f, 0.15f);
                    }

                    DamageReceiver myReceiver = GetComponent<DamageReceiver>();
                    if (myReceiver != null) myReceiver.TakeDamage(rawDamage * myDamageShare, "撞击反作用力");
                }
            }
        }
    }

    public void ExecuteDash(Vector2 direction, float speedMultiplier, float duration)
    {
        isDashing = true;
        dashTimer = duration;
        rb.drag = 0.5f;
        rb.velocity = direction.normalized * (CurrentSpeed * speedMultiplier);
    }

    private void OnDrawGizmos()
    {
        if (runtimeData == null) return;

        Vector3 logicCenter = Application.isPlaying ? transform.TransformPoint(runtimeData.LogicCenterOffset) : transform.position;

        // 画出最优火力线 (黄色)
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.4f);
        Gizmos.DrawWireSphere(logicCenter, optimalFireRange);

        // 画出最远射程边缘 (蓝色)
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.1f);
        Gizmos.DrawWireSphere(logicCenter, maxWeaponRange);

        // 画出死角边缘 (红色)
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Gizmos.DrawWireSphere(logicCenter, minWeaponRange);
    }
}