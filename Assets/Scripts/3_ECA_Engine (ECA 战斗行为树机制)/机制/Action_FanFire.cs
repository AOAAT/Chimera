// --- Action_FanFire.cs (V2.0) ---
using UnityEngine;

[CreateAssetMenu(fileName = "FanFire_V2", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 扇面喷射 V2")]
public class Action_FanFire : ECAAction
{
    [Header("=== 散射配置 ===")]
    public float SpreadAngle = 40f;

    public Action_FanFire() { Priority = 200; } // 投递层

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        // 1. 获取弹丸数量
        int pelletCount = Mathf.Max(1, (int)context.SourceWeapon.GetStat(StatType.MultiShotCount));
        GameObject prefab = context.SourceWeapon.ProjectilePrefab;

        // 2. 计算方向
        Vector2 baseDir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;
        float centerAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        // 3. 循环生成弹丸
        for (int i = 0; i < pelletCount; i++)
        {
            float randomOffset = Random.Range(-SpreadAngle * 0.5f, SpreadAngle * 0.5f);
            float finalAngle = centerAngle + randomOffset;
            Quaternion rotation = Quaternion.AngleAxis(finalAngle, Vector3.forward);

            GameObject pellet = SimplePool.Spawn(prefab, context.ImpactPoint, rotation);
            Projectile pScript = pellet.GetComponent<Projectile>();

            if (pScript != null)
            {
                pScript.EnableHoming = false; // 散射通常不追踪

                // 🌟 关键：将当前 Context 传入子弹，以便子弹命中后接力执行 OnHit 管线
                pScript.FireV2(context);
            }
        }

        // 🌟 关键：标记发射已处理，防止 WeaponModule 再发普通子弹
        context.IsHandledByCustomDelivery = true;
    }
}