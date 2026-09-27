using UnityEngine;

[CreateAssetMenu(fileName = "ApplyTempMod", menuName = "Chimera Protocol/2. ECA 机制积木/修饰 - 瞬时倍率(本发有效)")]
public class Action_ApplyTempModifier : ECAAction
{
    public float DamageMult = 1.0f;
    public float CritChanceMult = 1.0f;

    public override void Execute(ECAContext context)
    {
        context.TemporaryDamageModifier *= DamageMult;
        context.TemporaryCritModifier *= CritChanceMult;

        // Debug.Log($"【倍率叠加】当前伤害倍率: {context.TemporaryDamageModifier}, 暴击倍率: {context.TemporaryCritModifier}");
    }
}