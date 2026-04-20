using UnityEngine;

[CreateAssetMenu(fileName = "BoostDamageByArmor", menuName = "Chimera Protocol/2. ECA 机制积木/修饰 - 护甲转化伤害")]
public class Action_BoostDamageByArmor : ECAAction
{
    [Tooltip("每 1 点护甲转化为多少点额外伤害（1.0 即 100% 转化）")]
    public float ArmorToDamageRatio = 1.0f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceEntity == null) return;

        // 直接从开火者身上抓取实时护甲值
        DamageReceiver dr = context.SourceEntity.GetComponent<DamageReceiver>();
        if (dr != null)
        {
            float bonus = dr.CurrentAP * ArmorToDamageRatio;

            // 核心魔法：直接修改 context.BaseDamage
            // 后面的子弹生成或范围伤害积木，读到的将是“巨额加成”后的新底数
            context.BaseDamage += bonus;

            Debug.Log($"<color=#9932CC>【引力过载】</color> 抓取护甲 {dr.CurrentAP:F0}，伤害加深 {bonus:F0}！最终底数: {context.BaseDamage}");
        }
    }
}