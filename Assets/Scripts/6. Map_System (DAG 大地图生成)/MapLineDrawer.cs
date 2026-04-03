using UnityEngine;
using UnityEngine.UI;

public class MapLineDrawer : MonoBehaviour
{
    [Header("=== 连线配置 ===")]
    public GameObject DotPrefab;       // 一个白色小圆点的UI预制体
    public int DotsPerLine = 10;       // 一条线由多少个点组成
    public float CurveIntensity = 50f; // 曲线的弯曲程度

    private MapNodeData fromNode;
    private MapNodeData toNode;

    // 画线逻辑
    public void DrawCurve(MapNodeData from, MapNodeData to, Vector2 startPos, Vector2 endPos)
    {
        fromNode = from;
        toNode = to;
        gameObject.name = $"Line_{from.NodeID}_to_{to.NodeID}";

        // 计算控制点，制造出向上流动的 S 型曲线感
        Vector2 controlPointA = startPos + new Vector2(0, CurveIntensity);
        Vector2 controlPointB = endPos - new Vector2(0, CurveIntensity);

        for (int i = 1; i < DotsPerLine; i++)
        {
            float t = i / (float)DotsPerLine;
            Vector2 pointPos = CalculateCubicBezierPoint(t, startPos, controlPointA, controlPointB, endPos);

            // 生成虚线点点
            GameObject dotObj = Instantiate(DotPrefab, this.transform);
            RectTransform dotRect = dotObj.GetComponent<RectTransform>();

            dotRect.anchorMin = new Vector2(0.5f, 0f);
            dotRect.anchorMax = new Vector2(0.5f, 0f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = pointPos;
        }

        RefreshLineState();
    }

    // 根据节点状态，决定连线是否发光
    public void RefreshLineState()
    {
        bool isUnlocked = (fromNode.NodeState == MapNodeState.Passed && toNode.NodeState == MapNodeState.Selectable) ||
                          (fromNode.NodeState == MapNodeState.Passed && toNode.NodeState == MapNodeState.Passed);

        Color lineColor = isUnlocked ? new Color(0.2f, 0.8f, 1f, 1f) : new Color(0.3f, 0.3f, 0.3f, 0.5f);

        foreach (Transform child in transform)
        {
            child.GetComponent<Image>().color = lineColor;
        }
    }

    // 纯正的数学之美：三次贝塞尔曲线算法
    private Vector2 CalculateCubicBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector2 p = uuu * p0; // 第一项
        p += 3 * uu * t * p1; // 第二项
        p += 3 * u * tt * p2; // 第三项
        p += ttt * p3;        // 第四项

        return p;
    }
}