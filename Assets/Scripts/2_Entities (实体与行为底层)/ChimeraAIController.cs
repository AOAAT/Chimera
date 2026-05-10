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

    [Header("=== 战术视觉引用 ===")]
    public GameObject WaypointPrefab; // 在 Inspector 中拖入你的路点预制体
    private GameObject currentWaypointInstance;


    private Vector2? manualMovePoint = null;
    private Transform manualAttackTarget = null;

    // 向武器模块暴露：当前是否正处于手控位移中
    public bool IsManuallyMoving => manualMovePoint.HasValue;


    private float maxWeaponRange, minWeaponRange, optimalFireRange;
    public void AbortDash()
    {
        isDashing = false;
        if (rb != null) rb.velocity = Vector2.zero;
        rb.drag = 5f; // 恢复正常摩擦力
    }
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

    public void SetManualMovePoint(Vector2 point)
    {
        // --- 👇 核心逻辑 A：互斥熔断 ---
        manualAttackTarget = null; // 移动时，立即断开集火红线

        manualMovePoint = point;

        // --- 👇 核心逻辑 B：路点视觉管理 ---
        if (currentWaypointInstance != null) Destroy(currentWaypointInstance);

        if (WaypointPrefab != null)
        {
            currentWaypointInstance = Instantiate(WaypointPrefab, new Vector3(point.x, point.y, 0), Quaternion.identity);
        }

        Debug.Log("<color=cyan>【指令】</color> 正在脱离交火，前往坐标点。");
    }
    public void SetManualTarget(Transform target)
    {
        // --- 👇 核心逻辑 C：互斥熔断 ---
        ClearMoveCommand(); // 锁定敌人时，立即停止当前的路径导航

        manualAttackTarget = target;
        Debug.Log($"<color=red>【指令】</color> 已截获目标信号：{target.name}，开始拉扯集火。");
    }

    public void ClearMoveCommand()
    {
        manualMovePoint = null;
        if (currentWaypointInstance != null)
        {
            // 这里可以播放一个“抵达”或“消失”的微弱特效
            Destroy(currentWaypointInstance);
        }
    }
    public bool HasManualTarget() => manualAttackTarget != null;
    public Transform GetManualTarget() => manualAttackTarget;

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

        // 2. 计算射程区间 (支持 Buff 修正)
        maxWeaponRange = 0f; minWeaponRange = 0f; optimalFireRange = float.MaxValue;

        if (runtimeData.EquippedWeapons.Count == 0) optimalFireRange = 1.5f * distMult;
        else
        {
            foreach (var wpn in runtimeData.EquippedWeapons)
            {
                float rMax = wpn.GetStat(StatType.MaxRange);
                float rMin = wpn.GetStat(StatType.MinRange);

                if (myBuffMgr != null)
                {
                    // 👇【核心系统调用】
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

    private void Update()
    {
        // 如果血量归零，强制停止所有物理输出
        if (myReceiver == null || myReceiver.CurrentHP <= 0)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        if (myReceiver == null || myReceiver.CurrentHP <= 0) { if (rb != null) rb.velocity = Vector2.zero; return; }
        if (runtimeData == null || (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive))
        { if (rb != null) rb.velocity = Vector2.zero; return; }

        if (isStaggered) { staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { isStaggered = false; rb.drag = 5f; } return; }
        if (isDashing) { dashTimer -= Time.deltaTime; if (dashTimer <= 0) { isDashing = false; rb.drag = 5f; } return; }

        // 1. 索敌逻辑：手控目标具有绝对持久性
        if (manualAttackTarget != null && !IsTargetValid(manualAttackTarget))
        {
            manualAttackTarget = null; // 目标死亡或消失，清除锁定
        }

        // 2. 只有在没有有效手控目标时，才跑自动索敌
        if (manualAttackTarget != null)
        {
            currentTarget = manualAttackTarget;
        }
        else
        {
            FindTargetAuto();
        }

        HandleMovement();
    }

    private bool IsTargetValid(Transform t)
    {
        if (t == null) return false;
        DamageReceiver dr = t.GetComponentInParent<DamageReceiver>();
        return dr != null && dr.CurrentHP > 0 && t.gameObject.activeInHierarchy;
    }

    private void FindTargetAuto()
    {
        bool iAmEnemy = myReceiver.isEnemy;
        var targetList = iAmEnemy ? CombatDirector.ActivePlayerUnits : CombatDirector.ActiveEnemies;
        if (targetList.Count == 0) { currentTarget = null; return; }

        // 自动索敌逻辑 (按距离找最近)
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

    private void HandleMovement()
    {
        // 优先级 A：手控位移 (手动风筝/撤离)
        if (manualMovePoint.HasValue)
        {
            float dist = Vector2.Distance(transform.position, manualMovePoint.Value);
            // 抵达判定阈值：0.4米
            if (dist < 0.4f)
            {
                ClearMoveCommand(); // 抵达后，自动销毁地面光圈
                rb.velocity = Vector2.zero;
            }
            else
            {
                rb.velocity = (manualMovePoint.Value - (Vector2)transform.position).normalized * CurrentSpeed;
            }
            return;
        }

        // 优先级 B：AI 自动位移
        // (如果当前是锁定目标打，AI 也会尝试根据射程自动拉扯)
        Vector2 dirToTarget = (currentTarget.position - transform.position).normalized;
        float distToTarget = Vector2.Distance(transform.position, currentTarget.position);

        Vector2 targetVelocity = Vector2.zero;
        if (distToTarget > optimalFireRange) targetVelocity = dirToTarget * CurrentSpeed;
        else if (distToTarget < minWeaponRange) targetVelocity = -dirToTarget * (CurrentSpeed * 0.5f);

        if (rb != null) rb.velocity = targetVelocity;
    }


    // ==========================================
    // 物理接口与碰撞逻辑 (保持原有逻辑不变)
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
                rb.velocity = Vector2.zero; // 物理熔断
                rb.drag = 2f;

                // 如果正在冲刺，强制强断
                if (isDashing) AbortDash();
            }
        }

        // 修复：直接使用物理推力，不再进行二次复杂的 deltaV 换算
        rb.AddForce(dir * impulse, ForceMode2D.Impulse);
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

    private void OnDisable()
    {
        if (currentWaypointInstance != null) Destroy(currentWaypointInstance);
    }
}