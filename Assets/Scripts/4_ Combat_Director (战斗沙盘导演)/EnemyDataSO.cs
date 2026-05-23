using System.Collections.Generic;
using UnityEngine;

public enum EnemyArchetype { Static, Modular }


[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Chimera Protocol/1. 核心图纸库/敌人本体 (Enemy)")]
public class EnemyDataSO : ScriptableObject
{
    [Header("=== 核心模式切换 ===")]
    public EnemyArchetype Archetype = EnemyArchetype.Static;

    [Header("=== 基础识别信息 ===")]
    public string EnemyID = "ENM_000";
    public string EnemyName = "未知生物";
    [TextArea] public string Description = "风味描述...";

    [Header("=== 静态模式视觉 (Archetype = Static) ===")]
    public Sprite EnemySprite;
    public RuntimeAnimatorController AnimController;

    [Header("=== 组装模式配置 (Archetype = Modular) ===")]
    public ChassisDataSO Chassis;
    public List<ComponentDataSO> Components = new List<ComponentDataSO>();
    [Range(1, 4)] public int EliteComponentLevel = 1;

    [Header("=== 核心意图策略 (去哪/怎么打) ===")]
    public EnemyMovementStrategy MovementLogic = EnemyMovementStrategy.Swarm;
    public TargetingStrategy TargetingLogic = TargetingStrategy.Nearest;

    [Tooltip("【蜂拥】模式下的停步距离")]
    public float StopDistance = 0.8f;
    [Tooltip("【炮台】模式下的维持距离")]
    public float HoverDistance = 5.0f;

    [Header("=== 核心数值池 ===")]
    public List<StatEntry> BaseStats = new List<StatEntry>();
    

    [Header("=== 通用视觉属性 ===")]
    [Range(0.1f, 5f)] public float VisualScaleMultiplier = 1.0f;
    public bool OverrideShadow = false;
    public Vector2 ShadowOffset = new Vector2(0f, -0.8f);
    public float ShadowWidth = 1.5f;
    public float ShadowHeight = 0.5f;

    [Header("=== 技能池 (意图驱动模式的核心) ===")]
    public List<EnemySkillSO> Skills = new List<EnemySkillSO>();

    [Header("=== 生命周期 ECA ===")]
    public List<ECAAction> OnSpawnActions = new List<ECAAction>();
    public List<ECAAction> OnDeathActions = new List<ECAAction>();
    public List<ECAAction> OnTakeDamageActions = new List<ECAAction>();

    // --- 在 EnemyDataSO.cs 中修改 ---
    [Header("=== 死亡表现 (Corpse) ===")]
    [Tooltip("死亡后，尸体在原地完全静止的时长 (秒)")]
    public float CorpseLingerTime = 2.0f;

    [Tooltip("尸体从可见到完全透明的过渡时长 (秒)")]
    public float FadeDuration = 1.5f; // 👈 新增：在 Unity 里直接控制渐变快慢
    public float GetStat(StatType type)
    {
        if (BaseStats == null) return 0f;
        foreach (var stat in BaseStats) if (stat.StatID == type) return stat.Value;
        return 0f;
    }
}