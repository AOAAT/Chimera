using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "BurstFire", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 爆发连射")]
public class Action_BurstFire : ECAAction
{
    [Header("=== 连发配置 ===")]
    public int ShotCount = 3;
    public float Interval = 0.1f;
    public float DamageMult = 0.6f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        // 利用战斗导演的协程，因为积木本身是瞬时的 SO
        CombatDirector.Instance.StartCoroutine(DoBurst(context));
    }

    private IEnumerator DoBurst(ECAContext context)
    {
        // 缓存数据，防止协程执行期间 context 里的引用发生变化
        Transform target = context.PrimaryTarget;
        RuntimeWeapon weapon = context.SourceWeapon;
        RuntimeChimeraData owner = context.ChassisData;
        Transform shooter = context.SourceEntity;
        float dmg = context.BaseDamage * DamageMult;
        bool isEnemy = context.IsEnemyFire; // 👈【核心修复】：动态获取阵营
        bool crit = context.IsCriticalHit;

        for (int i = 0; i < ShotCount; i++)
        {
            if (target == null || !target.gameObject.activeInHierarchy) yield break;

            // 获取当前枪口指向目标的实时角度
            Vector2 dir = (target.position - shooter.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 生成子弹
            GameObject proj = SimplePool.Spawn(weapon.ProjectilePrefab, context.ImpactPoint, Quaternion.AngleAxis(angle, Vector3.forward));

            Projectile pScript = proj.GetComponent<Projectile>();
            if (pScript != null)
            {
                // 参数对齐：目标, 伤害, 武器, 玩家黑盒, 自身, 是否敌火, 是否暴击, 代际, 是否奶弹
                pScript.Fire(target, dmg, weapon, owner, shooter, isEnemy, crit, 0, false);
            }

            if (ScreenEffectManager.Instance != null)
                ScreenEffectManager.Instance.TriggerShake(0.05f, 0.05f);

            yield return new WaitForSeconds(Interval);
        }
    }
}