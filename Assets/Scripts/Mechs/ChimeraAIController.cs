using System.Collections.Generic;
using UnityEngine;

public class ChimeraAIController : MonoBehaviour
{
    private RuntimeChimeraData runtimeData;
    private Transform currentTarget;
    private Rigidbody2D rb;
    private BuffManager myBuffMgr;
    private DamageReceiver myReceiver;

    [Header("=== 动态物理数据 ===")]
    public float CurrentSpeed;

    [Header("=== 状态控制 ===")]
    public bool isStaggered = false;
    private float staggerTimer = 0f;
    public bool isDashing = false;
    private float dashTimer = 0f;

    [Header("=== RTS 指令寄存器 ===")]
    // 使用 Vector2 存储手动位移点，取消了 WaypointPrefab 实例化
    private Vector2? manualMovePoint = null;
    private Transform manualAttackTarget = null;


    private List<Vector3> currentPath = null;
    private int pathIndex = 0;
    // 向武器模块暴露：当前是否正处于手控位移中
    public bool IsManuallyMoving => manualMovePoint.HasValue;

    private float maxWeaponRange, minWeaponRange, optimalFireRange;

    // ==========================================
    // 🚀 RTS 核心指令接口 (上层指挥官通过这些接口下令)
    // ==========================================

    public void SetManualMovePoint(Vector2 point)
    {
        // 新的移动命令会取消此前的攻击和移动命令。
        manualAttackTarget = null;
        ClearMoveCommand();

        currentPath = GridPathfinder.FindPath(transform.position, point);
        pathIndex = 0;

        // 只有获得了有效路径，才进入“手动移动中”状态。
        // WeaponModule 会根据这个状态禁止普通机甲在移动途中开火。
        if (currentPath != null && currentPath.Count > 1)
        {
            manualMovePoint = point;
        }
        else
        {
            currentPath = null;
            manualMovePoint = null;
        }
    }


    public void SetManualTarget(Transform target)
    {
        ClearMoveCommand(); // 下达集火指令时，完整取消此前的移动路径
        manualAttackTarget = target;
    }

    public void ClearMoveCommand()
    {
        manualMovePoint = null;
        currentPath = null;
        pathIndex = 0;
        if (rb != null) rb.velocity = Vector2.zero;
    }

    public bool HasManualTarget() => manualAttackTarget != null;
    public Transform GetManualTarget() => manualAttackTarget;

    // ==========================================
    // 🛠️ 初始化与属性同步
    // ==========================================

    public void Initialize(RuntimeChimeraData data)
    {
        runtimeData = data;
        rb = GetComponent<Rigidbody2D>();
        myReceiver = GetComponent<DamageReceiver>();
        myBuffMgr = GetComponent<BuffManager>();

        if (rb != null)
        {
            rb.drag = 5f;
            rb.freezeRotation = true;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (myBuffMgr != null)
        {
            // 订阅 Buff 变动，实时重算移速和射程
            myBuffMgr.OnBuffsChanged += RecalculateSpeedAndRanges;
        }

        RecalculateSpeedAndRanges();
        Debug.Log($"<color=#B0C4DE>[AI-中枢] {gameObject.name} 初始化完成。数据代号: {runtimeData.UnitName}</color>");
    }

    private void OnDestroy()
    {
        if (myBuffMgr != null) myBuffMgr.OnBuffsChanged -= RecalculateSpeedAndRanges;
    }

    public void RecalculateSpeedAndRanges()
    {
        if (runtimeData == null) return;

        float speedMult = CombatSandbox.GetSpeed(1f);
        float distMult = CombatSandbox.GetDist(1f);

        // 1. 计算引擎出力 (支持 Buff 修正)
        float pwr = runtimeData.TotalEnginePower;
        if (myBuffMgr != null)
        {
            pwr = myBuffMgr.GetAdjustedStat(StatType.EnginePower, pwr);
        }

        CurrentSpeed = GameFormulas.CalcMoveSpeed(pwr, runtimeData.TotalMass, speedMult);

        // 2. 深度遍历武器库，解算最优交战距离
        maxWeaponRange = 0f; minWeaponRange = 0f; optimalFireRange = float.MaxValue;

        if (runtimeData.EquippedWeapons.Count == 0)
        {
            optimalFireRange = 1.5f * distMult;
        }
        else
        {
            foreach (var wpn in runtimeData.EquippedWeapons)
            {
                float rMax = wpn.GetStat(StatType.MaxRange);
                float rMin = wpn.GetStat(StatType.MinRange);

                if (myBuffMgr != null)
                {
                    rMax = myBuffMgr.GetAdjustedStat(StatType.MaxRange, rMax);
                    rMin = myBuffMgr.GetAdjustedStat(StatType.MinRange, rMin);
                }

                if (rMax * distMult > maxWeaponRange) maxWeaponRange = rMax * distMult;
                if (rMin * distMult > minWeaponRange) minWeaponRange = rMin * distMult;
                if (rMax * distMult < optimalFireRange) optimalFireRange = rMax * distMult;
            }
        }

        if (optimalFireRange < minWeaponRange) optimalFireRange = minWeaponRange + 0.5f;
        if (optimalFireRange == float.MaxValue) optimalFireRange = 5f * distMult;
    }

    // ==========================================
    // 🧠 核心决策 Update
    // ==========================================

    private void Update()
    {
        if (myReceiver == null || myReceiver.CurrentHP <= 0)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        // --- 物理状态阻塞 ---
        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (staggerTimer <= 0) { isStaggered = false; if (rb) rb.drag = 5f; }
            return;
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) { isDashing = false; if (rb) rb.drag = 5f; }
            return;
        }

