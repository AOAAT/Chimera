
using System.Collections.Generic;

public static class StatTranslation
{
    private static readonly Dictionary<StatType, string> StatNames = new Dictionary<StatType, string>
    {
        // --- 单一词条 ---
        { StatType.AddedHP, "血量加成" },
        { StatType.AddedAP, "护甲强化" },
        { StatType.AddedBlock, "格挡加成" },
        { StatType.AddedMass, "质量加成" },
        { StatType.PowerCost, "耗电量" },
        { StatType.EnginePower, "引擎动力" },
        { StatType.AttackSpeed, "攻击速度" },
        { StatType.CriticalChance, "暴击概率" },
        { StatType.CritMultiplier, "暴击倍率" },
        { StatType.ProjectileSpeed, "子弹速度" },
        { StatType.MultiShotCount, "攻击目标数" },
        { StatType.ExplosionRadius, "冲击范围" },

        // --- 🌟【新增】专门给合并行准备的占位符（不对应真实 StatType） ---
        // 我们用一些特殊的逻辑 ID 来标记它们
    };

    public static string Get(StatType type) => StatNames.ContainsKey(type) ? StatNames[type] : type.ToString();

    // 🌟 专门处理合并行的翻译
    public static string GetCompound(string key)
    {
        switch (key)
        {
            case "DamageRange": return "攻击力区间";
            case "RangeInterval": return "有效攻击范围";
            case "TacticalRole": return "战斗机制";
            default: return "未定义属性";
        }
    }
}