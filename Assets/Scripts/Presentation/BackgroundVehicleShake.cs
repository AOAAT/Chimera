using UnityEngine;

public class BackgroundVehicleShake : MonoBehaviour
{
    [Header("=== 晃动幅度 (米) ===")]
    [Tooltip("左右晃动的最大距离")]
    public float AmountX = 0.2f;
    [Tooltip("上下晃动的最大距离")]
    public float AmountY = 0.1f;

    [Header("=== 晃动频率 (速度) ===")]
    [Tooltip("数值越大晃得越快")]
    public float SpeedX = 0.5f;
    public float SpeedY = 0.7f;

    [Header("=== 机械抖动 (高频微颤) ===")]
    [Tooltip("模拟引擎运转的微小颤抖幅度")]
    public float JitterAmount = 0.02f;
    public float JitterSpeed = 20f;

    private Vector3 initialPosition;
    private float randomOffset;

    void Start()
    {
        // 记录初始位置，确保晃动不跑偏
        initialPosition = transform.localPosition;
        // 给一个随机偏移，防止多个背景层完全同步晃动
        randomOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        float t = Time.time + randomOffset;

        // 1. 低频大幅度摇晃 (模拟底盘颠簸)
        float offsetX = Mathf.Sin(t * SpeedX) * AmountX;
        float offsetY = Mathf.Cos(t * SpeedY) * AmountY;

        // 2. 高频微小抖动 (模拟引擎震传)
        float jitterX = (Mathf.PerlinNoise(t * JitterSpeed, 0) - 0.5f) * JitterAmount;
        float jitterY = (Mathf.PerlinNoise(0, t * JitterSpeed) - 0.5f) * JitterAmount;

        // 3. 应用位移
        transform.localPosition = initialPosition + new Vector3(offsetX + jitterX, offsetY + jitterY, 0);
    }
}