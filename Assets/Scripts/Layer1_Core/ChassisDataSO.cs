using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewChassis", menuName = "Chimera Protocol/Chassis Data")]
public class ChassisDataSO : ScriptableObject
{
    [Header("底盘基础信息")]
    public string ChassisID = "CH_000";
    public string ChassisName = "新底盘";
    public Sprite ChassisSprite; // 底盘的主贴图

    [Header("=== 文本与世界观 ===")]
    [TextArea] public string Description = "底盘的风味描述";
    [TextArea] public string SpecialMechanicDesc = "特殊机制";

    [Header("=== 核心标识 (三级流派标签) ===")]
    public List<ComponentTag> Tags = new List<ComponentTag>();

    [Header("底盘自身属性加成")]
    public List<StatEntry> BaseStats = new List<StatEntry>();

    [Header("接口/插槽定义 (极其核心)")]
    public List<SlotDefinition> Sockets = new List<SlotDefinition>();
}

// 接口的精确定义
[System.Serializable]
public class SlotDefinition
{
    public string SlotName;
    public List<ComponentType> AllowedTypes;
    public Vector2 LocalPosition;

    [Header("姿态控制")]
    [Range(-180f, 180f)]
    public float MountAngle = 0f; // 插槽的默认旋转角度 (比如左侧武器朝左填 90)

    [Range(0.1f, 5f)]
    public float DefaultComponentScale = 1.0f;
}