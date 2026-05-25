using System.Collections.Generic;
using UnityEngine;

public abstract class BuildingBase : MonoBehaviour, IResidentCarrier
{
    // 🌟 全局建筑注册表，用于连通性他检
    public static List<BuildingBase> AllPlacedBuildings = new List<BuildingBase>();

    [Header("=== 建筑功能契约 ===")]
    public bool SupportsStaff = false; // 🌟 只有勾选了，HUD 才会显示“工作人员”按钮

    [Header("=== 基础信息 ===")]
    public string BuildingName = "新建筑";
    public Sprite BuildingIcon;
    public GameObject FunctionUIPrefab; // 对应底部舞台的模块

    [Header("=== 空间足迹 (Footprint) ===")]
    [Tooltip("建筑占用的格子相对坐标。")]
    public List<Vector2Int> FootprintOffsets = new List<Vector2Int> { new Vector2Int(0, 0) };
    [Tooltip("枢轴点位置，决定鼠标牵引位置。")]
    public Vector2Int PivotOffset = new Vector2Int(0, 0);

    [Header("=== 交互格设置 (多出口) ===")]
    public List<Vector2Int> InteractionOffsets = new List<Vector2Int> { new Vector2Int(1, 0) };

    [Header("=== 视觉表现 ===")]
    public SpriteRenderer GhostRenderer;
    public GameObject SelectionVisual;

    [Header("=== 岗位系统 ===")]
    public int MaxStaffCapacity = 4;
    protected List<ResidentData> currentStaff = new List<ResidentData>();
    public string GetCarrierName() => BuildingName;

    // 🌟 核心修改：显式告诉接口，你的 MaxStaffCapacity 就是返回这个字段的值
    int IResidentCarrier.MaxStaffCapacity => MaxStaffCapacity;

    public List<ResidentData> GetStaffList() => currentStaff;

    protected List<BoxCollider2D> subColliders = new List<BoxCollider2D>();
    protected List<SpriteRenderer> gridIndicators = new List<SpriteRenderer>();
    protected bool isPlaced = false;
    protected bool isSelected = false;


    public virtual bool TryAddStaff(ResidentData data)
    {
        // 🔍 DEBUG 4: 检查准入条件
        if (!SupportsStaff) { Debug.LogWarning($"[建筑] {BuildingName} 根本没开启 SupportsStaff 属性！"); return false; }

        if (currentStaff.Count >= MaxStaffCapacity)
        {
            Debug.LogWarning($"[建筑] {BuildingName} 岗位已满 ({currentStaff.Count}/{MaxStaffCapacity})");
            return false;
        }

        data.Status = ResidentStatus.Working;
        currentStaff.Add(data);
        Debug.Log($"<color=cyan>[建筑] {BuildingName} 成功登记员工: {data.ResidentName}。当前在职: {currentStaff.Count}</color>");
        return true;
    }

    public virtual void RemoveStaff(ResidentData data)
    {
        if (currentStaff.Contains(data))
        {
            currentStaff.Remove(data);
            StartCoroutine(EjectResidentRoutine(data));
        }
    }

    // 🌟 核心：一个个走出来 (排队遣散)
    private System.Collections.IEnumerator EjectResidentRoutine(ResidentData data)
    {
        Vector3 spawnPos = GetInteractionPoint();

        // 1. 在门口生成实体
        GameObject resObj = Instantiate(PopulationManager.Instance.ResidentPrefab, spawnPos, Quaternion.identity);
        ResidentEntity entity = resObj.GetComponent<ResidentEntity>();

        // 2. 注入灵魂
        data.Status = ResidentStatus.Idle;
        entity.Initialize(data, PopulationManager.Instance.IdentityLibrary.DefaultResidentHP);

        // 3. 视觉渐现 (预留程序渐变)
        // StartCoroutine(FadeIn(resObj));

        // 4. 指令：向外走一步，防止堵门口
        entity.SetDestination(spawnPos + (Vector3)Random.insideUnitCircle * 1.5f);

        yield return new WaitForSeconds(0.5f); // 间隔半秒出下一个人
    }
    public void DismissAllStaff()
    {
        // 复制一份列表防止遍历时修改导致报错
        var list = new List<ResidentData>(currentStaff);
        foreach (var staff in list)
        {
            RemoveStaff(staff);
        }
    }

    public Vector3 GetInteractionPoint()
    {
        if (InteractionOffsets.Count > 0)
        {
            float cellSize = RTSGridSystem.Instance.CellSize;
            return transform.position + new Vector3(InteractionOffsets[0].x * cellSize, InteractionOffsets[0].y * cellSize, 0);
        }
        return transform.position;
    }
    protected virtual void Awake()
    {
        GeneratePhysicalFootprint();
    }

    private void OnDestroy()
    {
        AllPlacedBuildings.Remove(this);
    }

    // --- 物理与吸附 ---
    public void SnapToGrid(Vector3 rawWorldPos)
    {
        if (RTSGridSystem.Instance == null) return;
        Vector2Int mouseGridIdx = RTSGridSystem.Instance.WorldToGrid(rawWorldPos);
        float cellSize = RTSGridSystem.Instance.CellSize;
        Vector3 cellWorldPos = RTSGridSystem.Instance.GetCell(mouseGridIdx.x, mouseGridIdx.y).WorldPos;
        Vector3 finalPos = cellWorldPos - new Vector3(PivotOffset.x * cellSize, PivotOffset.y * cellSize, 0);
        transform.position = new Vector3(finalPos.x, finalPos.y, 0);
    }

