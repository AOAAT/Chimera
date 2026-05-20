// --- START OF FILE RTSGridSystem.cs ---
using UnityEngine;
using System.Collections.Generic;

// 1. 地块风味枚举
public enum TileFlavor { TechBase, Wasteland, FleshNest }

// 2. 逻辑方格类：承载每一个格子的物理与逻辑属性
[System.Serializable]
public class GridCell
{
    public Vector2Int GridPos;    // 逻辑坐标索引 (例如 0,0 代表左下角第一格)
    public Vector3 WorldPos;      // 该格子的物理中心点 (用于物体吸附)
    public bool IsOccupied = false; // 是否已被建筑占用
    public bool IsWalkable = true;  // 是否可通过 (未来可用于地形障碍)
    public TileFlavor Flavor;     // 该格子的艺术风格 (由生成器读取)
    public float ScrapDensity = 0; // 该格子蕴含的资源量
    public GameObject Occupant;    // 占用该格子的物体引用
}

public class RTSGridSystem : MonoBehaviour
{
    public static RTSGridSystem Instance;

    [Header("=== 战场规格设置 ===")]
    [Tooltip("地图横向的总长度 (格)")]
    public int MapWidth = 100;

    [Tooltip("地图纵向的宽度 (格)")]
    public int MapHeight = 12;

    [Tooltip("物理尺寸标准：1.0 代表 1米，配合 PPU=32 的贴图可完美拼接")]
    public float CellSize = 1.0f;

    // 内部逻辑矩阵
    private GridCell[,] grid;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        GenerateLogicGrid();
    }

    /// <summary>
    /// 核心算法：生成逻辑网格并计算垂直居中偏移
    /// </summary>
    private void GenerateLogicGrid()
    {
        grid = new GridCell[MapWidth, MapHeight];

        // --- 👇【居中算法核心】---
        // 计算起始 Y 坐标，使得整个地图的高度中点落在世界坐标 Y = 0 上
        // 公式：- (总高度 / 2) + (单个格子高度 / 2)
        float yOffset = -(MapHeight * CellSize) / 2f + (CellSize / 2f);

        for (int x = 0; x < MapWidth; x++)
        {
            // 基于 X 轴进度计算风味权重 (0.0 到 1.0)
            float progress = (float)x / MapWidth;
            TileFlavor currentFlavor;
            if (progress < 0.25f) currentFlavor = TileFlavor.TechBase;
            else if (progress < 0.7f) currentFlavor = TileFlavor.Wasteland;
            else currentFlavor = TileFlavor.FleshNest;

            for (int y = 0; y < MapHeight; y++)
            {
                // 计算每一格的中心点世界坐标
                // X 轴从 0 开始向右延伸
                // Y 轴由偏移量起步，向上叠放
                Vector3 worldPos = new Vector3(x * CellSize, yOffset + (y * CellSize), 0);

                grid[x, y] = new GridCell
                {
                    GridPos = new Vector2Int(x, y),
                    WorldPos = worldPos,
                    Flavor = currentFlavor,
                    IsWalkable = true,
                    // 只有非玩家基地核心区才铺撒资源
                    ScrapDensity = (progress > 0.4f && Random.value < 0.1f) ? Random.Range(50f, 200f) : 0
                };
            }
        }
        Debug.Log($"<color=#00FFFF>【RTS内核】逻辑网格已重构。Map: {MapWidth}x{MapHeight}, 物理中心已校准至 Y=0。</color>");
    }

    // ==========================================
    // 🛠️ 公共工具接口 (供其他系统调用)
    // ==========================================

    /// <summary>
    /// 将任意世界坐标“磁吸”到最近的逻辑格点中心
    /// </summary>
    public Vector3 GetSnappedWorldPos(Vector3 worldPos)
    {
        Vector2Int gridIndex = WorldToGrid(worldPos);
        return grid[gridIndex.x, gridIndex.y].WorldPos;
    }

    /// <summary>
    /// 坐标换算：世界坐标 -> 数组索引 (反向补偿 yOffset)
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float yOffset = -(MapHeight * CellSize) / 2f + (CellSize / 2f);

        int x = Mathf.Clamp(Mathf.RoundToInt(worldPos.x / CellSize), 0, MapWidth - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt((worldPos.y - yOffset) / CellSize), 0, MapHeight - 1);

        return new Vector2Int(x, y);
    }

    /// <summary>
    /// 安全访问指定的格子数据
    /// </summary>
    public GridCell GetCell(int x, int y)
    {
        if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight) return grid[x, y];
        return null;
    }

    // ==========================================
    // 🔍 场景视图调试：在 Scene 窗口画出淡白色的辅助线
    // ==========================================
    private void OnDrawGizmos()
    {
        if (grid == null) return;

        Gizmos.color = new Color(1, 1, 1, 0.1f);
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (grid[x, y] != null)
                {
                    Gizmos.DrawWireCube(grid[x, y].WorldPos, Vector3.one * CellSize * 0.95f);
                }
            }
        }
    }
}