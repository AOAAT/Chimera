using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class IvoryThemeChecks
{
    private const string Key = "IvoryThemeChecks";
    private static int stage;
    private static double started, next;
    private static GlobalWarehouseUI ui;
    private static string first, second, itemKey, itemID;
    private static ChassisDataSO chassis;
    private static string Output => Path.Combine(Directory.GetCurrentDirectory(), "ThemeResults");
    private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static void Check(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        File.AppendAllText(Path.Combine(Output, "checks.txt"), "PASS " + text + "\n");
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "checks.txt"), "");
        EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
        Check(Object.FindObjectsOfType<Transform>(true).Any(x => x.name == ChimeraIvoryAuthoring.Stamp), "scene has serialized theme stamp");
        foreach (var image in Object.FindObjectsOfType<Image>(true))
            if (image.sprite != null) Check(!ChimeraIvoryAuthoring.IsLegacy(AssetDatabase.GetAssetPath(image.sprite)), "no legacy image: " + image.name);
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup; next = started + 5;
        EditorApplication.update += Tick;
    }
    private static void Click(GameObject go)
    {
        Canvas.ForceUpdateCanvases();
        var rect = go.GetComponent<RectTransform>(); var canvas = go.GetComponentInParent<Canvas>();
        var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        var handler = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
        Check(handler != null && (handler == go || handler.transform.IsChildOf(go.transform)), "click reaches " + go.name + " (" + string.Join(", ", hits.Take(5).Select(x=>x.gameObject.name)) + ")");
        ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .8;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 130) throw new Exception("Timed out");
            var inv = PlayerInventoryManager.Instance; var logistics = LogisticsManager.Instance;
            switch (stage++)
            {
                case 0:
                    if (logistics == null || !logistics.Ready) { stage--; return; }
                    logistics.enabled = false;
                    foreach (var factory in Object.FindObjectsOfType<FactoryBuilding>()) factory.enabled = false;
                    foreach (var store in logistics.Data.Storages) store.Cargo.Clear();
                    logistics.Data.Jobs.Clear();
                    first = logistics.Warehouses.First().ID; second = "test-second-store";
                    logistics.Data.Storages.Add(new LogisticsStorage {ID=second, Kind=StorageKind.Warehouse, Name="第二仓库", Capacity=10000});
                    chassis = inv.AllChassisDatabase.First(x => x != null);
                    inv.AddChassisToWarehouse(chassis, 81);
                    inv.AddComponentToWarehouse(inv.AllComponentDatabase.First(x => x != null), 1, 3);
                    var component = inv.GetAvailableComponents().First();
                    logistics.Get(first).Remove("component:" + component.InstanceID, 1);
                    logistics.Get(second).Add("component:" + component.InstanceID, 1);
                    ui = Object.FindObjectOfType<GlobalWarehouseUI>(true); ui.OpenWarehouse(first);
                    break;
                case 1:
                    Check(ui.FilteredCount == 83, "single warehouse excludes another warehouse's item");
                    Check(ui.ContentRoot.childCount <= 24, "large inventory only renders visible rows");
                    Check(ui.transform.Find("WindowArea") == null && ui.transform.Find("CloseButton") == null, "old warehouse nodes removed");
                    Check(ui.GetComponentsInChildren<TMP_Text>(true).All(t => t.text != "上一页" && t.text != "下一页"), "pagination removed");
                    var originalCards=ui.ContentRoot.Cast<Transform>().Select(x=>x.GetInstanceID()).ToArray();
                    typeof(GlobalWarehouseUI).GetMethod("RefreshWarehouse",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null);
                    Check(originalCards.SequenceEqual(ui.ContentRoot.Cast<Transform>().Select(x=>x.GetInstanceID())),"inventory refresh reuses cards without destroying them");
                    ui.MainCategoryDropdown.value=1;
                    break;
                case 2:
                    Check(ui.FilteredCount == 81, "chassis shown individually");
                    Click(ui.ContentRoot.GetChild(0).gameObject);
                    itemKey=Field<string>(ui,"selectedKey");itemID=itemKey.Substring(17);
                    Click(Field<Button>(ui,"renameButton").gameObject);
                    break;
                case 3:
                    Field<TMP_InputField>(ui,"renameInput").text="守望者测试";
                    Click(Field<Button>(ui,"saveNameButton").gameObject);
                    break;
                case 4:
                    Check(inv.GetChassisInstance(itemID).CustomName=="守望者测试", "rename UI updates the selected chassis instance");
                    Check(inv.GetAvailableChassis().Count(x=>x.CustomName=="守望者测试")==1, "same model siblings keep their original names");
                    ui.SearchInput.text="守望者测试"; break;
                case 5:
                    Check(ui.FilteredCount==1,"custom name search works");
                    Check(ui.ContentRoot.Cast<Transform>().Count(x=>x.gameObject.activeSelf)==1,"filtered pool deactivates all unmatched cards");
                    ui.SearchInput.text=chassis.ChassisName;break;
                case 6:
                    Check(ui.FilteredCount==81,"original model name remains searchable after rename");
                    ui.SearchInput.text="";
                    Field<ScrollRect>(ui,"itemScroll").verticalNormalizedPosition=0;
                    break;
                case 7:
                    Check(ui.ContentRoot.childCount<=24,"scrolling does not materialize entire inventory");
                    Capture(ui.GetComponent<Canvas>(),"warehouse-bottom.png");
                    ui.MainCategoryDropdown.Show();break;
                case 8:
                    var blocker=ui.GetComponentsInChildren<Image>().FirstOrDefault(x=>x.name=="Blocker");
                    Check(blocker!=null && blocker.color.a==0,"dropdown blocker stays transparent");
                    Capture(ui.GetComponent<Canvas>(),"warehouse-dropdown.png");
                    ui.MainCategoryDropdown.Hide(); ui.OpenWarehouse(second);break;
                case 9:
                    ui.MainCategoryDropdown.value=0;break;
                case 10:
                    Check(ui.FilteredCount==1,"switching warehouses shows only physical local cargo");
                    Check(!inv.RenameItem(itemKey,"<b>bad</b>"),"rich-text names rejected");
                    var component2=inv.ComponentInventory.First(x=>logistics.Get(second).Count("component:"+x.InstanceID)>0);
                    inv.RenameItem("component:"+component2.InstanceID,"精工核心");
                    var saved=JsonUtility.FromJson<InventorySaveData>(JsonUtility.ToJson(inv.CaptureSaveData()));
                    Check(saved.ChassisAreInstances && saved.ChassisWarehouse.Count==0,"new save has one canonical chassis registry");
                    inv.RestoreSaveData(saved,new SaveDefinitionResolver(inv,BuildingManager.Instance));
                    Check(inv.GetChassisInstance(itemID).CustomName=="守望者测试","chassis identity and name survive JSON restore");
                    Check(inv.GetComponentInstance(component2.InstanceID).CustomName=="精工核心","component name survives JSON restore");
                    var original=inv.GetChassisInstance(itemID);var profile=new SavedUnitProfile(original,"装配测试");
                    Check(inv.TryTakeChassis(original,profile.UnitID),"specific chassis can be taken for assembly");
                    inv.ReleaseChassis(profile); inv.ReleaseChassis(profile);
                    Check(logistics.Data.Storages.Sum(x=>x.Count(itemKey))==1 && inv.GetChassisInstance(itemID).CustomName=="守望者测试","cancel/recycle return same named chassis exactly once");
                    float before=logistics.Get(second).Used;
                    logistics.Get(second).Add("chassis:"+chassis.ChassisID,3);
                    logistics.MigrateChassisLocations();logistics.MigrateChassisLocations();
                    Check(logistics.Get(second).Used==before+3 && logistics.Get(second).Cargo.All(x=>!x.Key.StartsWith("chassis:")),"legacy physical stacks migrate in place idempotently");
                    ui.CloseWarehouse();ui.OpenWarehouse(first);ui.MainCategoryDropdown.value=0;break;
                case 11:
                    Capture(ui.GetComponent<Canvas>(),"warehouse.png");
                    Capture(ui.GetComponent<Canvas>(),"warehouse-1280.png",1280,720);
                    Capture(ui.GetComponent<Canvas>(),"warehouse-1024.png",1024,768);ui.CloseWarehouse();
                    ResidentRosterPanelUI.OpenRoster();break;
                case 12:
                    var roster=Object.FindObjectOfType<ResidentRosterPanelUI>(true);
                    Capture(roster.GetComponent<Canvas>(),"roster.png");roster.Close();
                    var selectedFactory=Object.FindObjectOfType<FactoryBuilding>();
                    SelectionContextHUD.Instance.Refresh(selectedFactory);
                    selectedFactory.AddToQueue(chassis,chassis.ChassisName,chassis.ChassisSprite,chassis.BaseProductionTime,chassis.ProductionCost);
                    selectedFactory.AddToQueue(chassis,"测试较长名称的生产任务",chassis.ChassisSprite,chassis.BaseProductionTime,chassis.ProductionCost);
                    break;
                case 13:
                    var taskRows=Object.FindObjectsOfType<ProductionTaskUIItem>();
                    Check(taskRows.Length>=2,"production queue renders populated rows");
                    Check(taskRows.All(x=>Field<TMP_Text>(x,"pauseActionLabel").isActiveAndEnabled),"production pause labels are visible");
                    Check(taskRows.All(x=>x.transform.Find("Handle").GetComponent<Image>().color.a==0),"legacy white drag handles are transparent");
                    taskRows[0].OnClickTogglePause();
                    Check(taskRows[0].BindedTask.IsPaused,"production pause remains functional after layout migration");
                    Capture(SelectionContextHUD.Instance.GetComponentInParent<Canvas>(),"factory-hud.png");
                    SelectionContextHUD.Instance.Refresh(Object.FindObjectOfType<ResidentEntity>());break;
                case 14:
                    Capture(SelectionContextHUD.Instance.GetComponentInParent<Canvas>(),"resident-hud.png");
                    Check(Field<RectTransform>(Object.FindObjectOfType<LogisticsPanelUI>(true),"window").GetComponent<Image>().color == (Color)new Color32(31,42,55,252),"logistics keeps original palette");
                    PauseMenuUI.Instance.PauseGame();break;
                case 15:
                    Capture(PauseMenuUI.Instance.PausePanel.GetComponentInParent<Canvas>(),"pause.png");
                    PauseMenuUI.Instance.ResumeGame();
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Scene_MainMenu");break;
                case 16:
                    var menu=Object.FindObjectOfType<MainMenuUI>();
                    Check(menu!=null,"main menu scene opens with new theme");
                    Capture(menu.NewGameButton.GetComponentInParent<Canvas>(),"main-menu.png");
                    File.AppendAllText(Path.Combine(Output,"checks.txt"),"COMPLETE\n");
                    Finish(0);break;
            }
        }
        catch(Exception e){if(ui!=null && ui.gameObject.activeSelf)Capture(ui.GetComponent<Canvas>(),"failure.png");File.AppendAllText(Path.Combine(Output,"checks.txt"),"FAIL "+e+"\n");Debug.LogException(e);Finish(1);}
    }
    private static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    public static void Capture(Canvas canvas,string name,int width=1920,int height=1080)
    {
        var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;
        var camera=new GameObject("ThemeCapture").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.3f,.32f,.30f);camera.cullingMask=1<<30;camera.orthographic=true;
        var capturedObjects=canvas.GetComponentsInChildren<Transform>(true).Select(x=>x.gameObject).ToArray();
        var originalLayers=capturedObjects.Select(x=>x.layer).ToArray();
        foreach(var obj in capturedObjects)obj.layer=30;
        camera.transform.position=new Vector3(0,0,-100);camera.nearClipPlane=.1f;camera.farClipPlane=1000;
        var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
        Canvas.ForceUpdateCanvases();
        var warehouse=canvas.GetComponent<GlobalWarehouseUI>();
        if(warehouse!=null) typeof(GlobalWarehouseUI).GetMethod("RenderVisible",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(warehouse,new object[]{true});
        foreach(var child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
        Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Output,name),texture.EncodeToPNG());RenderTexture.active=old;
        canvas.renderMode=mode;canvas.worldCamera=previousCamera;camera.targetTexture=null;rt.Release();
        for(int i=0;i<capturedObjects.Length;i++)if(capturedObjects[i]!=null)capturedObjects[i].layer=originalLayers[i];
        if(warehouse!=null)typeof(GlobalWarehouseUI).GetMethod("RenderVisible",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(warehouse,new object[]{true});
        Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(camera.gameObject);
    }
}
