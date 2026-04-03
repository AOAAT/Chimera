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

    // 👇【新增】：物理失控态
    [Header("=== 物理状态 ===")]
    public bool isStaggered = false;
    private float staggerTimer = 0f;

    // 缓存武器射程数据
    private float maxWeaponRange = 0f;
    private float minWeaponRange = 0f;

    public void Initialize(RuntimeChimeraData data)
    {
        runtimeData = data;

        float speedMult = 1f;
        float distMult = 1f;
        if (CombatSandbox.Instance != null)
        {
            speedMult = CombatSandbox.Instance.SpeedMultiplier;
            distMult = CombatSandbox.Instance.DistanceMultiplier;
        }

        float mass = Mathf.Max(runtimeData.TotalMass, 0.5f);
        CurrentSpeed = Mathf.Max(0.1f, (runtimeData.TotalEnginePower / mass) * speedMult);

        float powerCost = Mathf.Max(runtimeData.TotalPowerCost, 1f);
        MaxStamina = Mathf.Max(20f, (runtimeData.TotalEnginePower / powerCost) * 0.1f);
        CurrentStamina = MaxStamina;

        if (runtimeData.EquippedWeapons.Count > 0)
        {
            maxWeaponRange = runtimeData.EquippedWeapons.Max(w => w.GetStat(StatType.MaxRange)) * distMult;
            minWeaponRange = runtimeData.EquippedWeapons.Min(w => w.GetStat(StatType.MaxRange)) * distMult;
        }

        runtimeData.SafeDodgeDistance *= distMult;

        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (runtimeData == null) return;

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        // 👇【物理引擎强制接管】：挨打硬直期间，大脑断电！
        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0)
            {
                isStaggered = false;
                rb.drag = 0f; // 恢复正常摩擦力
            }
            return;
        }

        if (IsExhausted)
        {
            if (rb != null) rb.velocity = Vector2.zero;

            exhaustionTimer -= Time.deltaTime;
            CurrentStamina += (MaxStamina * 0.2f) * Time.deltaTime;
            TintMech(new Color(1f, 0.5f, 0.5f));

            if (exhaustionTimer <= 0)
            {
                IsExhausted = false;
                TintMech(Color.white);
            }
            return;
        }

        TintMech(Color.white);

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
            if (CurrentStamina <= 0)
            {
                CurrentStamina = 0;
                IsExhausted = true;
                exhaustionTimer = 3f;
            }
        }
        else
        {
            if (CurrentStamina < MaxStamina) CurrentStamina += 3f * Time.deltaTime;
        }
    }

    // 👇【全新机制】：物理冲击接收器
    public void ApplyImpulse(Vector2 dir, float impulse)
    {
        float mass = runtimeData != null ? Mathf.Max(runtimeData.TotalMass, 0.5f) : 10f;
        float deltaV = impulse / mass;

        if (deltaV < 0.5f) return;

        float stunTime = deltaV * 0.05f;
        if (stunTime < 0.1f) stunTime = 0.1f;

        isStaggered = true;
        staggerTimer = stunTime;

        rb.drag = 5f; // 摩擦力飙升，模拟地上摩擦
        rb.velocity = Vector2.zero;
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying && runtimeData != null)
        {
            if (runtimeData.MovementLogic == MovementStrategy.Dodge)
            {
                Gizmos.color = Color.green;
                Vector3 logicCenter = transform.TransformPoint(runtimeData.LogicCenterOffset);
                Gizmos.DrawWireSphere(logicCenter, runtimeData.SafeDodgeDistance);
            }
        }
    }

    private void TintMech(Color targetColor)
    {
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in allRenderers) sr.color = targetColor;
    }
}