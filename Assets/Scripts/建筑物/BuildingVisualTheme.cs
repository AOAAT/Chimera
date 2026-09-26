using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Shared atlas for world buildings, placement ghosts, detail portraits and build buttons.
/// Art only: footprint, interaction cells and persistence identifiers are never changed.</summary>
public static class BuildingVisualTheme
{
    private const string ResourcePath = "Buildings/ColonyBuildings";
    private static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
    private static Texture2D atlas;
    private static Sprite entranceSprite;

    public static Sprite GetIcon(BuildingBase building)
    {
        if (building == null || !building.UseUnifiedBuildingArt) return null;
        int index = building is HeadquartersBuilding ? 0 : building is FactoryBuilding ? 1 :
            building is AssemblerBuilding ? 2 : building is HousingBuilding ? 3 : building is WarehouseBuilding ? 4 : -1;
        if (index < 0) return null;
        if (sprites.TryGetValue(index, out Sprite cached) && cached != null) return cached;
        string spriteName = new[] { "Command", "Factory", "Assembly", "Habitat", "Warehouse" }[index];
        Sprite imported = Resources.LoadAll<Sprite>(ResourcePath).FirstOrDefault(x => x.name == spriteName);
        if (imported != null) { sprites[index] = imported; return imported; }
        if (atlas == null) atlas = Resources.Load<Texture2D>(ResourcePath);
        if (atlas == null) return null;
        int width = atlas.width / 3, height = atlas.height / 2;
        int left = index % 3 * width, bottom = (1 - index / 3) * height;
        // Tight alpha bounds keep all five icons legible regardless of the atlas's transparent padding.
        int minX = width, minY = height, maxX = -1, maxY = -1;
        if (atlas.isReadable)
        {
            Color[] pixels = atlas.GetPixels(left, bottom, width, height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a < .1f) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
        }
        if (maxX < minX) { minX = 0; minY = 0; maxX = width - 1; maxY = height - 1; }
        var rect = new Rect(left + minX, bottom + minY, maxX - minX + 1, maxY - minY + 1);
        var sprite = Sprite.Create(atlas, rect, new Vector2(.5f, .5f), 64, 0, SpriteMeshType.FullRect);
        sprite.name = new[] { "Command", "Factory", "Assembly", "Habitat", "Warehouse" }[index];
        sprites[index] = sprite;
        return sprite;
    }

    public static void Apply(BuildingBase building, bool rebuild = false, float cellSize = 1f)
    {
        Sprite icon = GetIcon(building);
        if (icon == null) return;
        Transform visual = building.transform.Find("UnifiedBuildingVisual");
        // Authored visuals are the source of truth. Play mode must preserve Inspector edits.
        if (visual != null && !rebuild) return;
        if (visual == null)
        {
            var go = new GameObject("UnifiedBuildingVisual", typeof(SpriteRenderer));
            go.transform.SetParent(building.transform, false); visual = go.transform;
        }
        foreach (var renderer in building.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.transform == visual) continue;
            if (building.SelectionVisual != null && renderer.transform.IsChildOf(building.SelectionVisual.transform)) continue;
            renderer.enabled = false;
        }
        var offsets = building.FootprintOffsets;
        float cell = RTSGridSystem.Instance != null ? RTSGridSystem.Instance.CellSize : cellSize;
        int minX = offsets.Count > 0 ? offsets.Min(x => x.x) : 0, maxX = offsets.Count > 0 ? offsets.Max(x => x.x) : 0;
        int minY = offsets.Count > 0 ? offsets.Min(x => x.y) : 0, maxY = offsets.Count > 0 ? offsets.Max(x => x.y) : 0;
        visual.localPosition = new Vector3((minX + maxX) * cell * .5f, (minY + maxY) * cell * .5f, 0);
        float scale = Mathf.Min((maxX - minX + 1) * cell / icon.bounds.size.x, (maxY - minY + 1) * cell / icon.bounds.size.y) * .94f;
        visual.localScale = Vector3.one * scale;
        var spriteRenderer = visual.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = icon; spriteRenderer.color = Color.white; spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = building.GhostRenderer != null ? building.GhostRenderer.sortingOrder : 5;
        if (building.GhostRenderer != null) spriteRenderer.sortingLayerID = building.GhostRenderer.sortingLayerID;
        building.GhostRenderer = spriteRenderer;
        building.BuildingIcon = icon;

        // Mark the actual approach cell even when the painted door faces a different direction.
        if (building.InteractionOffsets.Count == 0) return;
        var marker = building.transform.Find("EntranceMarker");
        if (marker == null)
        {
            var go = new GameObject("EntranceMarker", typeof(SpriteRenderer)); go.transform.SetParent(building.transform, false); marker = go.transform;
        }
        if (entranceSprite == null) entranceSprite = Resources.Load<Sprite>("UI/ChimeraRounded");
        var gate = building.InteractionOffsets[0];
        marker.localPosition = new Vector3(gate.x * cell, gate.y * cell, 0);
        marker.localScale = new Vector3(cell * .24f, cell * .06f, 1);
        var entryRenderer = marker.GetComponent<SpriteRenderer>(); entryRenderer.sprite = entranceSprite;
        entryRenderer.enabled = true; entryRenderer.color = ChimeraUITheme.Accent;
        entryRenderer.sortingLayerID = spriteRenderer.sortingLayerID; entryRenderer.sortingOrder = spriteRenderer.sortingOrder;
    }
}
