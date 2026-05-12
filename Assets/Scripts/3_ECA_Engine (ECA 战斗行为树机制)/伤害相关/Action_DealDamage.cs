// --- Action_DealDamage.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "NewDealDamage", menuName = "Chimera Protocol/2. ECA 机制积木/结算 - 最终伤害扣除")]
public class Action_DealDamage : ECAAction
{
    public bool IsTrueDamage = false;

    public Action_DealDamage() { Priority = 300; } // 强制设定结算优先级

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;

        DamageReceiver receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (receiver != null && receiver.CurrentHP > 0)
        {
            // 🌟 核心：读取所有积木叠加后的最终倍率
            float finalDmg = context.BaseDamage * context.TemporaryDamageModifier;

            float hpBefore = receiver.CurrentHP;
            receiver.TakeDamage(finalDmg, context.SourceWeapon.WeaponName, IsTrueDamage, context.IsCriticalHit);

            // 记录击杀
            if (hpBefore > 0 && receiver.CurrentHP <= 0)
            {
                context.KillCountThisAction++;
            }
        }
    }
}