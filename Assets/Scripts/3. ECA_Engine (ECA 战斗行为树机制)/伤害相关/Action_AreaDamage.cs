// --- START OF FILE Action_AreaDamage.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "AreaDamage", menuName = "Chimera Protocol/ECA Actions/Area Damage (范围爆炸)")]
public class Action_AreaDamage : ECAAction
{
    [Range(0f, 3f)] public float DamageMultiplier = 1.0f;
    public float BonusRadius = 0f;
    public bool IsTrueDamage = false;

    public override void Execute(ECAContext context)
    {
        float baseRadius = context.SourceWeapon.GetStat(StatType.ExplosionRadius);
        float realRadius = (baseRadius + BonusRadius) * CombatSandbox.Instance.DistanceMultiplier;
        float finalDmg = context.BaseDamage * DamageMultiplier;

        DamageReceiver[] all = FindObjectsOfType<DamageReceiver>();
        foreach (var rec in all)
        {
            if (rec.isEnemy != context.IsEnemyFire && Vector3.Distance(context.ImpactPoint, rec.transform.position) <= realRadius)
            {
                // 👇 传参补全
                rec.TakeDamage(finalDmg, context.SourceWeapon.WeaponName + " (溅射)", IsTrueDamage, context.IsCriticalHit);
            }
        }
    }
}