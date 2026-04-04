using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewComponent", menuName = "Chimera Protocol/Component Blueprint")]
public class ComponentDataSO : ScriptableObject
{
    [Header("=== 基础身份信息 (Identity) ===")]
    public string ComponentBaseID = "WPN_001";
    public string ComponentName = "新组件";
    [TextArea] public string Description = "组件风味描述...";
    public Sprite ComponentIcon;
    public ComponentType Type;

    // 👇【新增】：动画与视觉配置
    [Header("=== 视觉表现层 (动画与特效) ===")]
    [Tooltip("如果不填，系统将默认使用静态帧图片")]
    public RuntimeAnimatorController AnimController;

    [Tooltip("仅武器有效：真实的枪口发射位置 (相对把手的偏移)")]
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

    [Header("=== 视觉与对齐修正 ===")]
    public Vector2 AnchorOffset = Vector2.zero;
    [Range(-180f, 180f)] public float BaseRotationOffset = 0f;
    [Range(0.1f, 5f)] public float VisualScaleMultiplier = 1.0f;

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
}
[System.Serializable]
public struct ECABlock
{
    public string TriggerEvent;
    public string Condition;
    public string Action;
}