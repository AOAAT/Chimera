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