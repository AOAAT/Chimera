// --- START OF FILE Action_ChainDamage.cs ---
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ChainDamage", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 造成连锁伤害 (Chain Damage)")]
public class Action_ChainDamage : ECAAction
{
    public int MaxTargets = 2;
    public float ChainRadius = 5f;
    [Range(0f, 1f)] public float DamageRatio = 0.5f;
    public bool IsTrueDamage = false;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;
        float realRadius = ChainRadius * (CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f);
        float chainDamage = context.BaseDamage * DamageRatio;

        var chainTargets = FindObjectsOfType<DamageReceiver>()
            .Where(r => r.isEnemy != context.IsEnemyFire && r.CurrentHP > 0)
            .Where(r => r.transform != context.PrimaryTarget)
            .Where(r => Vector3.Distance(context.ImpactPoint, r.transform.position) <= realRadius)
            .OrderBy(r => Vector3.Distance(context.ImpactPoint, r.transform.position))
            .Take(MaxTargets).ToList();

        foreach (var target in chainTargets)
        {
            // 👇 传参补全
            target.TakeDamage(chainDamage, context.SourceWeapon.WeaponName + " (连锁电弧)", IsTrueDamage, context.IsCriticalHit);
        }
    }
}