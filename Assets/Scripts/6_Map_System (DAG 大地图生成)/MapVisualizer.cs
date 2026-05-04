// --- START OF FILE MapVisualizer.cs ---
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapVisualizer : MonoBehaviour
{
    [Header("=== 预制体引用 ===")]
    public GameObject NodeUIPrefab;
    public GameObject LineDrawerPrefab;

    [Header("=== 排版缩放比例 ===")]
    public float X_Spacing = 150f;
    public float Y_Spacing = 200f;

    // 👇【新增 1】：关联背景图的引用
    [Header("=== 动态背景 ===")]
    [Tooltip("将地图容器下的背景 Image 拖拽到这里，防止生成时被误删！")]
    public RectTransform MapBackground;

    [System.Serializable]
    public struct NodeTypeIcon
    {
        public MapNodeType Type;
        public Sprite IconSprite;
    }

    [Header("=== 节点图标配置 (Icon Dictionary) ===")]
    public List<NodeTypeIcon> IconConfigs;

    public Sprite GetIconForType(MapNodeType type)
    {
        foreach (var config in IconConfigs)
        {
            if (config.Type == type) return config.IconSprite;
        }

        // 如果找不到，尝试返回 Start 节点的图标作为保底，防止崩溃
        Debug.LogWarning($"【地图视觉】未在 IconConfigs 中找到类型 [{type}] 的图标配置！请检查 Inspector 面板。");
        return IconConfigs.Count > 0 ? IconConfigs[0].IconSprite : null;
    }
    private List<MapNodeUI> spawnedNodes = new List<MapNodeUI>();
    private List<MapLineDrawer> spawnedLines = new List<MapLineDrawer>();

    private void Start()
    {
        Invoke(nameof(BuildMapVisuals), 0.1f);
    }

    public void BuildMapVisuals()
    {
        RectTransform contentRect = GetComponent<RectTransform>();
        contentRect.pivot = new Vector2(0.5f, 0f);
        contentRect.anchorMin = new Vector2(0.5f, 0f);
        contentRect.anchorMax = new Vector2(0.5f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        float bottomPadding = 200f;
        float topPadding = 300f;

        // 👇【核心修复】：打扫战场时，保护背景图不被销毁！
        foreach (Transform child in transform)
        {
            if (MapBackground != null && child == MapBackground) continue;
            Destroy(child.gameObject);
        }

        spawnedNodes.Clear();
        spawnedLines.Clear();

        var mapData = MapManager.Instance.GetComponent<MapGenerator>().GeneratedMap;
        Dictionary<string, RectTransform> nodeRects = new Dictionary<string, RectTransform>();

        // --- 摆放棋子 ---
        foreach (var kvp in mapData)
        {
            MapNodeData data = kvp.Value;
            GameObject nodeObj = Instantiate(NodeUIPrefab, this.transform);
            RectTransform rect = nodeObj.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            float finalY = (data.LogicalPosition.y * Y_Spacing) + bottomPadding;
            rect.anchoredPosition = new Vector2(data.LogicalPosition.x * X_Spacing, finalY);

            nodeRects.Add(data.NodeID, rect);

            MapNodeUI uiScript = nodeObj.GetComponent<MapNodeUI>();
            Sprite myIcon = GetIconForType(data.NodeType);
            uiScript.Initialize(data, myIcon);
            spawnedNodes.Add(uiScript);
        }

        // --- 绘制贝塞尔曲线 ---
        foreach (var kvp in mapData)
        {
            MapNodeData fromData = kvp.Value;
            RectTransform fromRect = nodeRects[fromData.NodeID];

            foreach (string nextID in fromData.NextNodeIDs)
            {
                MapNodeData toData = mapData[nextID];
                RectTransform toRect = nodeRects[nextID];

                GameObject lineObj = Instantiate(LineDrawerPrefab, this.transform);
                RectTransform lineRect = lineObj.GetComponent<RectTransform>();

                lineRect.anchorMin = new Vector2(0.5f, 0f);
                lineRect.anchorMax = new Vector2(0.5f, 0f);
                lineRect.pivot = new Vector2(0.5f, 0f);
                lineRect.anchoredPosition = Vector2.zero;

                // 连线不垫底了，因为背景要垫底
                // lineObj.transform.SetAsFirstSibling(); 

                MapLineDrawer lineDrawer = lineObj.GetComponent<MapLineDrawer>();
                lineDrawer.DrawCurve(fromData, toData, fromRect.anchoredPosition, toRect.anchoredPosition);
                spawnedLines.Add(lineDrawer);
            }
        }
        float maxH = (MapManager.Instance.GetComponent<MapGenerator>().TotalLayers - 1) * Y_Spacing;
        float totalHeight = maxH + bottomPadding + topPadding;

        // 设置 Content 容器的高度
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, totalHeight);

        // 👇【为《杀戮尖塔》手绘羊皮卷量身定制】：完美等比例缩放！
        if (MapBackground != null)
        {
            MapBackground.SetAsFirstSibling(); // 保证背景永远在最底层垫底

            Image bgImg = MapBackground.GetComponent<Image>();
            if (bgImg != null && bgImg.sprite != null)
            {
                // 1. 获取美术手绘图的原始长宽比 (宽 / 高)
                float aspect = bgImg.sprite.rect.width / bgImg.sprite.rect.height;

                // 2. 以代码算出来的地图总高度为基准，反推应该有多宽，保证画面绝不拉伸变形！
                float targetWidth = totalHeight * aspect;

                // 3. 完美锚定：将锚点和轴心全部钉死在底部中心
                MapBackground.anchorMin = new Vector2(0.5f, 0f);
                MapBackground.anchorMax = new Vector2(0.5f, 0f);
                MapBackground.pivot = new Vector2(0.5f, 0f);

                // 4. 赋予精准的物理尺寸
                MapBackground.sizeDelta = new Vector2(targetWidth, totalHeight);
                MapBackground.anchoredPosition = Vector2.zero;
            }
        }

        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public void RefreshAllVisuals()
    {
        foreach (var node in spawnedNodes) node.RefreshVisualState();
        foreach (var line in spawnedLines) line.RefreshLineState();
    }
}