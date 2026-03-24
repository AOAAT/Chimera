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

    [Header("调试信息")]
    public List<Transform> CurrentTargets = new List<Transform>();

    public void Initialize(RuntimeWeapon data, Vector2 centerOffset, Transform root)
    {
        weaponData = data;
        logicCenterOffset = centerOffset;
        mechRoot = root;
    }

    // 获取绝对世界坐标下的逻辑心脏
    public Vector3 GetLogicCenter()
    {
        if (mechRoot != null) return mechRoot.TransformPoint(logicCenterOffset);
        return transform.position;
    }

    // 👇【神级防坑】：智能寻找真正的转轴，绝不去碰贴图(Sprite_Visual)！
    private Transform GetActualHinge()
    {
        // 如果挂在了插槽 (Socket) 上，它的第一个子节点才是 Hinge
        if (transform.name.StartsWith("Socket_") && transform.childCount > 0)
        {
            return transform.GetChild(0);
        }
        // 如果它自己就叫 Hinge (测试台的情况)，那它自己就是转轴！
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
                // 精准剔除在“最小射程盲区”内的目标
                Vector2 closestPoint = x.Collider.ClosestPoint(center);
                float d = Vector2.Distance(center, closestPoint);
                return d >= minRange;
            })
            // 👇 因为同一个怪物可能有多个零件/Hitbox被扫到，必须去重！
            .GroupBy(x => x.Receiver)
            .Select(group => group.First()) // 每个怪物只取离中心最近的那个 Hitbox
            .OrderBy(x => {
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

        float finalDmg = Random.Range(weaponData.GetStat(StatType.MinDamage), weaponData.GetStat(StatType.MaxDamage));
        float totalCritChance = weaponData.GetStat(StatType.CriticalChance) + weaponData.BonusCriticalChance;
        bool isCrit = Random.value <= totalCritChance;
        if (isCrit) finalDmg *= 1.5f;

        // 👇【新增评估日志】：玩家武器开火详情
        string critLog = isCrit ? "<color=#FFD700><b>(暴击!)</b></color>" : "";
        Debug.Log($"<color=#00FFFF>【玩家开火】</color> 武器 [{weaponData.WeaponName}] 锁定了 [{CurrentTargets[0].name}] | 判定伤害: {finalDmg:F1} {critLog}");

        // 触发开火动作，标记 IsEnemyFire = false
        ECAContext fireContext = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = CurrentTargets[0],
            BaseDamage = finalDmg,
            SourceWeapon = weaponData,
            IsCriticalHit = isCrit,
            IsEnemyFire = false // 玩家开火！
        };

        if (weaponData.OnFireActions != null)
            foreach (var action in weaponData.OnFireActions)
                if (action != null) action.Execute(fireContext);

        Vector3 logicCenter = GetLogicCenter();
        Transform actualHinge = GetActualHinge();
        Vector3 spawnPos = Vector3.Lerp(logicCenter, actualHinge.position, 0.3f);

        foreach (var target in CurrentTargets)
        {
            if (weaponData.DeliveryType == WeaponDeliveryType.Melee)
            {
                ECAContext hitContext = new ECAContext { ImpactPoint = target.position, PrimaryTarget = target, BaseDamage = finalDmg, SourceWeapon = weaponData, IsCriticalHit = isCrit, IsEnemyFire = false };
                if (weaponData.OnHitActions != null) foreach (var action in weaponData.OnHitActions) if (action != null) action.Execute(hitContext);
                Debug.DrawLine(spawnPos, target.position, Color.yellow, 0.1f);
            }
            else if (weaponData.DeliveryType == WeaponDeliveryType.Ranged && weaponData.ProjectilePrefab != null)
            {
                Vector3 bulletDir = target.position - spawnPos;
                float bulletAngle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg;
                Quaternion bulletRot = Quaternion.AngleAxis(bulletAngle, Vector3.forward);

                GameObject projObj = Instantiate(weaponData.ProjectilePrefab, spawnPos, bulletRot);
                Projectile projectile = projObj.GetComponent<Projectile>();
                // 👇【核心对接】：告诉子弹这是玩家发射的！
                projectile.Fire(target, finalDmg, weaponData, isEnemy: false);
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