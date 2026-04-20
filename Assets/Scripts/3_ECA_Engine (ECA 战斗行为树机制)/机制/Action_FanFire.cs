using UnityEngine;

[CreateAssetMenu(fileName = "FanFire", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 扇面喷射")]
public class Action_FanFire : ECAAction
{
    public float SpreadAngle = 40f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        int pelletCount = Mathf.Max(1, (int)context.SourceWeapon.GetStat(StatType.MultiShotCount));
        GameObject prefab = context.SourceWeapon.ProjectilePrefab;

        Vector2 baseDir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;
        float centerAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < pelletCount; i++)
        {
            float randomOffset = Random.Range(-SpreadAngle * 0.5f, SpreadAngle * 0.5f);
            float finalAngle = centerAngle + randomOffset;
            Quaternion rotation = Quaternion.AngleAxis(finalAngle, Vector3.forward);

            GameObject pellet = SimplePool.Spawn(prefab, context.ImpactPoint, rotation);
            Projectile projScript = pellet.GetComponent<Projectile>();

            if (projScript != null)
            {
                projScript.EnableHoming = false;
                // 👇【核心修正】：参数顺序完全对齐
                projScript.Fire(null, context.BaseDamage, context.SourceWeapon, context.ChassisData, context.SourceEntity, context.IsEnemyFire, context.IsCriticalHit, 0, false);
            }
        }
        context.ExecutionAborted = true;
    }
}