using UnityEngine;

[CreateAssetMenu(fileName = "Action_DealDamage", menuName = "Chimera Protocol/ECA Actions/Deal Damage (造成单体伤害)")]
public class Action_DealDamage : ECAAction
{
    [Tooltip("伤害倍率 (默认 1.0)")]
    [Range(0f, 5f)] public float DamageMultiplier = 1.0f;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null) return;

        // 顺藤摸瓜找到目标身上的血条
        DamageReceiver receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
        {
            float finalDamage = context.BaseDamage * DamageMultiplier;
            // 获取武器名称，防止空指针报错
            string wpnName = context.SourceWeapon != null ? context.SourceWeapon.WeaponName : "未知来源";

            // 真正发号施令：扣血！
            receiver.TakeDamage(finalDamage, wpnName);
        }
    }
}