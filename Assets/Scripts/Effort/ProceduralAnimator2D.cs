// --- START OF FILE ProceduralAnimator2D.cs ---
using UnityEngine;

public class ProceduralAnimator2D : MonoBehaviour
{
    [Header("=== 核心控制 ===")]
    [Tooltip("勾选后，动画会根据 Rigidbody2D 的速度自动切换状态！")]
    public bool AutoSyncWithVelocity = true;
    [Tooltip("速度大于此值时，切换为【移动态】；否则为【呼吸态】")]
    public float WalkSpeedThreshold = 0.1f;

    [Header("=== 状态监视 (运行时自动切换) ===")]
    public bool IsMoving = false;

    [Header("=== 1. 待机呼吸态 (Idle/Breathing) ===")]
    public bool EnableBreathing = true;
    [Tooltip("呼吸的快慢 (频率)")]
    public float BreathSpeed = 2f;
    [Tooltip("呼吸时，Y轴放大的幅度")]
    public float BreathScaleY = 0.05f;
    [Tooltip("呼吸时，X轴略微收缩的幅度 (更有肉感)")]
    public float BreathScaleX = -0.02f;

    [Header("=== 2. 移动摇摆态 (Walking/Wobbling) ===")]
    public bool EnableWobble = true;
    [Tooltip("摇摆的快慢 (步频)")]
    public float WobbleSpeed = 10f;
    [Tooltip("左右摇晃的角度")]
    public float WobbleAngle = 8f;
    [Tooltip("走路时上下颠簸的幅度")]
    public float BobbingHeight = 0.1f;

    [Header("=== 3. 受击反馈 (Hit Squash & Stretch) ===")]
    [Tooltip("挨打时，瞬间被压扁的程度")]
    public float SquashAmount = -0.3f;
    [Tooltip("挨打后，恢复原状的速度")]
    public float SquashRecoverSpeed = 10f;

    // 内部缓存
    private Transform visualTransform; // 必须作用于贴图子节点，不能摇晃带有物理碰撞的根节点！
    private Rigidbody2D rb;
    private DamageReceiver receiver;

    private Vector3 originalScale;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;

    private float currentSquash = 0f;
    private float timeOffset; // 防止所有怪物的呼吸频率绝对同步，看起来像在跳团体操

    private void Awake()
    {
        // 1. 尝试寻找视觉子节点 (我们在 EnemyBrain 和 MechUnit2D 里都做过这种层级分离)
        // 极其重要：程序化动画绝对不能动带有 BoxCollider2D 和 Rigidbody2D 的根节点，否则物理引擎会崩溃！
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.transform != this.transform)
        {
            visualTransform = sr.transform;
        }
        else
        {
            // 兜底：如果没找到子节点，就只能动自己了 (不推荐用于带物理的单位)
            visualTransform = this.transform;
        }

        // 缓存初始状态
        originalScale = visualTransform.localScale;
        originalLocalPos = visualTransform.localPosition;
        originalLocalRot = visualTransform.localRotation;

        // 随机一个时间偏移，让每个怪物的呼吸错开
        timeOffset = Random.Range(0f, 100f);

        // 尝试获取物理和受伤组件
        rb = GetComponent<Rigidbody2D>();
        receiver = GetComponent<DamageReceiver>();

        // 👇 订阅挨打事件，触发“果冻效应 (Squash & Stretch)”
        if (receiver != null)
        {
            // 这里我们需要 DamageReceiver 里有一个事件。
            // 之前我们在 DamageReceiver 里写了 OnStatsChanged，可以用它来触发！
            // 但为了更精准，建议你在 DamageReceiver.TakeDamage 里加一行 OnTakeDamage?.Invoke();
            // 这里我们用一个比较 Hack 的方式：如果血量少了，就触发！
        }
    }

    private float lastHP = -1f;

    private void Update()
    {
        if (visualTransform == null) return;

        // --- 1. 自动状态判定 ---
        if (AutoSyncWithVelocity && rb != null)
        {
            IsMoving = rb.velocity.sqrMagnitude > (WalkSpeedThreshold * WalkSpeedThreshold);
        }

        // --- 2. 检测挨打 (触发果冻形变) ---
        if (receiver != null)
        {
            if (lastHP == -1f) lastHP = receiver.CurrentHP;
            if (receiver.CurrentHP < lastHP)
            {
                TriggerHitSquash();
                lastHP = receiver.CurrentHP;
            }
        }

        // 恢复形变
        currentSquash = Mathf.Lerp(currentSquash, 0f, Time.deltaTime * SquashRecoverSpeed);

        // --- 3. 动画计算 ---
        Vector3 targetScale = originalScale;
        Vector3 targetPos = originalLocalPos;
        Quaternion targetRot = originalLocalRot;

        float t = Time.time + timeOffset;

        if (IsMoving && EnableWobble)
        {
            // 移动态：左右摇摆 (Rotation) + 上下颠簸 (Position Y)
            float wobbleSin = Mathf.Sin(t * WobbleSpeed);
            float bobbingCos = Mathf.Abs(Mathf.Cos(t * WobbleSpeed * 0.5f)); // 取绝对值，只向上颠

            targetRot = originalLocalRot * Quaternion.Euler(0f, 0f, wobbleSin * WobbleAngle);
            targetPos = originalLocalPos + new Vector3(0f, bobbingCos * BobbingHeight, 0f);
        }
        else if (!IsMoving && EnableBreathing)
        {
            // 待机态：深呼吸缩放 (Scale X & Y 反向变化，保持体积感)
            float breathSin = Mathf.Sin(t * BreathSpeed);

            // 让正弦波变成 0~1 的范围，方便计算
            float normalizedBreath = (breathSin + 1f) / 2f;

            targetScale.y = originalScale.y * (1f + normalizedBreath * BreathScaleY);
            targetScale.x = originalScale.x * (1f + normalizedBreath * BreathScaleX);
        }

        // --- 4. 叠加受击的果冻形变 (Squash & Stretch) ---
        // 挨打时，Y轴被压扁 (变矮)，X轴被挤宽 (变胖)，极其带感！
        targetScale.y += currentSquash;
        targetScale.x -= currentSquash * 0.5f;

        // --- 5. 应用最终变换 ---
        visualTransform.localScale = targetScale;
        visualTransform.localPosition = targetPos;
        visualTransform.localRotation = targetRot;
    }

    public void TriggerHitSquash()
    {
        currentSquash = SquashAmount; // 瞬间施加一个压扁的数值
    }

    // 死亡时调用，彻底关闭动画，防止鞭尸时还在呼吸
    public void StopAnimation()
    {
        this.enabled = false;
        if (visualTransform != null)
        {
            // 恢复原状
            visualTransform.localScale = originalScale;
            visualTransform.localPosition = originalLocalPos;
            // 死亡倒地效果：Z轴转90度
            visualTransform.localRotation = originalLocalRot * Quaternion.Euler(0, 0, -90f);
        }
    }
}