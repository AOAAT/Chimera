using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class EditorVisualAssetChecks
{
    private const string Key = "EditorVisualAssetChecks";
    private static string Output => Path.Combine(Directory.GetCurrentDirectory(), "EditorVisualResults");
    private static double started;
    private static int stage;
    private static readonly Color CustomColor = new Color(.32f, .55f, .73f, 1);
    private static void Check(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        File.AppendAllText(Path.Combine(Output, "results.txt"), "PASS " + text + "\n");
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "results.txt"), "");
        try
        {
            Check(!EditorApplication.isPlaying, "checks begin in Edit mode");
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Buildings/ColonyBuildings.png").OfType<Sprite>().ToArray();
            Check(sprites.Length == 5 && sprites.All(EditorUtility.IsPersistent), "all five building sprites are persistent imported sub-assets");
            foreach (string path in Directory.GetFiles("Assets/Prefabs/建筑物预制体", "*.prefab").Concat(new[] { "Assets/Resources/Buildings/Warehouse.prefab" }))
            {
                var go = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var building = go.GetComponent<BuildingBase>();
                    Check(building.GhostRenderer != null && building.GhostRenderer.name == "UnifiedBuildingVisual" &&
                        EditorUtility.IsPersistent(building.GhostRenderer.sprite), Path.GetFileName(path) + " reopens with saved building art");
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
            var buildings = Object.FindObjectsOfType<BuildingBase>(true);
            Check(buildings.Length >= 4 && buildings.All(x => x.GhostRenderer.name == "UnifiedBuildingVisual" && EditorUtility.IsPersistent(x.BuildingIcon)),
                "reopened gameplay scene displays the new art without Play mode");
            var warehouse = Object.FindObjectOfType<GlobalWarehouseUI>(true);
            Check(warehouse.transform.Find("WarehouseWindow") != null && Field<bool>(warehouse, "built"), "warehouse structure and references persist in scene");
            Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/GlobalWarehousePanel.prefab").GetComponentInChildren<ScrollRect>(true) != null,
                "warehouse is also available as an editable prefab");
            var logistics = Object.FindObjectOfType<LogisticsPanelUI>(true);
            Check(logistics != null && logistics.transform.Find("LogisticsWindow") != null, "logistics window exists in Edit mode");
            Check(Object.FindObjectOfType<ResidentRosterPanelUI>(true) != null, "resident roster exists in scene for editor inspection");
            var hud = Object.FindObjectOfType<SelectionContextHUD>(true);
            int children = warehouse.GetComponentsInChildren<Transform>(true).Length + logistics.GetComponentsInChildren<Transform>(true).Length + hud.GetComponentsInChildren<Transform>(true).Length;
            warehouse.PrepareEditorView(); warehouse.PrepareEditorView(); logistics.PrepareEditorView(); logistics.PrepareEditorView(); hud.PrepareEditorLayout(); hud.PrepareEditorLayout();
            Check(children == warehouse.GetComponentsInChildren<Transform>(true).Length + logistics.GetComponentsInChildren<Transform>(true).Length + hud.GetComponentsInChildren<Transform>(true).Length,
                "repeated authoring calls do not duplicate windows, buttons or HP labels");
            int rootCount = scene.rootCount;
            ChimeraVisualAuthoring.UpgradeScene(scene);
            Check(rootCount == scene.rootCount, "scene migration is idempotent");
            Check(hud.ResHPBar.transform.Find("HP_Value") != null, "resident health label exists before Play mode");
            CaptureWarehouse(warehouse);
            var factory = buildings.OfType<FactoryBuilding>().First();
            factory.GhostRenderer.color = CustomColor;
            factory.GhostRenderer.transform.localScale = Vector3.one * .123f;
            Field<Button>(warehouse, "closeButton").GetComponent<Image>().color = CustomColor;
            hud.ResStatusText.rectTransform.sizeDelta = new Vector2(555, 99);
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        catch (Exception ex) { Fail(ex); }
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started < 5) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 90) throw new Exception("Play mode checks timed out");
            var warehouse = Object.FindObjectOfType<GlobalWarehouseUI>(true);
            var factory = Object.FindObjectOfType<FactoryBuilding>();
            if (stage == 0)
            {
                Check(factory.GhostRenderer.color == CustomColor && factory.GhostRenderer.transform.localScale == Vector3.one * .123f,
                    "Play mode preserves editor changes to building tint and scale");
                Check(Object.FindObjectOfType<SelectionContextHUD>(true).ResStatusText.rectTransform.sizeDelta == new Vector2(555, 99),
                    "Play mode preserves editor changes to resident UI layout");
                Check(Object.FindObjectsOfType<LogisticsPanelUI>(true).Length == 1, "logistics bootstrap reuses authored panel");
                warehouse.OpenWarehouse();
                Check(warehouse.gameObject.activeSelf, "authored warehouse opens");
                Field<Button>(warehouse, "closeButton").onClick.Invoke();
                Check(!warehouse.gameObject.activeSelf, "deserialized close button is bound and works");
                warehouse.OpenWarehouse(); warehouse.CloseWarehouse(); warehouse.OpenWarehouse();
                Check(warehouse.transform.Cast<Transform>().Count(x => x.name == "WarehouseWindow") == 1, "reopening warehouse never generates another window");
                warehouse.MainCategoryDropdown.value = 1;
                Check(!warehouse.TypeDropdown.interactable, "deserialized dropdown callbacks still update filters");
                warehouse.MainCategoryDropdown.value = 0;
                Check(warehouse.TypeDropdown.interactable, "component filter re-enables after switching category");
                LogisticsPanelUI.Instance.Open();
                var window = LogisticsPanelUI.Instance.transform.Find("LogisticsWindow");
                window.Find("货物位置").GetComponent<Button>().onClick.Invoke();
                Check(Field<int>(LogisticsPanelUI.Instance, "tab") == 2, "authored logistics tabs are bound");
                window.Find("关闭 ×").GetComponent<Button>().onClick.Invoke();
                Check(!window.gameObject.activeSelf, "authored logistics close action works");
                stage = 1; started = EditorApplication.timeSinceStartup; return;
            }
            Check(Field<Button>(warehouse, "closeButton").GetComponent<Image>().color == CustomColor,
                "editor-authored UI color survives multiple former theme refresh intervals");
            Check(Object.FindObjectsOfType<ChimeraUIThemeController>().Length == 0, "no periodic runtime theme controller is installed");
            Finish(0);
        }
        catch (Exception ex) { Fail(ex); }
    }
    private static void CaptureWarehouse(GlobalWarehouseUI warehouse)
    {
        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true)) canvas.enabled = false;
        var view = warehouse.GetComponent<Canvas>(); warehouse.gameObject.SetActive(true); view.enabled = true;
        var camera = new GameObject("EditModeCapture").AddComponent<Camera>();
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI"); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        view.renderMode = RenderMode.ScreenSpaceCamera; view.worldCamera = camera; view.planeDistance = 1;
        var rt = new RenderTexture(1920, 1080, 24); camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases(); camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(Output, "warehouse-edit-mode.png"), texture.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null; rt.Release();
        Object.DestroyImmediate(rt); Object.DestroyImmediate(texture); Object.DestroyImmediate(camera.gameObject);
        view.renderMode = RenderMode.ScreenSpaceOverlay; view.worldCamera = null; warehouse.gameObject.SetActive(false);
        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true)) canvas.enabled = true;
        Check(!EditorApplication.isPlaying, "warehouse preview rendered entirely in Edit mode");
    }
    private static void Fail(Exception ex) { File.AppendAllText(Path.Combine(Output, "results.txt"), "FAIL " + ex); Finish(1); }
    private static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.Exit(code); }
}