        // --- 目标有效性审计 ---
        if (manualAttackTarget != null && !IsTargetValid(manualAttackTarget))
        {
            manualAttackTarget = null;
        }

        if (manualAttackTarget != null)
        {
            currentTarget = manualAttackTarget;
        }
        else
        {
            FindTargetAuto();
        }

        // 执行位移
        HandleMovement();
    }

    private void HandleMovement()
    {
        // 优先级 1：RTS 手动指令位移
        if (currentPath != null && pathIndex < currentPath.Count)
        {
            Vector3 targetPos = currentPath[pathIndex];
            float dist = Vector2.Distance(transform.position, targetPos);

            if (dist < 0.2f) // 抵达当前转折点
            {
                pathIndex++;
                if (pathIndex >= currentPath.Count)
                {
                    // 到达最终目的地后退出手动移动状态，允许武器恢复开火。
                    ClearMoveCommand();
                }
            }
            else
            {
                Vector2 dir = (targetPos - transform.position).normalized;
                rb.velocity = dir * CurrentSpeed;
            }
            return;
        }


        // 优先级 2：自动拉扯逻辑 (Kiting)
        if (currentTarget != null)
        {
            float distToTarget = Vector2.Distance(transform.position, currentTarget.position);
            Vector2 dirToTarget = (currentTarget.position - transform.position).normalized;

            if (distToTarget > optimalFireRange)
                rb.velocity = dirToTarget * CurrentSpeed;
            else if (distToTarget < minWeaponRange)
                rb.velocity = -dirToTarget * (CurrentSpeed * 0.5f);
            else
                rb.velocity = Vector2.zero;
        }
        else
        {
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }

    // ==========================================
    // ⚔️ ECA 战斗与物理接口 (严禁改名，供积木调用)
    // ==========================================

    public void ApplyImpulse(Vector2 dir, float impulse, bool ignoreStun = false)
    {
        if (rb == null || runtimeData == null) return;
        float mass = Mathf.Max(runtimeData.TotalMass, 0.5f);

        if (!ignoreStun)
        {
            float stunTime = GameFormulas.CalcStaggerTime(impulse, mass);
            if (stunTime > 0.05f)
            {
                isStaggered = true;
                staggerTimer = stunTime;
                rb.velocity = Vector2.zero;
                rb.drag = 2f;
                if (isDashing) AbortDash();
            }
        }
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    public void ApplyRecoil(Vector2 dir, float impulse, float manualStunTime)
    {
        if (rb == null) return;
        isStaggered = true;
        staggerTimer = manualStunTime;
        rb.drag = 1.0f;
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
    }

    public void ExecuteDash(Vector2 direction, float speedMultiplier, float duration)
    {
        if (rb == null) return;
        isDashing = true;
        dashTimer = duration;
        rb.drag = 0.5f;
        rb.velocity = direction.normalized * (CurrentSpeed * speedMultiplier);

        // 冲刺碰撞检测 (泥头车流派)
        int targetMask = myReceiver.isEnemy ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1.2f, targetMask);
        foreach (var hit in hits)
        {
            DamageReceiver victim = hit.GetComponentInParent<DamageReceiver>();
            if (victim != null && victim.isEnemy != myReceiver.isEnemy)
            {
                float rawDamage = GameFormulas.CalcKineticRamDamage(runtimeData.TotalMass, 5f, rb.velocity.magnitude, 2.0f);
                victim.TakeDamage(rawDamage, runtimeData.UnitName + " (碾压)");
            }
        }
    }

    public void AbortDash()
    {
        isDashing = false;
        if (rb != null) { rb.velocity = Vector2.zero; rb.drag = 5f; }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (runtimeData == null || rb == null) return;
        float relVelocity = col.relativeVelocity.magnitude;
        if (relVelocity > 5.0f)
        {
            DamageReceiver victim = col.gameObject.GetComponentInParent<DamageReceiver>();
            if (victim != null && victim.isEnemy != myReceiver.isEnemy)
            {
                float rawDamage = GameFormulas.CalcKineticRamDamage(runtimeData.TotalMass, 5f, relVelocity, 1.5f);
                if (rawDamage > 10f) victim.TakeDamage(rawDamage, "碰撞");
            }
        }
    }

    // ==========================================
    // 🔍 内部辅助逻辑
    // ==========================================

    private bool IsTargetValid(Transform t)
    {
        if (t == null) return false;
        DamageReceiver dr = t.GetComponentInParent<DamageReceiver>();
        return dr != null && dr.CurrentHP > 0 && t.gameObject.activeInHierarchy;
    }

    private void FindTargetAuto()
    {
        bool iAmEnemy = myReceiver != null && myReceiver.isEnemy;
        var targetList = iAmEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
        if (targetList == null || targetList.Count == 0) { currentTarget = null; return; }

        DamageReceiver bestCandidate = null; float minDist = float.MaxValue;
        for (int i = 0; i < targetList.Count; i++)
        {
            DamageReceiver potential = targetList[i];
            if (potential == null || potential.CurrentHP <= 0) continue;
            float dist = Vector3.Distance(transform.position, potential.transform.position);
            if (dist < minDist) { minDist = dist; bestCandidate = potential; }
        }
        currentTarget = bestCandidate != null ? bestCandidate.transform : null;
    }
}
