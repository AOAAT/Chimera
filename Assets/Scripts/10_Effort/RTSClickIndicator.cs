using UnityEngine;

public class RTSClickIndicator : MonoBehaviour
{
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

            // 2. 【防丢失】：如果你拿不到 Knob.psd，可以用 Unity 内置的白色圆形：
            // sr.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            // 如果上面一行不行，请用下面这行（Unity所有版本通用的圆点）：
            sr.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            sr.color = new Color(0, 1, 0, 0.9f); // 亮绿色
            sr.sortingLayerName = "UI";         // 强制显示在 UI 层
            sr.sortingOrder = 100;              // 极高层级
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