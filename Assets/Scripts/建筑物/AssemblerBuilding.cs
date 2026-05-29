using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AssemblerBuilding : BuildingBase, ICoverageProvider
{
    [Header("=== 带宽配置 ===")]
    public float CoverageRadius = 12.0f; // 该厂提供的覆盖半径
    public Vector3 GetPosition() => transform.position;
    public float GetRadius() => CoverageRadius;
    [Header("=== 集合点配置 ===")]
    public Vector3 RallyWorldPos;

    [Header("=== 虚线视觉参数 (全程序化) ===")]
    public float DotSpacing = 0.5f;   // 点与点之间的距离
    public float ScrollSpeed = 2.0f;  // 虚线滑动的速度
    public float DotSize = 0.12f;     // 点的大小
    public Color LineColor = new Color(0, 1, 1, 0.8f); // 虚线颜色（青色）

    [Header("=== 实体生产配置 ===")]
    public GameObject MechBasePrefab; // 指向 [Base_Mech_Unit] 预制体

    private List<Transform> dotPool = new List<Transform>();
    private GameObject dotContainer;
    private Material internalMaterial;

    protected override void Awake()
    {
        base.Awake();

        // 1. 程序化创建材质（无需外部资源）
        internalMaterial = new Material(Shader.Find("Sprites/Default"));
        internalMaterial.color = LineColor;

        // 2. 初始化点阵容器
        dotContainer = new GameObject("RallyDotContainer");
        dotContainer.transform.SetParent(this.transform);
        dotContainer.SetActive(false);
    }

    private void Update()
    {
        // 只有被选中时才渲染虚线
        if (isSelected)
        {
            UpdateProceduralRallyLine();
        }
    }

    // ==========================================
    // 🏗️ 建筑生命周期：正式放置后初始化
    // ==========================================
    public override void OnPlaced()
    {
        base.OnPlaced();

        // 🌟 核心修正：放置完成后，初始集合点设为第一个交互格的位置
        if (InteractionOffsets != null && InteractionOffsets.Count > 0)
        {
            float cellSize = RTSGridSystem.Instance.CellSize;
            RallyWorldPos = transform.position + new Vector3(InteractionOffsets[0].x * cellSize, InteractionOffsets[0].y * cellSize, 0);
        }
        else
        {
            RallyWorldPos = transform.position;
        }
        if (ZoneCoverageManager.Instance != null)
            ZoneCoverageManager.Instance.RegisterProvider(this);
    }

    // ==========================================
    // ⚔️ 生产逻辑：机甲产出与动态切门
    // ==========================================
    public void OpenWorkshop()
    {
        if (AssemblyWorkshopUI.Instance != null)
        {
            // 🌟 修复：删掉原来的 -1 参数，只传 this
            AssemblyWorkshopUI.Instance.OpenEmptyWorkshop(this);
        }
    }
    public void SpawnMech(SavedUnitProfile profile)
    {
        Vector3 bestSpawnPos = CalculateBestSpawnLocation();
        GameObject go = Instantiate(MechBasePrefab, bestSpawnPos, Quaternion.identity);

        // 🌟 [核心修复]：先拿到脚本
        MechUnit2D unit = go.GetComponent<MechUnit2D>();
        if (unit == null) unit = go.AddComponent<MechUnit2D>();

        // 🌟 [核心修复]：强制初始化数据 (这会触发内部的 FullSetup)
        unit.InitUnitData(profile);

        // 🌟 [核心修复]：处理 RTS 圆形碰撞核
        // 此时 FullSetup 已经跑完了，物理层级也定好了
        var oldBox = go.GetComponent<BoxCollider2D>();
        if (oldBox != null) oldBox.enabled = false; // 禁用推挤方盒

        CircleCollider2D circle = go.GetComponent<CircleCollider2D>();
        if (circle == null) circle = go.AddComponent<CircleCollider2D>();
        circle.radius = 0.35f;
        circle.isTrigger = false; // 确保它是实体碰撞，不是触发器

        // 启动 AI
        ChimeraAIController ai = go.GetComponent<ChimeraAIController>();
        if (ai != null) StartCoroutine(DelayedCommand(ai));

        GlobalAudioManager.Instance?.PlayUISound(UISoundType.Mech_PowerOn);
    }

    private Vector3 CalculateBestSpawnLocation()
    {
        if (InteractionOffsets == null || InteractionOffsets.Count == 0) return transform.position;

        float cellSize = RTSGridSystem.Instance.CellSize;

        // 🌟 动态切门核心算法：
        // 权重 = 距离集合点的物理距离 + (如果被堵塞则增加 1000 米惩罚)
        var sortedGates = InteractionOffsets
            .Select(offset => transform.position + new Vector3(offset.x * cellSize, offset.y * cellSize, 0))
            .OrderBy(pos => {
                bool isBlocked = Physics2D.OverlapCircle(pos, 0.4f, LayerMask.GetMask("Player_Body", "Enemy_Body"));
                return Vector3.Distance(pos, RallyWorldPos) + (isBlocked ? 1000f : 0f);
            })
            .ToList();

        Vector3 finalPos = sortedGates[0];

        // 极致兜底：如果所有门口都挤满了人（所有门权重都 > 1000）
        if (Vector3.Distance(finalPos, RallyWorldPos) > 500f)
        {
            finalPos += (Vector3)Random.insideUnitCircle * 0.5f; // 随机挤出来
            Debug.Log("<color=orange>【物流拥堵】</color> 建筑出口全线爆满，执行紧急偏移产出。");
        }

        return finalPos;
    }

    private System.Collections.IEnumerator DelayedCommand(ChimeraAIController ai)
    {
        yield return null; // 等待一帧，确保 AI 初始化完毕
        ai.SetManualMovePoint(RallyWorldPos);
    }

    // ==========================================
    // 🎨 视觉逻辑：全程序化虚线 (从中心格开始)
    // ==========================================
    private void UpdateProceduralRallyLine()
    {
        // 🌟 视觉修正：起点始终锁定在建筑的枢轴中心（transform.position）
        Vector3 start = transform.position;
        Vector3 end = RallyWorldPos;
        float totalDist = Vector3.Distance(start, end);

        if (totalDist < 0.3f)
        {
            dotContainer.SetActive(false);
            return;
        }

        dotContainer.SetActive(true);

        int neededDots = Mathf.CeilToInt(totalDist / DotSpacing);
        AdjustDotPool(neededDots);

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
            float distOnLine = (i * DotSpacing) + timeOffset;

            // 循环效果逻辑
            if (distOnLine > totalDist) distOnLine -= totalDist;

            dotPool[i].position = start + dir * distOnLine;
        }
    }

    private void AdjustDotPool(int count)
    {
        while (dotPool.Count < count)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(dot.GetComponent<MeshCollider>()); // 移除不需要的 3D 物理

            dot.transform.SetParent(dotContainer.transform);
            dot.transform.localScale = Vector3.one * DotSize;
            dot.GetComponent<MeshRenderer>().material = internalMaterial;

            dotPool.Add(dot.transform);
        }
    }

    public void SetRallyPoint(Vector3 rawPos)
    {
        Vector3 snappedPos = rawPos;
        if (RTSGridSystem.Instance != null)
            snappedPos = RTSGridSystem.Instance.GetSnappedWorldPos(rawPos);

        // 🌟 检查这个点是否在任何一个覆盖区内
        if (ZoneCoverageManager.Instance != null && !ZoneCoverageManager.Instance.IsPointInCoverage(snappedPos))
        {
            Debug.LogWarning("无法将集合点设在带宽覆盖范围外！");
            return;
        }

        RallyWorldPos = snappedPos;
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

    private void OnDestroy()
    {
        if (ZoneCoverageManager.Instance != null)
            ZoneCoverageManager.Instance.UnregisterProvider(this);
    }
}