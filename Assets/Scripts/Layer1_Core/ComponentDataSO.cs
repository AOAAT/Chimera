using System.Collections.Generic;
using UnityEngine;

// 组件类型枚举（仅限组件使用，所以保留在这里）
public enum ComponentType
{
    Core,       // 核心
    Weapon,     // 武器
    Support,    // 辅助/插件
    Factory,    // 工厂
    Movement    // 移动组件（如腿部、履带）
}

[CreateAssetMenu(fileName = "NewComponent", menuName = "Chimera Protocol/Component Data")]
public class ComponentDataSO : ScriptableObject
{
    [Header("基础识别信息")]
    public string ComponentID = "WPN_000";
    public string ComponentName = "新组件";
    public ItemRarity Rarity = ItemRarity.Common;

    [TextArea]
    public string Description = "组件风味描述...";
    public Sprite ComponentIcon;

    [TextArea] public string SpecialMechanicDesc = "特殊机制";

    [Header("装配规则")]
    public ComponentType Type;

    [Header("=== 核心标识 ===")]
    public List<ComponentTag> Tags = new List<ComponentTag>();

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

    [Header("=== 武器专属机制 (仅武器有效) ===")]
    public WeaponDeliveryType DeliveryType = WeaponDeliveryType.Ranged;

    [Tooltip("如果是远程武器，请在这里放入子弹的预制体")]
    public GameObject ProjectilePrefab;

    [Header("=== ECA: 开火时触发 (On Fire) ===")]
    public List<ECAAction> OnFireActions = new List<ECAAction>();

    [Header("=== ECA: 命中时触发 (On Hit) ===")]
    public List<ECAAction> OnHitActions = new List<ECAAction>();

    [Header("=== ECA: 装配期触发 (On Assemble) ===")]
    public List<ECAAction> OnAssembleActions = new List<ECAAction>();

    [Header("=== 核心组件独有 AI 设定 (仅当 Type 为 Core 时有效) ===")]
    public TargetingStrategy TargetingLogic = TargetingStrategy.Nearest;
    public MovementStrategy MovementLogic = MovementStrategy.Active_Firepower;
    public float SafeDodgeDistance = 8f; // 躲避型专属：安全距离
}

// ECA 触发器结构体（组件附带，保留）
[System.Serializable]
public struct ECABlock
{
    public string TriggerEvent;
    public string Condition;
    public string Action;
}