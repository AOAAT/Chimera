using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public sealed class WarehouseBuilding : BuildingBase
{
    public float StorageCapacity = 10000;
    protected override void Awake()
    {
        BuildingName = "综合仓库";
        SupportsStaff = false;
        base.Awake();
        if (BuildingVisualTheme.GetIcon(this) != null) return;
        var visual = new GameObject("WarehouseVisual");
        visual.transform.SetParent(transform, false);
        var sprite = visual.AddComponent<SpriteRenderer>();
        var texture = new Texture2D(16, 16) { filterMode = FilterMode.Point };
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            bool border = x < 2 || y < 2 || x > 13 || y > 13;
            bool brace = x == y || x == 15 - y;
            texture.SetPixel(x, y, border ? new Color32(42, 69, 78, 255) : brace ?
                new Color32(129, 194, 169, 255) : new Color32(72, 121, 126, 255));
        }
        texture.Apply();
        sprite.sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 16);
        visual.transform.localScale = Vector3.one * (RTSGridSystem.Instance != null ? RTSGridSystem.Instance.CellSize * .94f : .94f);
        sprite.sortingOrder = 5;
        GhostRenderer = sprite;
        BuildingIcon = sprite.sprite;
    }
    public override void OnPlaced()
    {
        base.OnPlaced();
        LogisticsManager.EnsureInstance().RegisterWarehouse(this);
    }
    public static WarehouseBuilding Create(Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("Buildings/Warehouse");
        if (prefab != null) return Instantiate(prefab, position, Quaternion.identity).GetComponent<WarehouseBuilding>();
        var go = new GameObject("综合仓库");
        go.transform.position = position;
        return go.AddComponent<WarehouseBuilding>();
    }
    public static WarehouseBuilding CreateInitial()
    {
        var grid = RTSGridSystem.Instance;
        if (grid == null) return null;
        var headquarters = BuildingBase.AllPlacedBuildings.FirstOrDefault(x => x is HeadquartersBuilding);
        Vector3 origin = headquarters != null ? headquarters.GetInteractionPoint() : grid.WorldBounds.center;
        var cells = Enumerable.Range(0, grid.MapWidth).SelectMany(x => Enumerable.Range(0, grid.MapHeight)
            .Select(y => grid.GetCell(x, y))).Where(x => x != null && x.IsWalkable && !x.IsOccupied)
            .OrderBy(x => (x.WorldPos - origin).sqrMagnitude);
        foreach (var cell in cells)
        {
            var gate = grid.GetCell(cell.GridPos.x + 1, cell.GridPos.y);
            if (gate == null || gate.IsOccupied || !gate.IsWalkable) continue;
            // Do not seal an existing building entrance or the approach to it.
            if (BuildingBase.AllPlacedBuildings.Any(x => x != null && Vector2.Distance(x.GetInteractionPoint(), cell.WorldPos) < grid.CellSize * 1.5f)) continue;
            var accessible = ConnectivityManager.GetAccessibleArea(new HashSet<Vector2Int> { cell.GridPos });
            if (!accessible.Contains(gate.GridPos) || BuildingBase.AllPlacedBuildings.Any(x => x != null &&
                !accessible.Contains(grid.WorldToGrid(x.GetInteractionPoint())))) continue;
            cell.IsOccupied = true;
            bool reachable;
            try { reachable = GridPathfinder.FindPath(origin, gate.WorldPos, false) != null; }
            finally { cell.IsOccupied = false; }
            if (!reachable) continue;
            var warehouse = Create(cell.WorldPos);
            warehouse.BuildingName = "初始补给仓库";
            warehouse.OnPlaced();
            return warehouse;
        }
        return null;
    }
}
