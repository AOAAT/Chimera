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

    private Transform visualTransform;
    private Rigidbody2D rb;
    private DamageReceiver receiver;

    private Vector3 originalScale;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;

    private float currentSquash = 0f;
    private float timeOffset;

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
    }

    private float lastHP = -1f;

    private void Update()
    {
        if (visualTransform == null) return;

        if (AutoSyncWithVelocity && rb != null)
            IsMoving = rb.velocity.sqrMagnitude > (WalkSpeedThreshold * WalkSpeedThreshold);

        if (receiver != null)
        {
            if (lastHP == -1f) lastHP = receiver.CurrentHP;
            if (receiver.CurrentHP < lastHP)
            {
                TriggerHitSquash();
                lastHP = receiver.CurrentHP;
            }
        }

        currentSquash = Mathf.Lerp(currentSquash, 0f, Time.deltaTime * SquashRecoverSpeed);

        Vector3 targetScale = originalScale;
        Vector3 targetPos = originalLocalPos;
        Quaternion targetRot = originalLocalRot;

        float t = Time.time + timeOffset;

        // 1. 移动摇摆
        if (IsMoving && EnableWobble)
        {
            float wobbleSin = Mathf.Sin(t * WobbleSpeed);
            float bobbingCos = Mathf.Abs(Mathf.Cos(t * WobbleSpeed * 0.5f));

            targetRot = originalLocalRot * Quaternion.Euler(0f, 0f, wobbleSin * WobbleAngle);
            targetPos = originalLocalPos + new Vector3(0f, bobbingCos * BobbingHeight, 0f);
        }
        // 2. 生物呼吸
        else if (!IsMoving && EnableBreathing)
        {
            float normalizedBreath = (Mathf.Sin(t * BreathSpeed) + 1f) / 2f;
            targetScale.y = originalScale.y * (1f + normalizedBreath * BreathScaleY);
            targetScale.x = originalScale.x * (1f + normalizedBreath * BreathScaleX);
        }

        // 3. 机械震动 (待机时柴油机疯狂颤抖！)
        if (!IsMoving && EnableVibration)
        {
            // 利用柏林噪声产生极高频、无规律的机械震颤
            float vibX = (Mathf.PerlinNoise(t * VibrationSpeed, 0f) - 0.5f) * 2f * VibrationIntensity;
            float vibY = (Mathf.PerlinNoise(0f, t * VibrationSpeed) - 0.5f) * 2f * VibrationIntensity;
            targetPos += new Vector3(vibX, vibY, 0f);
        }

        // 4. 受击形变
        targetScale.y += currentSquash;
        targetScale.x -= currentSquash * 0.5f;

        visualTransform.localScale = targetScale;
        visualTransform.localPosition = targetPos;
        visualTransform.localRotation = targetRot;
    }

    public void TriggerHitSquash() { currentSquash = SquashAmount; }

    public void StopAnimation()
    {
        this.enabled = false;
        if (visualTransform != null)
        {
            visualTransform.localScale = originalScale;
            visualTransform.localPosition = originalLocalPos;
            visualTransform.localRotation = originalLocalRot * Quaternion.Euler(0, 0, -90f);
        }
    }

    // 👇 新增方法：强制刷新基础状态
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