using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

public static class WarehouseRegressionChecks
{
    private static double started;
    private static int stage;
    private static List<string> componentIDs;
    private static readonly List<string> results = new List<string>();
    private const string Key = "WarehouseRegressionRunning";
    private static string Output => Path.Combine(Directory.GetCurrentDirectory(), "RegressionResults");
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
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started < 4) return;
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key, false);
        int exit = 0;
        try
        {
            if (stage == 0)
            {
                PathChecks();
                StaffChecks();
                PrepareWarehouse();
                // 让运行时生成的 TMP 控件完成 Start 后，再模拟菜单交互。
                stage = 1;
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
                SessionState.SetBool(Key, true);
                return;
            }
            WarehouseChecks();
            File.WriteAllLines(Path.Combine(Output, "results.txt"), results);
        }
        catch (Exception ex)
        {
            exit = 1;
            File.WriteAllLines(Path.Combine(Output, "results.txt"), results.Concat(new[] { "FAIL " + ex }));
        }
        EditorApplication.Exit(exit);
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        results.Add("PASS " + label);
    }
    private static void PathChecks()
    {
        RTSGridSystem original = RTSGridSystem.Instance;
        RTSGridSystem.Instance = null;
        var go = new GameObject("RegressionGrid");
        go.SetActive(false);
        var grid = go.AddComponent<RTSGridSystem>();
        grid.MapWidth = 12; grid.MapHeight = 12; grid.CellSize = 1; grid.GridOrigin = Vector2.zero;
        go.SetActive(true);
        try
        {
            var watch = Stopwatch.StartNew();
            // 原实现可能卡死的反例：平滑路径时所有后续线段均被阻挡。
            grid.GetCell(1, 0).IsOccupied = true;
            grid.GetCell(2, 0).IsOccupied = true;
            var simplify = typeof(GridPathfinder).GetMethod("SimplifyPath", BindingFlags.Static | BindingFlags.NonPublic);
            var result = (List<Vector3>)simplify.Invoke(null, new object[] { new List<Vector3> {
                Vector3.zero, new Vector3(1,0), new Vector3(2,0), new Vector3(3,0) } });
            Check(result.Count >= 2 && result[result.Count - 1] == new Vector3(3,0), "Blocked smoothing terminates and preserves endpoint");
            var route = GridPathfinder.FindPath(Vector3.zero, new Vector3(4, 0));
            Check(route != null, "Path goes around occupied cells");
            for (int y = 0; y < 12; y++) grid.GetCell(5,y).IsOccupied = true;
            for (int i = 0; i < 100; i++)
                if (GridPathfinder.FindPath(Vector3.zero, new Vector3(10, 0)) != null)
                    throw new Exception("Unreachable path incorrectly succeeded: " + i);
            Check(true, "100 unreachable paths all return failure");
            Check(GridPathfinder.FindPath(Vector3.zero, new Vector3(1,0), false) == null, "Blocked exact gate rejected");
            watch.Stop();
            Check(watch.ElapsedMilliseconds < 3000, "100 blocked paths within 3 seconds: " + watch.ElapsedMilliseconds + "ms");
        }
        finally { Object.DestroyImmediate(go); RTSGridSystem.Instance = original; }
    }
    private static void StaffChecks()
    {
        var grid = RTSGridSystem.Instance;
        var originalOccupancy = new List<Tuple<GridCell,bool,bool>>();
        for(int x=0;x<grid.MapWidth;x++)
        for(int y=0;y<grid.MapHeight;y++)
        {
            var cell=grid.GetCell(x,y);
            originalOccupancy.Add(Tuple.Create(cell,cell.IsOccupied,cell.IsWalkable));
            cell.IsOccupied=false; cell.IsWalkable=true;
        }
        var go=new GameObject("RegressionFactory");
        go.SetActive(false);
        var factory=go.AddComponent<FactoryBuilding>();
        factory.FootprintOffsets.Clear();
        factory.MaxStaffCapacity=3; factory.SupportsStaff=true;
        factory.InteractionOffsets=new List<Vector2Int>{Vector2Int.right};
        go.transform.position=grid.GetCell(4,4).WorldPos;
        go.SetActive(true);
        var residents=new List<ResidentEntity>();
        try
        {
            for(int i=0;i<4;i++)
            {
                var residentGo=new GameObject("RegressionResident");
                var entity=residentGo.AddComponent<ResidentEntity>();
                entity.MyData=new ResidentData("回归居民"+i);
                PopulationManager.Instance.TotalResidents.Add(entity.MyData);
                residentGo.transform.position=grid.GetCell(i,2).WorldPos;
                residents.Add(entity);
                entity.OrderGarrison(factory);
            }
            Check(factory.GetReservedStaffCount()==3, "Multi-resident commands respect capacity including reservations");
            Check(residents[3].MyData.Status==ResidentStatus.Idle, "Excess resident remains idle");
            residents[0].CancelGarrisonOrder();
            Check(factory.GetReservedStaffCount()==2, "Cancel releases reservation");
            residents[3].OrderGarrison(factory);
            Check(factory.GetReservedStaffCount()==3, "Freed position can be reassigned");
            var enter=typeof(ResidentEntity).GetMethod("ExecuteEnterGarrison",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int i=1;i<4;i++) enter.Invoke(residents[i],null);
            Check(factory.GetStaffList().Count==3 && factory.GetReservedStaffCount()==0, "Three arrivals become staff exactly once");
            grid.GetCell(5,4).IsOccupied=true;
            factory.MaxStaffCapacity=4;
            residents[0].OrderGarrison(factory);
            Check(residents[0].MyData.Status==ResidentStatus.Idle, "Unreachable gate does not reserve a position");
        }
        finally
        {
            foreach(var entity in residents)
            {
                if(entity==null)continue;
                PopulationManager.Instance.TotalResidents.Remove(entity.MyData);
                Object.DestroyImmediate(entity.gameObject);
            }
            Object.DestroyImmediate(go);
            foreach(var saved in originalOccupancy){saved.Item1.IsOccupied=saved.Item2;saved.Item1.IsWalkable=saved.Item3;}
        }
    }
    private static void PrepareWarehouse()
    {
        var inventory=PlayerInventoryManager.Instance;
        var chassis=AssetDatabase.FindAssets("t:ChassisDataSO").Select(g=>AssetDatabase.LoadAssetAtPath<ChassisDataSO>(AssetDatabase.GUIDToAssetPath(g))).First();
        var component=AssetDatabase.FindAssets("t:ComponentDataSO").Select(g=>AssetDatabase.LoadAssetAtPath<ComponentDataSO>(AssetDatabase.GUIDToAssetPath(g))).First(x=>x.GetModelData(1)?.Stats?.Count>0);
        inventory.ComponentInventory.Clear();
        inventory.AddChassisToWarehouse(chassis,3);
        inventory.AddComponentToWarehouse(component,1,50);
        var ids=inventory.GetAvailableComponents().Select(c=>c.InstanceID).ToList();
        componentIDs=ids;
        Check(ids.Count==50 && ids.Distinct().Count()==50,"50 components retain unique identities");
        Check(inventory.GetAvailableStacks().All(s=>s.Quantity==1),"Component views contain one instance each");
        var window=GlobalWarehouseUI.Instance ?? Object.FindObjectOfType<GlobalWarehouseUI>(true);
        window.OpenWarehouse();
    }
    private static void WarehouseChecks()
    {
        var window=GlobalWarehouseUI.Instance;
        var inventory=PlayerInventoryManager.Instance;
        Canvas.ForceUpdateCanvases();
        Check(window.ContentRoot.childCount==40,"Warehouse paginates large inventory at 40 cards");
        Check(window.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay,"Warehouse uses independent overlay canvas");
        window.MainCategoryDropdown.value=1;
        typeof(GlobalWarehouseUI).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
        Check(window.ContentRoot.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==3,"Three chassis shown as three cards");
        window.MainCategoryDropdown.alphaFadeSpeed=0f;
        window.MainCategoryDropdown.Show();
        Canvas.ForceUpdateCanvases();
        Check(window.gameObject.activeInHierarchy && window.ContentRoot.gameObject.activeInHierarchy,"Dropdown opening keeps warehouse visible");
        var blocker = window.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Blocker");
        Check(blocker != null, "Dropdown creates click blocker");
        ChimeraUITheme.StyleButton(blocker);
        Check(blocker.GetComponent<Image>().color.a == 0f, "Theme preserves transparent dropdown blocker");
        var standalone = new GameObject("Blocker", typeof(RectTransform), typeof(Image), typeof(Button));
        standalone.GetComponent<Image>().color = Color.clear;
        ChimeraUITheme.StyleButton(standalone.GetComponent<Button>());
        Check(standalone.GetComponent<Image>().color.a == 0f, "Theme preserves blockers outside warehouse too");
        Object.DestroyImmediate(standalone);
        Capture(window,"warehouse-dropdown.png");
        window.MainCategoryDropdown.Hide();
        window.MainCategoryDropdown.value=2;
        typeof(GlobalWarehouseUI).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
        var card=window.ContentRoot.Cast<Transform>().First(t=>t.gameObject.activeSelf);
        card.GetComponent<Button>().onClick.Invoke();
        Check(window.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("编号")),"Selected component details show identity");
        Capture(window,"warehouse-components.png");
        for(int i=0;i<5;i++){window.CloseWarehouse();window.OpenWarehouse();}
        Check(window.gameObject.activeInHierarchy,"Repeated close/open preserves visibility");
        Check(componentIDs.SequenceEqual(inventory.GetAvailableComponents().Select(c=>c.InstanceID)),"UI operations do not change component identity");
    }
    private static void Capture(GlobalWarehouseUI window,string name)
    {
        var cameraGo=new GameObject("RegressionCamera");
        var camera=cameraGo.AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color32(6,12,20,255);
        camera.cullingMask=1<<LayerMask.NameToLayer("UI");
        var target=new RenderTexture(1920,1080,24);
        camera.targetTexture=target;
        var canvas=window.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;
        canvas.worldCamera=camera;
        canvas.planeDistance=1f;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var old=RenderTexture.active;
        RenderTexture.active=target;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
        File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());
        RenderTexture.active=old;
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(cameraGo);
        target.Release(); Object.DestroyImmediate(target);
    }
}
