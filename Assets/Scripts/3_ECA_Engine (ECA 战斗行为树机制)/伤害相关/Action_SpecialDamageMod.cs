using UnityEngine;

[CreateAssetMenu(fileName = "SpecialDamageMod", menuName = "Chimera Protocol/2. ECA 机制积木/修饰 - 特种伤害加深")]
public class Action_SpecialDamageMod : ECAAction
{
    public bool DoubleDamageToHP = true; // 狮子头特性
    public bool DoubleCritChance = false;

    public override void Execute(ECAContext context)
    {
        // 1. 暴击率改写
        if (DoubleCritChance) context.SourceWeapon.BonusCriticalChance += context.SourceWeapon.GetStat(StatType.CriticalChance);

        // 2. 对肉身（无AP）伤害改写
        if (DoubleDamageToHP && context.PrimaryTarget != null)
        {
            var receiver = context.PrimaryTarget.GetComponentInParent<DamageReceiver>();
            if (receiver != null && receiver.CurrentAP <= 0)
            {
                context.BaseDamage *= 2.0f;
                Debug.Log("【狮子头】目标护甲已碎，造成双倍肉身伤害！");
            }
        }
    }
}