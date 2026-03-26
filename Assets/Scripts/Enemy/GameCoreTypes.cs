using System;

public enum StatType
{
    // --- 通用生存与物理 ---
    AddedHP, AddedAP, AddedMass,

    // --- 玩家机甲专属 ---
    PowerCost, EnginePower,

    // --- 武器与战斗通用 ---
    MaxDamage, MinDamage, MaxRange, MinRange,
    AttackSpeed, CriticalChance, ExplosionRadius, MultiShotCount, ProjectileSpeed,

    // --- 敌人专属 ---
    // 👇【核心净化】：去掉了所有机制型参数，只保留真正的动态属性！
    HP, AP, Mass, MoveSpeed
}
// ==========================================
// 5. 战利品与经济系统枚举 (新增)
// ==========================================

// 物品稀有度 (决定背景颜色和掉落权重)
public enum ItemRarity
{
    Common,    // 普通 (白/灰)
    Uncommon,  // 罕见 (绿)
    Rare,      // 稀有 (蓝)
    Epic,      // 史诗 (紫)
    Legendary  // 传说 (金)
}

// ==========================================
// 1. 奖励类型的终极枚举
// ==========================================
public enum RewardCategory
{
    Resource,         // 资源类 (电量、金币等)
    RandomBlindBox,   // 盲盒单抽
    DraftChoice       // 核心三选一
}

// 目标类型：增加了“动态智能混合”选项！
public enum RewardTargetType
{
    ComponentOnly,    // 只抽组件
    ChassisOnly,      // 只抽底盘
    SmartMix          // 👇【核心新增】：智能混合！按权重掷骰子，并附带保底补给！
}
[Serializable]
public class StatEntry
{
    public StatType StatID;
    public float Value;
}

public enum WeaponDeliveryType { Melee, Ranged }
public enum ComponentTag { None, Factory, Flesh, Tech }
public enum TargetingStrategy { Nearest, MaxHPHighest, MaxHPLowest, CurrentHPHighest, CurrentHPLowest }
public enum MovementStrategy { Active_Firepower, Active_Survival, Dodge }

// 👇【全新独立】：这是专门为怪物AI开辟的兵种本能枚举！
public enum EnemyMovementStrategy { Swarm, Artillery, IntentDriven }