    private void GeneratePhysicalFootprint()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            if (transform.GetChild(i).name.StartsWith("Footprint_Node")) DestroyImmediate(transform.GetChild(i).gameObject);

        subColliders.Clear();
        float cellSize = (RTSGridSystem.Instance != null) ? RTSGridSystem.Instance.CellSize : 1.0f;

        foreach (Vector2Int offset in FootprintOffsets)
        {
            GameObject node = new GameObject($"Footprint_Node_{offset.x}_{offset.y}");
            node.transform.SetParent(this.transform);
            node.transform.localPosition = new Vector3(offset.x * cellSize, offset.y * cellSize, 0);
            node.layer = LayerMask.NameToLayer("Building");
            BoxCollider2D box = node.AddComponent<BoxCollider2D>();
            box.size = new Vector2(cellSize * 0.98f, cellSize * 0.98f);
            subColliders.Add(box);
        }
    }

    // --- 幽灵模式控制 ---
    public void InitGhostMode()
    {
        isPlaced = false;
        foreach (var col in subColliders) col.enabled = false;

        float cellSize = (RTSGridSystem.Instance != null) ? RTSGridSystem.Instance.CellSize : 1.0f;
        foreach (Vector2Int offset in FootprintOffsets)
        {
            GameObject indicator = new GameObject("GridIndicator");
            indicator.transform.SetParent(this.transform);
            indicator.transform.localPosition = new Vector3(offset.x * cellSize, offset.y * cellSize, 0.05f);
            var sr = indicator.AddComponent<SpriteRenderer>();
            sr.sprite = null; // 请在 Inspector 中或代码里指定色块 Sprite
            sr.size = new Vector2(cellSize * 0.9f, cellSize * 0.9f);
            sr.sortingLayerName = "UI";
            gridIndicators.Add(sr);
        }
    }

    public void UpdateGhostVisual(bool isValid)
    {
        Color targetColor = isValid ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
        if (GhostRenderer != null) GhostRenderer.color = targetColor;
        foreach (var sr in gridIndicators) if (sr) sr.color = targetColor;
    }

    public void FinalizePlacement()
    {
        foreach (var sr in gridIndicators) if (sr) Destroy(sr.gameObject);
        gridIndicators.Clear();
        if (GhostRenderer != null) GhostRenderer.color = Color.white;
        foreach (var col in subColliders) col.enabled = true;
        OnPlaced();
    }

    public virtual void OnPlaced()
    {
        isPlaced = true;
        AllPlacedBuildings.Add(this);
        if (RTSGridSystem.Instance == null) return;

        foreach (Vector2Int offset in FootprintOffsets)
        {
            Vector3 worldPos = transform.position + new Vector3(offset.x * RTSGridSystem.Instance.CellSize, offset.y * RTSGridSystem.Instance.CellSize, 0);
            Vector2Int gridIdx = RTSGridSystem.Instance.WorldToGrid(worldPos);
            GridCell cell = RTSGridSystem.Instance.GetCell(gridIdx.x, gridIdx.y);
            if (cell != null) { cell.IsOccupied = true; cell.Occupant = this.gameObject; }
        }
    }

    public virtual void SetSelected(bool state)
    {
        isSelected = state;
        if (SelectionVisual != null) SelectionVisual.SetActive(state);
        if (!state) OnDeSelected();
    }

    protected virtual void OnDeSelected() { }

    [Header("=== 开发辅助 (仅编辑器可见) ===")]
    public bool ShowDebugGizmos = true;

    private void OnDrawGizmos()
    {
        if (!ShowDebugGizmos) return;

        // 1. 获取网格规格，防止未运行报错，默认给 1.0
        float cellSize = (RTSGridSystem.Instance != null) ? RTSGridSystem.Instance.CellSize : 1.0f;

        // --- 绘图 A：绘制建筑占地体积 (蓝色) ---
        Gizmos.color = new Color(0.2f, 0.5f, 1.0f, 0.4f); // 半透明蓝色
        if (FootprintOffsets != null)
        {
            foreach (var offset in FootprintOffsets)
            {
                // 计算每个逻辑格中心的世界位置
                Vector3 cellPos = transform.position + new Vector3(offset.x * cellSize, offset.y * cellSize, 0);
                Gizmos.DrawCube(cellPos, new Vector3(cellSize * 0.95f, cellSize * 0.95f, 0.1f));

                // 画个细边框
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(cellPos, new Vector3(cellSize, cellSize, 0));
                Gizmos.color = new Color(0.2f, 0.5f, 1.0f, 0.4f);
            }
        }

        // --- 绘图 B：绘制交互格/门口 (黄色) ---
        Gizmos.color = Color.yellow;
        if (InteractionOffsets != null)
        {
            foreach (var offset in InteractionOffsets)
            {
                Vector3 interactPos = transform.position + new Vector3(offset.x * cellSize, offset.y * cellSize, 0);
                // 画一个线框球代表交互范围
                Gizmos.DrawWireSphere(interactPos, 0.4f);
                // 画个菱形表示这是一个“门”
                Gizmos.DrawIcon(interactPos, "d_FilterByLabel", true); // Unity 内置图标
            }
        }

        // --- 绘图 C：绘制枢轴点/鼠标点 (红色) ---
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.15f);
    }
}