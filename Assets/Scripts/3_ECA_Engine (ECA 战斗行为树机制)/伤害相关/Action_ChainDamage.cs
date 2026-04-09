// --- START OF FILE Action_ChainDamage.cs ---
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ChainDamage", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 造成连锁伤害 (Chain Damage)")]
public class Action_ChainDamage : ECAAction
{
    [Header("=== 连锁核心参数 ===")]
    public int MaxTargets = 2;
    public float ChainRadius = 5f;
    [Range(0f, 1f)] public float DamageRatio = 0.5f;
    public bool IsTrueDamage = false;

    [Header("=== 视觉表现 (闪电特效) ===")]
    [Tooltip("拖入我们做好的 LaserBeam 预制体 (建议做成曲折的闪电贴图)")]
    public GameObject LightningPrefab;
    [Tooltip("闪电在屏幕上残留的时间 (秒)")]
    public float LightningDuration = 0.2f;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;

        float realRadius = ChainRadius * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);
        float chainDamage = context.BaseDamage * DamageRatio;

        // 核心索敌：寻找主目标半径内的存活敌人 (按距离排序)
        var chainTargets = FindObjectsOfType<DamageReceiver>()
            .Where(r => r.isEnemy != context.IsEnemyFire && r.CurrentHP > 0)
            .Where(r => r.transform != context.PrimaryTarget)
            .Where(r => Vector3.Distance(context.ImpactPoint, r.transform.position) <= realRadius)
            .OrderBy(r => Vector3.Distance(context.ImpactPoint, r.transform.position))
            .Take(MaxTargets).ToList();

        // 👇【视觉重构：闪电接力赛！】
        // 第一道闪电的起点，肯定是武器命中主目标的位置 (ImpactPoint)
        Vector3 lastLightningPos = context.ImpactPoint;

        foreach (var target in chainTargets)
        {
            // 1. 造成电击伤害
            target.TakeDamage(chainDamage, context.SourceWeapon.WeaponName + " (连锁电弧)", IsTrueDamage, context.IsCriticalHit);

            // 2. 寻找敌人的受击中心 (防射脚底)
            Vector3 currentTargetPos = target.transform.position;
            Collider2D col = target.GetComponentInChildren<Collider2D>();
            if (col != null) currentTargetPos = col.bounds.center;

            // 3. 绘制闪电连线！(从上一个电击点，连向这个倒霉蛋)
            if (LightningPrefab != null)
            {
                GameObject lightningObj = Instantiate(LightningPrefab, lastLightningPos, Quaternion.identity);
                LaserBeam laserScript = lightningObj.GetComponent<LaserBeam>();
                if (laserScript != null)
                {
                    laserScript.Fire(lastLightningPos, currentTargetPos, LightningDuration);
                }
            }

            // 4. 将当前倒霉蛋的位置，作为下一道闪电的起点！(接力传递)
            lastLightningPos = currentTargetPos;
        }
    }
}