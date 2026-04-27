using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemySkill", menuName = "Chimera Protocol/1. 核心图纸库/敌人技能 (Enemy Skill)")]
public class EnemySkillSO : ScriptableObject
{
    [Header("=== 技能基础信息 ===")]
    public string SkillName = "撕咬";
    public float SelectionWeight = 10f; // 基础权重
    public float AttackSpeed = 50f;

    [Header("=== 独立索敌覆写 ===")]
    public bool OverrideTargeting = false;
    public TargetingStrategy SkillTargetingLogic = TargetingStrategy.MaxHPLowest;

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
    public TacticalDashDirection DashDirection = TacticalDashDirection.AwayFromTarget;
    public float DashImpulse = 300f;

    [Header("=== ECA 魔法机制 ===")]
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();

    // ==========================================
    // 🆕 【主程：以下是 AI 2.0 新增字段，请确保补齐】
    // ==========================================
    [Header("=== 决策系统 (Utility AI) ===")]
    public float BaseScore = 10f;
    public List<SkillEvaluator> Evaluators = new List<SkillEvaluator>();

    [Header("=== 令牌系统 ===")]
    public bool RequiresToken = false;
    public EnemyTokenType TokenType = EnemyTokenType.HeavyAttack;

    [Header("=== 意图预告 ===")]
    public bool ShowIntent = false;
    public Sprite IntentIcon;
    public float ChargeTime = 1.0f;

    [Header("=== 连招逻辑 (Next) ===")]
    public EnemySkillSO NextComboSkill;
}