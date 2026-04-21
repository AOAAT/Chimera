using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "BurstFire", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 爆发连射")]
public class Action_BurstFire : ECAAction
{
    [Header("=== 连发配置 ===")]
    public int ShotCount = 3;
    public float Interval = 0.1f;
    [Tooltip("每发子弹相对于总伤害的占比 (0.6 代表每发造成总威力的 60%)")]
    public float DamageRatio = 0.6f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null || context.PrimaryTarget == null) return;

        // 启动连射协程
        CombatDirector.Instance.StartCoroutine(DoBurst(context));
    }

    private IEnumerator DoBurst(ECAContext context)
    {
        // 1. 缓存数据（防止 context 在异步期间被改写）
        Transform target = context.PrimaryTarget;
        RuntimeWeapon weapon = context.SourceWeapon;
        RuntimeChimeraData owner = context.ChassisData;
        Transform shooter = context.SourceEntity;

        // 👇【核心修复】：应用瞬时伤害倍率，并计算单发威力
        float baseOutput = context.BaseDamage * context.TemporaryDamageModifier;
        float finalDmgPerShot = baseOutput * DamageRatio;

        bool isEnemy = context.IsEnemyFire;
        bool crit = context.IsCriticalHit;

        for (int i = 0; i < ShotCount; i++)
        {
            if (target == null || !target.gameObject.activeInHierarchy) yield break;

            // 实时修正角度
            Vector2 dir = (target.position - context.ImpactPoint).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 生成实体
            GameObject proj = SimplePool.Spawn(weapon.ProjectilePrefab, context.ImpactPoint, Quaternion.AngleAxis(angle, Vector3.forward));

            Projectile pScript = proj.GetComponent<Projectile>();
            if (pScript != null)
            {
                // 👇【参数完全对齐】：9 个参数
                pScript.Fire(
                    target,
                    finalDmgPerShot,
                    weapon,
                    owner,
                    shooter,
                    isEnemy,
                    crit,
                    0,
                    false
                );
            }

            if (ScreenEffectManager.Instance != null)
                ScreenEffectManager.Instance.TriggerShake(0.05f, 0.05f);

            yield return new WaitForSeconds(Interval);
        }
    }
}