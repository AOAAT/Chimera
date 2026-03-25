using UnityEngine;

[CreateAssetMenu(fileName = "BoostDamageByPower", menuName = "Chimera Protocol/ECA Actions/Modifier: Boost Damage By Power (电能伤害增幅)")]
public class Action_BoostDamageByPower : ECAAction
{
    [Tooltip("1点盈余电能转化为多少点额外伤害")]
    public float PowerToDamageRatio = 1.0f;

    public override void Execute(ECAContext context)
    {
        // 核心魔法：读取全局经济数据
        float bonusDamage = MockResourceManager.GetSurplusPower() * PowerToDamageRatio;

        // 直接修改数据包里的 BaseDamage！
        // 因为是引用传递，排在它后面的积木，读取到的都会是增幅后的数值！
        context.BaseDamage += bonusDamage;

        Debug.Log($"[{context.SourceWeapon.WeaponName}] 充能完毕！汲取 {MockResourceManager.GetSurplusPower()} 点电能，基础伤害飙升至: {context.BaseDamage}");
    }
}