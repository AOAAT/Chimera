using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewComponent", menuName = "Chimera Protocol/Component Blueprint")]
public class ComponentDataSO : ScriptableObject
{
    [Header("=== 基础身份信息 (Identity) ===")]
    [Tooltip("极其重要：必须唯一！这是合成时判断是否同源的唯一凭证！")]
    public string ComponentBaseID = "WPN_001";
    public string ComponentName = "新组件";
    [TextArea] public string Description = "组件风味描述...";
    public Sprite ComponentIcon;
    public ComponentType Type;

    [Header("=== 标签驱动与产出控制 (Tag & Drop) ===")]
    public MacroCategory MacroCategory = MacroCategory.Tech;
    public List<SubTag> BaseSubTags = new List<SubTag>();

    [Tooltip("最小掉落等级。填2则战利品绝不掉落1级。")]
    [Range(1, 4)] public int MinDropLevel = 1;

    [Header("=== 等级矩阵 (Level Matrix 1~4) ===")]
    [Tooltip("严格按照 1~4 级的顺序配置。没有配的等级代表无法升到该级。")]
    public List<ComponentLevelData> LevelMatrix = new List<ComponentLevelData>();

    [Header("=== 武器独有投递方式 ===")]
    public WeaponDeliveryType DeliveryType = WeaponDeliveryType.Ranged;
    public GameObject ProjectilePrefab;

    [Header("=== 视觉与对齐修正 ===")]
    public Vector2 AnchorOffset = Vector2.zero;
    [Range(-180f, 180f)] public float BaseRotationOffset = 0f;
    [Range(0.1f, 5f)] public float VisualScaleMultiplier = 1.0f;

    [Header("=== 核心独有 AI 设定 ===")]
    public TargetingStrategy TargetingLogic = TargetingStrategy.Nearest;
    public MovementStrategy MovementLogic = MovementStrategy.Active_Firepower;
    public float SafeDodgeDistance = 8f;

    // 获取特定等级的数据块 (带防呆降级)
    public ComponentLevelData GetLevelData(int level)
    {
        var data = LevelMatrix.Find(x => x.Level == level);
        if (data == null && LevelMatrix.Count > 0) return LevelMatrix[LevelMatrix.Count - 1];
        return data;
    }
}

[System.Serializable]
public struct ECABlock
{
    public string TriggerEvent;
    public string Condition;
    public string Action;
}