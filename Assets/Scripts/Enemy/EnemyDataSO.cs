using System.Collections.Generic;
using UnityEngine;

public enum EnemyMoveType { Normal, ChargeDash, Teleport, Stationary }

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Chimera Protocol/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("=== 基础识别信息 ===")]
    public string EnemyID = "ENM_000";
    public string EnemyName = "未知生物";
    [TextArea] public string Description = "敌人风味描述...";
    public Sprite EnemySprite;

    [Range(0.1f, 5f)] public float VisualScaleMultiplier = 1.0f;
    [TextArea] public string SpecialMechanicDesc = "特殊机制";

    [Header("=== 核心数值池 (仅放生存数值) ===")]
    public List<StatEntry> BaseStats = new List<StatEntry>();

    [Header("=== 战斗 AI 与移动逻辑 ===")]
    public TargetingStrategy TargetingLogic = TargetingStrategy.Nearest;
    public MovementStrategy MovementLogic = MovementStrategy.Active_Firepower;
    public float SafeDodgeDistance = 8f;
    public EnemyMoveType MoveType = EnemyMoveType.Normal;

    // 👇👇👇 【神级进化：怪物技能卡池！】 👇👇👇
    [Header("=== 技能池 (Skill Pool) ===")]
    [Tooltip("把做好的 EnemySkillSO 拖进来，怪物会自动根据射程和冷却随机抽卡释放！")]
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