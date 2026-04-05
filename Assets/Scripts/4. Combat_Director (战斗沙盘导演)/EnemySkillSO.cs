using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemySkill", menuName = "Chimera Protocol/1. 核心图纸库/敌人技能 (Enemy Skill)")]
public class EnemySkillSO : ScriptableObject
{
    [Header("=== 技能基础信息 ===")]
    public string SkillName = "撕咬";
    public float SelectionWeight = 10f;

    // 👇【核心重构】：用攻速替代绝对冷却时间！(50攻速 = 2秒CD)
    [Tooltip("攻击速度 (公式: 冷却时间 = 100 / 攻速)。例如 50 代表 2秒放一次。")]
    public float AttackSpeed = 50f;

    [Header("=== 索敌与射程限制 ===")]
    public float MaxRange = 2f;
    public float MinRange = 0f;

    [Header("=== 伤害与投递方式 ===")]
    public float MinDamage = 5f;
    public float MaxDamage = 10f;
    [Range(0f, 1f)] public float CriticalChance = 0.05f;

    public WeaponDeliveryType DeliveryType = WeaponDeliveryType.Melee;
    public GameObject ProjectilePrefab;
    [Tooltip("【仅远程有效】子弹飞行速度")]
    public float ProjectileSpeed = 10f;
    public float KnockbackForce = 5f;

    [Header("=== ECA 魔法机制 ===")]
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();
}