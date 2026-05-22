using System.Collections.Generic;
using UnityEngine;

public abstract class BuildingBase : MonoBehaviour
{
    [Header("=== 基础信息 ===")]
    public string BuildingName = "新建筑";
    public Sprite BuildingIcon;

    [Header("=== 空间足迹 (Footprint) ===")]
    [Tooltip("建筑占用的格子相对坐标列表。例如 (0,0) 代表枢轴格。")]
    public List<Vector2Int> FootprintOffsets = new List<Vector2Int> { new Vector2Int(0, 0) };

    [Tooltip("枢轴点在 Footprint 中的索引位置。影响建筑跟随鼠标时的视觉对齐。")]
    public Vector2Int PivotOffset = new Vector2Int(0, 0);

    [Header("=== 出口设置 (Spawn) ===")]
    [Tooltip("机甲产出的相对格点坐标（相对于枢轴点）。")]
    public Vector2Int SpawnOffset = new Vector2Int(1, 0);

    [Header("=== 选中视觉 (可选) ===")]
    public GameObject SelectionVisual; // 建筑底部的青色光圈或边框

    protected bool isSelected = false;

    // 运行时存储的子碰撞体引用
    protected List<BoxCollider2D> subColliders = new List<BoxCollider2D>();
    protected bool isPlaced = false;

    protected virtual void Awake()
    {
        // 自动生成复合碰撞体 (方案 B)
        GeneratePhysicalFootprint();
    }

    // ==========================================
    // 🛠️ 物理与对齐核心
    // ==========================================

    /// <summary>
    /// 核心算法：根据当前鼠标的世界位置，将建筑吸附到网格，并考虑 Pivot 修正
    /// </summary>
    public void SnapToGrid(Vector3 rawWorldPos)
    {
        if (RTSGridSystem.Instance == null) return;

        // 1. 获取鼠标当前所在的格点坐标索引
        Vector2Int mouseGridIdx = RTSGridSystem.Instance.WorldToGrid(rawWorldPos);

        // 2. 获取该格点的中心世界坐标
        Vector3 cellWorldPos = RTSGridSystem.Instance.GetCell(mouseGridIdx.x, mouseGridIdx.y).WorldPos;

        // 3. 应用 Pivot 修正：
        // 建筑的真正 Position = 枢轴格中心坐标 - (PivotOffset * CellSize)
        float cellSize = RTSGridSystem.Instance.CellSize;
        Vector3 finalPos = cellWorldPos - new Vector3(PivotOffset.x * cellSize, PivotOffset.y * cellSize, 0);

        transform.position = new Vector3(finalPos.x, finalPos.y, 0);
    }

    /// <summary>
    /// 方案 B：为每一个足迹格点生成一个独立的 BoxCollider2D
    /// </summary>
    private void GeneratePhysicalFootprint()
    {
        // 清理旧的（如果在编辑器下反复调试）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (transform.GetChild(i).name.StartsWith("Footprint_Node"))
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        subColliders.Clear();
        float cellSize = RTSGridSystem.Instance != null ? RTSGridSystem.Instance.CellSize : 1.0f;

        foreach (Vector2Int offset in FootprintOffsets)
        {
            GameObject node = new GameObject($"Footprint_Node_{offset.x}_{offset.y}");
            node.transform.SetParent(this.transform);

            // 计算子节点位置：基于格子大小
            node.transform.localPosition = new Vector3(offset.x * cellSize, offset.y * cellSize, 0);
            node.layer = LayerMask.NameToLayer("Building"); // 确保你有这个 Layer

            // 添加 1x1 的碰撞箱
            BoxCollider2D box = node.AddComponent<BoxCollider2D>();
            box.size = new Vector2(cellSize * 0.98f, cellSize * 0.98f); // 留极小缝隙防止物理重叠抖动

            // 设定为非触发器（物理拦截）
            box.isTrigger = false;

            subColliders.Add(box);
        }
    }

    /// <summary>
    /// 获取产出点的世界坐标
    /// </summary>
    public Vector3 GetSpawnWorldPos()
    {
        // --- 🌟 核心修复：增加判空保护 ---
        // 如果网格系统还没准备好，我们先假设格子大小是 1.0f
        float cellSize = (RTSGridSystem.Instance != null) ? RTSGridSystem.Instance.CellSize : 1.0f;

        return transform.position + new Vector3(SpawnOffset.x * cellSize, SpawnOffset.y * cellSize, 0);
    }
    // ==========================================
    // ⚔️ 网格锁定协议
    // ==========================================

    public virtual void OnPlaced()
    {
        isPlaced = true;
        if (RTSGridSystem.Instance == null) return;

        // 锁定所有占用的格子
        foreach (Vector2Int offset in FootprintOffsets)
        {
            Vector3 worldPos = transform.position + new Vector3(offset.x, offset.y, 0);
            Vector2Int gridIdx = RTSGridSystem.Instance.WorldToGrid(worldPos);
            GridCell cell = RTSGridSystem.Instance.GetCell(gridIdx.x, gridIdx.y);
            if (cell != null)
            {
                cell.IsOccupied = true;
                cell.Occupant = this.gameObject;
            }
        }
    }

    // 可视化调试：在编辑器画出出口位置
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(GetSpawnWorldPos(), 0.3f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.1f); // 枢轴点标记
    }

    public virtual void SetSelected(bool state)
    {
        isSelected = state;
        if (SelectionVisual != null) SelectionVisual.SetActive(state);

        // 如果取消选中，也要隐藏虚线（由子类实现）
        if (!state) OnDeSelected();
    }

    protected virtual void OnDeSelected() { }
}