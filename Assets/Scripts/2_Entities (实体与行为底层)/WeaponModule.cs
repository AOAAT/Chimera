// --- START OF FILE WeaponModule.cs ---
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponModule : MonoBehaviour
{
    // ==========================================
    // 状态机定义
    // ==========================================
    private enum WeaponState { Idle, Windup, Strike, Recovery }
    private WeaponState currentState = WeaponState.Idle;

    private RuntimeWeapon weaponData;

    // 核心时间轴变量
    private float totalCooldown = 0f; // 总攻击间隔
    private float stateTimer = 0f;    // 当前状态的倒数计时

    // 近战动作参数缓存
    private float t_Windup, t_Strike, t_Recovery;
    private Quaternion rot_Base, rot_Windup, rot_Strike;
    private float aimAngle; // 锁定敌人时的基准瞄准角度

    // 逻辑心脏数据
    private Vector2 logicCenterOffset;
    private Transform mechRoot;

    // 视觉节点
    private Transform actualHinge;
    private Transform muzzlePoint;
    private Animator myAnimator;

    [Header("调试信息")]
    public List<Transform> CurrentTargets = new List<Transform>();

    public void Initialize(RuntimeWeapon data, Vector2 centerOffset, Transform root)
    {
        weaponData = data;
        logicCenterOffset = centerOffset;
        mechRoot = root;

        actualHinge = GetActualHinge();

        if (actualHinge.childCount > 0)
            myAnimator = actualHinge.GetChild(0).GetComponent<Animator>();

        GameObject muzzleObj = new GameObject("MuzzlePoint");
        muzzleObj.transform.SetParent(actualHinge, false);
        muzzleObj.transform.localPosition = data.SourceSO.MuzzleOffset;
        muzzlePoint = muzzleObj.transform;

        currentState = WeaponState.Idle;
        stateTimer = 0f; // 开局可以直接开火
    }

    public Vector3 GetLogicCenter()
    {
        if (mechRoot != null) return mechRoot.TransformPoint(logicCenterOffset);
        return transform.position;
    }

    private Transform GetActualHinge()
    {
        if (transform.name.StartsWith("Socket_") && transform.childCount > 0)
        {
            return transform.GetChild(0);
        }
        return transform;
    }

    private float GetMaxDistanceFromBounds(Vector2 center, Bounds bounds)
    {
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;

        float d1 = Vector2.SqrMagnitude(center - new Vector2(min.x, min.y));
        float d2 = Vector2.SqrMagnitude(center - new Vector2(max.x, min.y));
        float d3 = Vector2.SqrMagnitude(center - new Vector2(min.x, max.y));
        float d4 = Vector2.SqrMagnitude(center - new Vector2(max.x, max.y));

        return Mathf.Sqrt(Mathf.Max(d1, Mathf.Max(d2, Mathf.Max(d3, d4))));
    }

    private void FindTarget()
    {
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1.0f;
        float maxRange = weaponData.GetStat(StatType.MaxRange) * distMult;
        float minRange = weaponData.GetStat(StatType.MinRange) * distMult;
        int maxLockCount = Mathf.Max((int)weaponData.GetStat(StatType.MultiShotCount), 1);
        Vector3 center = GetLogicCenter();

        DamageReceiver myReceiver = mechRoot.GetComponent<DamageReceiver>();
        bool amIEnemy = (myReceiver != null && myReceiver.isEnemy);

        int targetLayerMask = amIEnemy ? LayerMask.GetMask("Player_Hitbox") : LayerMask.GetMask("Enemy_Hitbox");

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, maxRange, targetLayerMask);

        CurrentTargets = hits
            .Select(hit => {
                DamageReceiver r = hit.GetComponentInParent<DamageReceiver>();
                return new { Collider = hit, Receiver = r };
            })
            .Where(x => x.Receiver != null && x.Receiver.CurrentHP > 0)
            .Where(x => {
                float distToFurthest = GetMaxDistanceFromBounds(center, x.Collider.bounds);
                return distToFurthest >= minRange;
            })
            .GroupBy(x => x.Receiver)
            .Select(group => group.First())
            .OrderBy(x => {
                Vector2 closestPoint = x.Collider.ClosestPoint(center);
                return Vector2.Distance(center, closestPoint);
            })
            .Take(maxLockCount)
            .Select(x => x.Receiver.transform)
            .ToList();
    }

    private void Update()
    {
        if (weaponData == null || actualHinge == null) return;

        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) return;

        FindTarget();

        // 获取或保持目标
        Transform primaryTarget = (CurrentTargets.Count > 0) ? CurrentTargets[0] : null;

        // ==========================================
        // 核心状态机：基于时间的动作流转
        // ==========================================
        switch (currentState)
        {
            case WeaponState.Idle:
                // 在 Idle 状态，武器死死盯住敌人
                if (primaryTarget != null)
                {
                    Vector3 targetCenter = GetTargetCenter(primaryTarget);
                    Vector3 aimDir = targetCenter - actualHinge.position;
                    aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
                    actualHinge.rotation = Quaternion.AngleAxis(aimAngle, Vector3.forward);

                    // 如果冷却好了，开始攻击！
                    if (stateTimer <= 0f)
                    {
                        InitiateAttack();
                    }
                }

                // 冷却倒数 (即使没有敌人，冷却也在走)
                if (stateTimer > 0f) stateTimer -= Time.deltaTime;
                break;

            case WeaponState.Windup:
                // 蓄力抬手阶段
                stateTimer -= Time.deltaTime;

                // 插值计算当前角度 (从 Base 缓慢拉到 Windup)
                float windupProgress = 1f - (stateTimer / t_Windup);
                actualHinge.rotation = Quaternion.Slerp(rot_Base, rot_Windup, windupProgress);

                if (stateTimer <= 0f)
                {
                    // 蓄力结束，进入下劈！
                    currentState = WeaponState.Strike;
                    stateTimer = t_Strike;
                }
                break;

            case WeaponState.Strike:
                // 下劈爆发阶段
                stateTimer -= Time.deltaTime;

                // 极速下砸！(从 Windup 瞬间砸到 Strike)
                float strikeProgress = 1f - (stateTimer / t_Strike);
                actualHinge.rotation = Quaternion.Slerp(rot_Windup, rot_Strike, strikeProgress);

                if (stateTimer <= 0f)
                {
                    // 👇【终极奥义】：下砸到最低点的瞬间，触发伤害判定和特效！
                    FirePayload();

                    // 下劈结束，进入收招！
                    currentState = WeaponState.Recovery;
                    stateTimer = t_Recovery;
                }
                break;

            case WeaponState.Recovery:
                // 僵直收招阶段
                stateTimer -= Time.deltaTime;

                // 缓慢收回，回到瞄准状态 (从 Strike 回到 Base)
                float recoveryProgress = 1f - (stateTimer / t_Recovery);
                actualHinge.rotation = Quaternion.Slerp(rot_Strike, rot_Base, recoveryProgress);

                if (stateTimer <= 0f)
                {
                    // 一个完整的攻击周期结束，回到 Idle 等待下一次开火
                    currentState = WeaponState.Idle;
                    stateTimer = 0f; // 理论上不需要等了，因为整个周期的时间就是总攻速
                }
                break;
        }
    }

    // ==========================================
    // 动作流转方法
    // ==========================================

    // 发起攻击 (计算各个阶段的时间和目标角度)
    private void InitiateAttack()
    {
        float atkSpeed = weaponData.GetStat(StatType.AttackSpeed);
        if (atkSpeed <= 0) atkSpeed = 100f;

        // 计算这一刀的总体时间 (受全局攻速公式影响)
        totalCooldown = GameFormulas.CalcCooldown(atkSpeed);

        // 如果是远程武器，或者根本没配动作比例，直接跳过动画进入 Fire
        if (weaponData.DeliveryType == WeaponDeliveryType.Ranged)
        {
            FirePayload();
            stateTimer = totalCooldown; // 远程武器依然走传统的冷却
            return;
        }

        // 👇【近战专属】：切分时间片与目标角度！
        t_Windup = totalCooldown * weaponData.SourceSO.WindupTimeRatio;
        t_Strike = totalCooldown * weaponData.SourceSO.StrikeTimeRatio;
        t_Recovery = totalCooldown - t_Windup - t_Strike;

        // 计算旋转四元数
        rot_Base = Quaternion.AngleAxis(aimAngle, Vector3.forward);
        rot_Windup = rot_Base * Quaternion.Euler(0f, 0f, weaponData.SourceSO.WindupAngle);
        rot_Strike = rot_Base * Quaternion.Euler(0f, 0f, weaponData.SourceSO.StrikeAngle);

        // 切换状态！开始蓄力！
        currentState = WeaponState.Windup;
        stateTimer = t_Windup;

        // 如果有真实动画，也可以在这里 Trigger
        if (myAnimator != null) myAnimator.SetTrigger("Windup");
    }

    // 真正执行伤害判定 (下砸到最低点时调用)
    private void FirePayload()
    {
        if (CurrentTargets.Count == 0) return;

        // 依然保留真实的动画触发器，双重保障
        if (myAnimator != null) myAnimator.SetTrigger("Fire");

        float safeMinDmg = Mathf.Max(0f, weaponData.GetStat(StatType.MinDamage));
        float safeMaxDmg = Mathf.Max(safeMinDmg, weaponData.GetStat(StatType.MaxDamage)); // 确保上限永远 >= 下限

        float finalDmg = Random.Range(safeMinDmg, safeMaxDmg);
        float totalCritChance = weaponData.GetStat(StatType.CriticalChance) + weaponData.BonusCriticalChance;
        bool isCrit = Random.value <= totalCritChance;
        if (isCrit) finalDmg *= 1.5f;

        ECAContext fireContext = new ECAContext
        {
            ImpactPoint = muzzlePoint.position,
            PrimaryTarget = CurrentTargets[0],
            BaseDamage = finalDmg,
            SourceWeapon = weaponData,
            IsCriticalHit = isCrit,
            IsEnemyFire = false,
            SourceEntity = mechRoot
        };

        if (weaponData.OnFireActions != null)
        {
            foreach (var action in weaponData.OnFireActions)
            {
                if (action != null)
                {
                    action.Execute(fireContext);
                    // 👇【熔断拦截】：如果刚才执行的积木（比如扣CP）失败并引发了熔断，直接停止开火！
                    if (fireContext.ExecutionAborted) return;
                }
            }
        }

        foreach (var target in CurrentTargets)
        {
            Vector3 visualTargetCenter = GetTargetCenter(target);

            if (weaponData.DeliveryType == WeaponDeliveryType.Melee)
            {
                ECAContext hitContext = new ECAContext { ImpactPoint = visualTargetCenter, PrimaryTarget = target, BaseDamage = finalDmg, SourceWeapon = weaponData, IsCriticalHit = isCrit, IsEnemyFire = false };
                if (weaponData.OnHitActions != null) foreach (var action in weaponData.OnHitActions) if (action != null) action.Execute(hitContext);
            }
            else if (weaponData.DeliveryType == WeaponDeliveryType.Ranged && weaponData.ProjectilePrefab != null)
            {
                Vector3 bulletDir = visualTargetCenter - muzzlePoint.position;
                float bulletAngle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg;
                Quaternion bulletRot = Quaternion.AngleAxis(bulletAngle, Vector3.forward);

                GameObject projObj = Instantiate(weaponData.ProjectilePrefab, muzzlePoint.position, bulletRot);
                Projectile projectile = projObj.GetComponent<Projectile>();
                projectile.Fire(target, finalDmg, weaponData, false, isCrit);
            }
        }
    }

    // 辅助方法：获取目标的物理中心
    private Vector3 GetTargetCenter(Transform target)
    {
        if (target == null) return Vector3.zero;
        Vector3 targetCenter = target.position;
        Collider2D targetCol = target.GetComponentInChildren<Collider2D>();
        if (targetCol != null) targetCenter = targetCol.bounds.center;
        return targetCenter;
    }

    private void OnDrawGizmos()
    {
        if (weaponData == null) return;
        float distanceMultiplier = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1.0f;
        float maxRange = weaponData.GetStat(StatType.MaxRange) * distanceMultiplier;
        float minRange = weaponData.GetStat(StatType.MinRange) * distanceMultiplier;

        Vector3 center = GetLogicCenter();
        Gizmos.color = new Color(0, 0, 1f, 0.3f);
        Gizmos.DrawWireSphere(center, maxRange);

        if (minRange > 0f)
        {
            Gizmos.color = new Color(1f, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(center, minRange);
        }
    }
}