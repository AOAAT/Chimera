using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "BurstFire_V2", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 爆发连射 V2")]
public class Action_BurstFire : ECAAction
{
    [Header("=== 连射配置 ===")]
    public int ShotCount = 3;
    public float Interval = 0.1f;

    [Header("=== 平衡杠杆 ===")]
    [Tooltip("每发子弹的伤害倍率")]
    [Range(0.1f, 2.0f)] public float DamageMultiplier = 0.6f;

    public Action_BurstFire() { Priority = 200; }

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        CombatDirector.Instance.StartCoroutine(DoBurst(context));
        context.IsHandledByCustomDelivery = true;
    }

    private IEnumerator DoBurst(ECAContext context)
    {
        GameObject prefab = context.SourceWeapon.ProjectilePrefab;

        for (int i = 0; i < ShotCount; i++)
        {
            // 实时检查目标状态
            if (context.PrimaryTarget == null || !context.PrimaryTarget.gameObject.activeInHierarchy) yield break;

            Vector2 dir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            GameObject proj = SimplePool.Spawn(prefab, context.ImpactPoint, Quaternion.AngleAxis(angle, Vector3.forward));
            Projectile pScript = proj.GetComponent<Projectile>();

            if (pScript != null)
            {
                // --- 👇【核心平衡注入】 ---
                ECAContext burstCtx = new ECAContext
                {
                    SourceEntity = context.SourceEntity,
                    PrimaryTarget = context.PrimaryTarget,
                    ImpactPoint = context.ImpactPoint,
                    SourceWeapon = context.SourceWeapon,
                    IsEnemyFire = context.IsEnemyFire,
                    BaseDamage = context.BaseDamage * DamageMultiplier, // 🌟 应用倍率
                    Generation = context.Generation
                };
                pScript.FireV2(burstCtx);
            }

            yield return new WaitForSeconds(Interval);
        }
    }
}