using UnityEngine;

public enum TileFlavor { TechBase, Wasteland, FleshNest }

[System.Serializable]
public class GridCell
{
    public Vector2Int GridPos;
    public Vector3 WorldPos;
    public bool IsOccupied;
    public bool IsWalkable = true;
    public TileFlavor Flavor;
    public float ScrapDensity;
    public GameObject Occupant;
}

[DefaultExecutionOrder(-100)]
public class RTSGridSystem : MonoBehaviour
{
    public static RTSGridSystem Instance;

    [Header("地图规格（宽、高独立配置）")]
    [Min(1)] public int MapWidth = 100;
    [Min(1)] public int MapHeight = 12;
    [Min(0.01f)] public float CellSize = 1f;
    [Tooltip("左下角格子中心的世界坐标，不依赖世界原点或展开方向。")]
    public Vector2 GridOrigin;
    public TileFlavor DefaultFlavor = TileFlavor.Wasteland;
    private GridCell[,] grid;

    public Bounds WorldBounds => new Bounds(
        new Vector3(GridOrigin.x + (MapWidth - 1) * CellSize * 0.5f,
            GridOrigin.y + (MapHeight - 1) * CellSize * 0.5f, 0),
        new Vector3(MapWidth * CellSize, MapHeight * CellSize, 0));

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        MapWidth = Mathf.Max(1, MapWidth);
        MapHeight = Mathf.Max(1, MapHeight);
        CellSize = Mathf.Max(0.01f, CellSize);
        grid = new GridCell[MapWidth, MapHeight];
        for (int x = 0; x < MapWidth; x++)
        for (int y = 0; y < MapHeight; y++)
        {
            grid[x, y] = new GridCell
            {
                GridPos = new Vector2Int(x, y),
                WorldPos = new Vector3(GridOrigin.x + x * CellSize, GridOrigin.y + y * CellSize, 0),
                Flavor = DefaultFlavor
            };
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public Vector3 GetSnappedWorldPos(Vector3 worldPos)
    {
        Vector2Int index = WorldToGrid(worldPos);
        return grid[index.x, index.y].WorldPos;
    }

    // 移动目标可吸附到边缘；建筑合法性检查使用 TryWorldToGrid。
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        TryWorldToGrid(worldPos, out Vector2Int index);
        return new Vector2Int(Mathf.Clamp(index.x, 0, MapWidth - 1), Mathf.Clamp(index.y, 0, MapHeight - 1));
    }

    public bool TryWorldToGrid(Vector3 worldPos, out Vector2Int index)
    {
        index = new Vector2Int(Mathf.RoundToInt((worldPos.x - GridOrigin.x) / CellSize),
            Mathf.RoundToInt((worldPos.y - GridOrigin.y) / CellSize));
        return index.x >= 0 && index.x < MapWidth && index.y >= 0 && index.y < MapHeight;
    }

    public GridCell GetCell(int x, int y)
    {
        return grid != null && x >= 0 && x < MapWidth && y >= 0 && y < MapHeight ? grid[x, y] : null;
    }

    private void OnDrawGizmos()
    {
        if (grid == null) return;
        Gizmos.color = new Color(1, 1, 1, 0.1f);
        foreach (GridCell cell in grid)
            Gizmos.DrawWireCube(cell.WorldPos, Vector3.one * CellSize * 0.95f);
    }
}
