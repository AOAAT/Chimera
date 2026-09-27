using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ResidentArtAuthoring
{
    public const string SourcePath = "ArtSource/Residents/Worker32_v1/worker-directions-carry-concept.png";
    public const string AtlasPath = "Assets/Art/Residents/Worker48.png";
    public const string SetPath = "Assets/Resources/Residents/WorkerSprites.asset";
    public const string SelectionMaterialPath = "Assets/Art/Residents/ResidentSelection.mat";
    public const string PrefabPath = "Assets/Prefabs/Units/Resident_Base.prefab";
    public const string AvatarPath = "Assets/Prefabs/UI/Colony/居民小图.prefab";
    public const string ReadyPath = "Assets/Resources/Residents/ResidentArtReady.txt";
    public static readonly string[] FrameNames = {
        "worker_idle_front", "worker_idle_left", "worker_idle_right", "worker_idle_back",
        "worker_carry_front", "worker_carry_left", "worker_carry_right", "worker_carry_back"
    };

    [MenuItem("Tools/Chimera/居民美术/从源稿生成48像素精灵（50PPU）")]
    public static void ImportArtwork()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("居民源稿不存在", SourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
        Directory.CreateDirectory(Path.GetDirectoryName(SetPath));
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var atlas = new Texture2D(192, 96, TextureFormat.RGBA32, false);
        try
        {
            if (!source.LoadImage(File.ReadAllBytes(SourcePath))) throw new InvalidDataException("无法读取居民源稿");
            var input = source.GetPixels32();
            var output = new Color32[192 * 96];
            for (int frame = 0; frame < 8; frame++)
            {
                // Generated source dimensions need not divide by four. Find each frame independently.
                int col = frame % 4, row = frame / 4;
                int x0 = Mathf.RoundToInt(col * source.width / 4f), x1 = Mathf.RoundToInt((col + 1) * source.width / 4f);
                int y0 = Mathf.RoundToInt((1 - row) * source.height / 2f), y1 = Mathf.RoundToInt((2 - row) * source.height / 2f);
                var bounds = MainSilhouette(input, source.width, new RectInt(x0, y0, x1 - x0, y1 - y0));
                float scale = Mathf.Min(36f / bounds.width, 44f / bounds.height);
                int width = Mathf.Max(1, Mathf.RoundToInt(bounds.width * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(bounds.height * scale));
                int left = col * 48 + (48 - width) / 2, bottom = (1 - row) * 48 + 2;
                // Asset import resampling: nearest neighbour only, with a shared two-pixel foot baseline.
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = bounds.x + Mathf.Min(bounds.width - 1, Mathf.FloorToInt((x + .5f) * bounds.width / width));
                    int sy = bounds.y + Mathf.Min(bounds.height - 1, Mathf.FloorToInt((y + .5f) * bounds.height / height));
                    var pixel = input[sy * source.width + sx];
                    if (pixel.a < 160) continue;
                    pixel.a = 255;
                    output[(bottom + y) * 192 + left + x] = pixel;
                }
            }
            atlas.SetPixels32(output); atlas.Apply();
            File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(atlas); }
        AssetDatabase.Refresh();
        ConfigureImporter();
        var slices = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(x => x.name);
        if (slices.Count != 8) throw new InvalidDataException("居民图集必须包含八张精灵");
        var set = AssetDatabase.LoadAssetAtPath<ResidentSpriteSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<ResidentSpriteSet>(); AssetDatabase.CreateAsset(set, SetPath); }
        set.Idle = FrameNames.Take(4).Select(x => slices[x]).ToArray();
        set.Carry = FrameNames.Skip(4).Select(x => slices[x]).ToArray();
        EditorUtility.SetDirty(set);
        if (AssetDatabase.LoadAssetAtPath<Material>(SelectionMaterialPath) == null)
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")), SelectionMaterialPath);
        BakePrefab(PrefabPath, root => PrepareResident(root.GetComponent<ResidentEntity>(), set));
        BakePrefab(AvatarPath, root => {
            var portrait = root.GetComponentsInChildren<Image>(true).FirstOrDefault(x => x.name == "Icon" || x.name == "Avatar_Icon");
            if (portrait != null) SetPortrait(portrait, set.Portrait);
        });
        AssetDatabase.SaveAssets();
    }

    private static RectInt MainSilhouette(Color32[] pixels, int stride, RectInt cell)
    {
        // Ignore isolated generation speckles when measuring the body; preserve its original colors.
        var seen = new bool[cell.width * cell.height];
        int bestCount = 0; RectInt best = default;
        for (int y = 0; y < cell.height; y++)
        for (int x = 0; x < cell.width; x++)
        {
            int start = y * cell.width + x;
            if (seen[start] || pixels[(cell.y + y) * stride + cell.x + x].a < 160) continue;
            var queue = new Queue<Vector2Int>(); queue.Enqueue(new Vector2Int(x, y)); seen[start] = true;
            int count = 0, minX = x, maxX = x, minY = y, maxY = y;
            while (queue.Count > 0)
            {
                var point = queue.Dequeue(); count++;
                minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
                foreach (var offset in Neighbours)
                {
                    var next = point + offset;
                    if (next.x < 0 || next.y < 0 || next.x >= cell.width || next.y >= cell.height) continue;
                    int index = next.y * cell.width + next.x;
                    if (seen[index]) continue;
                    seen[index] = true;
                    if (pixels[(cell.y + next.y) * stride + cell.x + next.x].a >= 160) queue.Enqueue(next);
                }
            }
            if (count > bestCount) { bestCount = count; best = new RectInt(cell.x + minX, cell.y + minY, maxX - minX + 1, maxY - minY + 1); }
        }
        if (bestCount == 0) throw new InvalidDataException("源稿的某一格没有可见人物");
        return best;
    }
    private static readonly Vector2Int[] Neighbours = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    private static void ConfigureImporter()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = WorldPixelMetrics.PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.isReadable = false;
        importer.maxTextureSize = 256;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().ToDictionary(x => x.name, x => x.spriteID);
        var rects = FrameNames.Select((name, index) => new SpriteRect {
            name = name, rect = new Rect(index % 4 * 48, (1 - index / 4) * 48, 48, 48),
            alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, 2f / 48),
            spriteID = previous.TryGetValue(name, out var id) ? id : GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(
            rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }
    private static void BakePrefab(string path, Action<GameObject> action)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void PrepareResident(ResidentEntity resident, ResidentSpriteSet set)
    {
        if (resident == null) return;
        var visual = resident.GetComponent<ResidentVisual2D>();
        if (visual == null) visual = resident.gameObject.AddComponent<ResidentVisual2D>();
        visual.Sprites = set;
        visual.Body = resident.transform.Find("Visual_Sprite")?.GetComponent<SpriteRenderer>();
        if (visual.Body == null) throw new InvalidDataException("居民缺少 Visual_Sprite");
        visual.Body.color = Color.white;
        visual.Body.spriteSortPoint = SpriteSortPoint.Pivot;
        visual.RefreshVisual();
        if (resident.SelectionCircle != null)
        {
            // The former selection renderer used the HP-heart placeholder. Use a simple world-space outline.
            var old = resident.SelectionCircle.GetComponent<SpriteRenderer>();
            if (old != null) Object.DestroyImmediate(old);
            resident.SelectionCircle.transform.localPosition = new Vector3(0, -.44f, 0);
            resident.SelectionCircle.transform.localScale = Vector3.one;
            var ring = resident.SelectionCircle.GetComponent<LineRenderer>();
            if (ring == null) ring = resident.SelectionCircle.AddComponent<LineRenderer>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(SelectionMaterialPath);
            ring.sharedMaterial = material; ring.useWorldSpace = false; ring.loop = true;
            ring.startColor = ring.endColor = new Color32(143, 227, 176, 255);
            ring.startWidth = ring.endWidth = WorldPixelMetrics.PixelSize;
            ring.positionCount = 24;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * .34375f, Mathf.Sin(angle) * .09375f, 0));
            }
            ring.sortingLayerID = visual.Body.sortingLayerID; ring.sortingOrder = visual.Body.sortingOrder - 1;
        }
    }
    private static void SetPortrait(Image target, Sprite sprite)
    {
        target.sprite = sprite; target.overrideSprite = null; target.color = Color.white; target.preserveAspect = true;
    }
    public static bool ApplyScene(Scene scene, ResidentSpriteSet set)
    {
        bool changed = false;
        var avatar = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var hud in root.GetComponentsInChildren<SelectionContextHUD>(true))
        {
            if (hud.ResIconImage != null && hud.ResIconImage.sprite != set.Portrait)
            {
                Undo.RecordObject(hud.ResIconImage, "替换居民头像"); SetPortrait(hud.ResIconImage, set.Portrait);
                EditorUtility.SetDirty(hud.ResIconImage);
                if (PrefabUtility.IsPartOfPrefabInstance(hud.ResIconImage)) PrefabUtility.RecordPrefabInstancePropertyModifications(hud.ResIconImage);
                changed = true;
            }
            if (avatar != null && hud.StaffAvatarPrefab != avatar)
            {
                Undo.RecordObject(hud, "使用居民岗位头像预制体"); hud.StaffAvatarPrefab = avatar;
                EditorUtility.SetDirty(hud);
                if (PrefabUtility.IsPartOfPrefabInstance(hud)) PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
                changed = true;
            }
        }
        return changed;
    }
    // Isolated batch entry; ordinary import never opens or replaces the user's current scene.
    public static void BakeProject()
    {
        ImportArtwork();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
        ApplyScene(scene, AssetDatabase.LoadAssetAtPath<ResidentSpriteSet>(SetPath));
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
}

[InitializeOnLoad]
public sealed class ResidentArtReady : AssetPostprocessor
{
    static ResidentArtReady()
    {
        EditorApplication.delayCall += ApplyLoaded;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += ApplyLoaded;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyLoaded;
        };
    }
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
    {
        if (imported.Contains(ResidentArtAuthoring.ReadyPath)) EditorApplication.delayCall += ApplyLoaded;
    }
    private static void ApplyLoaded()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(ResidentArtAuthoring.ReadyPath)) return;
        var set = AssetDatabase.LoadAssetAtPath<ResidentSpriteSet>(ResidentArtAuthoring.SetPath);
        if (set == null) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !scene.path.StartsWith("Assets/Scenes/")) continue;
            bool dirty = scene.isDirty;
            if (!ResidentArtAuthoring.ApplyScene(scene, set)) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!dirty) EditorSceneManager.SaveScene(scene);
        }
    }
}
