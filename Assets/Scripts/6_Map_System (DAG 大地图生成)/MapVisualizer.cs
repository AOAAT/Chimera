using System.Collections; // 【新增】用于协程
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

    // 👇【新增 1】：定义一个映射结构体
    [System.Serializable]
    public struct NodeTypeIcon
    {
        public MapNodeType Type;  // 节点类型 (如 Enemy, Elite, Boss)
        public Sprite IconSprite; // 对应的贴图
    }

    [Header("=== 节点图标配置 (Icon Dictionary) ===")]
    public List<NodeTypeIcon> IconConfigs; // 暴露出给您拖拽贴图的列表

    // 👇【新增 2】：写一个小工具函数，根据类型找贴图
    public Sprite GetIconForType(MapNodeType type)
    {
        foreach (var config in IconConfigs)
        {
            if (config.Type == type) return config.IconSprite;
        }
        return null; // 如果没配置，就返回空
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

        foreach (Transform child in transform) Destroy(child.gameObject);
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

            // 👇【主程的终极绝杀】：不管预制体怎么设的，强行把坐标系原点按死在底部！
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f); // 节点自己的轴心保持中心不变

            float finalY = (data.LogicalPosition.y * Y_Spacing) + bottomPadding;
            rect.anchoredPosition = new Vector2(data.LogicalPosition.x * X_Spacing, finalY);

            nodeRects.Add(data.NodeID, rect);

            MapNodeUI uiScript = nodeObj.GetComponent<MapNodeUI>();
            Sprite myIcon = GetIconForType(data.NodeType);
            uiScript.Initialize(data, myIcon); // 多传一个参数
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

                // 👇【连线容器也强制锁死在底部！】
                lineRect.anchorMin = new Vector2(0.5f, 0f);
                lineRect.anchorMax = new Vector2(0.5f, 0f);
                lineRect.pivot = new Vector2(0.5f, 0f);
                lineRect.anchoredPosition = Vector2.zero;

                lineObj.transform.SetAsFirstSibling();

                MapLineDrawer lineDrawer = lineObj.GetComponent<MapLineDrawer>();
                lineDrawer.DrawCurve(fromData, toData, fromRect.anchoredPosition, toRect.anchoredPosition);
                spawnedLines.Add(lineDrawer);
            }
        }

        float maxH = (MapManager.Instance.GetComponent<MapGenerator>().TotalLayers - 1) * Y_Spacing;
        contentRect.sizeDelta = new Vector2(0, maxH + bottomPadding + topPadding);

        StartCoroutine(ScrollToBottom());
    }
    private IEnumerator ScrollToBottom()
    {
        // 极其关键：必须等待当前帧的所有 UI 布局重算完毕
        yield return new WaitForEndOfFrame();

        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            // 0 代表绝对底部，1 代表绝对顶部
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public void RefreshAllVisuals()
    {
        foreach (var node in spawnedNodes) node.RefreshVisualState();
        foreach (var line in spawnedLines) line.RefreshLineState();
    }
}