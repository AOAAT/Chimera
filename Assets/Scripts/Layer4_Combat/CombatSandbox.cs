using UnityEngine;

public class CombatSandbox : MonoBehaviour
{
    public static CombatSandbox Instance;

    [Header("=== 全局度量衡调节 (Metrics) ===")]
    [Tooltip("数据表里的 1 点射程，等于 Unity 里的多少米？")]
    public float DistanceMultiplier = 1.0f;

    [Tooltip("数据表里的 1 点动力，等于每秒移动多少米？")]
    public float SpeedMultiplier = 1.0f;

    private void Awake()
    {
        Instance = this; // 简易单例，方便全局调用比例尺
    }
}