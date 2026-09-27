using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WorldPixelChecks
{
    const string Key = "WorldPixelChecks", Output = "WorldPixelResults";
    static int stage;
    static double started, next;
    static MechUnit2D mech;
    static ResidentEntity resident;
    static FactoryBuilding factory;
    static Vector3 destination;
    static ChassisDataSO chassis;
    static ComponentDataSO[] definitions;
    static T Find<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.FindAssets("t:" + typeof(T).Name)
        .Select(AssetDatabase.GUIDToAssetPath).Single(p => Path.GetFileNameWithoutExtension(p) == name));
    static void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); File.AppendAllText(Output + "/checks.txt", "PASS " + message + "\n"); }
    public static void Run() => RunChecks(true);
    public static void RunInstalled() => RunChecks(false);
    static void RunChecks(bool bake)
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/checks.txt", "");
        try
        {
            var components = AssetDatabase.FindAssets("t:ComponentDataSO", new[] { "Assets/Data/Mechs" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ComponentDataSO>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var oldRefs = components.Select(c => c.ComponentIcon).ToArray();
            if (bake) WorldPixelArtAuthoring.BakeProject();
            else EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
            Check(components.All(c => c.ComponentIcon == null || c.ComponentIcon.pixelsPerUnit == 50 && c.VisualScaleMultiplier == 1), "all component definitions use 50 PPU with scale one");
            if (bake) Check(components.Select(c => c.ComponentIcon).SequenceEqual(oldRefs), "re-running authoring preserves migrated component references");
            var set = Resources.Load<ResidentSpriteSet>("Residents/WorkerSprites");
            Check(set.Idle.Concat(set.Carry).All(s => s.rect.size == new Vector2(48, 48) && s.pixelsPerUnit == 50), "resident has eight native 48x48 frames at 50 PPU");
            var grid = Object.FindObjectOfType<RTSGridSystem>();
            Check(grid.MapWidth == 64 && grid.MapHeight == 64 && grid.CellSize == 1 && grid.GridOrigin == new Vector2(-31.5f, -31.5f), "map dimensions and origin remain unchanged");
            var visuals = grid.GetComponent<RTSMapVisuals>();
            Check(visuals.BaseTileSprite.rect.size == new Vector2(50, 50) && visuals.BaseTileSprite.pixelsPerUnit == 50 && visuals.PreviewMatches(grid), "50-pixel map tiles are baked in edit mode");
            Check(Object.FindObjectsOfType<BuildingBase>().Where(b => b.UseUnifiedBuildingArt).All(b => {
                var sr = b.transform.Find("UnifiedBuildingVisual").GetComponent<SpriteRenderer>();
                return sr.sprite.pixelsPerUnit == 50 && sr.transform.lossyScale == Vector3.one;
            }), "scene buildings use native density before Play");
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { File.AppendAllText(Output + "/checks.txt", "FAIL " + e + "\n"); EditorApplication.Exit(1); }
    }
    [InitializeOnLoadMethod] static void Resume()
    { if (!SessionState.GetBool(Key, false)) return; started = EditorApplication.timeSinceStartup; next = started + 5; EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .4;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Timed out stage " + stage);
            switch (stage++)
            {
                case 0:
                    if (LogisticsManager.Instance == null || !LogisticsManager.Instance.Ready) { stage--; return; }
                    LogisticsManager.Instance.enabled = false;
                    foreach (var h in Object.FindObjectsOfType<HeadquartersBuilding>()) h.enabled = false;
                    foreach (var f in Object.FindObjectsOfType<FactoryBuilding>()) f.enabled = false;
                    chassis = Find<ChassisDataSO>("CH1_禁军");
                    definitions = new[] { Find<ComponentDataSO>("CORE1_陷阵核心"), Find<ComponentDataSO>("SUP1_厚实护甲"),
                        Find<ComponentDataSO>("WPN4_狙击枪"), Find<ComponentDataSO>("WPN5_链锯"), Find<ComponentDataSO>("MOVE3_刀锋") };
                    var profile = new SavedUnitProfile(new InstancedChassis(chassis), "50PPU 验证机甲");
                    for (int i = 0; i < definitions.Length; i++)
                    {
                        var instance = new InstancedComponent(definitions[i], 2); PlayerInventoryManager.Instance.ComponentInventory.Add(instance);
                        profile.SlotIndices.Add(i); profile.EquippedComponentIDs.Add(instance.InstanceID);
                    }
                    var grid = RTSGridSystem.Instance;
                    float distance = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1;
                    if (CombatSandbox.Instance != null) CombatSandbox.Instance.DistanceMultiplier = 3;
                    mech = Object.FindObjectOfType<AssemblerBuilding>().SpawnMechAt(profile, grid.GetCell(3, 4).WorldPos, false);
                    if (CombatSandbox.Instance != null) CombatSandbox.Instance.DistanceMultiplier = distance;
                    foreach (var w in mech.GetComponentsInChildren<WeaponModule>()) w.enabled = false;
                    Check(mech.GlobalBattleScale == 1 && mech.transform.lossyScale == Vector3.one, "deployed mech uses scale one");
                    var body = mech.VisualRoot.Find("Visual_ChassisBase");
                    Check(body.GetComponent<BoxCollider2D>().size == (Vector2)chassis.ChassisSprite.bounds.size * .9f, "chassis hitbox follows the enlarged body");
                    Check(mech.GetComponent<CircleCollider2D>().radius * mech.transform.lossyScale.x < .5f, "movement collision still fits a one-cell corridor");
                    for (int i = 0; i < definitions.Length; i++)
                    {
                        var hinge = body.Find("Socket_" + chassis.Sockets[i].SlotName).GetChild(0);
                        var sr = hinge.GetComponentInChildren<SpriteRenderer>();
                        Check(sr.sprite.pixelsPerUnit == 50 && sr.transform.lossyScale == Vector3.one, "component " + i + " uses native world density");
                        Check(sr.sortingOrder == mech.BaseSortingOrder + WorldPixelMetrics.ComponentOrder(i), "component " + i + " has stable draw order");
                    }
                    var gunHinge = body.Find("Socket_" + chassis.Sockets[2].SlotName).GetChild(0);
                    var muzzle = gunHinge.Find("MuzzlePoint");
                    Check(Vector2.Distance(muzzle.localPosition, definitions[2].MuzzleOffset) < .00001f && muzzle.localPosition.x > 1,
                        "sniper muzzle is at the barrel and independent of combat range multiplier");
                    foreach (float angle in new[] { 0f, 22.5f, 45f, 90f, 180f, 270f })
                    {
                        gunHinge.localRotation = Quaternion.Euler(0, 0, angle);
                        var spriteTransform = gunHinge.Find("Visual_VisualSprite");
                        Vector3 barrel = spriteTransform.TransformPoint((new Vector2(91, 26.5f) - definitions[2].ComponentIcon.pivot) / 50);
                        Check(Vector3.Distance(muzzle.position, barrel) < .00001f, "muzzle follows drawn barrel at " + angle + " degrees");
                    }
                    gunHinge.localRotation = Quaternion.Euler(0, 0, definitions[2].BaseRotationOffset);
                    destination = grid.GetCell(7, 4).WorldPos; mech.GetComponent<ChimeraAIController>().SetManualMovePoint(destination);
                    break;
                case 1:
                    if (Vector2.Distance(mech.transform.position, destination) > .4f) { stage--; return; }
                    Check(true, "enlarged mech follows a real grid path and reaches its destination");
                    mech.GetComponent<ChimeraAIController>().ClearMoveCommand();
                    factory = Object.FindObjectOfType<FactoryBuilding>();
                    var identity = new ResidentData("像素规格测试居民"); PopulationManager.Instance.TotalResidents.Add(identity);
                    resident = PopulationManager.Instance.SpawnExistingResidentAt(identity, factory.transform.position + new Vector3(-1, -2, 0));
                    resident.enabled = false; resident.SetSelected(true);
                    mech.transform.position = factory.transform.position + new Vector3(2, -2, 0);
                    mech.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                    AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(mech); break;
                case 2:
                    var workshop = AssemblyWorkshopUI.Instance;
                    var scaler = workshop.ChassisVisualRoot.Find("UI_ScalerRoot");
                    var chassisUI = scaler.Find("UI_ChassisBase").GetComponent<Image>();
                    Check(Vector2.Distance(chassisUI.rectTransform.sizeDelta, (Vector2)chassis.ChassisSprite.bounds.size * 100) < .001f, "assembly preview uses explicit world-to-UI sizing");
                    var oldReference = workshop.GetComponentInParent<Canvas>().referencePixelsPerUnit;
                    workshop.GetComponentInParent<Canvas>().referencePixelsPerUnit = 400;
                    WorldPixelMetrics.SizePreview(chassisUI, 100);
                    Check(Vector2.Distance(chassisUI.rectTransform.sizeDelta, (Vector2)chassis.ChassisSprite.bounds.size * 100) < .001f, "assembly dimensions remain stable with a different canvas reference PPU");
                    workshop.GetComponentInParent<Canvas>().referencePixelsPerUnit = oldReference;
                    var viewport = workshop.ChassisVisualRoot as RectTransform;
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(workshop.ChassisVisualRoot, scaler);
                    Check(bounds.size.x <= viewport.rect.width + .01f && bounds.size.y <= viewport.rect.height + .01f, "fully assembled preview fits inside its viewport");
                    IvoryThemeChecks.Capture(workshop.GetComponentInParent<Canvas>(), "pixel50-assembly.png");
                    workshop.CancelAndExitWorkshop();
                    CaptureWorld(factory.transform.position + new Vector3(.5f, -1, 0));
                    File.AppendAllText(Output + "/checks.txt", "COMPLETE\n"); Finish(0); break;
            }
        }
        catch (Exception e) { File.AppendAllText(Output + "/checks.txt", "FAIL " + e + "\n"); Debug.LogException(e); Finish(1); }
    }
    static void CaptureWorld(Vector3 centre)
    {
        var cam = new GameObject("Pixel50Capture").AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 4;
        cam.transform.position = centre + new Vector3(0, 0, -10); cam.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.2f, .24f, .25f); cam.allowMSAA = false;
        var rt = new RenderTexture(1200, 800, 24); cam.targetTexture = rt; cam.Render();
        var old = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply(); File.WriteAllBytes(Output + "/pixel50-in-game.png", image.EncodeToPNG());
        RenderTexture.active = old; cam.targetTexture = null; rt.Release(); Object.Destroy(image); Object.Destroy(rt); Object.Destroy(cam.gameObject);
    }
    static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.Exit(code); }
}
