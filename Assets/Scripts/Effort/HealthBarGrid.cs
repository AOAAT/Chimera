// --- START OF FILE HealthBarGrid.cs ---
using UnityEngine;
using UnityEngine.UI;

public class HealthBarGrid : MonoBehaviour
{
    [Header("=== 格栅配置 ===")]
    [Tooltip("每个格子代表多少血量/护甲？")]
    public float ValuePerGrid = 100f;

    [Tooltip("黑线的绝对厚度 (像素)！设为 2 就是极细的完美黑线！")]
    public float LineThickness = 2f;

    public Color LineColor = new Color(0f, 0f, 0f, 0.8f); // 黑色，带一点点透明度更自然

    [Tooltip("防止血量一万时生成太多线条卡死，设个上限")]
    public int MaxLines = 50;

    private RectTransform container;

    // --- 请替换 HealthBarGrid.cs 中的 UpdateGrid 方法 ---

    // --- 替换 HealthBarGrid.cs 中的 UpdateGrid 方法 ---

    private float lastInitedMaxValue = -1f; // 缓存记录上次绘制的上限

    public void UpdateGrid(float maxValue)
    {
        if (ValuePerGrid <= 0) return;

        // 【核心节流】：如果血量上限没变，说明格子不需要重绘，直接返回
        if (Mathf.Approximately(maxValue, lastInitedMaxValue)) return;

        lastInitedMaxValue = maxValue; // 更新缓存

        if (container == null) container = GetComponent<RectTransform>();

        // 暴力打扫黑线
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (child.name.StartsWith("GridLine")) Destroy(child.gameObject);
        }

        int gridCount = Mathf.FloorToInt(maxValue / ValuePerGrid);
        if (gridCount <= 1) return;

        int lineCount = Mathf.Min(gridCount - 1, MaxLines);

        for (int i = 1; i <= lineCount; i++)
        {
            GameObject lineObj = new GameObject($"GridLine_{i}");
            lineObj.transform.SetParent(container, false);
            Image img = lineObj.AddComponent<Image>();
            img.color = LineColor;
            img.raycastTarget = false;

            RectTransform rect = lineObj.GetComponent<RectTransform>();
            float percent = (float)i / gridCount;
            rect.anchorMin = new Vector2(percent, 0f);
            rect.anchorMax = new Vector2(percent, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(LineThickness, 0f);
            rect.anchoredPosition = Vector2.zero;
            lineObj.transform.SetAsLastSibling();
        }
    }
}