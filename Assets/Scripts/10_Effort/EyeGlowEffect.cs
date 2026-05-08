using UnityEngine;
using UnityEngine.UI;

public class EyeGlowEffect : MonoBehaviour
{
    private Image glowImage;
    private float timer;

    void Awake() => glowImage = GetComponent<Image>();

    void Update()
    {
        // --- 方案 A：平滑呼吸感 ---
        // 使用正弦波，让透明度在 0.3 到 1.0 之间来回切换
        float alpha = 0.65f + Mathf.Sin(Time.time * 2f) * 0.35f;

        // --- 方案 B：偶尔的“电子故障”闪烁 (可选) ---
        if (Random.value > 0.98f) alpha = 1.0f;

        Color c = glowImage.color;
        c.a = alpha;
        glowImage.color = c;
    }
}