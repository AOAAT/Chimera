using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ChainDamage", menuName = "Chimera/ECA Actions/Chain Damage (连锁闪电)")]
public class Action_ChainDamage : ECAAction
{
    public int MaxTargets = 2;              // 弹射数量
    public float ChainRadius = 5f;          // 索敌半径
    [Range(0f, 1f)] public float DamageRatio = 0.5f; // 继承原伤害的 50%

    public override void Execute(ECAContext context)
    {
        // 如果主目标都没了，就不发生连锁
        if (context.PrimaryTarget == null) return;

        // 引入沙盒度量衡，保证地图缩放时闪电距离正确
        float realRadius = ChainRadius;
        if (CombatSandbox.Instance != null) realRadius *= CombatSandbox.Instance.DistanceMultiplier;

        // 计算衰减后的连锁伤害 (注意，这里的 BaseDamage 可能已经被上一块积木增幅过了！)
        float chainDamage = context.BaseDamage * DamageRatio;

        // 核心索敌：以命中点为中心，找离主目标最近的其他敌人
        DamageReceiver[] allReceivers = FindObjectsOfType<DamageReceiver>();

        var chainTargets = allReceivers
            .Where(r => r.isEnemy != context.IsEnemyFire && r.CurrentHP > 0)
            .Where(r => r.transform != context.PrimaryTarget) // 排除掉刚刚被主炮打中的那个人
            .Where(r => Vector3.Distance(context.ImpactPoint, r.transform.position) <= realRadius)
            .OrderBy(r => Vector3.Distance(context.ImpactPoint, r.transform.position))
            .Take(MaxTargets) // 只取前 2 个！
            .ToList();

        // 结算伤害与视觉特效
        foreach (var target in chainTargets)
        {
            target.TakeDamage(chainDamage, context.SourceWeapon.WeaponName + " (连锁电弧)");
            // 画一条黄色的闪电特效线
            Debug.DrawLine(context.ImpactPoint, target.transform.position, Color.yellow, 0.3f);
        }
    }
}