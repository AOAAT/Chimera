using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Native game textures are derived assets. Original hand drawings and the building atlas stay intact.
public static class WorldPixelArtAuthoring
{
    public const string ReadyPath = "Assets/Resources/WorldPixel50Ready.txt";
    public const string TilePath = "Assets/Art/World50/Grid50.png";
    static readonly string[] BuildingNames = { "Command", "Factory", "Assembly", "Habitat", "Warehouse" };
    static readonly string[] BuildingPaths = {
        "Assets/Prefabs/建筑物预制体/基地.prefab", "Assets/Prefabs/建筑物预制体/工厂建筑.prefab",
        "Assets/Prefabs/建筑物预制体/装配建筑.prefab", "Assets/Prefabs/建筑物预制体/房屋.prefab",
        "Assets/Resources/Buildings/Warehouse.prefab" };

    static Sprite NativeCopy(Sprite source, string path, int width, int height, int contentWidth, int contentHeight, Vector2 pivot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var raw = new Texture2D(2, 2); raw.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source.texture)));
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            var pixels = new Color32[width * height]; var src = raw.GetPixels32(); var rect = source.rect;
            int left = (width - contentWidth) / 2, bottom = (height - contentHeight) / 2;
            for (int y = 0; y < contentHeight; y++) for (int x = 0; x < contentWidth; x++)
            {
                int sx = (int)rect.x + Mathf.Min((int)rect.width - 1, Mathf.FloorToInt((x + .5f) * rect.width / contentWidth));
                int sy = (int)rect.y + Mathf.Min((int)rect.height - 1, Mathf.FloorToInt((y + .5f) * rect.height / contentHeight));
                pixels[(bottom + y) * width + left + x] = src[sy * raw.width + sx];
            }
            tex.SetPixels32(pixels); tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(raw); Object.DestroyImmediate(tex); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = WorldPixelMetrics.PixelsPerUnit; importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true; importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.maxTextureSize = Mathf.Max(256, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
        settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void Prefab(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [MenuItem("Tools/Chimera/美术规格/整理50PPU游戏资源")]
    public static void BakeAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        ResidentArtAuthoring.ImportArtwork();
        var originals = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Buildings/ColonyBuildings.png").OfType<Sprite>().ToArray();
        for (int i = 0; i < BuildingPaths.Length; i++)
        {
            var building = AssetDatabase.LoadAssetAtPath<GameObject>(BuildingPaths[i]).GetComponent<BuildingBase>();
            var offsets = building.FootprintOffsets;
            int w = (offsets.Max(p => p.x) - offsets.Min(p => p.x) + 1) * 50;
            int h = (offsets.Max(p => p.y) - offsets.Min(p => p.y) + 1) * 50;
            var sprite = originals.Single(s => s.name == BuildingNames[i]);
            float fit = Mathf.Min(w * .94f / sprite.rect.width, h * .94f / sprite.rect.height);
            NativeCopy(sprite, "Assets/Resources/Buildings/Pixel50/" + BuildingNames[i] + ".png", w, h,
                Mathf.RoundToInt(sprite.rect.width * fit), Mathf.RoundToInt(sprite.rect.height * fit), Vector2.one * .5f);
            Prefab(BuildingPaths[i], root => BuildingVisualTheme.Apply(root.GetComponent<BuildingBase>(), true));
        }
        NativeCopy(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/地块背景图.png"), TilePath, 50, 50, 50, 50, Vector2.one * .5f);
        foreach (string guid in AssetDatabase.FindAssets("t:ComponentDataSO", new[] { "Assets/Data/1_Blueprints (装备与底盘图纸)" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid); var data = AssetDatabase.LoadAssetAtPath<ComponentDataSO>(path);
            var sprite = data.ComponentIcon; if (sprite == null) continue;
            float scale = data.VisualScaleMultiplier;
            if (Mathf.Approximately(sprite.pixelsPerUnit, 50) && Mathf.Approximately(scale, 1)) continue;
            int w = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.width / sprite.pixelsPerUnit * scale * 50));
            int h = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height / sprite.pixelsPerUnit * scale * 50));
            var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            data.ComponentIcon = NativeCopy(sprite, "Assets/Art/World50/Components/" + guid + ".png", w, h, w, h, pivot);
            data.AnchorOffset *= scale; data.MuzzleOffset *= scale; data.VisualScaleMultiplier = 1;
            EditorUtility.SetDirty(data);
        }
        // Right-facing barrel mouth measured in the original 96x48 image: x=91, y=26.5 from bottom.
        var sniper = AssetDatabase.LoadAssetAtPath<ComponentDataSO>("Assets/Data/1_Blueprints (装备与底盘图纸)/2_Components/Weapon/WPN4_狙击枪/WPN4_狙击枪.asset");
        sniper.MuzzleOffset = (new Vector2(91, 26.5f) - sniper.ComponentIcon.pivot) / 50 - sniper.AnchorOffset;
        EditorUtility.SetDirty(sniper);
        foreach (var path in new[] { "Assets/Prefabs/2_Entities(游戏实体)/MechUnit.prefab", "Assets/Prefabs/2_Entities(游戏实体)/ModularEnemy_Template.prefab" })
            Prefab(path, root => root.GetComponent<MechUnit2D>().GlobalBattleScale = 1);
        AssetDatabase.SaveAssets();
    }
    public static bool ApplyScene(Scene scene)
    {
        bool changed = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var building in root.GetComponentsInChildren<BuildingBase>(true))
            {
                if (!building.UseUnifiedBuildingArt) continue;
                var icon = BuildingVisualTheme.GetIcon(building);
                if (icon == null || !Mathf.Approximately(icon.pixelsPerUnit, 50)) continue;
                var visual = building.transform.Find("UnifiedBuildingVisual");
                if (visual != null && visual.GetComponent<SpriteRenderer>().sprite == icon && visual.localScale == Vector3.one) continue;
                Undo.RegisterFullObjectHierarchyUndo(building.gameObject, "统一建筑像素密度");
                BuildingVisualTheme.Apply(building, true); EditorUtility.SetDirty(building);
                if (PrefabUtility.IsPartOfPrefabInstance(building)) PrefabUtility.RecordPrefabInstancePropertyModifications(building);
                changed = true;
            }
            foreach (var visuals in root.GetComponentsInChildren<RTSMapVisuals>(true))
            {
                var tile = AssetDatabase.LoadAssetAtPath<Sprite>(TilePath);
                if (tile == null || visuals.BaseTileSprite == tile) continue;
                Undo.RecordObject(visuals, "统一地图像素密度"); visuals.BaseTileSprite = tile;
                RTSMapEditor.UpdatePreview(visuals.GetComponent<RTSGridSystem>()); changed = true;
            }
        }
        return changed;
    }
    public static void BakeProject()
    {
        BakeAssets();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
        ApplyScene(scene); ResidentArtAuthoring.ApplyScene(scene, Resources.Load<ResidentSpriteSet>("Residents/WorkerSprites"));
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
}

[InitializeOnLoad]
public sealed class WorldPixelArtReady : AssetPostprocessor
{
    static WorldPixelArtReady()
    {
        EditorApplication.delayCall += Apply;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Apply; };
    }
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
    { if (imported.Contains(WorldPixelArtAuthoring.ReadyPath)) EditorApplication.delayCall += Apply; }
    static void Apply()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(WorldPixelArtAuthoring.ReadyPath)) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i); if (!scene.isLoaded || !scene.path.StartsWith("Assets/Scenes/")) continue;
            bool dirty = scene.isDirty; if (!WorldPixelArtAuthoring.ApplyScene(scene)) continue;
            EditorSceneManager.MarkSceneDirty(scene); if (!dirty) EditorSceneManager.SaveScene(scene);
        }
    }
}
