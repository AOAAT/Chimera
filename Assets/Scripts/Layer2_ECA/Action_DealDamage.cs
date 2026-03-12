using UnityEngine;

[CreateAssetMenu(fileName = "DealDamage", menuName = "Chimera/ECA Actions/Deal Damage (单体伤害)")]
public class Action_DealDamage : ECAAction
{
    // 这个系数允许你做“造成 50% 伤害”这种机制
    [Range(0f, 3f)] public float DamageMultiplier = 1.0f;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget != null)
        {
            DamageReceiver receiver = context.PrimaryTarget.GetComponent<DamageReceiver>();
            if (receiver != null)
            {
                float finalDmg = context.BaseDamage * DamageMultiplier;
                receiver.TakeDamage(finalDmg, context.SourceWeapon.WeaponName);
            }
        }
    }
}