// --- Action_BurstFire.cs (V2.0) ---
using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "BurstFire_V2", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 爆发连射 V2")]
public class Action_BurstFire : ECAAction
{
    public int ShotCount = 3;
    public float Interval = 0.1f;

    public Action_BurstFire() { Priority = 200; }

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        // 开启协程处理连射逻辑
        CombatDirector.Instance.StartCoroutine(DoBurst(context));

        // 标记已处理
        context.IsHandledByCustomDelivery = true;
    }

    private IEnumerator DoBurst(ECAContext context)
    {
        GameObject prefab = context.SourceWeapon.ProjectilePrefab;

        for (int i = 0; i < ShotCount; i++)
        {
            if (context.PrimaryTarget == null || !context.PrimaryTarget.gameObject.activeInHierarchy) yield break;

            Vector2 dir = (context.PrimaryTarget.position - context.ImpactPoint).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            GameObject proj = SimplePool.Spawn(prefab, context.ImpactPoint, Quaternion.AngleAxis(angle, Vector3.forward));
            Projectile pScript = proj.GetComponent<Projectile>();

            if (pScript != null) pScript.FireV2(context);

            yield return new WaitForSeconds(Interval);
        }
    }
}