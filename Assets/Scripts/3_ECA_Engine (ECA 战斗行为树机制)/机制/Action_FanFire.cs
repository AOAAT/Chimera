using UnityEngine;

[CreateAssetMenu(fileName = "FanFire", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 扇面喷射")]
public class Action_FanFire : ECAAction
{
    [Header("=== 散射配置 ===")]
    [Tooltip("扇面的总张角 (度)，如 45 代表中心左右各 22.5 度")]
    public float SpreadAngle = 40f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        // 1. 获取发射参数
        // 我们利用 StatType.MultiShotCount 来配置弹丸数量，方便升级
        int pelletCount = Mathf.Max(1, (int)context.SourceWeapon.GetStat(StatType.MultiShotCount));
        GameObject prefab = context.SourceWeapon.ProjectilePrefab;

        // 2. 计算基准方向 (枪口 -> 目标)
        Vector2 baseDir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;
        float centerAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        // 3. 循环喷射弹丸
        for (int i = 0; i < pelletCount; i++)
        {
            // 在扇面内随机偏移角度
            float randomOffset = Random.Range(-SpreadAngle * 0.5f, SpreadAngle * 0.5f);
            float finalAngle = centerAngle + randomOffset;

            // 生成子弹
            Quaternion rotation = Quaternion.AngleAxis(finalAngle, Vector3.forward);
            GameObject pellet = Instantiate(prefab, context.ImpactPoint, rotation);

            // 初始化子弹：设置 EnableHoming = false (通过预制体或代码强制)
            Projectile projScript = pellet.GetComponent<Projectile>();
            if (projScript != null)
            {
                projScript.EnableHoming = false; // 强制直线飞行
                projScript.Fire(null, context.BaseDamage, context.SourceWeapon, context.IsEnemyFire, context.IsCriticalHit);
            }
        }

        // 4. 【关键】：终止后续逻辑
        // 因为我们手动喷了子弹，如果不拦截，WeaponModule 还会再射一发精准的追踪弹
        context.ExecutionAborted = true;
    }
}