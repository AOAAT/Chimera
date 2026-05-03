using System;
using System.Collections.Generic;
using UnityEngine;

// ==========================================
// 1. 核心属性枚举
// ==========================================
public enum StatType
{
    AddedHP = 0, AddedAP = 1, AddedMass = 2, AddedBlock = 3,
    PowerCost = 4, EnginePower = 5,
    MaxDamage = 10, MinDamage = 11, CritMultiplier = 12,
    MaxRange = 13, MinRange = 14, AttackSpeed = 15,
    CriticalChance = 16, ExplosionRadius = 17, MultiShotCount = 18, ProjectileSpeed = 19,
    HP = 30, AP = 31, Mass = 32, MoveSpeed = 33, Block = 34
}

// ==========================================
// 2. 阵营与标签
// ==========================================
public enum MacroCategory { Tech, Flesh, Magic }
public enum SubTag { Ballistic, Energy, Shield, Drone, Mutation, Parasite, Acid, Biomass, Curse, Summon, Economy, Heavy }

// 👇【核心修复】：补全丢失的枚举
public enum SalvageDropType { SingleDrop, DraftThree }

// ==========================================
// 3. 投递模式 (改回原名，但加入 Special)
// ==========================================
public enum WeaponDeliveryType
{
    Melee,      // 近战
    Ranged,     // 远程
    Special     // 特殊 (满足你的配置需求)
}

public enum TargetingStrategy { FollowCoreAI = 0, Nearest = 1, MaxHPHighest = 2, MaxHPLowest = 3, CurrentHPHighest = 4, CurrentHPLowest = 5, Furthest = 6 }
public enum MovementStrategy { Active_Firepower, Active_Survival, Dodge }
public enum EnemyMovementStrategy { Swarm, Artillery, IntentDriven }
public enum ComponentType { Core, Weapon, Support, Factory, Movement }

// ==========================================
// 4. 数据结构体 (保持现状)
// ==========================================
[Serializable]
public class StatEntry { public StatType StatID; public float Value; }

[Serializable]
public class ComponentLevelData
{
    public int Level = 1; public int BasePrice = 100; public int ScrapValue = 10;
    public List<StatEntry> Stats = new List<StatEntry>();
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();
    public List<ECAAction> OnAssembleActions = new List<ECAAction>();
    public List<ECAAction> OnBattleStartActions = new List<ECAAction>();
    public List<SubTag> BonusTags = new List<SubTag>();
    [TextArea] public string SpecialMechanicDesc = "...";
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
    public List<ECAAction> OnSkillCastActions = new List<ECAAction>();
}