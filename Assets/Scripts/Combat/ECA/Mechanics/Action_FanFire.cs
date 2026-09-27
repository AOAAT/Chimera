using UnityEngine;

[CreateAssetMenu(fileName = "FanFire_V2", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 扇面喷射 V2")]
public class Action_FanFire : ECAAction
{
    [Header("=== 射击规模 (已解耦) ===")]
    [Tooltip("喷射出的弹丸总数。不再受武器面板的 MultiShotCount 限制")]
    public int PelletCount = 3;

    [Header("=== 平衡杠杆 ===")]
    [Tooltip("每颗弹丸造成的伤害倍率。例如 0.4 代表每颗子弹只造成原伤害的 40%")]
    [Range(0.1f, 2.0f)] public float DamageMultiplier = 0.4f;

    [Header("=== 散射配置 ===")]
    public float SpreadAngle = 40f;

    public Action_FanFire() { Priority = 200; } // 投递层

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        GameObject prefab = context.SourceWeapon.ProjectilePrefab;

        // 1. 计算中心方向
        Vector2 baseDir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;
        float centerAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        // 2. 循环生成弹丸
        for (int i = 0; i < PelletCount; i++)
        {
            float randomOffset = Random.Range(-SpreadAngle * 0.5f, SpreadAngle * 0.5f);
            float finalAngle = centerAngle + randomOffset;
            Quaternion rotation = Quaternion.AngleAxis(finalAngle, Vector3.forward);

            GameObject pellet = SimplePool.Spawn(prefab, context.ImpactPoint, rotation);
            Projectile pScript = pellet.GetComponent<Projectile>();

            if (pScript != null)
            {
                pScript.EnableHoming = false;

                // --- 👇【核心平衡注入】：构造独立子 Context ---
                ECAContext pelletCtx = new ECAContext
                {
                    SourceEntity = context.SourceEntity,
                    PrimaryTarget = context.PrimaryTarget,
                    ImpactPoint = context.ImpactPoint,
                    SourceWeapon = context.SourceWeapon,
                    ChassisData = context.ChassisData,
                    IsEnemyFire = context.IsEnemyFire,

                    // 🌟 关键：对单发伤害进行倍率修正
                    BaseDamage = context.BaseDamage * DamageMultiplier,

                    TemporaryDamageModifier = context.TemporaryDamageModifier,
                    TemporaryCritModifier = context.TemporaryCritModifier,
                    CustomStates = context.CustomStates,
                    Generation = context.Generation // 继承代际
                };

                pScript.FireV2(pelletCtx);
            }
        }

        // 🌟 标记发射已处理，拦截武器原生的那一发 100% 伤害的子弹
        context.IsHandledByCustomDelivery = true;
    }
}