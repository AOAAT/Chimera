// --- Action_DealDamage.cs (V2.0) ---
using UnityEngine;

[CreateAssetMenu(fileName = "DealDamage_V2", menuName = "Chimera Protocol/2. ECA 机制积木/结算 - 最终损害扣除 V2")]
public class Action_DealDamage : ECAAction
{
    [Tooltip("是否无视护甲与格挡？")]
    public bool IsTrueDamage = false;

    // 🌟 核心设定：结算优先级永远在 300
    public Action_DealDamage() { Priority = 300; }

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;

        DamageReceiver receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (receiver != null && receiver.CurrentHP > 0)
        {
            // 🚀 读取 Context 中经过“附魔”和“距离加成”累加后的最终数值
            float finalDmg = context.BaseDamage * context.TemporaryDamageModifier;

            // 如果是暴击，倍率已在 WeaponModule 算入 TemporaryDamageModifier
            // 这里执行物理扣血
            float hpBefore = receiver.CurrentHP;
            receiver.TakeDamage(finalDmg, context.SourceWeapon.WeaponName, IsTrueDamage, context.IsCriticalHit);

            // 统计击杀数，供后续“击杀回蓝”等积木使用
            if (hpBefore > 0 && receiver.CurrentHP <= 0)
            {
                context.KillCountThisAction++;
            }
        }
    }
}