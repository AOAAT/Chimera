using System.Collections.Generic;
using UnityEngine;

// 定义下拉菜单的选项（你可以随时在这里加新类型）
public enum ComponentType
{
    Core,       // 核心
    Weapon,     // 武器
    Support,    // 辅助/插件
    Factory,    // 工厂
    Movement    // 移动组件（如腿部、履带）
}
// 👇 【新增】这是你的“全局属性字典”！以后有新属性，直接在这张表里往下加！
public enum StatType
{
    AddedHP,         // 提供的血量加成
    AddedAP,         // 提供的护甲加成
    PowerCost,       // 耗电量加成
    AddedMass,       // 质量加成
    EnginePower,     // 动力加成
    MaxDamage,       // 攻击力上限（武器组件）
    MinDamage,       // 攻击力下限（武器组件）
    MaxRange,        // 最大攻击范围（武器组件）
    MinRange,        // 最小攻击范围（武器组件）
    AttackSpeed,     // 攻击速度（武器部件）
    CriticalChance,  // 暴击率（武器部件）
}

[CreateAssetMenu(fileName = "NewComponent", menuName = "Chimera Protocol/Component Data")]
public class ComponentDataSO : ScriptableObject
{
    [Header("基础识别信息")]
    public string ComponentID = "WPN_000";
    public string ComponentName = "新组件";
    [TextArea]
    public string Description = "组件描述...";
    public Sprite ComponentIcon;

    [Header("装配规则")]
    // 这就是你想要的下拉选择表！
    public ComponentType Type;
    // Tag 依然保留，用于极特殊的 ECA 逻辑判定（比如区分“血肉”还是“机械”）
    public List<string> Tags = new List<string> { "Mechanical" };

    [Header("核心数值池 (自由增删)")]
    public List<StatEntry> BaseStats = new List<StatEntry>();

    [Header("ECA 机制指令")]
    public List<ECABlock> ECA_Mechanics = new List<ECABlock>();

    [Header("视觉与对齐修正")]
    [Tooltip("组件的‘齿轮/关节’偏离中心的距离 (X,Y)")]
    public Vector2 AnchorOffset = Vector2.zero;

    [Tooltip("修正原画素材的初始倾斜角度 (绕齿轮点旋转)")]
    [Range(-180f, 180f)]
    public float BaseRotationOffset = 0f;

    [Range(0.1f, 5f)]
    public float VisualScaleMultiplier = 1.0f;
}

[System.Serializable]
public struct StatEntry
{
    public StatType StatID; // 以前这里是 string，现在变成了下拉菜单！
    public float Value;
}

[System.Serializable]
public struct ECABlock
{
    public string TriggerEvent;
    public string Condition;
    public string Action;
}