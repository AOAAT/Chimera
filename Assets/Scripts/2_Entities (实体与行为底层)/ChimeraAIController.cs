// --- START OF FILE ChimeraAIController.cs ---
using System.Linq;
using UnityEngine;

public class ChimeraAIController : MonoBehaviour
{
    private RuntimeChimeraData runtimeData;
    private Transform currentTarget;
    private Rigidbody2D rb;

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

        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;

        // 核心速度公式：产电量越高、质量越轻，跑得越快！
        CurrentSpeed = GameFormulas.CalcMoveSpeed(runtimeData.TotalEnginePower, runtimeData.TotalMass, speedMult);

        maxWeaponRange = 0f;
        minWeaponRange = float.MaxValue;

        foreach (var wpn in runtimeData.EquippedWeapons)
        {
            float maxR = wpn.GetStat(StatType.MaxRange) * distMult;
            float minR = wpn.GetStat(StatType.MinRange) * distMult;
            if (maxR > maxWeaponRange) maxWeaponRange = maxR;
            if (minR < minWeaponRange) minWeaponRange = minR;
        }

        // 防重叠灾难：强制保留至少 1.5 米的接敌距离
        if (minWeaponRange == float.MaxValue) minWeaponRange = 1.5f * distMult;
        else if (minWeaponRange < 1.5f * distMult) minWeaponRange = 1.5f * distMult;

        if (maxWeaponRange < minWeaponRange) maxWeaponRange = minWeaponRange;

        rb = GetComponent<Rigidbody2D>();

        if (rb != null) rb.drag = 5f; // 保持 5 的高摩擦力，防止被撞飞
    }

    private void Update()
    {
        if (runtimeData == null) return;

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        // 挨打硬直处理
        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0)
            {
                isStaggered = false;
                rb.drag = 5f;
            }
            return;
        }

        FindTarget();
        HandleMovement(); // 移除了 Stamina 处理
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

        Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);
        Vector3 dirToTarget = (currentTarget.position - logicCenter).normalized;
        float dist = Vector3.Distance(logicCenter, currentTarget.position);

        Collider2D[] enemyCols = currentTarget.GetComponentsInChildren<Collider2D>();
        Collider2D targetCol = null;
        foreach (var c in enemyCols) { if (c.isTrigger) { targetCol = c; break; } }
        if (targetCol == null && enemyCols.Length > 0) targetCol = enemyCols[0];

        if (targetCol != null)
        {
            Vector2 closestPoint = targetCol.ClosestPoint(logicCenter);
            dist = Vector2.Distance(logicCenter, closestPoint);
        }

        Vector2 targetVelocity = Vector2.zero;

        // 极度丝滑的永动机走位：要么风筝，要么突脸，要么边缘游走！
        if (runtimeData.MovementLogic == MovementStrategy.Dodge && dist < runtimeData.SafeDodgeDistance)
        {
            targetVelocity = -dirToTarget * CurrentSpeed;
        }
        else if (runtimeData.MovementLogic == MovementStrategy.Active_Survival && dist > maxWeaponRange)
        {
            targetVelocity = dirToTarget * CurrentSpeed;
        }
        else if (runtimeData.MovementLogic == MovementStrategy.Active_Firepower && dist > minWeaponRange)
        {
            targetVelocity = dirToTarget * CurrentSpeed;
        }

        if (rb != null) rb.velocity = targetVelocity;
    }

    public void ApplyImpulse(Vector2 dir, float impulse)
    {
        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;
        float stunTime = GameFormulas.CalcStaggerTime(impulse, mass);

        if (stunTime <= 0f) return;

        isStaggered = true;
        staggerTimer = stunTime;

        float deltaV = impulse / mass;
        float clampedDeltaV = Mathf.Clamp(deltaV, 0f, 20f);

        rb.drag = 5f;
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * clampedDeltaV * mass, ForceMode2D.Impulse);
    }
}