using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponModule : MonoBehaviour
{
    private RuntimeWeapon weaponData;
    private float fireCooldown = 0f;

    // 逻辑心脏数据
    private Vector2 logicCenterOffset;
    private Transform mechRoot;

    // 👇 新增：真实的枪口节点和动画器
    private Transform muzzlePoint;
    private Animator myAnimator;


    [Header("调试信息")]
    public List<Transform> CurrentTargets = new List<Transform>();

    public void Initialize(RuntimeWeapon data, Vector2 centerOffset, Transform root)
    {
        weaponData = data;
        logicCenterOffset = centerOffset;
        mechRoot = root;

        Transform actualHinge = GetActualHinge();

        // 1. 尝试获取动画器（如果在装配时挂载了的话）
        if (actualHinge.childCount > 0)
            myAnimator = actualHinge.GetChild(0).GetComponent<Animator>();

        // 2. 实例化真实枪口节点
        GameObject muzzleObj = new GameObject("MuzzlePoint");
        muzzleObj.transform.SetParent(actualHinge, false);
        // 枪口相对把手(Hinge)的偏移
        muzzleObj.transform.localPosition = data.SourceSO.MuzzleOffset;
        muzzlePoint = muzzleObj.transform;
    }

    // 获取绝对世界坐标下的逻辑心脏
    public Vector3 GetLogicCenter() => mechRoot != null ? mechRoot.TransformPoint(logicCenterOffset) : transform.position;

    private Transform GetActualHinge()
    {
        if (transform.name.StartsWith("Socket_") && transform.childCount > 0) return transform.GetChild(0);
        return transform;
    }

    private void Update()
    {
        if (weaponData == null) return;

        // 👇【核心静默控制】：没开战不准雷达扫怪，也不准冷却转CD！保险关死！
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive) return;

        fireCooldown -= Time.deltaTime;
        FindTarget();

        if (CurrentTargets != null && CurrentTargets.Count > 0)
        {
            Transform primaryTarget = CurrentTargets[0];
            if (primaryTarget != null)
            {
                // 👇【视觉修复】：枪管必须从自己的真实把手(Hinge)位置，死死盯住敌人！
                Transform actualHinge = GetActualHinge();
                Vector3 aimDir = primaryTarget.position - actualHinge.position;
                float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

                // 转动转轴，完美保留 -AnchorOffset 齿轮效应！
                actualHinge.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            if (fireCooldown <= 0f) Fire();
        }
    }

    // 👇【主程新增】：计算一个包围盒（Bounds）距离中心点的最远物理距离
    private float GetMaxDistanceFromBounds(Vector2 center, Bounds bounds)
    {
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;

        // 计算四个角的距离平方，取最大值
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

        // 👇【阵营判定】：看看这把枪是挂在机甲身上还是敌人身上？
        DamageReceiver myReceiver = mechRoot.GetComponent<DamageReceiver>();
        bool amIEnemy = (myReceiver != null && myReceiver.isEnemy);

        // 👇【雷达过滤】：机甲只扫 Enemy_Hitbox，敌人只扫 Player_Hitbox！
        int targetLayerMask = amIEnemy ?
            LayerMask.GetMask("Player_Hitbox") :
            LayerMask.GetMask("Enemy_Hitbox");

        // 👇【物理引擎级索敌】：瞬间拿到射程圈内的所有碰撞体，性能薄纱 FindObjectsOfType！
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, maxRange, targetLayerMask);

        CurrentTargets = hits
            .Select(hit => {
                // 顺藤摸瓜找血条
                DamageReceiver r = hit.GetComponentInParent<DamageReceiver>();
                return new { Collider = hit, Receiver = r };
            })
            .Where(x => x.Receiver != null && x.Receiver.CurrentHP > 0)
            .Where(x => {
                // 👇【核心修复】：从“查鼻尖”改为“查包围盒最远点”！
                // 只要怪物的最远端还在红圈外（距离 >= 最小射程），就说明它没有完全钻进盲区，依然可以攻击！
                float distToFurthest = GetMaxDistanceFromBounds(center, x.Collider.bounds);
                return distToFurthest >= minRange;
            })
            // 👇 因为同一个怪物可能有多个零件/Hitbox被扫到，必须去重！
            .GroupBy(x => x.Receiver)
            .Select(group => group.First()) // 每个怪物只取离中心最近的那个 Hitbox
            .OrderBy(x => {
                // 排序依然保留“查鼻尖”逻辑，确保炮口永远优先攻击物理上离自己最近的敌人
                Vector2 closestPoint = x.Collider.ClosestPoint(center);
                return Vector2.Distance(center, closestPoint);
            })
            .Take(maxLockCount)
            .Select(x => x.Receiver.transform)
            .ToList();
    }

    private void Fire()
    {
        float atkSpeed = weaponData.GetStat(StatType.AttackSpeed);
        if (atkSpeed <= 0) atkSpeed = 100f;
        fireCooldown = 100f / atkSpeed;

        if (CurrentTargets.Count == 0) return;

        // 👇【表现层：触发开火动画】
        if (myAnimator != null) myAnimator.SetTrigger("Fire");

        float finalDmg = Random.Range(weaponData.GetStat(StatType.MinDamage), weaponData.GetStat(StatType.MaxDamage));
        float totalCritChance = weaponData.GetStat(StatType.CriticalChance) + weaponData.BonusCriticalChance;
        bool isCrit = Random.value <= totalCritChance;
        if (isCrit) finalDmg *= 1.5f;

        // 👇【核心分离】：火光和事件，全部发生在真实的枪口 (MuzzlePoint)！
        ECAContext fireContext = new ECAContext
        {
            ImpactPoint = muzzlePoint.position, // <--- 这里！绝对精准！
            PrimaryTarget = CurrentTargets[0],
            BaseDamage = finalDmg,
            SourceWeapon = weaponData,
            IsCriticalHit = isCrit,
            IsEnemyFire = false
        };

        if (weaponData.OnFireActions != null)
            foreach (var action in weaponData.OnFireActions)
                if (action != null) action.Execute(fireContext);

        foreach (var target in CurrentTargets)
        {
            if (weaponData.DeliveryType == WeaponDeliveryType.Melee)
            {
                ECAContext hitContext = new ECAContext { ImpactPoint = target.position, PrimaryTarget = target, BaseDamage = finalDmg, SourceWeapon = weaponData, IsCriticalHit = isCrit, IsEnemyFire = false };
                if (weaponData.OnHitActions != null) foreach (var action in weaponData.OnHitActions) if (action != null) action.Execute(hitContext);
            }
            else if (weaponData.DeliveryType == WeaponDeliveryType.Ranged && weaponData.ProjectilePrefab != null)
            {
                // 子弹也从真实枪口飞出！
                Vector3 bulletDir = target.position - muzzlePoint.position;
                float bulletAngle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg;
                Quaternion bulletRot = Quaternion.AngleAxis(bulletAngle, Vector3.forward);

                GameObject projObj = Instantiate(weaponData.ProjectilePrefab, muzzlePoint.position, bulletRot);
                Projectile projectile = projObj.GetComponent<Projectile>();
                projectile.Fire(target, finalDmg, weaponData, false, isCrit);
            }
        }
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

        // 【开发可视化】：画一条紫色的线，帮你直观看到子弹从哪里飞出去的
        Transform actualHinge = GetActualHinge();
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(center, actualHinge.position);
        Gizmos.DrawSphere(Vector3.Lerp(center, actualHinge.position, 0.3f), 0.05f);
    }
}