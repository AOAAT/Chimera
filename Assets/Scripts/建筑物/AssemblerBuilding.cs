using System.Collections.Generic;
using UnityEngine;

public class AssemblerBuilding : BuildingBase
{
    [Header("=== 实体生产配置 ===")]
    public GameObject MechBasePrefab; // 指向你的 [Base_Mech_Unit] 预制体

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
    // 🌟 第二步：重写 OnPlaced 方法
    public override void OnPlaced()
    {
        // 先执行基类的网格锁定逻辑
        base.OnPlaced();

        // 核心修复：在建筑被正式放置到网格的那一刻，
        // 获取当前真实的出口坐标，并设为初始集合点。
        RallyWorldPos = GetSpawnWorldPos();

        Debug.Log($"<color=cyan>【初始化】</color> {BuildingName} 已就位，初始集合点设为出口：{RallyWorldPos}");
    }
    private void Start()
    {
      
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
        // 参数 1: -1 (代表不占用 0-7 号固定机库车位)
        // 参数 2: this (代表将当前建筑实例传给工坊)
        AssemblyWorkshopUI.Instance.OpenEmptyWorkshop(-1, this);
    }
    public void SpawnMech(SavedUnitProfile profile)
    {
        Vector3 spawnPos = GetSpawnWorldPos();

        // --- 1. 出口占用检测与偏移 ---
        // 检查出口 0.5 米范围内是否有其他单位（Layer 设为 Player_Body）
        Collider2D hit = Physics2D.OverlapCircle(spawnPos, 0.5f, LayerMask.GetMask("Player_Body"));
        if (hit != null)
        {
            // 如果被占了，产生一个随机的小偏移
            Vector2 offset = Random.insideUnitCircle * 0.6f;
            spawnPos += new Vector3(offset.x, offset.y, 0);
            Debug.Log("<color=yellow>【组装】</color> 出口被占用，已执行防卡死偏移。");
        }

        // --- 2. 实例化机甲 ---
        GameObject go = Instantiate(MechBasePrefab, spawnPos, Quaternion.identity);
        MechUnit2D unit = go.GetComponent<MechUnit2D>();

        // 注入数据 (注意：此时 AssemblyWorkshopUI 已经执行过 TryConsumeFromWarehouse 了)
        unit.InitUnitData(profile);

        // --- 3. RTS 物理重塑 (对齐你的 RTS 2.1 契约) ---
        var oldCol = go.GetComponent<BoxCollider2D>();
        if (oldCol != null) oldCol.enabled = false;
        var circle = go.AddComponent<CircleCollider2D>();
        circle.radius = 0.35f;

        // --- 4. 自动奔赴集合点 ---
        ChimeraAIController ai = go.GetComponent<ChimeraAIController>();
        if (ai != null)
        {
            // 延迟一帧下令，确保 AI 初始化完成
            StartCoroutine(SendToRallyPoint(ai));
        }

        GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_PowerOn);
    }

    private System.Collections.IEnumerator SendToRallyPoint(ChimeraAIController ai)
    {
        yield return null;
        ai.SetManualMovePoint(RallyWorldPos);
        Debug.Log($"<color=cyan>【调度】</color> 机甲已出厂，正在前往集合点。");
    }

}