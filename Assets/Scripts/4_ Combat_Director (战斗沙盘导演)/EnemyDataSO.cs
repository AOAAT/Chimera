using System.Collections.Generic;
using UnityEngine;

// 敌人大类：静态（原有杂兵） vs 组装（精英机甲）
public enum EnemyArchetype { Static, Modular }

[System.Serializable]
public struct SanPenaltyTier
{
    [Tooltip("残血百分比阈值 (例如 0.8 代表血量在 80%~100% 之间)")]
    [Range(0f, 1f)] public float HpThreshold;
    [Tooltip("在这个血量区间，玩家战败时会扣除多少 SAN 值")]
    public int SanDeduction;
}

public enum EnemyMoveType { Normal, ChargeDash, Teleport, Stationary }

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Chimera Protocol/1. 核心图纸库/敌人本体 (Enemy)")]
public class EnemyDataSO : ScriptableObject
{
    [Header("=== 核心模式切换 ===")]
    public EnemyArchetype Archetype = EnemyArchetype.Static;

    [Header("=== 基础识别信息 ===")]
    public string EnemyID = "ENM_000";
    public string EnemyName = "未知生物";
    [TextArea] public string Description = "敌人风味描述...";
    [TextArea] public string SpecialMechanicDesc = "特殊机制";

    [Header("=== 静态模式视觉 (Archetype = Static) ===")]
    public Sprite EnemySprite;
    public RuntimeAnimatorController AnimController;
    [Tooltip("死亡后，尸体在场景中保留的时间 (秒)")]
    public float CorpseLingerTime = 5f;

    [Header("=== 组装模式配置 (Archetype = Modular) ===")]
    public ChassisDataSO Chassis;
    [Tooltip("零件清单。索引需对应底盘插槽。")]
    public List<ComponentDataSO> Components = new List<ComponentDataSO>();
    [Range(1, 4)] public int EliteComponentLevel = 1;

    [Header("=== 通用视觉属性 ===")]
    [Range(0.1f, 5f)] public float VisualScaleMultiplier = 1.0f;
    public bool OverrideShadow = false;
    [Tooltip("阴影相对于物体中心的 X,Y 偏移")]
    public Vector2 ShadowOffset = new Vector2(0f, -0.8f);
    public float ShadowWidth = 1.5f;
    public float ShadowHeight = 0.5f;

    [Header("=== 核心数值池 (静态模式必填，组装模式可选作修正) ===")]
    public List<StatEntry> BaseStats = new List<StatEntry>();

    [Header("=== 战败惩罚 (Defeat Penalty) ===")]
    public List<SanPenaltyTier> SanPenalties = new List<SanPenaltyTier>();

    [Header("=== 战斗 AI 与移动逻辑 (去哪) ===")]
    public TargetingStrategy TargetingLogic = TargetingStrategy.Nearest;
    public EnemyMovementStrategy MovementLogic = EnemyMovementStrategy.Swarm;
    [Tooltip("仅当 AI 为 Artillery 时生效")]
    public float HoverDistance = 5f;

    [Header("=== 物理运动表现 (怎么去) ===")]
    public EnemyMoveType MoveType = EnemyMoveType.Normal;
    [Tooltip("【仅蜂群有效】距离目标多远时停止推挤")]
    public float StopDistance = 0.5f;
    public float MoveChargeTime = 1.0f;
    public float MoveCooldown = 1.5f;
    [Range(1f, 10f)] public float DashSpeedMultiplier = 4.0f;
    public float DashDuration = 0.3f;
    public float TeleportDistance = 5.0f;

    [Header("=== 技能池 (仅静态模式有效) ===")]
    public List<EnemySkillSO> Skills = new List<EnemySkillSO>();

    [Header("=== 生命周期 ECA ===")]
    public List<ECAAction> OnSpawnActions = new List<ECAAction>();
    public List<ECAAction> OnDeathActions = new List<ECAAction>();
    public List<ECAAction> OnTakeDamageActions = new List<ECAAction>();

    public float GetStat(StatType type)
    {
        if (BaseStats == null) return 0f;
        foreach (var stat in BaseStats) if (stat.StatID == type) return stat.Value;
        return 0f;
    }
}