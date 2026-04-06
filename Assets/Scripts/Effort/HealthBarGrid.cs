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

    public void UpdateGrid(float maxValue)
    {
        if (ValuePerGrid <= 0) return;

        if (container == null)
        {
            container = GetComponent<RectTransform>();
        }

        // 1. 先把旧的线条全部打扫干净
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        // 2. 算出需要切几刀？ (比如 500血，每格100，需要 4 根线)
        int gridCount = Mathf.FloorToInt(maxValue / ValuePerGrid);
        if (gridCount <= 1) return; // 只有 1 格就不需要切线了

        int lineCount = gridCount - 1;
        if (lineCount > MaxLines) lineCount = MaxLines;

        // 3. 用纯代码生成完美的切割线！
        for (int i = 1; i <= lineCount; i++)
        {
            GameObject lineObj = new GameObject($"GridLine_{i}");
            lineObj.transform.SetParent(container, false);

            // 添加图片并上色
            Image img = lineObj.AddComponent<Image>();
            img.color = LineColor;
            img.raycastTarget = false; // 绝不阻挡鼠标点击

            // 设置绝对定位
            RectTransform rect = lineObj.GetComponent<RectTransform>();

            // 计算这根线在血条上的百分比位置 (比如 0.2, 0.4, 0.6)
            float percent = (float)i / gridCount;

            // 极其巧妙的锚点设置：让它横向钉死在百分比位置，纵向铺满整个血条！
            rect.anchorMin = new Vector2(percent, 0f);
            rect.anchorMax = new Vector2(percent, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // 宽度直接写死像素！高度写 0 意味着跟随锚点拉伸！
            rect.sizeDelta = new Vector2(LineThickness, 0f);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}