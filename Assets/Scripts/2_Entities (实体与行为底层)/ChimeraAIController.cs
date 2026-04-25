using System.Collections.Generic;
using UnityEngine;

public class ChimeraAIController : MonoBehaviour
{
    private RuntimeChimeraData runtimeData;
    private Transform currentTarget;
    private Rigidbody2D rb;
    private BuffManager myBuffMgr;
    private DamageReceiver myReceiver;

    [Header("=== 性能设定 ===")]
    public float SearchInterval = 0.2f;
    private float searchTimer = 0f;

    [Header("=== 动态物理数据 ===")]
    public float CurrentSpeed;

    [Header("=== 状态控制 ===")]
    public bool isStaggered = false;
    private float staggerTimer = 0f;
    public bool isDashing = false;
    private float dashTimer = 0f;

    // --- 👇 指挥官手动覆写字段 ---
    private Vector2? manualMovePoint = null;
    private Transform manualAttackTarget = null;
    private float manualOverrideTimer = 0f;

    private float maxWeaponRange, minWeaponRange, optimalFireRange;

    public void Initialize(RuntimeChimeraData data)
    {
        runtimeData = data;
        rb = GetComponent<Rigidbody2D>();
        myReceiver = GetComponent<DamageReceiver>();
        if (rb != null) rb.drag = 5f;
        myBuffMgr = GetComponent<BuffManager>();
        if (myBuffMgr != null) myBuffMgr.OnBuffsChanged += RecalculateSpeedAndRanges;
        RecalculateSpeedAndRanges();
    }

    private void OnDestroy() { if (myBuffMgr != null) myBuffMgr.OnBuffsChanged -= RecalculateSpeedAndRanges; }

    public void SetManualMovePoint(Vector2 point) { manualMovePoint = point; manualAttackTarget = null; manualOverrideTimer = 10f; }
    public void SetManualTarget(Transform target) { manualAttackTarget = target; manualMovePoint = null; manualOverrideTimer = 15f; }
    public bool HasManualTarget() => manualAttackTarget != null;
    public Transform GetManualTarget() => manualAttackTarget;

    public void RecalculateSpeedAndRanges()
    {
        if (runtimeData == null) return;
        float speedMult = CombatSandbox.GetSpeed(1f);
        float distMult = CombatSandbox.GetDist(1f);
        float pwr = runtimeData.TotalEnginePower;
        if (myBuffMgr != null && myBuffMgr.BuffStatModifiers.ContainsKey(StatType.EnginePower)) pwr += myBuffMgr.BuffStatModifiers[StatType.EnginePower];
        CurrentSpeed = GameFormulas.CalcMoveSpeed(pwr, runtimeData.TotalMass, speedMult);

        maxWeaponRange = 0f; minWeaponRange = 0f; optimalFireRange = float.MaxValue;
        if (runtimeData.EquippedWeapons.Count == 0) optimalFireRange = 1.5f * distMult;
        else
        {
            foreach (var wpn in runtimeData.EquippedWeapons)
            {
                float rMax = wpn.GetStat(StatType.MaxRange); float rMin = wpn.GetStat(StatType.MinRange);
                if (myBuffMgr != null)
                {
                    if (myBuffMgr.BuffStatModifiers.ContainsKey(StatType.MaxRange)) rMax += myBuffMgr.BuffStatModifiers[StatType.MaxRange];
                    if (myBuffMgr.BuffStatModifiers.ContainsKey(StatType.MinRange)) rMin += myBuffMgr.BuffStatModifiers[StatType.MinRange];
                }
                if (rMax * distMult > maxWeaponRange) maxWeaponRange = rMax * distMult;
                if (rMin * distMult > minWeaponRange) minWeaponRange = rMin * distMult;
                if (rMax * distMult < optimalFireRange) optimalFireRange = rMax * distMult;
            }
        }
        if (optimalFireRange < minWeaponRange) optimalFireRange = minWeaponRange + 0.5f;
        if (optimalFireRange == float.MaxValue) optimalFireRange = 5f * distMult;
    }

    private void Update()
    {
        if (myReceiver == null || myReceiver.CurrentHP <= 0) { if (rb != null) rb.velocity = Vector2.zero; return; }
        if (runtimeData == null || (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)) { if (rb != null) rb.velocity = Vector2.zero; return; }

        if (isStaggered) { staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { isStaggered = false; rb.drag = 5f; } return; }
        if (isDashing) { dashTimer -= Time.deltaTime; if (dashTimer <= 0) { isDashing = false; rb.drag = 5f; } return; }

        // 指令耗时更新
        if (manualOverrideTimer > 0) manualOverrideTimer -= Time.deltaTime;

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0 || currentTarget == null || !IsTargetValid(currentTarget))
        {
            FindTargetOptimized();
            searchTimer = SearchInterval;
        }

