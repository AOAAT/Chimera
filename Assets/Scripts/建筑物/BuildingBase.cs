using System.Collections.Generic;
using UnityEngine;

public abstract class BuildingBase : MonoBehaviour
{
    // 🌟 全局建筑注册表，用于连通性他检
    public static List<BuildingBase> AllPlacedBuildings = new List<BuildingBase>();

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

    protected List<BoxCollider2D> subColliders = new List<BoxCollider2D>();
    protected List<SpriteRenderer> gridIndicators = new List<SpriteRenderer>();
    protected bool isPlaced = false;
    protected bool isSelected = false;

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
}