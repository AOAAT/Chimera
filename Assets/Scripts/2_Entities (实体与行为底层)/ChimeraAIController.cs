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
    public bool isDashing = false;
    private float dashTimer = 0f;


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
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (rb != null) rb.velocity = Vector2.zero; return;
        }

        // 处理受击硬直
        if (isStaggered) { staggerTimer -= Time.deltaTime; if (staggerTimer <= 0) { isStaggered = false; rb.drag = 5f; } return; }

        // 👇【新增】：处理冲刺状态 (冲刺期间无视常规走位！)
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
                rb.drag = 5f; // 冲刺结束，恢复刹车阻力
            }
            return; // 冲刺期间，直接 return，不要执行下面的 HandleMovement！
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

    // ==========================================
    // 👇【核心升级】：大运流！真实的物理碰撞判定
    // ==========================================
    private void OnCollisionEnter2D(Collision2D col)
    {
        if (runtimeData == null || rb == null) return;

        Rigidbody2D targetRb = col.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        // 1. 获取双方碰撞瞬间的“相对速度” (Relative Velocity)
        // 比如机甲速度 10，怪物速度 -10 (对撞)，相对速度就是 20！
        float relVelocity = col.relativeVelocity.magnitude;

        // 2. 设定“起撞阈值”：只有相对速度超过 5.0，才算作有效撞击，防止平时走路挤在一起疯狂扣血。
        if (relVelocity > 5.0f)
        {
            DamageReceiver victim = col.gameObject.GetComponentInParent<DamageReceiver>();

            // 撞到了敌人！
            if (victim != null && victim.isEnemy)
            {
                EnemyBrain enemyAI = victim.GetComponent<EnemyBrain>();
                float enemyMass = enemyAI != null && enemyAI.MyData != null ? enemyAI.MyData.GetStat(StatType.Mass) : 5f;

                // 3. 呼叫全局动能公式！(传入自身质量、敌人质量、相对速度)
                float rawDamage = GameFormulas.CalcKineticRamDamage(runtimeData.TotalMass, enemyMass, relVelocity, 2.0f);

                if (rawDamage > 0)
                {
                    // 4. 谁受的伤更重？(重车撞轻车，轻车吃全额伤害，重车只吃少量反弹伤害)
                    // 分配比例：对方越轻，我方受到的反弹伤害越小。
                    float myDamageShare = enemyMass / (runtimeData.TotalMass + enemyMass);
                    float enemyDamageShare = runtimeData.TotalMass / (runtimeData.TotalMass + enemyMass);

                    // 给怪物造成碾压伤害
                    victim.TakeDamage(rawDamage * enemyDamageShare, runtimeData.UnitName + " (泥头车碾压)");

                    // 给自己造成少量的反作用力结构损伤 (如果你觉得玩家撞人自己不该掉血，可以把这行删掉)
                    DamageReceiver myReceiver = GetComponent<DamageReceiver>();
                    if (myReceiver != null) myReceiver.TakeDamage(rawDamage * myDamageShare, "撞击反作用力");

                    // 5. 视角震动反馈 (撞得越狠，震得越厉害)
                    if (ScreenEffectManager.Instance != null)
                    {
                        ScreenEffectManager.Instance.TriggerShake(Mathf.Clamp(rawDamage / 100f, 0.1f, 0.5f), 0.15f);
                    }

                    Debug.Log($"<color=#FFD700>【大运流】</color> 相对速度 {relVelocity:F1}！机甲(M:{runtimeData.TotalMass}) 撞击 怪物(M:{enemyMass})！怪物承受 {rawDamage * enemyDamageShare:F0} 伤害，机甲承受 {rawDamage * myDamageShare:F0} 反噬！");
                }
            }
        }
    }

    // 👇【新增】：提供给 ECA 积木调用的“主动冲刺”接口！
    public void ExecuteDash(Vector2 direction, float speedMultiplier, float duration)
    {
        isDashing = true;
        dashTimer = duration;

        // 冲刺期间极大地降低空气阻力，让它像炮弹一样滑出去！
        rb.drag = 0.5f;

        // 瞬间赋予极高的物理速度 (基础移速 * 冲刺倍率)
        rb.velocity = direction.normalized * (CurrentSpeed * speedMultiplier);
    }
}