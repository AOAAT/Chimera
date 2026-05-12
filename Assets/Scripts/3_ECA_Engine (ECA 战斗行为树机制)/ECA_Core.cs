// --- ECA_Core.cs (全量兼容版) ---
using UnityEngine;
using System.Collections.Generic;

public class ECAContext
{
    // --- 1. 基础战斗数据 (旧脚本强依赖) ---
    public Vector3 ImpactPoint;
    public Transform PrimaryTarget;
    public float BaseDamage;
    public RuntimeWeapon SourceWeapon;
    public bool IsCriticalHit;          // 确保名字和旧版完全一致
    public RuntimeChimeraData ChassisData;
    public ComponentDataSO SourceComponentSO;
    public bool IsEnemyFire;
    public Transform SourceEntity;

    // --- 2. 逻辑控制 (2.0 新增) ---
    public bool ExecutionAborted = false;
    public bool IsHandledByCustomDelivery = false;
    public float TemporaryCritModifier = 1.0f;
    public float TemporaryDamageModifier = 1.0f;
    public int KillCountThisAction = 0;

    // --- 3. 物理与特殊参数 (兼容旧积木) ---
    public int PiercingIndex = 0;
    public Vector2 StrikeDirection;
    public float ImpactVelocity;
    public float ImpactMass;
    public Vector2 ImpactNormal;
    public int Generation = 0;

    // 👇【核心新增】：支持奶弹等友军判定
    public bool HitAllies = false;

    // --- 4. 通讯字典 ---
    public Dictionary<string, float> CustomStates = new Dictionary<string, float>();
}

public abstract class ECAAction : ScriptableObject
{
    [Header("=== ECA 2.0 优先级 (小值先行) ===")]
    public int Priority = 200;

    public abstract void Execute(ECAContext context);

    [Header("=== 配件注入契约 (Injection Contract) ===")]
    public ComponentType AllowedComponentTypes = ComponentType.Weapon; // 只能装在武器上？
    public WeaponDeliveryType RequiredDelivery = WeaponDeliveryType.Ranged; // 必须是远程？
    public List<SubTag> RequiredTags = new List<SubTag>(); // 必须有[动能]标签？
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