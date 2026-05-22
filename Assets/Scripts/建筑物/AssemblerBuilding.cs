using System.Collections.Generic;
using UnityEngine;

public class AssemblerBuilding : BuildingBase
{
    [Header("=== 集合点配置 ===")]
    public Vector3 RallyWorldPos;

    [Header("=== 虚线参数 ===")]
    public float DotSpacing = 0.5f; // 点与点之间的距离
    public float ScrollSpeed = 2.0f; // 虚线滑动的速度
    public float DotSize = 0.12f;   // 点的大小

    private List<Transform> dotPool = new List<Transform>();
    private GameObject dotContainer;
    private Material internalMaterial;

    protected override void Awake()
    {
        base.Awake();
        // 把设置 RallyWorldPos 的那行代码从这里删掉！挪到下面的 Start 里。

        // 1. 程序化创建一个简单的材质
        internalMaterial = new Material(Shader.Find("Sprites/Default"));
        internalMaterial.color = new Color(0, 1, 1, 0.8f);

        // 2. 创建容器
        dotContainer = new GameObject("RallyDotContainer");
        dotContainer.transform.SetParent(this.transform);
        dotContainer.SetActive(false);
    }
    private void Start()
    {
        // 此时 RTSGridSystem 肯定已经初始化完成了
        RallyWorldPos = GetSpawnWorldPos();
    }
    private void Update()
    {
        if (isSelected)
        {
            UpdateProceduralRallyLine();
        }
    }

    /// <summary>
    /// 全程序化生成虚线：通过在路径上分布小点实现
    /// </summary>
    private void UpdateProceduralRallyLine()
    {
        Vector3 start = GetSpawnWorldPos();
        Vector3 end = RallyWorldPos;
        float totalDist = Vector3.Distance(start, end);

        // 如果距离太近，隐藏所有点
        if (totalDist < 0.2f)
        {
            dotContainer.SetActive(false);
            return;
        }

        dotContainer.SetActive(true);

        // 计算当前帧需要的点的数量
        int neededDots = Mathf.CeilToInt(totalDist / DotSpacing);

        // 动态调整对象池大小
        AdjustDotPool(neededDots);

        // 让虚线动起来：计算一个随时间变化的偏移量
        float timeOffset = (Time.time * ScrollSpeed) % DotSpacing;

        Vector3 dir = (end - start).normalized;

        for (int i = 0; i < dotPool.Count; i++)
        {
            if (i >= neededDots)
            {
                dotPool[i].gameObject.SetActive(false);
                continue;
            }

            dotPool[i].gameObject.SetActive(true);

            // 计算每个点的位置 = 起点 + 方向 * (索引 * 间距 + 时间偏移)
            float distOnLine = (i * DotSpacing) + timeOffset;

            // 如果点超出了终点，就把它拉回到起点循环，实现流水效果
            if (distOnLine > totalDist) distOnLine -= totalDist;

            dotPool[i].position = start + dir * distOnLine;
        }
    }

    private void AdjustDotPool(int count)
    {
        while (dotPool.Count < count)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(dot.GetComponent<MeshCollider>()); // 移除不需要的物理

            dot.transform.SetParent(dotContainer.transform);
            dot.transform.localScale = Vector3.one * DotSize;

            // 应用程序生成的材质
            dot.GetComponent<MeshRenderer>().material = internalMaterial;

            dotPool.Add(dot.transform);
        }
    }

    public void SetRallyPoint(Vector3 rawPos)
    {
        if (RTSGridSystem.Instance != null)
            RallyWorldPos = RTSGridSystem.Instance.GetSnappedWorldPos(rawPos);
        else
            RallyWorldPos = rawPos;
    }

    public override void SetSelected(bool state)
    {
        base.SetSelected(state);
        if (dotContainer != null) dotContainer.SetActive(state);
    }

    protected override void OnDeSelected()
    {
        if (dotContainer != null) dotContainer.SetActive(false);
    }

    public void OpenWorkshop()
    {
        Debug.Log($"<color=cyan>【组装中心】</color> 正在开启工坊...");
    }
}