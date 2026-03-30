using System.Collections.Generic;
using System.Linq;

// ==========================================
// A. 单条属性的差异对比 (Diff)
// ==========================================
public struct StatDiff
{
    public StatType StatID;
    public float CurrentValue;
    public float NextValue;

    public float Delta => NextValue - CurrentValue;
    public bool HasChanged => Delta != 0f;

    // 智能判决：增加这个属性是好事还是坏事？(UI 着色依据)
    public bool IsBuff
    {
        get
        {
            // 耗电量、重量增加是“代价 (Red)”；伤害、血量增加是“收益 (Green)”
            if (StatID == StatType.PowerCost || StatID == StatType.AddedMass)
                return Delta < 0f; // 耗电减少才是 Buff！

            return Delta > 0f;     // 其他属性增加就是 Buff
        }
    }
}

// ==========================================
// B. 传递给 UI 预览面板的总数据包
// ==========================================
public class UpgradePreviewData
{
    public InstancedComponent TargetItem;    // 主体 (即将升星)
    public InstancedComponent MaterialItem;  // 祭品 (即将被销毁)

    public int CurrentLevel;
    public int NextLevel;

    public List<StatDiff> StatDiffs = new List<StatDiff>();
    public List<SubTag> NewTags = new List<SubTag>(); // 将显示为金色
    public string NewMechanicDesc;                    // 将显示为金色
}