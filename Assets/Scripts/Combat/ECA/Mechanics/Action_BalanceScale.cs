using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "BalanceScale", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 天平属性调律(手术级版)")]
public class Action_BalanceScale : ECAAction
{
    [Header("=== 天平核心增益 ===")]
    public float BonusAttackSpeed = 5f;
    public float BonusCriticalChance = 0.02f;

    // --- Action_BalanceScale.cs 诊断版 ---
    public override void Execute(ECAContext context)
    {
        if (context.ChassisData == null || context.ChassisData.EquippedWeapons.Count <= 1) return;

        var weapons = context.ChassisData.EquippedWeapons;
        int count = weapons.Count;
        float totalAS = 0f;
        float totalCrit = 0f;

        foreach (var wpn in weapons)
        {
            // 👇 【探测针 3】：在平均化之前，先偷看一眼子弹速度
            // float debugSpeedBefore = wpn.GetStat(StatType.ProjectileSpeed);

            totalAS += wpn.GetStat(StatType.AttackSpeed);
            totalCrit += wpn.GetStat(StatType.CriticalChance) + wpn.BonusCriticalChance;
        }

        float targetAS = (totalAS / count) + BonusAttackSpeed;
        float targetCrit = (totalCrit / count) + BonusCriticalChance;

        foreach (var wpn in weapons)
        {
            float deltaAS = targetAS - wpn.GetStat(StatType.AttackSpeed);
            float deltaCrit = targetCrit - wpn.GetStat(StatType.CriticalChance);

            context.ChassisData.ModifyStat(wpn.SourceSO, StatType.AttackSpeed, deltaAS);
            context.ChassisData.ModifyStat(wpn.SourceSO, StatType.CriticalChance, deltaCrit);

            wpn.BonusCriticalChance = 0;

            // 👇 【探测针 4】：在修改完 AS 之后，确认子弹速度是否依然活着
            float debugSpeedAfter = wpn.GetStat(StatType.ProjectileSpeed);
            if (debugSpeedAfter <= 0)
            {
                Debug.LogError($"<color=red>【天平警报】</color> 修改攻速后，武器 {wpn.WeaponName} 的子弹速度变成了 0！怀疑 ModifyStat 逻辑存在覆盖漏洞。");
            }
        }
    }
}