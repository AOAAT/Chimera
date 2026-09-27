using UnityEngine;

// --- 修改 CombatSandbox.cs ---
public class CombatSandbox : MonoBehaviour
{
    public static CombatSandbox Instance;
    public float DistanceMultiplier = 1.0f;
    public float SpeedMultiplier = 1.0f;

    private void Awake() { Instance = this; }

    // 👇【核心新增】：一行代码获取真实距离
    public static float GetDist(float rawDist)
    {
        return rawDist * (Instance != null ? Instance.DistanceMultiplier : 1f);
    }

    // 👇【核心新增】：一行代码获取真实速度
    public static float GetSpeed(float rawSpeed)
    {
        return rawSpeed * (Instance != null ? Instance.SpeedMultiplier : 1f);
    }
}