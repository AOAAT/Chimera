using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewChassis", menuName = "Chimera Protocol/1. 核心图纸库/装甲底盘 (Chassis)")]
public class ChassisDataSO : ScriptableObject
{
    [Header("=== 基础信息 (底盘无等级、不参与合成) ===")]
    public string ChassisID = "CH_000";
    public string ChassisName = "新底盘";
    public Sprite ChassisSprite;

    // 👇【核心新增】：让每个底盘自带它的专属详情页背景框！
    [Tooltip("在详情页展示时使用的底图 (比如带有专属插槽孔位的背景)")]
    public Sprite DetailBackgroundSprite;

    [TextArea] public string Description = "底盘风味描述";
    [TextArea] public string SpecialMechanicDesc = "特殊机制";


    [Header("=== 工业成本 ===")]
    public ResourceSet ProductionCost;

    [Header("=== 标签 ===")]
    public MacroCategory MacroCategory = MacroCategory.Tech;
    public List<SubTag> SubTags = new List<SubTag>();

    [Header("=== 工业生产数据 ===")]
    public float BaseProductionTime = 10f; // 默认生产底盘需要 10 秒

    [Header("=== 底盘自身属性加成 ===")]
    public List<StatEntry> BaseStats = new List<StatEntry>();

    [Header("=== 战斗逻辑心脏 (Logic Center) ===")]
    public Vector2 LogicCenterOffset = Vector2.zero;

    [Header("=== 接口/插槽定义 (极其核心) ===")]
    public List<SlotDefinition> Sockets = new List<SlotDefinition>();

    [Header("=== 生命周期 ECA (New!) ===")]
    [Tooltip("当底盘被加载/装配到战斗实体时触发（通常用于初始化永久效果）")]
    public List<ECAAction> OnAssembleActions = new List<ECAAction>();

    [Tooltip("该底盘特有的开战协议")]
    public List<ECAAction> OnBattleStartActions = new List<ECAAction>();

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