using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatType
{
    AddedHP, AddedAP, AddedMass,
    PowerCost, EnginePower,
    MaxDamage, MinDamage, MaxRange, MinRange,
    AttackSpeed, CriticalChance, ExplosionRadius, MultiShotCount, ProjectileSpeed,
    HP, AP, Mass, MoveSpeed
}

// ==========================================
// 全新系统：阵营大类与细分标签树 (The Bazaar Style)
// ==========================================
public enum MacroCategory
{
    Tech,   // 科技阵营
    Flesh,  // 血肉阵营
    Magic   // 魔法阵营 (可随时扩充)
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

// --- 请替换 GameCoreTypes.cs 中的 WeaponDeliveryType 并增加新枚举 ---

// 在末尾加上 Tactical_Dash 战术位移
public enum WeaponDeliveryType { Melee, Ranged, Tactical_Dash }

// 👇【新增】：怪物的战术位移方向
public enum TacticalDashDirection { AwayFromTarget, TowardsTarget, Lateral }
public enum ComponentType { Core, Weapon, Support, Factory, Movement }
public enum TargetingStrategy { Nearest, MaxHPHighest, MaxHPLowest, CurrentHPHighest, CurrentHPLowest }
public enum MovementStrategy { Active_Firepower, Active_Survival, Dodge }
public enum EnemyMovementStrategy { Swarm, Artillery, IntentDriven }

[Serializable]
public class StatEntry
{
    public StatType StatID;
    public float Value;
}

// ==========================================
// 全新系统：等级数据矩阵块 (Level Matrix Block)
// ==========================================
// --- 请替换 GameCoreTypes.cs 中的 ComponentLevelData 类 ---

[Serializable]
public class ComponentLevelData
{
    public int Level = 1; // 1, 2, 3, 4
    public int BasePrice = 100;
    public int ScrapValue = 10;

    [Header("本级绝对数值 (非乘区)")]
    public List<StatEntry> Stats = new List<StatEntry>();

    [Header("本级新增的基础 ECA 机制 (万能积木)")]
    public List<ECABlock> Mechanics = new List<ECABlock>();

    [Header("本级专属生命周期动作")]
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();
    public List<ECAAction> OnAssembleActions = new List<ECAAction>();

    [Header("本级新增的额外标签")]
    public List<SubTag> BonusTags = new List<SubTag>();

    [TextArea] public string SpecialMechanicDesc = "等级特效描述...";

    // 👇【核心修复】：强制 new 一个对象，绝对防空！
    [Header("=== 本级主动技能 (仅装在核心槽位生效) ===")]
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