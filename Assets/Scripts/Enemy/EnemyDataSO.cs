using System.Collections.Generic;
using UnityEngine;

public enum EnemyMoveType { Normal, ChargeDash, Teleport, Stationary }
public enum EnemyDamageTag { Kinetic, Energy, Corrosion }
public enum EnemyTargetType { Single, MultiTarget, AreaOfEffect }

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Chimera Protocol/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("=== 基础识别信息 ===")]
    public string EnemyID = "ENM_000";
    public string EnemyName = "未知生物";
    [TextArea] public string Description = "敌人风味描述...";
    public Sprite EnemySprite;
    [TextArea] public string SpecialMechanicDesc = "特殊机制";

    [Header("=== 核心标识 ===")]
    public List<ComponentTag> Tags = new List<ComponentTag>();

    [Header("=== 核心数值池 (绝对值) ===")]
    [Tooltip("请在此配置: HP, AP, Mass, MoveSpeed, MinDamage, MaxDamage, AttackSpeed 等")]
    public List<StatEntry> BaseStats = new List<StatEntry>();

    [Header("=== 战斗 AI 与移动逻辑 ===")]
    public TargetingStrategy TargetingLogic = TargetingStrategy.Nearest;
    public MovementStrategy MovementLogic = MovementStrategy.Active_Firepower;
    public EnemyMoveType MoveType = EnemyMoveType.Normal;

    [Header("=== 武器与伤害投递机制 ===")]
    public WeaponDeliveryType DeliveryType = WeaponDeliveryType.Melee;
    public EnemyTargetType TargetType = EnemyTargetType.Single;
    public EnemyDamageTag DamageTag = EnemyDamageTag.Kinetic;
    public GameObject ProjectilePrefab;

    // 👇👇👇 【主策的神级扩展：怪物全生命周期 ECA 魔法槽！】 👇👇👇
    [Header("=== ECA: 生命周期触发 ===")]
    [Tooltip("当怪物刚刚刷新在战场上时触发（例如：给周围友军加个护盾光环）")]
    public List<ECAAction> OnSpawnActions = new List<ECAAction>();

    [Tooltip("当怪物死亡时触发（例如：自爆！产生范围毒气弹，或者分裂成两个小怪）")]
    public List<ECAAction> OnDeathActions = new List<ECAAction>();

    [Header("=== ECA: 战斗交互触发 ===")]
    [Tooltip("当怪物打中玩家时触发（例如：给玩家挂流血 DOT、吸血）")]
    public List<ECAAction> OnAttackHitActions = new List<ECAAction>();

    [Tooltip("当怪物挨打时触发（例如：每次挨打增加移速，或者反伤电击）")]
    public List<ECAAction> OnTakeDamageActions = new List<ECAAction>();

    public float GetStat(StatType type)
    {
        if (BaseStats == null) return 0f;
        foreach (var stat in BaseStats) if (stat.StatID == type) return stat.Value;
        return 0f;
    }
}