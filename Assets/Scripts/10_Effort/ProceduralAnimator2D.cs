// --- 替换 ProceduralAnimator2D.cs 全量代码 ---
using UnityEngine;

public class ProceduralAnimator2D : MonoBehaviour
{
    private Transform visualTransform;
    private Rigidbody2D rb;
    private DamageReceiver receiver;
    private SpriteRenderer[] cachedRenderers; // 【新增】缓存渲染器数组

    [Header("=== 核心控制 ===")]
    public bool AutoSyncWithVelocity = true;
    public float WalkSpeedThreshold = 0.1f;
    public bool IsMoving = false;

    [Header("=== 1. 待机呼吸态 (生物用) ===")]
    public bool EnableBreathing = true;
    public float BreathSpeed = 2f;
    public float BreathScaleY = 0.05f;
    public float BreathScaleX = -0.02f;

    [Header("=== 2. 移动摇摆态 (通用) ===")]
    public bool EnableWobble = true;
    public float WobbleSpeed = 10f;
    public float WobbleAngle = 8f;
    public float BobbingHeight = 0.1f;

    [Header("=== 3. 机械震动态 (柴油机) ===")]
    public bool EnableVibration = false;
    public float VibrationSpeed = 40f;
    public float VibrationIntensity = 0.03f;

    [Header("=== 4. 受击反馈 (果冻效应) ===")]
    public float SquashAmount = -0.3f;
    public float SquashRecoverSpeed = 10f;

    [Header("=== 5. 损毁表现 ===")]
    public GameObject SmokePrefab;
    public float PanicVibrationMultiplier = 2.0f;

    private GameObject activeSmoke;
    private float baseVibrationSpeed, baseVibrationIntensity;
    private bool baseEnableVibration;
    private Vector3 originalScale, originalLocalPos;
    private Quaternion originalLocalRot;
    private float currentSquash = 0f;
    private float timeOffset;
    private float lastHP = -1f;

    public void SetTargetVisual(Transform target)
    {
        visualTransform = target;
        // 【优化】：设置目标时立即缓存所有渲染器，避免后续重复搜寻
        cachedRenderers = visualTransform.GetComponentsInChildren<SpriteRenderer>();
        RefreshBaseState();
    }

    private void Awake()
    {
        timeOffset = Random.Range(0f, 100f);
        rb = GetComponent<Rigidbody2D>();
        receiver = GetComponent<DamageReceiver>();
        baseVibrationSpeed = VibrationSpeed;
        baseVibrationIntensity = VibrationIntensity;
        baseEnableVibration = EnableVibration;
    }

    private void Update()
    {
        if (visualTransform == null) return;

        // 状态同步
        if (AutoSyncWithVelocity && rb != null)
            IsMoving = rb.velocity.sqrMagnitude > (WalkSpeedThreshold * WalkSpeedThreshold);

        if (receiver != null)
        {
            if (lastHP == -1f) lastHP = receiver.CurrentHP;
            if (receiver.CurrentHP < lastHP)
            {
                currentSquash = SquashAmount;
                lastHP = receiver.CurrentHP;
            }
            HandleDamageVisualsOptimized();
        }

        // 形变恢复
        currentSquash = Mathf.Lerp(currentSquash, 0f, Time.deltaTime * SquashRecoverSpeed);

        float t = Time.time + timeOffset;
        Vector3 targetScale = originalScale;
        Vector3 targetPos = originalLocalPos;
        Quaternion targetRot = originalLocalRot;

        if (IsMoving && EnableWobble)
        {
            targetRot = originalLocalRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * WobbleSpeed) * WobbleAngle);
            targetPos = originalLocalPos + new Vector3(0f, Mathf.Abs(Mathf.Cos(t * WobbleSpeed * 0.5f)) * BobbingHeight, 0f);
        }
        else if (!IsMoving && EnableBreathing)
        {
            float normalizedBreath = (Mathf.Sin(t * BreathSpeed) + 1f) / 2f;
            targetScale.y *= (1f + normalizedBreath * BreathScaleY);
            targetScale.x *= (1f + normalizedBreath * BreathScaleX);
        }

        if (EnableVibration)
        {
            targetPos += new Vector3((Mathf.PerlinNoise(t * VibrationSpeed, 0f) - 0.5f) * 2f * VibrationIntensity,
                                     (Mathf.PerlinNoise(0f, t * VibrationSpeed) - 0.5f) * 2f * VibrationIntensity, 0f);
        }

        targetScale.y += currentSquash;
        targetScale.x -= currentSquash * 0.5f;

        visualTransform.localScale = targetScale;
        visualTransform.localPosition = targetPos;
        visualTransform.localRotation = targetRot;
    }

    private void HandleDamageVisualsOptimized()
    {
        float hpPercent = receiver.CurrentHP / receiver.MaxHP;

        // 烟雾逻辑
        if (hpPercent < 0.5f)
        {
            if (activeSmoke == null && SmokePrefab != null)
            {
                activeSmoke = Instantiate(SmokePrefab, transform.position, Quaternion.identity, transform);
            }
        }
        else if (activeSmoke != null) { Destroy(activeSmoke); }

        // 濒死震动逻辑
        if (hpPercent < 0.3f)
        {
            EnableVibration = true;
            VibrationSpeed = baseVibrationSpeed * PanicVibrationMultiplier;
            VibrationIntensity = baseVibrationIntensity * PanicVibrationMultiplier;
        }
        else
        {
            EnableVibration = baseEnableVibration;
            VibrationSpeed = baseVibrationSpeed;
            VibrationIntensity = baseVibrationIntensity;
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