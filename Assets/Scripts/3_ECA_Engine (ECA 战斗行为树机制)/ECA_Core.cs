using UnityEngine;
using System.Collections.Generic;

// ==========================================
// 1. ECA 通讯上下文 (全功能集成版)
// ==========================================
public class ECAContext
{
    // --- 基础战斗数据 ---
    public Vector3 ImpactPoint;
    public Transform PrimaryTarget;
    public float BaseDamage;
    public RuntimeWeapon SourceWeapon;
    public bool IsCriticalHit;
    public RuntimeChimeraData ChassisData;
    public ComponentDataSO SourceComponentSO;
    public bool IsEnemyFire;
    public Transform SourceEntity;

    // --- 逻辑控制 ---
    public bool ExecutionAborted = false;
    public float TemporaryCritModifier = 1.0f;
    public float TemporaryDamageModifier = 1.0f;

    // --- 👇【系统化扩展 A】：线性穿透参数 (用于凝望者/轨道炮) ---
    public int PiercingIndex = 0;
    public Vector2 StrikeDirection;

    // --- 👇【系统化扩展 B】：动能物理参数 (用于牛牛/冲撞) ---
    public float ImpactVelocity;         // 碰撞瞬间的相对速度
    public float ImpactMass;             // 发动者的质量
    public Vector2 ImpactNormal;         // 碰撞法线

    // --- 通讯字典 ---
    public Dictionary<string, float> CustomStates = new Dictionary<string, float>();
}

// ==========================================
// 2. ECA 行为基类
// ==========================================
public abstract class ECAAction : ScriptableObject
{
    public abstract void Execute(ECAContext context);
}

// ==========================================
// 3. 系统化配置：线性激光标准
// ==========================================
[System.Serializable]
public class LinearLaserConfig
{
    [Header("=== 1. 时间轴分配 (总和需 = 1.0) ===")]
    [Range(0f, 1f)] public float TrackingRatio = 0.4f;
    [Range(0f, 1f)] public float LockingRatio = 0.2f;
    [Range(0f, 1f)] public float FiringRatio = 0.4f;

    [Header("=== 2. 判定模式设定 ===")]
    public bool IsSustainedDamage = true;
    public float TickRate = 10f;

    [Header("=== 3. 穿透与射程 ===")]
    public float BeamWidth = 0.5f;
    [Tooltip("如果武器/技能没配射程，则使用此默认距离 (米)")]
    public float MaxDistance = 15f;
    [Range(0f, 1f)] public float PiercingDecay = 0.7f;
    public int MaxTargets = 8;

    [Header("=== 4. 视觉与噪波 ===")]
    public float JitterAmplitude = 0.05f;
    public float NoiseIntensity = 0.3f;
    public int SubdivisionPoints = 20;
    public Color TrackingColor = new Color(0.5f, 0f, 1f, 0.3f);
    public Color FiringColor = new Color(0.8f, 0.3f, 1f, 1f);

    [Header("=== 5. 稳定性控制 ===")]
    [Tooltip("如果勾选，激光在追踪和锁定阶段免疫打断")]
    public bool IsUnstoppable = false;
}