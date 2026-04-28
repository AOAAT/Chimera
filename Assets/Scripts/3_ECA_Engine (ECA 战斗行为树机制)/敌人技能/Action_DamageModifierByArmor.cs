using UnityEngine;

[CreateAssetMenu(fileName = "Act_ArmorBreaker", menuName = "Chimera Protocol/2. ECA 机制积木/战斗 - 碎甲判定(带调试)")]
public class Action_DamageModifierByArmor : ECAAction
{
    public float ArmorMultiplier = 1.5f;

    public override void Execute(ECAContext context)
    {
        if (context.PrimaryTarget == null)
        {
            Debug.LogWarning("<color=yellow>【碎甲审计】</color> 命中目标为空，判定终止。");
            return;
        }

        DamageReceiver receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
        {
            if (receiver.CurrentAP > 0)
            {
                float oldMod = context.TemporaryDamageModifier;
                context.TemporaryDamageModifier *= ArmorMultiplier;

                Debug.Log($"<color=#FF4500>【碎甲成功】</color> 目标 [{receiver.name}] 护甲剩余: {receiver.CurrentAP}。倍率: {oldMod} -> {context.TemporaryDamageModifier}");
            }
            else
            {
                Debug.Log($"<color=#7F7F7F>【碎甲跳过】</color> 目标 [{receiver.name}] 护甲已碎。保持倍率: {context.TemporaryDamageModifier}");
            }
        }
    }
}