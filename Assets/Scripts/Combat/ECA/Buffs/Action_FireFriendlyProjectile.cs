// --- Action_FireFriendlyProjectile_V2.cs (增加速度控制) ---
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "FireFriendlyProjectile", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 发射友方增益弹 V2")]
public class Action_FireFriendlyProjectile : ECAAction
{
    [Header("=== 发射配置 ===")]
    public GameObject ProjectilePrefab;
    public float Range = 10f;

    // 👇【核心新增】：允许直接在积木里设置子弹速度
    public float ProjectileSpeed = 15f;

    public Action_FireFriendlyProjectile() { Priority = 200; }

    // --- Action_FireFriendlyProjectile.cs ---

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null || ProjectilePrefab == null) return;

        // 1. 获取队友并根据距离排序
        var allies = context.IsEnemyFire ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;
        float realRange = CombatSandbox.GetDist(Range);

        var target = allies
            .Where(a => a != null && a.CurrentHP > 0 && a.transform != context.SourceEntity)
            .Where(a => Vector3.Distance(context.SourceEntity.position, a.transform.position) <= realRange)
            .OrderBy(a => Vector3.Distance(context.SourceEntity.position, a.transform.position))
            .FirstOrDefault();

        // 👇 探测点：如果搜不到人，报出当前阵营和范围
        if (target == null)
        {
            Debug.LogWarning($"<color=yellow>【肾上腺-搜寻失败】</color> 阵营:{(context.IsEnemyFire ? "怪" : "玩家")} | 搜索人数:{allies.Count} | 半径:{realRange}。附近没有活着的队友！");
            return;
        }

        // 2. 逻辑代理回查
        RuntimeWeapon myRuntimeModule = null;
        if (context.ChassisData != null && context.SourceComponentSO != null)
        {
            context.ChassisData.ComponentToRuntimeMap.TryGetValue(context.SourceComponentSO, out myRuntimeModule);
        }

        if (myRuntimeModule == null)
        {
            Debug.LogError($"<color=red>【肾上腺-代理丢失】</color> 无法找到零件 [{context.SourceComponentSO.ComponentName}] 的逻辑代理！");
            return;
        }

        // 3. 计算物理参数
        Vector2 fireDir = (target.transform.position - context.SourceEntity.position).normalized;
        Vector3 spawnPos = context.SourceEntity.position + (Vector3)fireDir * 0.8f;
        float angle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;

        ECAContext projectileCtx = new ECAContext
        {
            SourceEntity = context.SourceEntity,
            PrimaryTarget = target.transform,
            ImpactPoint = spawnPos,
            SourceWeapon = myRuntimeModule,
            ChassisData = context.ChassisData,
            IsEnemyFire = context.IsEnemyFire,
            HitAllies = true,
            CustomStates = context.CustomStates
        };

        GameObject projObj = SimplePool.Spawn(ProjectilePrefab, spawnPos, Quaternion.AngleAxis(angle, Vector3.forward));
        Projectile pScript = projObj.GetComponent<Projectile>();

        if (pScript != null)
        {
            pScript.SetSpeedOverride(ProjectileSpeed);
            pScript.FireV2(projectileCtx);
            Debug.Log($"<color=#00FF00>【肾上腺-发射成功】</color> 目标:{target.name} | 子弹速度:{ProjectileSpeed}");
        }
    }
}