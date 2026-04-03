// --- START OF FILE Action_DealDamage.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "Action_DealDamage", menuName = "Chimera Protocol/ECA Actions/Deal Damage (造成单体伤害)")]
public class Action_DealDamage : ECAAction
{
    [Range(0f, 5f)] public float DamageMultiplier = 1.0f;
    public bool IsTrueDamage = false;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;
        DamageReceiver receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
        {
            float finalDamage = context.BaseDamage * DamageMultiplier;
            string wpnName = context.SourceWeapon != null ? context.SourceWeapon.WeaponName : "未知来源";
            // 👇 传参补全
            receiver.TakeDamage(finalDamage, wpnName, IsTrueDamage, context.IsCriticalHit);
        }
    }
}