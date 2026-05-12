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
public enum MacroCategory { Tech, Flesh, Magic } ////
public enum SubTag 
{
    //通用==========================================

    StrongAcid,//强酸
    Melee,//近战
    Ranged,//远程
    Charge,//冲撞
    Armor,//装甲
    Heavy,//重型
    Devotion,//奉献
    Smash,//强击
    Knockback,//击退

    //科技==========================================

    Wasteland,//废土
    Industry,//工业
    Firearms,//枪械
    Laboratory,//实验室
    Reload,//装填
    Kinetic,//动能
    Plasma,//等离子

    //血肉==========================================

    Head,//头颅
    Organs,//内脏
    Limbs,//四肢
    Parasite,//寄生
    Pain,//痛苦
   

    //魔法==========================================

    Artifact,//遗物
    Otherworld,//异界
    Mana,//魔力
    Chaos,//混沌
    Order,//秩序
}

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
// --- 在 GameCoreTypes.cs 中追加 ---
public enum BuffModifierType
{
    Additive,   // 绝对值加减 (如: HP +50)
    Multiplier  // 百分比乘率 (如: 伤害 +20%)
}

[Serializable]
public class StatEntry
{
    public StatType StatID;
    public float Value;
    // 👇【核心新增】：区分加法还是乘法，默认为加法以兼容旧数据
    public BuffModifierType ModType = BuffModifierType.Additive;
}

// --- GameCoreTypes.cs ---

[Serializable]
public class ComponentLevelData
{
    public int Level = 1;
    public int BasePrice = 100;
    public int ScrapValue = 10;
    public List<StatEntry> Stats = new List<StatEntry>();

    // 原有的管线
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();
    public List<ECAAction> OnAssembleActions = new List<ECAAction>();
    public List<ECAAction> OnBattleStartActions = new List<ECAAction>();

    // --- 👇【核心新增】：正式开辟 OnTick 生命周期管线 ---
    public List<ECAAction> OnTickActions = new List<ECAAction>();

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
