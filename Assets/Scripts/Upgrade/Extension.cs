using System.Collections.Generic;
using System.Linq;

public static class ComponentUpgradeExtensions
{
    // ==========================================
    // 核心算法：生成安全的预览快照 (Mock Data)
    // ==========================================
    public static UpgradePreviewData GenerateUpgradePreview(this InstancedComponent target, InstancedComponent material)
    {
        var blueprint = target.BaseData;
        int currentLv = target.CurrentLevel;
        int nextLv = currentLv + 1;

        var currentData = blueprint.GetLevelData(currentLv);
        var nextData = blueprint.GetLevelData(nextLv);

        UpgradePreviewData preview = new UpgradePreviewData
        {
            TargetItem = target,
            MaterialItem = material,
            CurrentLevel = currentLv,
            NextLevel = nextLv,
            NewMechanicDesc = nextData.SpecialMechanicDesc
        };

        // 1. 抽取新增的 Tag (过滤掉当前等级已经有的 Tag)
        preview.NewTags = nextData.BonusTags.Except(currentData.BonusTags).ToList();

        // 2. 属性 Diff 计算 (合并当前和下一级的所有属性 Key)
        var allStatTypes = currentData.Stats.Select(s => s.StatID)
            .Union(nextData.Stats.Select(s => s.StatID))
            .Distinct();

        foreach (var statType in allStatTypes)
        {
            float curVal = currentData.Stats.FirstOrDefault(s => s.StatID == statType)?.Value ?? 0f;
            float nextVal = nextData.Stats.FirstOrDefault(s => s.StatID == statType)?.Value ?? 0f;

            preview.StatDiffs.Add(new StatDiff
            {
                StatID = statType,
                CurrentValue = curVal,
                NextValue = nextVal
            });
        }

        return preview;
    }
}