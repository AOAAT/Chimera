using UnityEngine;

[CreateAssetMenu(fileName = "FanFire", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 扇面喷射")]
public class Action_FanFire : ECAAction
{
    [Header("=== 散射配置 ===")]
    public float SpreadAngle = 40f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        // 1. 抓取弹丸数量
        int pelletCount = Mathf.Max(1, (int)context.SourceWeapon.GetStat(StatType.MultiShotCount));
        GameObject prefab = context.SourceWeapon.ProjectilePrefab;

        // 2. 👇【核心修复】：计算经过乘法修正后的最终单发伤害
        // 最终伤害 = (基础伤害 + 电磁炮加成) * 狮子头瞬时倍率
        float finalDamage = context.BaseDamage * context.TemporaryDamageModifier;

        // 3. 计算方向
        Vector2 baseDir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;
        float centerAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        // 4. 循环生成
        for (int i = 0; i < pelletCount; i++)
        {
            float randomOffset = Random.Range(-SpreadAngle * 0.5f, SpreadAngle * 0.5f);
            float finalAngle = centerAngle + randomOffset;
            Quaternion rotation = Quaternion.AngleAxis(finalAngle, Vector3.forward);

            // 从对象池取子弹
            GameObject pellet = SimplePool.Spawn(prefab, context.ImpactPoint, rotation);
            Projectile pScript = pellet.GetComponent<Projectile>();

            if (pScript != null)
            {
                pScript.EnableHoming = false; // 扇面通常不追踪

                // 👇【参数完全对齐】：9 个参数。此时 isCrit 已由 WeaponModule 算好并存在 context 里
                pScript.Fire(
                    null,                  // target
                    finalDamage,           // damage (已缩放)
                    context.SourceWeapon,  // weaponData
                    context.ChassisData,   // ownerData
                    context.SourceEntity,  // shooter
                    context.IsEnemyFire,   // isEnemy
                    context.IsCriticalHit, // isCrit (由 context 提供)
                    0,                     // gen
                    false                  // targetAllies
                );
            }
        }

        // 5. 拦截默认子弹
        context.ExecutionAborted = true;
    }
}