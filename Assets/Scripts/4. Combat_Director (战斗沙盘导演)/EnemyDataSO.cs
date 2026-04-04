using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct SanPenaltyTier
{
    [Tooltip("残血百分比阈值 (例如 0.8 代表血量在 80%~100% 之间)")]
    [Range(0f, 1f)] public float HpThreshold;
    [Tooltip("在这个血量区间，玩家战败时会扣除多少 SAN 值")]
    public int SanDeduction;
}

// 保留咱们定义好的物理运动表现
public enum EnemyMoveType { Normal, ChargeDash, Teleport, Stationary }

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Chimera Protocol/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("=== 基础识别信息 ===")]
    public string EnemyID = "ENM_000";
    public string EnemyName = "未知生物";
    [TextArea] public string Description = "敌人风味描述...";
    public Sprite EnemySprite;

    [Header("=== 视觉表现层 (动画与尸体) ===")]
    public RuntimeAnimatorController AnimController;
    [Tooltip("死亡后，尸体在场景中保留的时间 (秒)")]
    public float CorpseLingerTime = 5f;

    [Range(0.1f, 5f)] public float VisualScaleMultiplier = 1.0f;
    [TextArea] public string SpecialMechanicDesc = "特殊机制";

    [Header("=== 核心数值池 (仅放生存数值) ===")]
    public List<StatEntry> BaseStats = new List<StatEntry>();

    [Header("=== 战败惩罚 (Defeat Penalty) ===")]
    [Tooltip("请从高血量往低血量配置，例如：1.0扣3点，0.5扣1点。")]
    public List<SanPenaltyTier> SanPenalties = new List<SanPenaltyTier>();

    [Header("=== 战斗 AI 与移动逻辑 (去哪) ===")]
    public TargetingStrategy TargetingLogic = TargetingStrategy.Nearest;
    public EnemyMovementStrategy MovementLogic = EnemyMovementStrategy.Swarm;
    [Tooltip("仅当 AI 为 Artillery(炮台) 时生效，怪物会试图保持这个距离")]
    public float HoverDistance = 5f;

    // 👇👇👇 【核心扩建】：极其详尽的物理运动表现配置 👇👇👇
    [Header("=== 物理运动表现 (怎么去) ===")]
    public EnemyMoveType MoveType = EnemyMoveType.Normal;

    // 👇【主程新增】：蜂群怪物的接敌刹车距离（表面到表面的距离）
    [Tooltip("【仅蜂群有效】距离目标多远时停止推挤 (0表示死死贴住)")]
    public float StopDistance = 0.5f;

    [Tooltip("【传送/冲刺】起步前的蓄力前摇时间 (秒)")]
    public float MoveChargeTime = 1.0f;

    [Tooltip("【传送/冲刺】动作结束后的疲劳僵直时间 (秒)")]
    public float MoveCooldown = 1.5f;

    [Tooltip("【仅冲刺有效】冲刺期间的速度倍率 (基于基础移速)")]
    [Range(1f, 10f)] public float DashSpeedMultiplier = 4.0f;

    [Tooltip("【仅冲刺有效】冲刺动作的持续时间 (秒)")]
    public float DashDuration = 0.3f;

    [Tooltip("【仅传送有效】单次传送的最大距离")]
    public float TeleportDistance = 5.0f;

    [Header("=== 技能池 (Skill Pool) ===")]
    public List<EnemySkillSO> Skills = new List<EnemySkillSO>();

    [Header("=== 全局 ECA: 生命周期触发 ===")]
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