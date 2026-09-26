using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class BuildingBase : MonoBehaviour, IResidentCarrier
{
    // 🌟 全局建筑注册表，用于连通性他检
    public static List<BuildingBase> AllPlacedBuildings = new List<BuildingBase>();

    [Header("=== 建筑功能契约 ===")]
    public bool SupportsStaff = false; // 🌟 只有勾选了，HUD 才会显示“工作人员”按钮

    [Header("=== 基础信息 ===")]
    [Tooltip("运行时建筑实例的稳定标识。由图纸 ID 与网格位置生成。")]
    [SerializeField] private string persistentID;
    [Tooltip("生成该建筑的图纸稳定 ID；场景预放建筑允许留空并使用脚本类型。")]
    [SerializeField] private string definitionID;
    public string BuildingName = "新建筑";
    public Sprite BuildingIcon;
    [Tooltip("使用统一建筑图集；关闭后保留原有美术。") ]
    public bool UseUnifiedBuildingArt = true;
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
    public event Action<BuildingBase> OnStaffChanged;
    public string GetCarrierName() => BuildingName;

    // 🌟 核心修改：显式告诉接口，你的 MaxStaffCapacity 就是返回这个字段的值
    int IResidentCarrier.MaxStaffCapacity => MaxStaffCapacity;

    public List<ResidentData> GetStaffList() => currentStaff;
    public string PersistentID => EnsurePersistentID();
    public string DefinitionID => definitionID;

    protected List<BoxCollider2D> subColliders = new List<BoxCollider2D>();
    protected List<SpriteRenderer> gridIndicators = new List<SpriteRenderer>();
    protected bool isPlaced = false;
    protected bool isSelected = false;
    private bool isGhost;

    protected virtual void Start()
    {
        // 预放建筑与玩家建造建筑使用同一注册流程。所有 Awake 已在 Start 前完成。
        if (isGhost || AllPlacedBuildings.Contains(this)) return;
        if (RTSGridSystem.Instance != null)
            transform.position = RTSGridSystem.Instance.GetSnappedWorldPos(transform.position);
        OnPlaced();
    }

    public int GetReservedStaffCount()
    {
        if (PopulationManager.Instance == null) return 0;
        int count = 0;
        foreach (ResidentData resident in PopulationManager.Instance.TotalResidents)
        {
            if (resident == null || resident.Status != ResidentStatus.TravelingToWork) continue;
            if (resident.CurrentCarrierID == PersistentID) count++;
        }
        return count;
    }

    public bool CanAcceptStaffOrder(ResidentData resident)
    {
        if (!SupportsStaff || resident == null || currentStaff.Contains(resident)) return false;
        if (resident.Status != ResidentStatus.Idle) return false;
        return currentStaff.Count + GetReservedStaffCount() < MaxStaffCapacity;
    }

    public void NotifyStaffReservationChanged()
    {
        OnStaffChanged?.Invoke(this);
    }


    public virtual bool TryAddStaff(ResidentData data)
    {
        // 🔍 DEBUG 4: 检查准入条件
        if (!SupportsStaff) { Debug.LogWarning($"[建筑] {BuildingName} 根本没开启 SupportsStaff 属性！"); return false; }

        if (currentStaff.Count >= MaxStaffCapacity)
        {
            Debug.LogWarning($"[建筑] {BuildingName} 岗位已满 ({currentStaff.Count}/{MaxStaffCapacity})");
            return false;
        }

        if (data == null || currentStaff.Contains(data)) return false;
        data.Status = ResidentStatus.Working;
        data.CurrentCarrierID = PersistentID;
        currentStaff.Add(data);
        OnStaffChanged?.Invoke(this);
        if (PopulationManager.Instance != null) PopulationManager.Instance.NotifyResidentStateChanged();
        Debug.Log($"<color=cyan>[建筑] {BuildingName} 成功登记员工: {data.ResidentName}。当前在职: {currentStaff.Count}</color>");
        return true;
    }

    public virtual void RemoveStaff(ResidentData data)
    {
        if (currentStaff.Contains(data))
        {
            currentStaff.Remove(data);
            data.Status = ResidentStatus.Idle;
            data.CurrentCarrierID = string.Empty;
            OnStaffChanged?.Invoke(this);
            if (PopulationManager.Instance != null) PopulationManager.Instance.NotifyResidentStateChanged();
            StartCoroutine(EjectResidentRoutine(data));
        }
    }

    // 🌟 核心：一个个走出来 (排队遣散)
    private System.Collections.IEnumerator EjectResidentRoutine(ResidentData data)
    {
        EjectResident(data);
        yield return null;
    }
    public void DismissAllStaff()
    {
        var list = new List<ResidentData>(currentStaff);
        if (list.Count == 0) return;
        currentStaff.Clear();
        foreach (ResidentData staff in list)
        {
            staff.Status = ResidentStatus.Idle;
            staff.CurrentCarrierID = string.Empty;
        }
        OnStaffChanged?.Invoke(this);
        if (PopulationManager.Instance != null) PopulationManager.Instance.NotifyResidentStateChanged();
        StartCoroutine(EjectResidentsSequentially(list));
    }

    private System.Collections.IEnumerator EjectResidentsSequentially(List<ResidentData> residents)
    {
        foreach (ResidentData resident in residents)
        {
            EjectResident(resident);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void EjectResident(ResidentData data)
    {
        if (data == null || PopulationManager.Instance == null) return;
        Vector3 spawnPos = GetInteractionPoint();
        ResidentEntity entity = PopulationManager.Instance.SpawnExistingResidentAt(data, spawnPos);
        data.Status = ResidentStatus.Idle;
        data.CurrentCarrierID = string.Empty;
        if (entity != null)
            entity.SetDestination(spawnPos + (Vector3)UnityEngine.Random.insideUnitCircle * 1.5f);
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
        BuildingVisualTheme.Apply(this);
    }

    protected virtual void OnDestroy()
    {
        if (AllPlacedBuildings.Contains(this)) LogisticsManager.Instance?.ReleaseBuilding(this);
        AllPlacedBuildings.Remove(this);
        var grid = RTSGridSystem.Instance;
        if (grid == null) return;
        foreach (Vector2Int offset in FootprintOffsets)
        {
            Vector3 position = transform.position + new Vector3(offset.x * grid.CellSize, offset.y * grid.CellSize, 0);
            if (!grid.TryWorldToGrid(position, out Vector2Int index)) continue;
            GridCell cell = grid.GetCell(index.x, index.y);
            if (cell != null && cell.Occupant == gameObject)
            {
                cell.IsOccupied = false;
                cell.Occupant = null;
            }
        }
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
        isGhost = true;
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

    public void InitializePersistence(string sourceDefinitionID)
    {
        if (!string.IsNullOrWhiteSpace(sourceDefinitionID)) definitionID = sourceDefinitionID;
        persistentID = string.Empty;
    }

    public void RestorePersistence(string savedInstanceID, string savedDefinitionID, Vector3 savedPosition)
    {
        transform.position = savedPosition;
        definitionID = savedDefinitionID;
        persistentID = savedInstanceID;
        OnPlaced();
    }

    public void RestoreStaff(IEnumerable<ResidentData> residents)
    {
        currentStaff.Clear();
        if (residents == null) return;
        foreach (ResidentData resident in residents)
        {
            if (resident == null || currentStaff.Count >= MaxStaffCapacity) continue;
            resident.Status = ResidentStatus.Working;
            resident.CurrentCarrierID = PersistentID;
            currentStaff.Add(resident);
        }
        OnStaffChanged?.Invoke(this);
    }

    private string EnsurePersistentID()
    {
        if (!string.IsNullOrWhiteSpace(persistentID)) return persistentID;
        string source = string.IsNullOrWhiteSpace(definitionID) ? GetType().Name : definitionID;
        Vector3 position = transform.position;
        persistentID = string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1}:{2:0.###}:{3:0.###}",
            SceneManager.GetActiveScene().name,
            source,
            position.x,
            position.y);
        return persistentID;
    }

    public virtual void OnPlaced()
    {
        if (AllPlacedBuildings.Contains(this)) return;
        isGhost = false;
        isPlaced = true;
        EnsurePersistentID();
        AllPlacedBuildings.Add(this);
        if (RTSGridSystem.Instance == null) return;

        foreach (Vector2Int offset in FootprintOffsets)
        {
            Vector3 worldPos = transform.position + new Vector3(offset.x * RTSGridSystem.Instance.CellSize, offset.y * RTSGridSystem.Instance.CellSize, 0);
            if (!RTSGridSystem.Instance.TryWorldToGrid(worldPos, out Vector2Int gridIdx)) continue;
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
