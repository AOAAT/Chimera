using System;
using System.Collections.Generic;
using UnityEngine;

// ==========================================
// 1. 核心属性枚举 (StatType)
// 【主程预警】：此处已手动分配固定 ID，严禁修改已有数字！
// ==========================================
public enum StatType
{
    // --- 基础加成属性 (0~9) ---
    AddedHP = 0,
    AddedAP = 1,
    AddedMass = 2,
    AddedBlock = 3,
    PowerCost = 4,
    EnginePower = 5,

    // --- 武器/战斗专属属性 (10~29) ---
    MaxDamage = 10,
    MinDamage = 11,
    CritMultiplier = 12,     // 暴击伤害倍率 (例如 2.0 代表 200%)
    MaxRange = 13,
    MinRange = 14,
    AttackSpeed = 15,
    CriticalChance = 16,
    ExplosionRadius = 17,
    MultiShotCount = 18,
    ProjectileSpeed = 19,

    // --- 实体运行时实时属性 (30~49) ---
    HP = 30,
    AP = 31,
    Mass = 32,
    MoveSpeed = 33,
    Block = 34
}

// ==========================================
// 2. 阵营与标签系统
// ==========================================
public enum MacroCategory
{
    Tech,   // 科技阵营
    Flesh,  // 血肉阵营
    Magic   // 魔法阵营
}

public enum SubTag
{
    // --- Tech 专属 ---
    Ballistic, Energy, Shield, Drone,
    // --- Flesh 专属 ---
    Mutation, Parasite, Acid, Biomass,
    // --- Magic / 通用 ---
    Curse, Summon, Economy, Heavy
}

public enum SalvageDropType
{
    SingleDrop, // 单选盲盒
    DraftThree  // 三选一
}

// ==========================================
// 3. 战斗与 AI 策略枚举
// ==========================================
public enum WeaponDeliveryType
{
    Melee,
    Ranged,
    Tactical_Dash // 战术位移
}

public enum TacticalDashDirection
{
    AwayFromTarget,
    TowardsTarget,
    Lateral
}

public enum ComponentType
{
    Core,
    Weapon,
    Support,
    Factory,
    Movement
}

public enum TargetingStrategy
{
    FollowCoreAI = 0,    // 👈【核心新增】：默认继承核心大脑的设定
    Nearest = 1,
    MaxHPHighest = 2,
    MaxHPLowest = 3,
    CurrentHPHighest = 4,
    CurrentHPLowest = 5,
    Furthest = 6
}

public enum MovementStrategy
{
    Active_Firepower,
    Active_Survival,
    Dodge
}

public enum EnemyMovementStrategy
{
    Swarm,
    Artillery,
    IntentDriven
}

// ==========================================
// 4. 数据结构体
// ==========================================

[Serializable]
public class StatEntry
{
    public StatType StatID;
    public float Value;
}

[Serializable]
public class ComponentLevelData
{
    public int Level = 1; // 1, 2, 3, 4
    public int BasePrice = 100;
    public int ScrapValue = 10;

    [Header("📊 本级绝对数值 (非乘区)")]
    public List<StatEntry> Stats = new List<StatEntry>();

    [Header("🧩 本级新增的基础 ECA 机制 (万能积木)")]
    public List<ECABlock> Mechanics = new List<ECABlock>();

    [Header("🎬 本级专属生命周期管线")]
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();
    public List<ECAAction> OnAssembleActions = new List<ECAAction>();

    [Tooltip("战斗开始发令枪响的那一刻触发")]
    public List<ECAAction> OnBattleStartActions = new List<ECAAction>();

    [Header("🏷️ 本级新增的额外标签")]
    public List<SubTag> BonusTags = new List<SubTag>();

    [TextArea] public string SpecialMechanicDesc = "等级特效描述...";

    // 👇【核心修复】：强制 new 一个对象，绝对防止 Inspector 空指针！
    [Header("⚡ 本级主动技能 (仅装在核心槽位生效)")]
    public ActiveSkillConfig ActiveSkill = new ActiveSkillConfig();
}

[Serializable]
public class ActiveSkillConfig
{
    public bool HasActiveSkill = false;
    public string SkillName = "核心过载";
    public Sprite SkillIcon;
    public float CPCost = 3f;
    public float Cooldown = 10f;

    [Header("技能执行的 ECA 魔法积木")]
    public List<ECAAction> OnSkillCastActions = new List<ECAAction>();
}