        HandleMovement();
    }

    private bool IsTargetValid(Transform t)
    {
        if (t == null) return false;
        DamageReceiver dr = t.GetComponentInParent<DamageReceiver>();
        return dr != null && dr.CurrentHP > 0 && t.gameObject.activeInHierarchy;
    }

    private void FindTargetOptimized()
    {
        // 手动目标优先
        if (manualOverrideTimer > 0 && manualAttackTarget != null && IsTargetValid(manualAttackTarget))
        {
            currentTarget = manualAttackTarget;
            return;
        }

        bool iAmEnemy = myReceiver.isEnemy;
        var targetList = iAmEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
        if (targetList.Count == 0) { currentTarget = null; return; }

        TargetingStrategy strategy = (manualOverrideTimer > 0 && manualAttackTarget != null) ? TargetingStrategy.Nearest : runtimeData.TargetingLogic;
        if (strategy == TargetingStrategy.FollowCoreAI) strategy = TargetingStrategy.Nearest;

        DamageReceiver bestCandidate = null; float bestScore = -float.MaxValue;
        for (int i = 0; i < targetList.Count; i++)
        {
            DamageReceiver potential = targetList[i];
            if (potential == null || potential.CurrentHP <= 0) continue;
            float dist = Vector3.Distance(transform.position, potential.transform.position);
            float currentScore = 0f;
            switch (strategy)
            {
                case TargetingStrategy.Nearest: currentScore = -dist; break;
                case TargetingStrategy.Furthest: currentScore = dist; break;
                case TargetingStrategy.MaxHPHighest: currentScore = potential.MaxHP; break;
                default: currentScore = -dist; break;
            }
            if (currentScore > bestScore) { bestScore = currentScore; bestCandidate = potential; }
        }
        currentTarget = bestCandidate != null ? bestCandidate.transform : null;
    }

    private void HandleMovement()
    {
        // 优先级 A：手动位移点指令
        if (manualOverrideTimer > 0 && manualMovePoint.HasValue)
        {
            float dist = Vector2.Distance(transform.position, manualMovePoint.Value);
            if (dist < 0.4f) { manualMovePoint = null; rb.velocity = Vector2.zero; }
            else { rb.velocity = (manualMovePoint.Value - (Vector2)transform.position).normalized * CurrentSpeed; }
            return; // 拦截 AI
        }

        if (currentTarget == null) { if (rb != null) rb.velocity = Vector2.zero; return; }

        // 优先级 B：AI 或 手动集火拉扯逻辑
        MovementStrategy activeLogic = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideMovement : runtimeData.MovementLogic;
        float activeDodgeDist = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideDodgeDist : runtimeData.SafeDodgeDistance;

        Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);
        float distToTarget = Vector3.Distance(logicCenter, currentTarget.position);
        Collider2D targetCol = currentTarget.GetComponentInChildren<Collider2D>();
        if (targetCol != null) distToTarget = Vector2.Distance(logicCenter, targetCol.ClosestPoint(logicCenter));

        Vector2 dirToTarget = (currentTarget.position - logicCenter).normalized;
        Vector2 targetVelocity = Vector2.zero;

        // 集火模式强制使用 Firepower 逻辑以确保在射程内
        if (activeLogic == MovementStrategy.Dodge && distToTarget < activeDodgeDist) targetVelocity = -dirToTarget * CurrentSpeed;
        else if (activeLogic == MovementStrategy.Active_Survival && distToTarget > maxWeaponRange) targetVelocity = dirToTarget * CurrentSpeed;
        else
        {
            // 火力优先逻辑
            if (distToTarget > optimalFireRange) targetVelocity = dirToTarget * CurrentSpeed;
            else if (distToTarget < minWeaponRange) targetVelocity = -dirToTarget * (CurrentSpeed * 0.5f);
        }

        if (rb != null) rb.velocity = targetVelocity;
    }

    // ==========================================
    // 物理接口与碰撞逻辑 (保持原有逻辑不变)
    // ==========================================

    public void ApplyImpulse(Vector2 dir, float impulse, bool ignoreStun = false)
    {
        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;
        if (!ignoreStun)
        {
            float stunTime = GameFormulas.CalcStaggerTime(impulse, mass);
            if (stunTime > 0f) { isStaggered = true; staggerTimer = stunTime; }
        }
        float speedMult = CombatSandbox.GetSpeed(1f);
        float clampedDeltaV = Mathf.Clamp((impulse / mass) * speedMult, 0f, 25f);
        if (rb != null) { rb.drag = 5f; if (!ignoreStun) rb.velocity = Vector2.zero; rb.AddForce(dir * clampedDeltaV * mass, ForceMode2D.Impulse); }
    }

    public void ApplyRecoil(Vector2 dir, float impulse, float manualStunTime)
    {
        if (rb == null) return;
        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;
        float speedMult = CombatSandbox.GetSpeed(1f);
        isStaggered = true; staggerTimer = manualStunTime; rb.drag = 1.0f;
        float deltaV = (impulse / mass) * speedMult;
        rb.velocity = dir * deltaV;
        rb.AddForce(dir * impulse * speedMult, ForceMode2D.Impulse);
    }

    public void ExecuteDash(Vector2 direction, float speedMultiplier, float duration)
    {
        if (rb == null) return;
        isDashing = true; dashTimer = duration;
        float dashSpeed = CurrentSpeed * speedMultiplier;
        Vector2 velocity = direction.normalized * dashSpeed;
        bool iAmEnemy = myReceiver != null && myReceiver.isEnemy;
        int targetMask = iAmEnemy ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");

        float scanDist = 1.2f * CombatSandbox.GetDist(1f);
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
        rb.drag = 0.5f; rb.velocity = velocity;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (runtimeData == null || rb == null) return;
        Rigidbody2D targetRb = col.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;
        float relVelocity = col.relativeVelocity.magnitude;
        float speedMult = CombatSandbox.GetSpeed(1f);
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