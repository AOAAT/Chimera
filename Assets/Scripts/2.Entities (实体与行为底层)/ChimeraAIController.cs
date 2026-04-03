// --- START OF FILE ChimeraAIController.cs ---
using System.Linq;
using UnityEngine;

public class ChimeraAIController : MonoBehaviour
{
    private RuntimeChimeraData runtimeData;
    private Transform currentTarget;
    private Rigidbody2D rb;
    private Collider2D myCollider;

    [Header("=== 动态物理计算结果 ===")]
    public float CurrentSpeed;
    public float MaxStamina;
    public float CurrentStamina;
    public bool IsExhausted = false;
    private float exhaustionTimer = 0f;

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

        CurrentSpeed = GameFormulas.CalcMoveSpeed(runtimeData.TotalEnginePower, runtimeData.TotalMass, speedMult);
        MaxStamina = GameFormulas.CalcMaxStamina(runtimeData.TotalEnginePower, runtimeData.TotalPowerCost);
        CurrentStamina = MaxStamina;

        maxWeaponRange = 0f;
        minWeaponRange = float.MaxValue;
        foreach (var wpn in runtimeData.EquippedWeapons)
        {
            float maxR = wpn.GetStat(StatType.MaxRange) * distMult;
            float minR = wpn.GetStat(StatType.MinRange) * distMult;
            if (maxR > maxWeaponRange) maxWeaponRange = maxR;
            if (minR < minWeaponRange) minWeaponRange = minR;
        }

        // 👇【防排斥灾难 1】：强制保留至少 1.5 米的接敌距离，绝对不准机甲钻进敌人体内！
        if (minWeaponRange == float.MaxValue) minWeaponRange = 1.5f * distMult;
        else if (minWeaponRange < 1.5f * distMult) minWeaponRange = 1.5f * distMult;

        if (maxWeaponRange < minWeaponRange) maxWeaponRange = minWeaponRange;

        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();

        // 👇【防排斥灾难 2】：强制给机甲上 5 的物理摩擦力（阻力），让机甲变得稳如泰山！
        if (rb != null) rb.drag = 5f;
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
            if (staggerTimer <= 0)
            {
                isStaggered = false;
                // 👇【物理修复 3】：硬直恢复后，必须恢复 5 点摩擦力，绝对不能设回 0！
                rb.drag = 5f;
            }
            return;
        }

        if (IsExhausted)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            exhaustionTimer -= Time.deltaTime;
            CurrentStamina += (MaxStamina * 0.2f) * Time.deltaTime;
            if (exhaustionTimer <= 0) IsExhausted = false;
            return;
        }

        FindTarget();
        HandleMovementAndStamina();
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

    private void HandleMovementAndStamina()
    {
        if (currentTarget == null)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (CurrentStamina < MaxStamina) CurrentStamina += 3f * Time.deltaTime;
            return;
        }

        bool isMoving = false;
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

        if (runtimeData.MovementLogic == MovementStrategy.Dodge && dist < runtimeData.SafeDodgeDistance)
        {
            targetVelocity = -dirToTarget * CurrentSpeed;
            isMoving = true;
        }
        else if (runtimeData.MovementLogic == MovementStrategy.Active_Survival && dist > maxWeaponRange)
        {
            targetVelocity = dirToTarget * CurrentSpeed;
            isMoving = true;
        }
        else if (runtimeData.MovementLogic == MovementStrategy.Active_Firepower && dist > minWeaponRange)
        {
            targetVelocity = dirToTarget * CurrentSpeed;
            isMoving = true;
        }

        if (IsExhausted) targetVelocity = Vector2.zero;
        if (rb != null) rb.velocity = targetVelocity;

        if (isMoving)
        {
            CurrentStamina -= 5f * Time.deltaTime;
            if (CurrentStamina <= 0) { CurrentStamina = 0; IsExhausted = true; exhaustionTimer = 3f; }
        }
        else
        {
            if (CurrentStamina < MaxStamina) CurrentStamina += 3f * Time.deltaTime;
        }
    }

    public void ApplyImpulse(Vector2 dir, float impulse)
    {
        float mass = runtimeData != null ? runtimeData.TotalMass : 10f;
        float stunTime = GameFormulas.CalcStaggerTime(impulse, mass);

        if (stunTime <= 0f) return;

        isStaggered = true;
        staggerTimer = stunTime;

        rb.drag = 5f;
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }
}