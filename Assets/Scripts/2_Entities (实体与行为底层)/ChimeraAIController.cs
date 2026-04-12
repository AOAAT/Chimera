// --- START OF FILE ChimeraAIController.cs ---
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

    private float maxWeaponRange = 0f;
    private float minWeaponRange = 0f;

    public void Initialize(RuntimeChimeraData data)
    {
        runtimeData = data;
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.drag = 5f;

        // 👇 监听 Buff 变化事件！
        myBuffMgr = GetComponent<BuffManager>();
        if (myBuffMgr != null) myBuffMgr.OnBuffsChanged += RecalculateSpeedAndRanges;

        RecalculateSpeedAndRanges();
    }

    private void OnDestroy()
    {
        if (myBuffMgr != null) myBuffMgr.OnBuffsChanged -= RecalculateSpeedAndRanges;
    }

    // 👇【核心新增】：每次增减 Buff，重新算一遍自己的面板属性！
    private void RecalculateSpeedAndRanges()
    {
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;

        // 抓取并叠加 Buff 里增加的引擎马力 (EnginePower)
        float currentEnginePower = runtimeData.TotalEnginePower;
        if (myBuffMgr != null && myBuffMgr.BuffStatModifiers.ContainsKey(StatType.EnginePower))
        {
            currentEnginePower += myBuffMgr.BuffStatModifiers[StatType.EnginePower];
        }

        CurrentSpeed = GameFormulas.CalcMoveSpeed(currentEnginePower, runtimeData.TotalMass, speedMult);

        maxWeaponRange = 0f;
        minWeaponRange = float.MaxValue;

        // 这里也粗略地叠加上武器射程 Buff，决定机甲要站在多远的地方
        float bonusMaxRange = (myBuffMgr != null && myBuffMgr.BuffStatModifiers.ContainsKey(StatType.MaxRange)) ? myBuffMgr.BuffStatModifiers[StatType.MaxRange] : 0f;
        float bonusMinRange = (myBuffMgr != null && myBuffMgr.BuffStatModifiers.ContainsKey(StatType.MinRange)) ? myBuffMgr.BuffStatModifiers[StatType.MinRange] : 0f;

        foreach (var wpn in runtimeData.EquippedWeapons)
        {
            float maxR = (wpn.GetStat(StatType.MaxRange) + bonusMaxRange) * distMult;
            float minR = (wpn.GetStat(StatType.MinRange) + bonusMinRange) * distMult;
            if (maxR > maxWeaponRange) maxWeaponRange = maxR;
            if (minR < minWeaponRange) minWeaponRange = minR;
        }

        if (minWeaponRange == float.MaxValue) minWeaponRange = 1.5f * distMult;
        else if (minWeaponRange < 1.5f * distMult) minWeaponRange = 1.5f * distMult;
        if (maxWeaponRange < minWeaponRange) maxWeaponRange = minWeaponRange;
    }

    private void Update()
    {
        if (runtimeData == null) return;
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) { if (rb != null) rb.velocity = Vector2.zero; return; }
        if (isStaggered) { staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { isStaggered = false; rb.drag = 5f; } return; }

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
        if (currentTarget == null) { if (rb != null) rb.velocity = Vector2.zero; return; }

        // AI覆写由 BuffManager 决定
        MovementStrategy activeLogic = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideMovement : runtimeData.MovementLogic;
        float activeDodgeDist = (myBuffMgr != null && myBuffMgr.HasAIOverride) ? myBuffMgr.CurrentOverrideDodgeDist : runtimeData.SafeDodgeDistance;

        Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);
        Vector3 dirToTarget = (currentTarget.position - logicCenter).normalized;
        float dist = Vector3.Distance(logicCenter, currentTarget.position);

        Collider2D[] enemyCols = currentTarget.GetComponentsInChildren<Collider2D>();
        Collider2D targetCol = null;
        foreach (var c in enemyCols) { if (c.isTrigger) { targetCol = c; break; } }
        if (targetCol == null && enemyCols.Length > 0) targetCol = enemyCols[0];
        if (targetCol != null) dist = Vector2.Distance(logicCenter, targetCol.ClosestPoint(logicCenter));

        Vector2 targetVelocity = Vector2.zero;

        if (activeLogic == MovementStrategy.Dodge && dist < activeDodgeDist) targetVelocity = -dirToTarget * CurrentSpeed;
        else if (activeLogic == MovementStrategy.Active_Survival && dist > maxWeaponRange) targetVelocity = dirToTarget * CurrentSpeed;
        else if (activeLogic == MovementStrategy.Active_Firepower && dist > minWeaponRange) targetVelocity = dirToTarget * CurrentSpeed;

        if (rb != null) rb.velocity = targetVelocity;
    }

    public void ApplyImpulse(Vector2 dir, float impulse)
    {
        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;
        float stunTime = GameFormulas.CalcStaggerTime(impulse, mass);
        if (stunTime <= 0f) return;
        isStaggered = true; staggerTimer = stunTime;
        float clampedDeltaV = Mathf.Clamp(impulse / mass, 0f, 20f);
        rb.drag = 5f; rb.velocity = Vector2.zero;
        rb.AddForce(dir * clampedDeltaV * mass, ForceMode2D.Impulse);
    }
}