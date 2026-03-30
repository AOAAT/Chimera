using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewChassis", menuName = "Chimera Protocol/Chassis Blueprint")]
public class ChassisDataSO : ScriptableObject
{
    [Header("=== 基础信息 (底盘无等级、不参与合成) ===")]
    public string ChassisID = "CH_000";
    public string ChassisName = "新底盘";
    public Sprite ChassisSprite;
    [TextArea] public string Description = "底盘风味描述";
    [TextArea] public string SpecialMechanicDesc = "特殊机制";

    [Header("=== 标签驱动 (决定何种图掉落) ===")]
    public MacroCategory MacroCategory = MacroCategory.Tech;
    public List<SubTag> SubTags = new List<SubTag>();

    [Header("=== 底盘自身属性加成 ===")]
    public List<StatEntry> BaseStats = new List<StatEntry>();

    [Header("=== 战斗逻辑心脏 (Logic Center) ===")]
    public Vector2 LogicCenterOffset = Vector2.zero;

    [Header("=== 接口/插槽定义 (极其核心) ===")]
    public List<SlotDefinition> Sockets = new List<SlotDefinition>();
}

[System.Serializable]
public class SlotDefinition
{
    public string SlotName;
    public List<ComponentType> AllowedTypes;
    public Vector2 LocalPosition;
    [Range(-180f, 180f)] public float MountAngle = 0f;
    [Range(0.1f, 5f)] public float DefaultComponentScale = 1.0f;
}