using UnityEngine;

[ExecuteInEditMode]
public class CameraResolutionFitter : MonoBehaviour
{
    [Header("=== 目标追踪 ===")]
    [Tooltip("将场景里的战场地板(Arena)拖到这里，脚本会自动计算它的尺寸")]
    public Transform ArenaTransform;

    [Header("=== 视觉比例控制 ===")]
    [Range(0.1f, 1.0f)]
    [Tooltip("战场垂直方向占据屏幕的百分比。建议 0.85 (即上下留一点空隙)")]
    public float ScreenHeightUsage = 0.85f;

    private Camera cam;

    void Awake() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (cam == null || ArenaTransform == null) return;

        // 1. 获取战场真实的物理尺寸（基于它的碰撞盒）
        BoxCollider2D col = ArenaTransform.GetComponent<BoxCollider2D>();
        if (col == null) return;

        float worldHeight = col.size.y * ArenaTransform.lossyScale.y;
        float worldWidth = col.size.x * ArenaTransform.lossyScale.x;

        // 2. 计算理想的正交大小 (Orthographic Size)
        // 核心公式：正交大小 = (世界高度 / 2) / 屏幕占比
        float targetSize = (worldHeight / 2f) / ScreenHeightUsage;

        // 3. 屏幕横向安全检测（防止在手机窄屏或 iPad 下战场两边被切掉）
        float screenAspect = (float)Screen.width / Screen.height;
        float arenaAspect = worldWidth / worldHeight;

        if (screenAspect < arenaAspect)
        {
            // 如果屏幕太窄，以宽度为基准进行计算，确保宽度 100% 完整显示
            float horizontalFittingSize = (worldWidth / 2f / screenAspect) / ScreenHeightUsage;
            targetSize = Mathf.Max(targetSize, horizontalFittingSize);
        }

        // 4. 应用计算结果
        cam.orthographicSize = targetSize;

    }
}