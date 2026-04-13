// --- START OF FILE EnemySkillSO.cs ---
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemySkill", menuName = "Chimera Protocol/1. 核心图纸库/敌人技能 (Enemy Skill)")]
public class EnemySkillSO : ScriptableObject
{
    [Header("=== 技能基础信息 ===")]
    public string SkillName = "撕咬";
    public float SelectionWeight = 10f;
    public float AttackSpeed = 50f;

    [Header("=== 独立索敌覆写 (Targeting Override) ===")]
    [Tooltip("勾选后，这个技能会无视怪物本身的索敌，去寻找特定的目标！")]
    public bool OverrideTargeting = false;
    public TargetingStrategy SkillTargetingLogic = TargetingStrategy.MaxHPLowest; // 比如：专打血最少的！

    [Header("=== 射程限制 ===")]
    public float MaxRange = 2f;
    public float MinRange = 0f;

    [Header("=== 伤害与投递方式 ===")]
    public float MinDamage = 5f;
    public float MaxDamage = 10f;
    [Range(0f, 1f)] public float CriticalChance = 0.05f;

    public WeaponDeliveryType DeliveryType = WeaponDeliveryType.Melee;

    [Tooltip("【仅远程有效】")]
    public GameObject ProjectilePrefab;
    public float ProjectileSpeed = 10f;
    public float KnockbackForce = 5f;

    [Header("=== 战术位移专属 (仅 Tactical_Dash 生效) ===")]
    [Tooltip("位移的方向 (后跳拉扯 / 突进 / 侧滑)")]
    public TacticalDashDirection DashDirection = TacticalDashDirection.AwayFromTarget;
    [Tooltip("位移冲量 (会被怪物的 Mass 稀释，填大一点比如 300)")]
    public float DashImpulse = 300f;

    [Header("=== ECA 魔法机制 ===")]
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();
}