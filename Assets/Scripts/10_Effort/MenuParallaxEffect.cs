using UnityEngine;

public class MenuParallaxEffect : MonoBehaviour
{
    [Header("=== 偏移范围 (像素) ===")]
    [SerializeField] private float rangeX = 50f;
    [SerializeField] private float rangeY = 30f;

    [Header("=== 灵敏度 (缓动速度) ===")]
    [SerializeField] private float sensitivity = 5f;

    private RectTransform rectTransform;
    private Vector2 initialAnchoredPos;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        // 记录 UI 元素的初始锚点位置
        initialAnchoredPos = rectTransform.anchoredPosition;
    }

    private void Update()
    {
        // 1. 获取鼠标在屏幕上的百分比位置 (0到1)
        float mouseXPercent = Input.mousePosition.x / Screen.width;
        float mouseYPercent = Input.mousePosition.y / Screen.height;

        // 2. 转化为中心偏差 (-0.5 到 0.5)
        float offsetX = mouseXPercent - 0.5f;
        float offsetY = mouseYPercent - 0.5f;

        // 3. 计算目标位置 (注意：这里用了负号实现反向偏移)
        float targetX = initialAnchoredPos.x + (-offsetX * rangeX);
        float targetY = initialAnchoredPos.y + (-offsetY * rangeY);

        Vector2 targetPos = new Vector2(targetX, targetY);

        // 4. 使用 Vector2.Lerp 实现平滑移动
        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            targetPos,
            Time.deltaTime * sensitivity
        );
    }
}