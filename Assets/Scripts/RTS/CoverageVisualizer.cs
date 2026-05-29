using System.Collections.Generic;
using UnityEngine;

public class CoverageVisualizer : MonoBehaviour
{
    public static CoverageVisualizer Instance;

    [Header("=== 视觉配置 ===")]
    public Sprite CircleSprite; // 拖入一个白色的圆环 Sprite
    public Color AreaColor = new Color(0, 0.6f, 1f, 0.15f); // 半透明天蓝色
    public float FadeSpeed = 5.0f;

    private List<SpriteRenderer> circlePool = new List<SpriteRenderer>();
    private CanvasGroup globalAlphaControl; // 如果需要整体淡入淡出
    private bool shouldShow = false;
    private float currentAlpha = 0f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ZoneCoverageManager.Instance.OnProvidersChanged += RefreshCircles;
        RefreshCircles();
    }

    // 🌟 控制显隐的接口
    public void SetVisible(bool visible) => shouldShow = visible;

    private void Update()
    {
        // 平滑淡入淡出效果
        currentAlpha = Mathf.MoveTowards(currentAlpha, shouldShow ? 1.0f : 0f, Time.deltaTime * FadeSpeed);

        foreach (var sr in circlePool)
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.a = AreaColor.a * currentAlpha;
            sr.color = c;
        }
    }

    public void RefreshCircles()
    {
        var providers = ZoneCoverageManager.Instance.GetAllProviders();

        // 1. 确保池子大小足够
        while (circlePool.Count < providers.Count)
        {
            GameObject go = new GameObject("CoverageCircle_FX");
            go.transform.SetParent(this.transform);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSprite;
            sr.sortingLayerName = "Floor"; // 🌟 放在地板层
            sr.sortingOrder = 5;
            circlePool.Add(sr);
        }

        // 2. 同步位置和大小
        for (int i = 0; i < circlePool.Count; i++)
        {
            if (i < providers.Count)
            {
                circlePool[i].gameObject.SetActive(true);
                circlePool[i].transform.position = providers[i].GetPosition() + Vector3.forward * 0.5f;
                // Sprite 默认直径是 1个单位，所以缩放 = 半径 * 2
                float diameter = providers[i].GetRadius() * 2f;
                circlePool[i].transform.localScale = new Vector3(diameter, diameter, 1f);
                circlePool[i].color = AreaColor;
            }
            else
            {
                circlePool[i].gameObject.SetActive(false);
            }
        }
    }
}