
using System.Collections.Generic;

public static class StatTranslation
{
    private static readonly Dictionary<StatType, string> StatNames = new Dictionary<StatType, string>
    {
        // --- 单一词条 ---
        { StatType.AddedHP, "结构耐久加成" },
        { StatType.AddedAP, "外部装甲强化" },
        { StatType.AddedBlock, "物理冲击格挡" },
        { StatType.AddedMass, "组件自重(t)" },
        { StatType.PowerCost, "额定电力负荷" },
        { StatType.EnginePower, "引擎动力输出" },
        { StatType.AttackSpeed, "自动装填频率" },
        { StatType.CriticalChance, "结构弱点感知" },
        { StatType.CritMultiplier, "暴击损毁倍率" },
        { StatType.ProjectileSpeed, "弹丸初速" },
        { StatType.MultiShotCount, "多重齐发规模" },
        { StatType.ExplosionRadius, "冲击扩散范围" },

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