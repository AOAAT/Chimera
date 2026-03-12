using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponModule : MonoBehaviour
{
    private RuntimeWeapon weaponData;
    private float fireCooldown = 0f;

    [Header("调试信息")]
    public Transform CurrentTarget;

    // 接收车间传来的独立武器数据
    public void Initialize(RuntimeWeapon data)
    {
        weaponData = data;
    }

    private void Update()
    {
        if (weaponData == null) return;

        fireCooldown -= Time.deltaTime;

        // 全新多目标雷达扫描
        FindTarget();

        // 👇【核心修复】：检查新的多目标列表里有没有敌人！
        if (CurrentTargets != null && CurrentTargets.Count > 0)
        {
            // 瞄准逻辑：武器的枪口默认对准列表里的第一个（也就是最近的）那个敌人
            Transform primaryTarget = CurrentTargets[0];
            if (primaryTarget != null)
            {
                Vector3 dir = primaryTarget.position - transform.position;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            // 开火逻辑
            if (fireCooldown <= 0f)
            {
                Fire();
            }
        }
    }

    public List<Transform> CurrentTargets = new List<Transform>();

    private void FindTarget()
    {
        float maxRange = weaponData.GetStat(StatType.MaxRange) * CombatSandbox.Instance.DistanceMultiplier;
        float minRange = weaponData.GetStat(StatType.MinRange) * CombatSandbox.Instance.DistanceMultiplier;

        // 读取武器能同时锁定几个目标（没填默认就是 1 个）
        int maxLockCount = (int)weaponData.GetStat(StatType.MultiShotCount);
        if (maxLockCount <= 0) maxLockCount = 1;

        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();

        // 简易的就近排序机制（利用 C# 的 LINQ 库，需要在文件顶部加上 using System.Linq;）
        CurrentTargets = allReceivers
            .Where(r => r.isEnemy && r.CurrentHP > 0)
            .Where(r => {
                float d = Vector3.Distance(transform.position, r.transform.position);
                return d >= minRange && d <= maxRange;
            })
            .OrderBy(r => Vector3.Distance(transform.position, r.transform.position))
            .Take(maxLockCount) // 截取前 N 个最近的敌人！
            .Select(r => r.transform)
            .ToList();
    }

    private void Fire()
    {
        float atkSpeed = weaponData.GetStat(StatType.AttackSpeed);
        if (atkSpeed <= 0) atkSpeed = 100f;
        fireCooldown = 100f / atkSpeed;

        if (CurrentTargets.Count == 0) return;

        // 1. 基础浮动伤害
        float finalDmg = Random.Range(weaponData.GetStat(StatType.MinDamage), weaponData.GetStat(StatType.MaxDamage));

        // 2. 动态暴击结算（基础暴击率 + 临时叠起来的暴击率）
        float totalCritChance = weaponData.GetStat(StatType.CriticalChance) + weaponData.BonusCriticalChance;
        bool isCrit = Random.value <= totalCritChance;

        if (isCrit)
        {
            finalDmg *= 1.5f; // 暴击伤害倍率
            Debug.Log($"【暴击触发！】当前总暴击率: {totalCritChance:F2}");
        }

        // 3. 👇【全新机制：开火事件派发！】（挥动电锯/开枪的瞬间触发）
        ECAContext fireContext = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = CurrentTargets[0],
            BaseDamage = finalDmg,
            SourceWeapon = weaponData,
            IsCriticalHit = isCrit // 把暴击结果传进去！
        };

        if (weaponData.OnFireActions != null)
        {
            foreach (var action in weaponData.OnFireActions)
            {
                if (action != null) action.Execute(fireContext);
            }
        }

        // 4. 遍历所有目标派发命中事件 (向下兼容你之前的代码)
        foreach (var target in CurrentTargets)
        {
            if (weaponData.DeliveryType == WeaponDeliveryType.Melee)
            {
                ECAContext hitContext = new ECAContext
                {
                    ImpactPoint = transform.position,
                    PrimaryTarget = target,
                    BaseDamage = finalDmg,
                    SourceWeapon = weaponData,
                    IsCriticalHit = isCrit // 同样传给命中事件
                };

                if (weaponData.OnHitActions != null)
                {
                    foreach (var action in weaponData.OnHitActions)
                    {
                        if (action != null) action.Execute(hitContext);
                    }
                }
                Debug.DrawLine(transform.position, target.position, Color.yellow, 0.1f);
            }
            // ... 远程分支 (Projectile) 同样传参，如果需要的话可以把子弹的 Fire 方法也加上 isCrit 参数，这里为了简化先略过
            else if (weaponData.DeliveryType == WeaponDeliveryType.Ranged && weaponData.ProjectilePrefab != null)
            {
                GameObject projObj = Instantiate(weaponData.ProjectilePrefab, transform.position, transform.rotation);
                Projectile projectile = projObj.GetComponent<Projectile>();
                projectile.Fire(target, finalDmg, weaponData);
            }
        }
    }
    private void OnDrawGizmos()
    {
        // 如果武器数据还没注入（比如还没按下 Play 键），就不画
        if (weaponData == null) return;

        // 获取沙盒的全局度量衡比例（防呆：如果沙盒没挂载，默认按 1.0 算）
        float distanceMultiplier = 1.0f;
        if (CombatSandbox.Instance != null)
        {
            distanceMultiplier = CombatSandbox.Instance.DistanceMultiplier;
        }

        // 提取面板数据并乘以全局度量衡，得到真实的物理射程
        float maxRange = weaponData.GetStat(StatType.MaxRange) * distanceMultiplier;
        float minRange = weaponData.GetStat(StatType.MinRange) * distanceMultiplier;

        // 1. 绘制最大攻击距离（蓝色圆圈）
        // transform.position 完美对应了这把武器所在“接口”的绝对物理坐标！
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, maxRange);

        // 2. 绘制最小攻击盲区（红色圆圈）
        // 只有当策划设置了最小射程（大于 0）时，才绘制红圈
        if (minRange > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, minRange);
        }
    }
}