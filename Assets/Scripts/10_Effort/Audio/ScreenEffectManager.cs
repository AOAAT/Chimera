// --- START OF FILE ScreenEffectManager.cs ---
using UnityEngine;
using UnityEngine.UI;

public class ScreenEffectManager : MonoBehaviour
{
    public static ScreenEffectManager Instance;

    [Header("=== 震动参数 (Shake) ===")]
    public Transform CameraTransform; // 必须挂载一个纯粹的摄像机容器
    private float shakeTimer = 0f;
    private float currentShakeIntensity = 0f;
    private Vector3 originalCamPos;

    [Header("=== 闪烁参数 (Flash) ===")]
    public Image FullscreenFlashImage; // 屏幕UI层的一个全屏纯色Image
    private float flashTimer = 0f;
    private float flashDuration = 0f;
    private Color flashColor;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (CameraTransform != null) originalCamPos = CameraTransform.localPosition;
    }

    public void TriggerShake(float intensity, float duration)
    {
        currentShakeIntensity = intensity;
        shakeTimer = duration;
    }

    public void TriggerFlash(Color color, float duration)
    {
        if (FullscreenFlashImage == null) return;
        flashColor = color;
        flashDuration = duration;
        flashTimer = duration;
        FullscreenFlashImage.gameObject.SetActive(true);
    }

    private void Update()
    {
        // --- 1. 处理震动 ---
        if (shakeTimer > 0 && CameraTransform != null)
        {
            // Perlin Noise 制作极其顺滑的高级震动感，拒绝生硬的 Random.insideUnitSphere
            float x = (Mathf.PerlinNoise(Time.time * 50f, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, Time.time * 50f) - 0.5f) * 2f;

            CameraTransform.localPosition = originalCamPos + new Vector3(x, y, 0) * currentShakeIntensity;

            shakeTimer -= Time.deltaTime;
            currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, 0f, Time.deltaTime * 5f); // 震动逐渐衰减
        }
        else if (CameraTransform != null && CameraTransform.localPosition != originalCamPos)
        {
            // 震动结束，瞬间复位
            CameraTransform.localPosition = originalCamPos;
        }

        // --- 2. 处理闪烁 ---
        if (flashTimer > 0 && FullscreenFlashImage != null)
        {
            flashTimer -= Time.deltaTime;
            float alpha = flashTimer / flashDuration; // 1 -> 0 渐变

            Color tempColor = flashColor;
            tempColor.a = alpha * flashColor.a; // 保留配置的最大透明度
            FullscreenFlashImage.color = tempColor;

            if (flashTimer <= 0) FullscreenFlashImage.gameObject.SetActive(false);
        }
    }
}