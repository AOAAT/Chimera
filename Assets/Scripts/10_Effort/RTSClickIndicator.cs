// --- RTSClickIndicator.cs (打包安全加固版) ---
using UnityEngine;

public class RTSClickIndicator : MonoBehaviour
{
    [Header("=== 视觉配置 ===")]
    [Tooltip("请在 Inspector 面板中拖入你想要显示的箭头/圆点贴图")]
    public Sprite ArrowSprite;

    public float Duration = 0.5f;

    private float timer = 0f;
    private SpriteRenderer[] parts;

    void Start()
    {
        // 1. 【防遮挡】：让它稍微靠近摄像机一点，防止被地板遮住
        transform.position = new Vector3(transform.position.x, transform.position.y, -1f);

        parts = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject go = new GameObject("ArrowPart");
            go.transform.SetParent(this.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();

            // 2. 👇【核心修复】：使用面板拖入的资源，彻底移除 UnityEditor 依赖
            if (ArrowSprite != null)
            {
                sr.sprite = ArrowSprite;
            }
            else
            {
                // 如果策划忘了拖图，我们给一个默认的 Log 提示
                Debug.LogWarning("【系统提示】RTSClickIndicator 缺少贴图引用！请在预制体面板拖入贴图。");
            }

            sr.color = new Color(0, 1, 0, 0.9f); // 亮绿色
            sr.sortingLayerName = "UI";
            sr.sortingOrder = 100;
            parts[i] = sr;

            float angle = i * 90 * Mathf.Deg2Rad;
            go.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.6f;
            go.transform.localScale = Vector3.one * 0.25f;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        float p = timer / Duration;
        if (p >= 1f) { Destroy(gameObject); return; }

        foreach (var sr in parts)
        {
            // 动画：向中心靠拢
            sr.transform.localPosition = Vector3.Lerp(sr.transform.localPosition, Vector3.zero, p);
            // 渐隐
            Color c = sr.color; c.a = 1 - p; sr.color = c;
        }
    }
}