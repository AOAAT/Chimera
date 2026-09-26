using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class RosterAndBuildingVisualChecks
{
    private const string Key = "RosterBuildingChecks";
    private static readonly List<string> results = new List<string>();
    private static double started;
    private static int stage;
    private static Dictionary<string, int> identities;
    private static ResidentRosterPanelUI panel;
    private static Color backdrop;
    private static float scrollPosition;
    private static string Output => Path.Combine(Directory.GetCurrentDirectory(), "RosterVisualResults");
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        results.Add("PASS " + message);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started < 4) return;
        try
        {
            if (stage == 0)
            {
                foreach (var hq in Object.FindObjectsOfType<HeadquartersBuilding>()) hq.enabled = false;
                var population = PopulationManager.Instance;
                for (int i = population.TotalResidents.Count; i < 16; i++)
                    population.TotalResidents.Add(new ResidentData("测试居民" + i.ToString("00")));
                ResidentRosterPanelUI.OpenRoster(); panel = ResidentRosterPanelUI.Instance;
                Refresh(); Canvas.ForceUpdateCanvases();
                identities = Rows().ToDictionary(r => r.NameText.text, r => r.GetInstanceID());
                Check(identities.Count == 16, "roster initially displays each resident once");
                var scroll = panel.GetComponentInChildren<ScrollRect>(); scroll.verticalNormalizedPosition = .42f;
                Canvas.ForceUpdateCanvases(); scrollPosition = scroll.verticalNormalizedPosition;
                backdrop = panel.BackdropButton.GetComponent<Image>().color;
                for (int i = 0; i < 100; i++) population.NotifyResidentStateChanged();
                Refresh();
                Check(Rows().All(r => identities[r.NameText.text] == r.GetInstanceID()), "100 repeated population notifications reuse existing row objects");
                Check(Mathf.Abs(scroll.verticalNormalizedPosition - scrollPosition) < .01f, "unchanged rows preserve scroll position");
                int resourceEvents = 0;
                Action countEvent = () => resourceEvents++;
                var resources = GlobalResourceManager.Instance; resources.OnResourceChanged += countEvent;
                for (int i = 0; i < 100; i++) resources.SyncLogisticsTotals(resources.CurrentScrap, resources.CurrentBiomass, resources.CurrentManaStone);
                resources.OnResourceChanged -= countEvent;
                Check(resourceEvents == 0, "unchanged logistics totals do not broadcast resource events");
                int populationEvents = 0;
                countEvent = () => populationEvents++;
                population.OnPopulationChanged += countEvent;
                population.RefreshMaxCapacity(); populationEvents = 0;
                for (int i = 0; i < 100; i++) population.RefreshMaxCapacity();
                population.OnPopulationChanged -= countEvent;
                Check(populationEvents == 0, "unchanged population capacity does not broadcast roster events");
                stage = 1; started = EditorApplication.timeSinceStartup; return;
            }
            Check(panel.BackdropButton.GetComponent<Image>().color == backdrop, "periodic global theme preserves roster backdrop across multiple cycles");
            Check(Rows().All(r => identities[r.NameText.text] == r.GetInstanceID()), "roster stays stable during live logistics and theme updates");
            var newResident = new ResidentData("新增居民"); PopulationManager.Instance.TotalResidents.Add(newResident);
            PopulationManager.Instance.NotifyResidentStateChanged(); Refresh();
            Check(Rows().Count == 17 && Rows().Where(r => identities.ContainsKey(r.NameText.text)).All(r => identities[r.NameText.text] == r.GetInstanceID()), "recruitment adds one row without recreating existing residents");
            PopulationManager.Instance.TotalResidents.Remove(newResident); PopulationManager.Instance.NotifyResidentStateChanged(); Refresh();
            Check(Rows().Count == 16 && Rows().All(r => identities[r.NameText.text] == r.GetInstanceID()), "removal deletes only the departing resident row");
            var data = PopulationManager.Instance.TotalResidents.First(); data.Level = 7;
            PopulationManager.Instance.NotifyResidentStateChanged(); Refresh();
            Check(Rows().First(r => r.NameText.text == data.ResidentName).MetaText.text.Contains("Lv.7"), "retained row updates resident data in place");
            panel.FilterButton.onClick.Invoke();
            panel.FilterButton.onClick.Invoke();
            Check(Rows().Count == 0 && panel.EmptyText.gameObject.activeSelf, "working filter handles an empty result without stale rows");
            panel.FilterButton.onClick.Invoke();
            Check(Rows().Count == 16, "filter restores resident rows");
            CaptureRoster();
            CheckBuildings();
            Finish(0);
        }
        catch (Exception ex) { results.Add("FAIL " + ex); Finish(1); }
    }
    private static List<ResidentRosterRowUI> Rows() => panel.ListContent.GetComponentsInChildren<ResidentRosterRowUI>()
        .Where(r => r != panel.RowTemplate && r.gameObject.activeSelf).ToList();
    private static void Refresh() => typeof(ResidentRosterPanelUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
    private static void CaptureRoster()
    {
        foreach (var t in panel.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("UI");
        var canvas = panel.GetComponent<Canvas>();
        var camera = new GameObject("RosterCapture").AddComponent<Camera>();
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI"); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.04f, .06f, .08f);
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Capture(camera, "resident-roster.png"); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.Destroy(camera.gameObject); panel.Close();
    }
    private static void CheckBuildings()
    {
        var atlas = Resources.Load<Texture2D>("Buildings/ColonyBuildings");
        Check(atlas != null && atlas.isReadable && atlas.filterMode == FilterMode.Point, "atlas imports with point filtering and readable pixels");
        Check(atlas.GetPixel(0, 0).a < .01f && atlas.GetPixel(atlas.width - 10, 10).a < .01f, "atlas padding and unused cell are transparent");
        foreach (var placed in BuildingBase.AllPlacedBuildings.Where(x => x != null && BuildingVisualTheme.GetIcon(x) != null))
            Check(placed.GhostRenderer != null && placed.GhostRenderer.name == "UnifiedBuildingVisual" && placed.BuildingIcon == placed.GhostRenderer.sprite,
                placed.BuildingName + " actual scene building replaces placeholder renderer");
        var warehouse = BuildingBase.AllPlacedBuildings.OfType<WarehouseBuilding>().First();
        var inventory = LogisticsManager.Instance.Get(warehouse.PersistentID);
        var ghost = WarehouseBuilding.Create(warehouse.transform.position); ghost.InitGhostMode();
        Object.DestroyImmediate(ghost.gameObject);
        Check(inventory.Kind == StorageKind.Warehouse, "cancelled overlapping warehouse ghost does not release live warehouse storage");
        var types = new[] { typeof(HeadquartersBuilding), typeof(FactoryBuilding), typeof(AssemblerBuilding), typeof(HousingBuilding), typeof(WarehouseBuilding) };
        var names = new[] { "基地", "工厂", "装配站", "住宅", "仓库" };
        var objects = new List<GameObject>();
        for (int i = 0; i < types.Length; i++)
        {
            GameObject go = new GameObject(names[i]); go.transform.position = new Vector3(1000 + i * 3, 1000, 0);
            var building = (BuildingBase)go.AddComponent(types[i]); building.enabled = false;
            building.FootprintOffsets = new List<Vector2Int> { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
            var oldFootprint = building.FootprintOffsets.ToArray(); var oldGate = building.GetInteractionPoint();
            string id = building.PersistentID;
            BuildingVisualTheme.Apply(building);
            Check(building.BuildingIcon != null && building.GhostRenderer.sprite == building.BuildingIcon, names[i] + " uses same art for map and detail icon");
            Check(building.FootprintOffsets.SequenceEqual(oldFootprint) && building.GetInteractionPoint() == oldGate && building.PersistentID == id, names[i] + " preserves footprint, entrance and persistent ID");
            Check(building.GhostRenderer.bounds.size.x <= 2.01f && building.GhostRenderer.bounds.size.y <= 2.01f, names[i] + " art fits within footprint");
            building.InitGhostMode(); building.UpdateGhostVisual(false);
            Check(building.GhostRenderer.color.a < 1 && building.GhostRenderer.color.r > .9f, names[i] + " placement ghost uses new art and validity tint");
            building.UpdateGhostVisual(true); building.GhostRenderer.color = Color.white;
            var marker = go.transform.Find("EntranceMarker"); if (marker != null) marker.gameObject.SetActive(false);
            objects.Add(go);
        }
        var camera = new GameObject("BuildingGalleryCamera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 2.2f;
        camera.transform.position = new Vector3(1006.5f, 1000.5f, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .17f, .20f);
        Capture(camera, "building-gallery.png", 1920, 600);
        Check(true, "five-building gallery rendered in Unity");
        Object.Destroy(camera.gameObject); foreach (var go in objects) Object.Destroy(go);
    }
    private static void Capture(Camera camera, string name, int width = 1920, int height = 1080)
    {
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(Path.Combine(Output, name), texture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(texture);
    }
    private static void Finish(int code)
    {
        File.WriteAllLines(Path.Combine(Output, "results.txt"), results);
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.Exit(code);
    }
}
