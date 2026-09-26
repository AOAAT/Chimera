using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LogisticsRegressionChecks
{
    private const string Key = "ChimeraLogisticsRegression";
    private static readonly List<string> results = new List<string>();
    private static double started;
    private static int stage;
    private static LogisticsManager manager;
    private static FactoryBuilding factory;
    private static ResidentEntity first, second;
    private static ComponentDataSO definition;
    private static string productID, factoryID, workerID;
    private static float savedScrap;
    private static Vector3 firstStart;
    private static bool moved, savedInTransit;
    private static string Output => Path.Combine(Directory.GetCurrentDirectory(), "LogisticsResults");
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        results.Add("PASS " + message);
        File.WriteAllLines(Path.Combine(Output, "results.txt"), results);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started < (stage == 1 ? .1 : 5)) return;
        try
        {
            if (stage == 0) { Prepare(); stage = 1; started = EditorApplication.timeSinceStartup; return; }
            if (stage == 1)
            {
                moved |= Vector3.Distance(first.transform.position, firstStart) > 1;
                if (!savedInTransit && manager.Data.Jobs.Any(x => x.PickedUp))
                {
                    manager.enabled = false; factory.enabled = false;
                    var snapshot = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(Capture()));
                    Check(snapshot.Version == 4 && snapshot.Logistics.Jobs.Any(x => x.PickedUp), "v4 snapshot includes in-transit jobs");
                    float cargoBefore = Total(LogisticsKeys.Scrap);
                    var definitions = new SaveDefinitionResolver(PlayerInventoryManager.Instance, BuildingManager.Instance);
                    factory.RestoreProductionQueue(snapshot.Buildings.First(x => x.InstanceID == factory.PersistentID).ProductionQueue, definitions);
                    manager.Restore(snapshot.Logistics);
                    first.StopLogisticsMovement(); second.StopLogisticsMovement();
                    Check(Mathf.Approximately(Total(LogisticsKeys.Scrap), cargoBefore), "JSON restore preserves warehouse/backpack/material totals");
                    Check(manager.Data.Jobs.Where(x => x.PickedUp).All(x => manager.Get(LogisticsManager.BagID(x.WorkerID)).Count(x.Key) >= x.Amount), "restored picked jobs refer to real backpack cargo");
                    savedInTransit = true; manager.enabled = true; factory.enabled = true;
                }
                var product = PlayerInventoryManager.Instance.ComponentInventory.FirstOrDefault(x => x.CraftedAtBuildingID == factory.PersistentID);
                if (product != null && manager.ItemAvailable("component:" + product.InstanceID) && factory.TaskQueue.Count == 0)
                {
                    productID = product.InstanceID;
                    Check(moved, "residents physically move along paths without teleporting");
                    Check(savedInTransit, "in-transit save/restore exercised before delivery");
                    Check(Mathf.Abs(Total(LogisticsKeys.Scrap) - 90) < .001f && Mathf.Abs(Total(LogisticsKeys.Biomass) - 20) < .001f, "production consumes exactly one recipe (50 scrap, 10 biomass)");
                    Check(manager.Data.Storages.Sum(x => x.Count("component:" + productID)) == 1, "finished quality component has exactly one physical location");
                    Check(product.CraftSeed != 0 && PlayerInventoryManager.Instance.GetAvailableComponents().Contains(product), "delivered component retains quality and becomes available to assembly");
                    EdgeChecks(); stage = 2; started = EditorApplication.timeSinceStartup;
                    LogisticsPanelUI.Instance.Open(); return;
                }
                if (EditorApplication.timeSinceStartup - started > 100)
                    throw new Exception("Physical delivery timed out: " + manager.WorkerSummary(first.MyData) + " / " + manager.WorkerSummary(second.MyData) + " jobs=" + JsonUtility.ToJson(manager.Data));
                return;
            }
            if (stage == 2)
            {
                Screenshot();
                var snapshot = Capture();
                savedScrap = Total(LogisticsKeys.Scrap); factoryID = factory.PersistentID; workerID = first.MyData.InstanceID;
                // Exercise the actual scene-reload restore pipeline without touching the user's save file.
                stage = 3; started = EditorApplication.timeSinceStartup;
                var routine = (System.Collections.IEnumerator)typeof(SaveGameManager).GetMethod("LoadRoutine", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(SaveGameManager.Instance, new object[] { snapshot });
                SaveGameManager.Instance.StartCoroutine(routine);
                return;
            }
            if (stage == 3)
            {
                if (SaveGameManager.Instance.IsLoading) return;
                manager = LogisticsManager.Instance;
                Check(manager != null && manager.Ready, "full scene reload reinitializes logistics manager");
                Check(Mathf.Abs(Total(LogisticsKeys.Scrap) - savedScrap) < .001f, "full save/load preserves all resource locations and total");
                Check(manager.Data.Storages.Sum(x => x.Count("component:" + productID)) == 1, "full save/load preserves component identity and physical location");
                Check(PopulationManager.Instance.TotalResidents.Any(x => x.InstanceID == workerID && !x.HaulingEnabled && x.CarryCapacity == 10), "full save/load restores resident hauling permissions and capacity");
                Check(BuildingBase.AllPlacedBuildings.OfType<WarehouseBuilding>().Any(x => manager.Get(x.PersistentID) != null), "runtime-created warehouse reconstructed on scene reload");
                Finish(0); return;
            }
        }
        catch (Exception ex)
        {
            results.Add("FAIL " + ex); Finish(1);
        }
    }
    private static void Prepare()
    {
        manager = LogisticsManager.Instance;
        Check(manager != null && manager.Ready && manager.Warehouses.Any(), "scene automatically provides an initial physical warehouse");
        manager.enabled = false;
        foreach (var hq in Object.FindObjectsOfType<HeadquartersBuilding>()) hq.enabled = false;
        foreach (var f in Object.FindObjectsOfType<FactoryBuilding>()) { f.TaskQueue.Clear(); f.enabled = false; }
        factory = BuildingBase.AllPlacedBuildings.OfType<FactoryBuilding>().First();
        factory.TaskQueue.Clear();
        foreach (var store in manager.Data.Storages) store.Cargo.Clear();
        manager.Data.Jobs.Clear();
        PlayerInventoryManager.Instance.ComponentInventory.Clear();
        manager.DepositResources(new ResourceSet(140, 30, 0));
        var warehouse = manager.Warehouses.First();
        Check(GridPathfinder.FindPath(warehouse.Position.ToVector3() + Vector3.up * .4f, warehouse.Position.ToVector3(), false).Last() == warehouse.Position.ToVector3(), "same-cell exact path retains interaction destination");
        Check(GridPathfinder.FindPath(warehouse.Position.ToVector3(), factory.GetInteractionPoint(), false) != null, "warehouse entrance reaches actual scene factory entrance");
        first = Spawn("物流甲", warehouse.Position.ToVector3() + Vector3.up * .4f);
        second = Spawn("物流乙", warehouse.Position.ToVector3() + Vector3.down * .4f);
        firstStart = first.transform.position;
        definition = PlayerInventoryManager.Instance.AllComponentDatabase.First(x => x != null);
        factory.AddToQueue(definition, definition.ComponentName, definition.ComponentIcon, 1, new ResourceSet(50, 10, 0));
        Check(Mathf.Approximately(Total(LogisticsKeys.Scrap), 140), "placing a production order does not consume materials");
        manager.Tick();
        Check(manager.Data.Jobs.Sum(x => x.Amount) <= 60 && manager.Data.Jobs.Select(x => x.ID).Distinct().Count() == manager.Data.Jobs.Count, "multiple workers share unique, bounded material reservations");
        Check(manager.Warehouses.All(s => manager.ReservedOut(s.ID, LogisticsKeys.Scrap) <= s.Count(LogisticsKeys.Scrap)), "reserved source stock never exceeds actual stock");
        manager.enabled = true; factory.enabled = true; Time.timeScale = 3;
    }
    private static ResidentEntity Spawn(string name, Vector3 position)
    {
        var data = new ResidentData(name) { HaulingEnabled = true, CarryCapacity = 10 };
        PopulationManager.Instance.TotalResidents.Add(data);
        return PopulationManager.Instance.SpawnExistingResidentAt(data, position);
    }
    private static float Total(string key) => manager.Data.Storages.Sum(x => x.Count(key));
    private static GameSaveData Capture() => (GameSaveData)typeof(SaveGameManager).GetMethod("CaptureCurrentGame", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(SaveGameManager.Instance, null);
    private static void EdgeChecks()
    {
        manager.enabled = false; factory.enabled = false;
        first.MyData.HaulingEnabled = false; second.MyData.HaulingEnabled = false;
        manager.Interrupt(first); manager.Interrupt(second);
        foreach (var job in manager.Data.Jobs.ToArray()) manager.Data.Jobs.Remove(job);
        float total = Total(LogisticsKeys.Scrap);
        factory.AddToQueue(definition, "取消测试", null, 10, new ResourceSet(15, 0, 0));
        var task = factory.TaskQueue.Last(); manager.Tick();
        Check(manager.Data.Jobs.Any(x => x.OrderID == task.TaskID), "an idle order requests materials without a worker");
        factory.CancelTask(task);
        Check(!manager.Data.Jobs.Any(x => x.OrderID == task.TaskID) && Mathf.Approximately(Total(LogisticsKeys.Scrap), total), "cancel before pickup releases reservations without refund duplication");

        factory.AddToQueue(definition, "运输取消", null, 10, new ResourceSet(15, 0, 0));
        task = factory.TaskQueue.Last(); manager.Tick();
        var pending = manager.Data.Jobs.First(x => x.OrderID == task.TaskID);
        var source = manager.Get(pending.SourceID);
        // Deterministic edge-case setup; the end-to-end check above used real movement.
        source.Remove(pending.Key, 5); manager.EnsureBag(first).Add(pending.Key, 5);
        pending.Amount = 5; pending.PickedUp = true; pending.WorkerID = first.MyData.InstanceID;
        factory.CancelTask(task);
        Check(manager.EnsureBag(first).Count(LogisticsKeys.Scrap) == 5 && Mathf.Approximately(Total(LogisticsKeys.Scrap), total), "cancel after pickup keeps cargo in backpack without duplicating it");
        manager.ReleaseResident(first);
        Check(manager.EnsureBag(first).Used == 0 && manager.Data.Storages.Any(x => x.Kind == StorageKind.Recovery && x.Count(LogisticsKeys.Scrap) == 5), "resident removal preserves a recoverable cargo container");

        var output = manager.EnsureOutput(factory);
        output.Capacity = 1;
        var testProduct = ComponentQualityGenerator.Create(definition, 1, 231, 1, new List<string>(), factory.PersistentID);
        PlayerInventoryManager.Instance.AddComponentInstance(testProduct, false);
        output.Add("component:" + testProduct.InstanceID, 1);
        Check(!PlayerInventoryManager.Instance.TryEquipComponent(testProduct, "test-unit"), "assembly cannot take a component waiting at factory output");
        factory.AddToQueue(definition, "满载测试", null, 10, new ResourceSet(0, 0, 0)); task = factory.TaskQueue.Last();
        Check(!manager.TryStartProduction(factory, task) && !task.MaterialsConsumed, "full output buffer blocks starting another product");
        factory.CancelTask(task);
        foreach (var warehouse in manager.Warehouses) warehouse.Capacity = warehouse.Used;
        manager.Tick();
        Check(!manager.Data.Jobs.Any(x => x.SourceID == output.ID), "full warehouses do not accept output reservations");
        foreach (var warehouse in manager.Warehouses) warehouse.Capacity += 100;
        manager.Tick();
        Check(manager.Data.Jobs.Any(x => x.SourceID == output.ID), "output hauling resumes when storage capacity is available");
        Check(manager.Warehouses.All(s => s.Used + manager.ReservedIn(s.ID) <= s.Capacity), "incoming jobs reserve destination capacity");

        var product = PlayerInventoryManager.Instance.GetComponentInstance(productID);
        Check(PlayerInventoryManager.Instance.TryEquipComponent(product, "test-unit"), "warehouse component can be equipped");
        Check(manager.Locate("component:" + productID) == null, "equipping removes physical warehouse location");
        PlayerInventoryManager.Instance.ReleaseComponent(product);
        Check(manager.Data.Storages.Sum(x => x.Count("component:" + productID)) == 1, "unequipping returns the same permanent instance once");
        PlayerInventoryManager.Instance.ReleaseComponent(product);
        Check(manager.Data.Storages.Sum(x => x.Count("component:" + productID)) == 1, "repeated release does not duplicate component");

        var chassis = PlayerInventoryManager.Instance.AllChassisDatabase.First(x => x != null);
        PlayerInventoryManager.Instance.AddChassisToWarehouse(chassis, 1);
        Check(PlayerInventoryManager.Instance.TryConsumeChassisFromWarehouse(chassis) &&
            !PlayerInventoryManager.Instance.TryConsumeChassisFromWarehouse(chassis), "chassis consumption cannot reuse a stale warehouse selection");
        var legacy = new GameSaveData { Version = 3, SceneName = "RTS_World_Master" };
        typeof(SaveGameManager).GetMethod("ValidateAndNormalize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { legacy });
        Check(legacy.Version == 4 && legacy.Logistics == null, "version 3 saves migrate to physical-storage initialization");
        factory.AddToQueue(definition, "旧订单", null, 10, new ResourceSet(7, 0, 0)); task = factory.TaskQueue.Last(); task.UsesLogistics = false;
        var input = manager.EnsureInput(factory, task); manager.EnsureInput(factory, task);
        Check(input.Count(LogisticsKeys.Scrap) == 7 && task.UsesLogistics, "legacy paid order receives material exactly once");
        factory.CancelTask(task);
        // Blocked source must back off instead of attempting pathfinding every tick.
        factory.AddToQueue(definition, "阻路测试", null, 10, new ResourceSet(5, 0, 0)); task = factory.TaskQueue.Last();
        var gate = manager.Warehouses.First().Position.ToVector3();
        var grid = RTSGridSystem.Instance; var index = grid.WorldToGrid(gate); var cell = grid.GetCell(index.x, index.y);
        bool occupied = cell.IsOccupied;
        first.StopLogisticsMovement(); first.transform.position = factory.GetInteractionPoint();
        first.LogisticsHoldUntil = 0; first.MyData.HaulingEnabled = true;
        try
        {
            cell.IsOccupied = true; manager.Tick();
            Check(manager.Data.Jobs.Any(x => x.OrderID == task.TaskID && x.RetryAt > Time.time && string.IsNullOrEmpty(x.WorkerID)), "unreachable pickup releases worker and applies retry cooldown");
        }
        finally { cell.IsOccupied = occupied; first.MyData.HaulingEnabled = false; factory.CancelTask(task); }
        var snapshot = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(Capture()));
        Check(snapshot.Residents.Any(x => x.InstanceID == first.MyData.InstanceID && x.CarryCapacity == 10), "backpack capacity and work permissions participate in full game snapshots");
    }
    private static void Screenshot()
    {
        var canvas = LogisticsPanelUI.Instance.GetComponent<Canvas>();
        Canvas.ForceUpdateCanvases();
        var cameraObject = new GameObject("LogisticsCaptureCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .07f, .10f);
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        var rt = new RenderTexture(1920, 1080, 24); camera.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Output, "logistics-dashboard.png"), image.EncodeToPNG());
        Check(image.GetPixels32().Count(p => p.r > 20 && p.g > 30 && p.b > 40) > 100000, "dashboard screenshot contains visible panel content");
        RenderTexture.active = previous; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.Destroy(image); Object.Destroy(cameraObject); rt.Release(); Object.Destroy(rt);
        Check(true, "generated logistics dashboard rendered to 1920x1080 screenshot");
    }
    private static void Finish(int code)
    {
        File.WriteAllLines(Path.Combine(Output, "results.txt"), results);
        EditorApplication.update -= Tick; SessionState.SetBool(Key, false); EditorApplication.Exit(code);
    }
}
