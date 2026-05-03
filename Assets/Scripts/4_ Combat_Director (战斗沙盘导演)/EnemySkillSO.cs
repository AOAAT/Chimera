using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemySkill", menuName = "Chimera Protocol/1. 核心图纸库/敌人技能 (Enemy Skill)")]
public class EnemySkillSO : ScriptableObject
{
    [Header("=== 技能基础信息 ===")]
    public string SkillName = "攻击";
    public float AttackSpeed = 50f;

    [Header("=== 冷却配置 (AI 2.0) ===")]
    public float CooldownMultiplier = 1.0f;

    [Header("=== 独立索敌覆写 ===")]
    public bool OverrideTargeting = false;
    public TargetingStrategy SkillTargetingLogic = TargetingStrategy.MaxHPLowest;

    [Header("=== 决策系统 (Utility AI) ===")]
    public float BaseScore = 10f;
    public List<SkillEvaluator> Evaluators = new List<SkillEvaluator>();

    [Header("=== 令牌系统 ===")]
    public bool RequiresToken = false;
    public EnemyTokenType TokenType = EnemyTokenType.HeavyAttack;

    [Header("=== 意图预告 ===")]
    public bool ShowIntent = false;
    public Sprite IntentIcon;

    [Header("=== 连招逻辑 (Next) ===")]
    public EnemySkillSO NextComboSkill;

    // --- 在 EnemySkillSO.cs 中补全 ---
    [Header("=== 射程控制 ===")]
    [Tooltip("如果勾选，该技能将忽略射程判定，只要抽取成功就立即在原地进入蓄力/释放")]
    public bool IgnoreRange = false; // 👈 召唤、自爆、强化、瞬移类技能勾选此项

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


    [Header("=== ECA 魔法机制 ===")]
    public List<ECAAction> OnFireActions = new List<ECAAction>();
    public List<ECAAction> OnHitActions = new List<ECAAction>();
}