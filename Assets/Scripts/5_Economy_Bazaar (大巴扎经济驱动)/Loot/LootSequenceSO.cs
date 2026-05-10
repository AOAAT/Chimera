using System;
using System.Collections.Generic;
using UnityEngine;

public enum LootDropMode { MacroCategorySingle, SystemAssignedTag, PlayerDrivenFilter, CustomPoolDrop }
public enum TagPoolSource { MapNodeDefault, CustomMacros, CustomSubTags }

[Serializable]
public class CustomDropEntry
{
    [Tooltip("如果是发放组件，请拖入组件图纸")]
    public ComponentDataSO ComponentBlueprint;

    [Tooltip("如果是发放底盘，请拖入底盘图纸（此时等级设置无效）")]
    public ChassisDataSO ChassisBlueprint; // 👈 新增：底盘槽位

    [Range(1, 4)] public int Level = 1;

    // 辅助判定：这到底是个啥？
    public bool IsChassis => ChassisBlueprint != null;
}

[Serializable]
public class LootTaskConfig
{
    [Header("=== 基础模式设定 ===")]
    public LootDropMode Mode;

    [Range(0f, 1f)] public float TripleChoiceProbability = 0f;

    [Header("=== 等级生成权重 (Level Weights) ===")]
    [Tooltip("控制抽出组件的初始星级概率")]
    public int Weight_Lv1 = 100;
    public int Weight_Lv2 = 0;
    public int Weight_Lv3 = 0;
    public int Weight_Lv4 = 0;

    [Header("=== 标签池控制 (Tag Pool Control) ===")]
    public TagPoolSource PoolSource = TagPoolSource.MapNodeDefault;
    public List<MacroCategory> CustomMacroMix = new List<MacroCategory>();
    public List<SubTag> CustomSubTagMix = new List<SubTag>();

    [Header("=== 模式4专属：自定义奖池 ===")]
    public List<CustomDropEntry> CustomPool = new List<CustomDropEntry>();
}

[CreateAssetMenu(fileName = "NewLootSequence", menuName = "Chimera Protocol/3. 宏观控制/战利品掉落池 (Loot Sequence)")]
public class LootSequenceSO : ScriptableObject
{
    public List<LootTaskConfig> Tasks = new List<LootTaskConfig>();
}