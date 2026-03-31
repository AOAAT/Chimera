using System;
using System.Collections.Generic;
using UnityEngine;

public enum LootDropMode
{
    MacroCategorySingle, // 模式1：大类保底单抽
    SystemAssignedTag,   // 模式2：系统指定盲盒
    PlayerDrivenFilter,  // 模式3：定向双重筛选 (玩家三选一Tag)
    CustomPoolDrop       // 模式4：策划纯手动配置池 (无视任何标签)
}

// 👇【全新加入】：卡池来源控制权！
public enum TagPoolSource
{
    MapNodeDefault, // 默认：跟随大地图当前节点的大类 (如：科技图只出科技)
    CustomMacros,   // 混池：自定义多个大类混搭 (如：科技 + 血肉双拼)
    CustomSubTags   // 纯享池：纯自定义细分标签 (如：这个池子只出“实弹”和“寄生”两种标签的装备)
}

[Serializable]
public class CustomDropEntry
{
    public ComponentDataSO Blueprint;
    [Range(1, 4)] public int Level = 1;
}

[Serializable]
public class LootTaskConfig
{
    [Header("=== 基础模式设定 ===")]
    public LootDropMode Mode;

    [Header("多态生成：三选一概率")]
    [Range(0f, 1f)] public float TripleChoiceProbability = 0f;

    // 👇【核心扩展】：标签池控制矩阵
    [Header("=== 标签池控制 (Tag Pool Control) ===")]
    [Tooltip("控制当前任务的掉落池是从哪里抽取的。")]
    public TagPoolSource PoolSource = TagPoolSource.MapNodeDefault;

    [Tooltip("当 PoolSource = CustomMacros 时生效 (勾选你想混搭的大类)")]
    public List<MacroCategory> CustomMacroMix = new List<MacroCategory>();

    [Tooltip("当 PoolSource = CustomSubTags 时生效 (极其精准地指定只出哪些流派)")]
    public List<SubTag> CustomSubTagMix = new List<SubTag>();

    [Header("=== 模式4专属：自定义奖池 ===")]
    public List<CustomDropEntry> CustomPool = new List<CustomDropEntry>();
}

[CreateAssetMenu(fileName = "NewLootSequence", menuName = "Chimera Protocol/Economy/Loot Sequence Config")]
public class LootSequenceSO : ScriptableObject
{
    public List<LootTaskConfig> Tasks = new List<LootTaskConfig>();
}