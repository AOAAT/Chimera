using UnityEngine;

[CreateAssetMenu(fileName = "Action_DealDamage", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 造成单体伤害")]
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
            // --- 👇【关键修复点】：将 context 的临时倍率乘进去 ---
            float finalDamage = context.BaseDamage * this.DamageMultiplier * context.TemporaryDamageModifier;

            string wpnName = context.SourceWeapon != null ? context.SourceWeapon.WeaponName : "未知来源";

            // 调试日志：展示最终结算公式
     

            receiver.TakeDamage(finalDamage, wpnName, IsTrueDamage, context.IsCriticalHit);
        }
    }
}