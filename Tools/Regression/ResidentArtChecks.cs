using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ResidentArtChecks
{
    const string Key = "ChimeraResidentArtChecks";
    const string Output = "ResidentArtResults";
    static int stage;
    static double next, started;
    static ResidentEntity resident;
    static ResidentData identity;
    static Vector3 target;
    static FactoryBuilding factory;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        File.AppendAllText(Output + "/checks.txt", "PASS " + message + "\n");
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/checks.txt", "");
        try
        {
            ResidentArtAuthoring.BakeProject();
            var set = AssetDatabase.LoadAssetAtPath<ResidentSpriteSet>(ResidentArtAuthoring.SetPath);
            Check(set != null && set.Idle.Length == 4 && set.Carry.Length == 4, "sprite set has four idle and four carrying directions");
            var sprites = set.Idle.Concat(set.Carry).ToArray();
            Check(sprites.All(s => s != null && s.rect.size == new Vector2(48, 48) && s.pixelsPerUnit == 50 && s.pivot == new Vector2(24, 2)), "all frames are 48x48 with consistent foot pivot and 50 PPU");
            var importer = (TextureImporter)AssetImporter.GetAtPath(ResidentArtAuthoring.AtlasPath);
            Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed, "pixel art is point-filtered without mipmaps or compression");
            var ids = sprites.Select(s => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long local); return guid + ":" + local; }).ToArray();
            ResidentArtAuthoring.ImportArtwork();
            set = AssetDatabase.LoadAssetAtPath<ResidentSpriteSet>(ResidentArtAuthoring.SetPath);
            var nextIDs = set.Idle.Concat(set.Carry).Select(s => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long local); return guid + ":" + local; }).ToArray();
            Check(ids.SequenceEqual(nextIDs), "reimport preserves sprite references and file IDs");
            var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(ResidentArtAuthoring.AtlasPath));
            Check(texture.width == 192 && texture.height == 96, "atlas is native 192x96 pixels, not the enlarged concept sheet");
            var pixels = texture.GetPixels32();
            Check(pixels.All(p => p.a == 0 || p.a == 255), "sprite edges have clean binary alpha");
            for (int frame = 0; frame < 8; frame++)
            {
                int left = frame % 4 * 48, bottom = (1 - frame / 4) * 48;
                int minX = 48, minY = 48, maxX = -1, maxY = -1;
                for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
                    if (pixels[(bottom + y) * 192 + left + x].a > 0) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                Check(maxX >= minX && maxX - minX + 1 <= 36 && maxY - minY + 1 <= 44 && minY == 2 && maxY <= 45, "frame " + frame + " fits one tile with aligned feet and transparent margins");
            }
            var preview = new Texture2D(1536, 768, TextureFormat.RGBA32, false);
            var previewPixels = new Color32[1536 * 768];
            for (int y = 0; y < 768; y++) for (int x = 0; x < 1536; x++) previewPixels[y * 1536 + x] = pixels[(y / 8) * 192 + x / 8];
            preview.SetPixels32(previewPixels); preview.Apply(); File.WriteAllBytes(Output + "/sprite-sheet-8x.png", preview.EncodeToPNG());
            Object.DestroyImmediate(texture); Object.DestroyImmediate(preview);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResidentArtAuthoring.PrefabPath);
            var visual = prefab.GetComponent<ResidentVisual2D>();
            Check(visual != null && visual.Body.sprite == set.Portrait && visual.Body.color == Color.white, "prefab already shows new art before Play");
            Check(prefab.GetComponent<CircleCollider2D>().radius == .2f && prefab.transform.Find("Hitbox").GetComponent<CircleCollider2D>().radius == .4f, "movement and selection colliders are unchanged");
            var hud = Object.FindObjectOfType<SelectionContextHUD>(true);
            Check(hud.ResIconImage.sprite == set.Portrait, "scene resident portrait is serialized before Play");
            File.AppendAllText(Output + "/checks.txt", "EDITOR COMPLETE\n");
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        catch (Exception error) { File.AppendAllText(Output + "/checks.txt", "FAIL " + error + "\n"); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup; next = started + 5; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .35;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 80) throw new Exception("Resident visual test timed out");
            var manager = LogisticsManager.Instance;
            switch (stage++)
            {
                case 0:
                    if (manager == null || !manager.Ready) { stage--; return; }
                    manager.enabled = false;
                    foreach (var building in Object.FindObjectsOfType<HeadquartersBuilding>()) building.enabled = false;
                    foreach (var building in Object.FindObjectsOfType<FactoryBuilding>()) building.enabled = false;
                    factory = Object.FindObjectOfType<FactoryBuilding>();
                    identity = new ResidentData("新居民美术测试") { CurrentHP = 47, HaulingEnabled = true };
                    PopulationManager.Instance.TotalResidents.Add(identity);
                    var grid = RTSGridSystem.Instance;
                    var start = grid.GetCell(3, 3).WorldPos; target = grid.GetCell(6, 3).WorldPos;
                    resident = PopulationManager.Instance.SpawnExistingResidentAt(identity, start);
                    resident.enabled = false;
                    break;
                case 1:
                    var art = resident.GetComponent<ResidentVisual2D>();
                    string snapshot = JsonUtility.ToJson(identity) + JsonUtility.ToJson(manager.Data);
                    var physics = resident.GetComponent<Rigidbody2D>();
                    var directions = new[] { Vector2.down, Vector2.left, Vector2.right, Vector2.up };
                    for (int i = 0; i < directions.Length; i++)
                    {
                        physics.velocity = directions[i]; art.RefreshVisual();
                        Check(art.Facing == (ResidentFacing)i && art.Body.sprite == art.Sprites.Idle[i] && !art.Body.flipX, "velocity selects independent direction " + i + " without legacy flipping");
                        Check(art.Body.bounds.size.x <= gridSize() + .001f && art.Body.bounds.size.y <= gridSize() + .001f && Vector2.Distance(art.Body.bounds.center, resident.transform.position) < .001f, "full frame remains centred within one cell in direction " + i);
                    }
                    physics.velocity = Vector2.zero; art.RefreshVisual();
                    Check(art.Facing == ResidentFacing.Back, "stopping preserves the last facing");
                    Check(snapshot == JsonUtility.ToJson(identity) + JsonUtility.ToJson(manager.Data), "visual updates do not mutate resident or logistics data");
                    float old = RTSGridSystem.Instance.CellSize; RTSGridSystem.Instance.CellSize = 2; art.RefreshVisual();
                    Check(Mathf.Abs(art.Body.bounds.size.x - .96f) < .001f && art.Body.bounds.size.y <= 2.001f && resident.transform.localScale == Vector3.one, "non-default cell size preserves native pixel density and root physics");
                    RTSGridSystem.Instance.CellSize = old; art.RefreshVisual();
                    manager.EnsureBag(resident).Add(LogisticsKeys.Scrap, 5); break;
                case 2:
                    var carrying = resident.GetComponent<ResidentVisual2D>(); carrying.RefreshVisual();
                    Check(carrying.IsCarrying && carrying.Body.sprite == carrying.Sprites.Carry[(int)carrying.Facing], "actual backpack cargo activates carrying pose");
                    manager.Interrupt(resident);
                    carrying.RefreshVisual();
                    Check(carrying.IsCarrying, "interrupted order keeps carrying pose while cargo remains in backpack");
                    manager.Get(LogisticsManager.BagID(identity.InstanceID)).Cargo.Clear(); break;
                case 3:
                    var idle = resident.GetComponent<ResidentVisual2D>(); idle.RefreshVisual();
                    Check(!idle.IsCarrying && idle.Body.sprite == idle.Sprites.Idle[(int)idle.Facing], "empty backpack restores idle pose");
                    resident.enabled = true; resident.SetDestination(target); break;
                case 4:
                    if (Vector2.Distance(resident.transform.position, target) > .3f) { stage--; return; }
                    Check(resident.GetComponent<ResidentVisual2D>().Facing == ResidentFacing.Right, "real path movement changes facing and reaches destination");
                    resident.SetSelected(true);
                    Check(resident.SelectionCircle.activeSelf && Physics2D.OverlapPoint(resident.transform.position, LayerMask.GetMask("Resident")) != null, "resident remains selectable with visible selection marker");
                    SelectionContextHUD.Instance.Refresh(resident);
                    Check(SelectionContextHUD.Instance.ResIconImage.sprite == resident.GetComponent<ResidentVisual2D>().Sprites.Portrait, "resident HUD uses the new portrait");
                    resident.transform.position = factory.GetInteractionPoint();
                    resident.OrderGarrison(factory); break;
                case 5:
                    if (resident != null) { stage--; return; }
                    Check(factory.GetStaffList().Contains(identity) && identity.Status == ResidentStatus.Working, "new art does not interfere with entering a workplace");
                    SelectionContextHUD.Instance.Refresh(factory); SelectionContextHUD.Instance.OnClickStaffToggle();
                    var portraits = SelectionContextHUD.Instance.StaffListContainer.GetComponentsInChildren<UnityEngine.UI.Image>().Where(x => x.name == "Avatar_Icon" || x.name == "Icon").ToArray();
                    Check(portraits.Length == factory.GetStaffList().Count && portraits.All(x => x.sprite != null), "staff cards show new portraits only for occupied slots");
                    factory.RemoveStaff(identity); break;
                case 6:
                    resident = ResidentEntity.ActiveResidents.FirstOrDefault(x => x.MyData?.InstanceID == identity.InstanceID);
                    if (resident == null) { stage--; return; }
                    Check(resident.GetComponent<ResidentVisual2D>()?.Sprites != null && identity.CurrentHP == 47, "dismissal recreates the same resident with new art and preserved health");
                    resident.SetDestination(resident.transform.position);
                    resident.SetSelected(true); SelectionContextHUD.Instance.Refresh(resident); break;
                case 7:
                    Check(resident.SelectionCircle.GetComponent<LineRenderer>()?.enabled == true && resident.SelectionCircle.GetComponent<SpriteRenderer>() == null, "selected resident uses a foot outline instead of the old heart sprite");
                    CaptureWorld(factory.transform.position, "resident-in-game.png");
                    IvoryThemeChecks.Capture(SelectionContextHUD.Instance.GetComponentInParent<Canvas>().rootCanvas, "resident-new-portrait.png");
                    File.AppendAllText(Output + "/checks.txt", "COMPLETE\n"); Finish(0); break;
            }
        }
        catch (Exception error) { File.AppendAllText(Output + "/checks.txt", "FAIL " + error + "\n"); Debug.LogException(error); Finish(1); }
    }
    static float gridSize() => RTSGridSystem.Instance.CellSize;
    static void CaptureWorld(Vector3 centre, string name)
    {
        var camera = new GameObject("ResidentArtCapture").AddComponent<Camera>();
        camera.transform.position = new Vector3(centre.x, centre.y, -10); camera.orthographic = true; camera.orthographicSize = 4;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.23f, .27f, .30f);
        camera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
        var rt = new RenderTexture(1024, 768, 24); camera.targetTexture = rt;
        camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1024, 768, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); image.Apply();
        File.WriteAllBytes(Output + "/" + name, image.EncodeToPNG()); RenderTexture.active = old;
        camera.targetTexture = null; rt.Release(); Object.Destroy(image); Object.Destroy(rt); Object.Destroy(camera.gameObject);
    }
    static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.Exit(code); }
}
