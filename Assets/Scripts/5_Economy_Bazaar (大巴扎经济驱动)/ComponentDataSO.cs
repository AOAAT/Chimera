// --- 请替换 FILE ComponentDataSO.cs ---
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewComponent", menuName = "Chimera Protocol/1. 核心图纸库/机甲组件 (Component)")]
public class ComponentDataSO : ScriptableObject
{
    [Header("=== 基础身份信息 (Identity) ===")]
    public string ComponentBaseID = "WPN_001";
    public string ComponentName = "新组件";
    [TextArea(3, 5)] public string Description = "组件风味描述...";
    public Sprite ComponentIcon;
    public ComponentType Type;

    [Header("=== 战术定位描述 (仅UI展示) ===")]
    public string TacticalRoleDesc = "远程单体";

    [Header("=== 视觉表现层 (动画与特效) ===")]
    public RuntimeAnimatorController AnimController;
    public Vector2 MuzzleOffset = Vector2.zero;

    [Header("=== 标签驱动与产出控制 ===")]
    public MacroCategory MacroCategory = MacroCategory.Tech;
    public List<SubTag> BaseSubTags = new List<SubTag>();
    [Range(1, 4)] public int MinDropLevel = 1;

    [Header("=== 等级矩阵 (Level Matrix 1~4) ===")]
    public List<ComponentLevelData> LevelMatrix = new List<ComponentLevelData>();

    [Header("=== 武器独有投递方式 ===")]
    public WeaponDeliveryType DeliveryType = WeaponDeliveryType.Ranged;
    public GameObject ProjectilePrefab;

    // 👇【核心新增】：近战武器的三段式动作配置！
    [Header("=== 近战动作配置 (仅 DeliveryType=Melee 生效) ===")]
    [Tooltip("蓄力抬手角度 (负数代表向后拉)")]
    public float WindupAngle = -45f;

    [Tooltip("下劈砸中敌人的角度 (正数代表向前砸)")]
    public float StrikeAngle = 60f;

    [Tooltip("蓄力时间占总攻击间隔的百分比 (如 0.3 代表 30% 时间在抬手)")]
    [Range(0.01f, 0.9f)] public float WindupTimeRatio = 0.3f;

    [Tooltip("下劈时间占总攻击间隔的百分比 (如 0.1 代表 10% 时间砸下去)")]
    [Range(0.01f, 0.9f)] public float StrikeTimeRatio = 0.1f;

    [Header("⚔️ 武器专属索敌 (仅 Weapon 类型生效)")]
    [Tooltip("设置为 FollowCoreAI 时，将严格遵循核心组件的 AI 逻辑")]
    public TargetingStrategy TargetingOverride = TargetingStrategy.FollowCoreAI;

    [Header("=== 视觉与对齐修正 ===")]
    public Vector2 AnchorOffset = Vector2.zero;
    [Range(-180f, 180f)] public float BaseRotationOffset = 0f;
    [Range(0.1f, 5f)] public float VisualScaleMultiplier = 1.0f;

    [Header("=== 阴影微调 (仅对移动组件有效) ===")]
    public bool OverrideShadow = false;
    [Tooltip("阴影相对于该组件挂载点的偏移")]
    public Vector2 ShadowOffset = new Vector2(0f, -0.5f);
    [Tooltip("阴影的水平宽度")]
    public float ShadowWidth = 1.2f;
    [Tooltip("阴影的垂直高度")]
    public float ShadowHeight = 0.4f;

    [Header("=== 核心独有 AI 设定 ===")]
    public TargetingStrategy TargetingLogic = TargetingStrategy.Nearest;
    public MovementStrategy MovementLogic = MovementStrategy.Active_Firepower;
    public float SafeDodgeDistance = 8f;


    public ComponentLevelData GetLevelData(int level)
    {
        var data = LevelMatrix.Find(x => x.Level == level);
        if (data == null && LevelMatrix.Count > 0) return LevelMatrix[LevelMatrix.Count - 1];
        return data;
    }

    // 简单的数据校验，防止策划填错比例导致动画崩溃
    private void OnValidate()
    {
        if (Type == ComponentType.Weapon && DeliveryType == WeaponDeliveryType.Melee)
        {
            if (WindupTimeRatio + StrikeTimeRatio >= 0.99f)
            {
                Debug.LogWarning($"[{ComponentName}] 的近战动作比例设置错误！Windup + Strike 不能超过 0.99，必须给 Recovery 留出时间！");
                StrikeTimeRatio = 0.9f - WindupTimeRatio;
            }
        }
    }
}
[System.Serializable]
public struct ECABlock
{
    public string TriggerEvent;
    public string Condition;
    public string Action;
}