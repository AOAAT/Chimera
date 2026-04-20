using UnityEngine;

[CreateAssetMenu(fileName = "ChaosRange", menuName = "Chimera Protocol/2. ECA 机制积木/修饰 - 混沌之心")]
public class Action_ChaosRange : ECAAction
{
    public float MinDamageReduction = 10f;
    public float MaxDamageAddition = 20f;

    public override void Execute(ECAContext context)
    {
        if (context.SourceWeapon == null) return;

        // 修改武器的本地运行时数值
        if (context.SourceWeapon.WeaponStats.ContainsKey(StatType.MinDamage))
            context.SourceWeapon.WeaponStats[StatType.MinDamage] = Mathf.Max(1, context.SourceWeapon.WeaponStats[StatType.MinDamage] - MinDamageReduction);

        if (context.SourceWeapon.WeaponStats.ContainsKey(StatType.MaxDamage))
            context.SourceWeapon.WeaponStats[StatType.MaxDamage] += MaxDamageAddition;

        Debug.Log($"【混沌之心】伤害区间已畸变：{context.SourceWeapon.GetStat(StatType.MinDamage)} - {context.SourceWeapon.GetStat(StatType.MaxDamage)}");
    }
}