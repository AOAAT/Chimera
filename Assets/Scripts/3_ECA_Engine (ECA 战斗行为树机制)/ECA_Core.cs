using UnityEngine;
using System.Collections.Generic;

// ==========================================
// 1. ECA 通讯上下文
// ==========================================
public class ECAContext
{
    public Vector3 ImpactPoint;
    public Transform PrimaryTarget;
    public float BaseDamage;
    public RuntimeWeapon SourceWeapon;
    public bool IsCriticalHit;
    public RuntimeChimeraData ChassisData;
    public ComponentDataSO SourceComponentSO;
    public bool IsEnemyFire;
    public Transform SourceEntity;
    public bool ExecutionAborted = false;

    public float TemporaryCritModifier = 1.0f;
    public float TemporaryDamageModifier = 1.0f;

    public int PiercingIndex = 0;
    public Vector2 StrikeDirection;

    public Dictionary<string, float> CustomStates = new Dictionary<string, float>();
}

public abstract class ECAAction : ScriptableObject
{
    public abstract void Execute(ECAContext context);
}

// ==========================================
// 2. 线性激光标准配置 (Linear Strike Standard)
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

    [Header("=== 3. 穿透与射程 (核心改动) ===")]
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
}