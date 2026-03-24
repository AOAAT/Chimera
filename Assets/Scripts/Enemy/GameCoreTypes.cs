// 文件名：GameCoreTypes.cs
// 作用：全游戏通用的底层枚举与数据结构字典（绝对中立，不偏袒玩家也不偏袒敌人）

using System;

// ==========================================
// 1. 全局度量衡字典 (原来在 ComponentDataSO 里)
// ==========================================
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
    // 👇【新增】：实体绝对生存数值 (敌人、中立生物专属)
    HP,
    AP,
    Mass, MoveSpeed, SafeDodgeDistance, KnockbackForce, ChargeTime, TeleportDistance, SkillCooldown
}

[Serializable]
public class StatEntry
{
    public StatType StatID;
    public float Value;
}

// ==========================================
// 2. 战斗与伤害投递 (原来在 ComponentDataSO 里)
// ==========================================
public enum WeaponDeliveryType { Melee, Ranged }

// ==========================================
// 3. 阵营与流派标签 (原来在 ComponentTag 里)
// ==========================================
public enum ComponentTag { None, Factory, Flesh, Tech }

// ==========================================
// 4. ECA 通用智能 AI 大脑 (原来在 ComponentDataSO 里)
// ==========================================
public enum TargetingStrategy { Nearest, MaxHPHighest, MaxHPLowest, CurrentHPHighest, CurrentHPLowest }
public enum MovementStrategy { Active_Firepower, Active_Survival, Dodge }