// --- START OF FILE ProceduralAnimator2D.cs ---
using UnityEngine;

public class ProceduralAnimator2D : MonoBehaviour
{
    [Header("=== 核心控制 ===")]
    public bool AutoSyncWithVelocity = true;
    public float WalkSpeedThreshold = 0.1f;
    public bool IsMoving = false;

    [Header("=== 1. 待机呼吸态 (生物用) ===")]
    public bool EnableBreathing = true;
    public float BreathSpeed = 2f;
    public float BreathScaleY = 0.05f;
    public float BreathScaleX = -0.02f;

    [Header("=== 2. 移动摇摆态 (生物/履带机甲通用) ===")]
    public bool EnableWobble = true;
    public float WobbleSpeed = 10f;
    public float WobbleAngle = 8f;
    public float BobbingHeight = 0.1f;

    [Header("=== 3. 机械震动态 (柴油机专属！) ===")]
    public bool EnableVibration = false;
    [Tooltip("震动的频率 (推荐 30~50)")]
    public float VibrationSpeed = 40f;
    [Tooltip("震动的幅度 (推荐 0.02~0.05)")]
    public float VibrationIntensity = 0.03f;

    [Header("=== 4. 受击反馈 (果冻效应) ===")]
    public float SquashAmount = -0.3f;
    public float SquashRecoverSpeed = 10f;

    [Header("=== 5. 损毁表现 (Juiciness) ===")]
    [Tooltip("血量低于 50% 时生成的烟雾/火花预制体")]
    public GameObject SmokePrefab;
    [Tooltip("血量低于 30% 时的额外震动增益")]
    public float PanicVibrationMultiplier = 2.0f;

    private GameObject activeSmoke;
    private float baseVibrationSpeed;
    private float baseVibrationIntensity;
    private bool baseEnableVibration;

    private Transform visualTransform;
    private Rigidbody2D rb;
    private DamageReceiver receiver;

    private Vector3 originalScale;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;

    private float currentSquash = 0f;
    private float timeOffset;
    private float lastHP = -1f;

    // 👇【新增】：允许外部代码精确指定到底要摇晃哪一块贴图
    public void SetTargetVisual(Transform target)
    {
        visualTransform = target;
        originalScale = visualTransform.localScale;
        originalLocalPos = visualTransform.localPosition;
        originalLocalRot = visualTransform.localRotation;
    }

    private void Awake()
    {
        timeOffset = Random.Range(0f, 100f);
        rb = GetComponent<Rigidbody2D>();
        receiver = GetComponent<DamageReceiver>();

        // 备份策划在 Inspector 填写的初始值
        baseVibrationSpeed = VibrationSpeed;
        baseVibrationIntensity = VibrationIntensity;
        baseEnableVibration = EnableVibration;
    }

    private void Update()
    {
        if (visualTransform == null) return;

        // 1. 状态同步：物理速度决定是否处于“移动态”
        if (AutoSyncWithVelocity && rb != null)
            IsMoving = rb.velocity.sqrMagnitude > (WalkSpeedThreshold * WalkSpeedThreshold);

        // 2. 状态同步：受击判定
        if (receiver != null)
        {
            if (lastHP == -1f) lastHP = receiver.CurrentHP;
            if (receiver.CurrentHP < lastHP)
            {
                TriggerHitSquash();
                lastHP = receiver.CurrentHP;
            }
            HandleDamageVisuals(); // 处理烟雾和濒死震动
        }

        // 3. 计算果冻形变恢复
        currentSquash = Mathf.Lerp(currentSquash, 0f, Time.deltaTime * SquashRecoverSpeed);

        Vector3 targetScale = originalScale;
        Vector3 targetPos = originalLocalPos;
        Quaternion targetRot = originalLocalRot;

        float t = Time.time + timeOffset;

        // --- 表现层计算 ---

        // A. 移动摇摆
        if (IsMoving && EnableWobble)
        {
            float wobbleSin = Mathf.Sin(t * WobbleSpeed);
            float bobbingCos = Mathf.Abs(Mathf.Cos(t * WobbleSpeed * 0.5f));

            targetRot = originalLocalRot * Quaternion.Euler(0f, 0f, wobbleSin * WobbleAngle);
            targetPos = originalLocalPos + new Vector3(0f, bobbingCos * BobbingHeight, 0f);
        }
        // B. 生物呼吸 (仅在不移动时触发)
        else if (!IsMoving && EnableBreathing)
        {
            float normalizedBreath = (Mathf.Sin(t * BreathSpeed) + 1f) / 2f;
            targetScale.y = originalScale.y * (1f + normalizedBreath * BreathScaleY);
            targetScale.x = originalScale.x * (1f + normalizedBreath * BreathScaleX);
        }

        // C. 机械震动 (核心损毁或待机时)
        if (EnableVibration)
        {
            // 利用柏林噪声产生不规则的高频机械感
            float vibX = (Mathf.PerlinNoise(t * VibrationSpeed, 0f) - 0.5f) * 2f * VibrationIntensity;
            float vibY = (Mathf.PerlinNoise(0f, t * VibrationSpeed) - 0.5f) * 2f * VibrationIntensity;
            targetPos += new Vector3(vibX, vibY, 0f);
        }

        // D. 应用受击形变
        targetScale.y += currentSquash;
        targetScale.x -= currentSquash * 0.5f;

        // 最终应用到变换组件
        visualTransform.localScale = targetScale;
        visualTransform.localPosition = targetPos;
        visualTransform.localRotation = targetRot;
    }

    private void HandleDamageVisuals()
    {
        float hpPercent = receiver.CurrentHP / receiver.MaxHP;

        // --- 烟雾逻辑 (低于 50%) ---
        if (hpPercent < 0.5f)
        {
            if (activeSmoke == null && SmokePrefab != null)
            {
                activeSmoke = Instantiate(SmokePrefab, transform.position, Quaternion.identity, transform);
                activeSmoke.name = "FX_DamageSmoke";
            }
        }
        else
        {
            if (activeSmoke != null) Destroy(activeSmoke);
        }

        // --- 濒死震动逻辑 (低于 30%) ---
        if (hpPercent < 0.3f)
        {
            EnableVibration = true;
            VibrationSpeed = baseVibrationSpeed * PanicVibrationMultiplier;
            VibrationIntensity = baseVibrationIntensity * PanicVibrationMultiplier;
        }
        else
        {
            // 恢复策划设定的初始震动状态
            EnableVibration = baseEnableVibration;
            VibrationSpeed = baseVibrationSpeed;
            VibrationIntensity = baseVibrationIntensity;
        }
    }

    public void TriggerHitSquash() { currentSquash = SquashAmount; }

    public void StopAnimation()
    {
        this.enabled = false;
        if (activeSmoke != null) Destroy(activeSmoke);
        if (visualTransform != null)
        {
            visualTransform.localScale = originalScale;
            visualTransform.localPosition = originalLocalPos;
            visualTransform.localRotation = originalLocalRot * Quaternion.Euler(0, 0, -90f);
        }
    }

    public void RefreshBaseState()
    {
        if (visualTransform != null)
        {
            originalScale = visualTransform.localScale;
            originalLocalPos = visualTransform.localPosition;
            originalLocalRot = visualTransform.localRotation;
        }
    }
}