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

public static class ItemHoverChecks
{
    const string Key = "ItemHoverChecks";
    static string Output => Path.Combine(Directory.GetCurrentDirectory(),"HoverResults");
    static int stage;
    static double next,started;
    static FactoryUIModule factory;
    static AssemblyWorkshopUI workshop;
    static RectTransform target;
    static Vector2 placement;
    static ChassisDataSO chassis;
    static InstancedComponent component;
    static int socket;
    static ProductionTask liveTask;
    static ProductionTaskUIItem liveRow;
    static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    static void Check(bool value,string text)
    {
        if(!value)throw new Exception(text);
        File.AppendAllText(Path.Combine(Output,"checks.txt"),"PASS "+text+"\n");
    }
    public static void Run()
    {
        ItemHoverAuthoring.Bake();
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"checks.txt"),"");
        foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1280,720),new Vector2(1024,768)})
        {
            var safe=new Rect(12,12,size.x-24,size.y-24);
            foreach(var origin in new[]{new Vector2(12,12),new Vector2(size.x-112,12),new Vector2(12,size.y-112),new Vector2(size.x-112,size.y-112),size/2})
            {
                var item=new Rect(origin,new Vector2(100,100));var result=ItemHoverTooltip.Place(item,new Vector2(430,500),safe,null);
                Check(safe.Contains(result.min)&&result.xMax<=safe.xMax+.01f&&result.yMax<=safe.yMax+.01f&&!result.Overlaps(item),"placement avoids target and viewport edges at "+size+" / "+origin);
            }
        }
        var protectedArea=new Rect(1050,200,430,500);
        Check(!ItemHoverTooltip.Place(new Rect(900,450,100,100),new Vector2(430,500),new Rect(0,0,1920,1080),new[]{protectedArea}).Overlaps(protectedArea),"placement avoids protected controls when alternate space exists");
        EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if(!SessionState.GetBool(Key,false))return;
        started=EditorApplication.timeSinceStartup;next=started+6;EditorApplication.update+=Tick;
    }
    static PointerEventData Pointer(GameObject go)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)go.transform;var canvas=go.GetComponentInParent<Canvas>().rootCanvas;
        return new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
    }
    static void Enter(GameObject go)
    {
        var data=Pointer(go);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0&&(hits[0].gameObject==go||hits[0].gameObject.transform.IsChildOf(go.transform)),"hover raycast reaches "+go.name);
        data.pointerEnter=hits[0].gameObject;
        ExecuteEvents.Execute(go,data,ExecuteEvents.pointerEnterHandler);target=(RectTransform)go.transform;
    }
    static void Click(GameObject go)
    {
        var data=Pointer(go);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        var handler=hits.Count>0?ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject):null;
        Check(handler==go||handler!=null&&handler.transform.IsChildOf(go.transform),"tooltip does not block click on "+go.name+"; top hit: "+(hits.Count>0?hits[0].gameObject.name:"none"));
        ExecuteEvents.Execute(handler,data,ExecuteEvents.pointerClickHandler);
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.65;
        try
        {
            if(EditorApplication.timeSinceStartup-started>140)throw new Exception("Timeout");
            var inv=PlayerInventoryManager.Instance;
            switch(stage++)
            {
                case 0:
                    if(LogisticsManager.Instance==null||!LogisticsManager.Instance.Ready){stage--;return;}
                    LogisticsManager.Instance.enabled=false;
                    foreach(var f in Object.FindObjectsOfType<FactoryBuilding>())f.enabled=false;
                    UIClarityChecks.Production();
                    SelectionContextHUD.Instance.Refresh(Object.FindObjectOfType<FactoryBuilding>());
                    factory=Object.FindObjectOfType<FactoryUIModule>();factory.ShowComponentShelf();break;
                case 1:
                    Enter(factory.ShelfGrid.GetChild(0).gameObject);
                    Check(!ItemHoverTooltip.Instance.Visible,"hover starts hidden for delay");Time.timeScale=0;break;
                case 2:
                    var view=ItemHoverTooltip.Instance;
                    Check(view.Visible,"tooltip delay uses unscaled time while paused");
                    Check(view.BodyText.Contains("标准属性（未计品质）")&&view.BodyText.Contains("生产需求")&&view.BodyText.Contains("当前预计"),"recipe uses blueprint stats, cost and effective production time");
                    Check(!view.BodyText.Contains("品质修正"),"recipe does not invent random instance quality");
                    Check(view.Card.GetComponent<Image>().color.a==1&&!view.GetComponent<CanvasGroup>().blocksRaycasts,"opaque readable card is non-blocking");
                    Check(!Object.FindObjectsOfType<Transform>(true).Any(x=>x.name.Contains("ItemDetailPanelUI")),"legacy floating panel is absent, including inactive objects");
                    placement=view.Card.anchoredPosition;
                    Capture("production-hover.png",factory.GetComponentInParent<Canvas>().rootCanvas,view.GetComponent<Canvas>());break;
                case 3:
                    Check(ItemHoverTooltip.Instance.Visible&&Vector2.Distance(placement,ItemHoverTooltip.Instance.Card.anchoredPosition)<.1f,"card remains stable while hovering same item");
                    var old=target;Enter(factory.ShelfGrid.GetChild(1).gameObject);
                    ExecuteEvents.Execute(old.gameObject,Pointer(old.gameObject),ExecuteEvents.pointerExitHandler);break;
                case 4:
                    Check(ItemHoverTooltip.Instance.Visible,"late exit from old item does not hide new item");
                    target.GetComponentInParent<ScrollRect>().onValueChanged.Invoke(new Vector2(0,.5f));
                    Check(!ItemHoverTooltip.Instance.Visible,"scrolling dismisses tooltip immediately");
                    Enter(target.gameObject);factory.gameObject.SetActive(false);break;
                case 5:
                    Check(!ItemHoverTooltip.Instance.Visible,"closing source panel cancels pending hover");
                    Time.timeScale=1;
                    chassis=inv.AllChassisDatabase.First(x=>x.Sockets.Any(s=>s.AllowedTypes.Contains(ComponentType.Core)));
                    socket=chassis.Sockets.FindIndex(x=>x.AllowedTypes.Contains(ComponentType.Core));
                    inv.AddChassisToWarehouse(chassis,1);
                    inv.AddComponentToWarehouse(inv.AllComponentDatabase.First(x=>x.Type==ComponentType.Core),1,1);
                    Object.FindObjectOfType<AssemblerBuilding>().OpenWorkshop();
                    workshop=Object.FindObjectOfType<AssemblyWorkshopUI>(true);break;
                case 6: Click(workshop.GhostChassisPrompt);break;
                case 7:
                    var ch=RightInventoryPanelUI.Instance.ContentRoot.GetComponentsInChildren<InventoryItemSlotUI>().First(x=>Field<InstancedChassis>(x,"cachedChassis")?.BaseData==chassis);
                    Enter(ch.gameObject);break;
                case 8:
                    Check(ItemHoverTooltip.Instance.Visible&&ItemHoverTooltip.Instance.TitleText==chassis.ChassisName,"assembly chassis uses same shared tooltip");
                    Capture("assembly-chassis-hover.png",workshop.GetComponentInParent<Canvas>().rootCanvas,ItemHoverTooltip.Instance.GetComponent<Canvas>());
                    Click(target.gameObject);break;
                case 9:
                    var node=workshop.ChassisVisualRoot.GetComponentsInChildren<Button>().First(x=>x.name=="UI_Socket_"+chassis.Sockets[socket].SlotName);
                    Enter(node.gameObject);break;
                case 10:
                    Check(ItemHoverTooltip.Instance.Visible&&ItemHoverTooltip.Instance.BodyText.Contains("可安装类型"),"empty node describes compatible components");
                    Click(target.gameObject);break;
                case 11:
                    var slot=RightInventoryPanelUI.Instance.ContentRoot.GetComponentsInChildren<InventoryItemSlotUI>().First(x=>Field<InstancedComponent>(x,"cachedComponent")!=null);
                    component=Field<InstancedComponent>(slot,"cachedComponent");component.CustomName="测试独立核心";
                    component.Affixes=new List<ComponentAffixInstance>{new ComponentAffixInstance {DisplayName="装甲测试",Description="提高组件提供的护甲。",
                        Modifiers=new List<StatEntry>{new StatEntry {StatID=StatType.AddedAP,ModType=BuffModifierType.Multiplier,Value=1.12f}}}};
                    UIClarityChecks.Comparison(workshop,chassis,socket,component);
                    Enter(slot.gameObject);break;
                case 12:
                    Check(ItemHoverTooltip.Instance.Visible&&ItemHoverTooltip.Instance.TitleText==component.DisplayName&&ItemHoverTooltip.Instance.BodyText.Contains("实际属性")&&ItemHoverTooltip.Instance.BodyText.Contains("品质修正"),"actual component shows its own name, resolved stats and quality");
                    Check(ItemHoverTooltip.Instance.BodyText.Contains("装甲测试"),"instance affix description is displayed");
                    foreach(var button in workshop.GetComponentsInChildren<Button>().Where(x=>x.onClick.GetPersistentEventCount()>0))
                        Check(!ScreenRect(ItemHoverTooltip.Instance.Card).Overlaps(ScreenRect((RectTransform)button.transform)),"component tooltip avoids action button "+button.name);
                    Capture("assembly-component-hover.png",workshop.GetComponentInParent<Canvas>().rootCanvas,ItemHoverTooltip.Instance.GetComponent<Canvas>());
                    Click(target.gameObject);break;
                case 13:
                    Check(Field<SavedUnitProfile>(workshop,"currentEditingProfile").EquippedComponentIDs.Contains(component.InstanceID),"hovering still allows actual component installation");
                    IvoryThemeChecks.Capture(workshop.GetComponentInParent<Canvas>().rootCanvas,"assembly-1280.png",1280,720);
                    IvoryThemeChecks.Capture(workshop.GetComponentInParent<Canvas>().rootCanvas,"assembly-1024.png",1024,768);
                    var visual=workshop.ChassisVisualRoot.GetComponentsInChildren<Image>().First(x=>x.name=="Sprite_Visual"&&x.sprite==component.BaseData.ComponentIcon);
                    Enter(visual.gameObject);break;
                case 14:
                    Check(ItemHoverTooltip.Instance.Visible&&ItemHoverTooltip.Instance.TitleText==component.DisplayName,"installed component visual has matching hover information");
                    workshop.CancelAndExitWorkshop();break;
                case 15:
                    Check(!ItemHoverTooltip.Instance.Visible,"closing workshop removes tooltip");
                    var owner=Object.FindObjectOfType<FactoryBuilding>();
                    liveTask=new ProductionTask(null,"持续悬停状态测试",null,100,new ResourceSet{Scrap=10}){UsesLogistics=true};
                    owner.TaskQueue.Clear();owner.TaskQueue.Add(liveTask);
                    SelectionContextHUD.Instance.Refresh(null);SelectionContextHUD.Instance.Refresh(owner);
                    factory=Object.FindObjectOfType<FactoryUIModule>();
                    typeof(FactoryUIModule).GetMethod("RefreshQueueUI",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(factory,new object[]{owner});break;
                case 16:
                    liveRow=factory.TaskQueueContainer.GetComponentsInChildren<ProductionTaskUIItem>().First(x=>x.BindedTask==liveTask);
                    Enter(liveRow.gameObject);break;
                case 17:
                    Check(ItemHoverTooltip.Instance.Visible&&ItemHoverTooltip.Instance.TitleText==liveTask.ItemName,"production row has a readable status tooltip");
                    liveTask.IsPaused=true;break;
                case 18:
                    Check(ItemHoverTooltip.Instance.Visible&&ItemHoverTooltip.Instance.BodyText.Contains("已送达原料保留"),"visible production tooltip refreshes when state changes without moving the pointer");
                    Capture("production-status-hover.png",factory.GetComponentInParent<Canvas>().rootCanvas,ItemHoverTooltip.Instance.GetComponent<Canvas>());
                    ItemHoverTooltip.Hide();
                    IvoryThemeChecks.Capture(factory.GetComponentInParent<Canvas>().rootCanvas,"factory-1280.png",1280,720);
                    IvoryThemeChecks.Capture(factory.GetComponentInParent<Canvas>().rootCanvas,"factory-1024.png",1024,768);
                    liveRow.OnClickTogglePause();
                    Check(!liveTask.IsPaused&&liveRow.CurrentStatus.State!=ProductionViewState.Paused,"continue action immediately clears paused row state");
                    File.AppendAllText(Path.Combine(Output,"checks.txt"),"COMPLETE\n");Finish(0);break;
            }
        }
        catch(Exception e){File.AppendAllText(Path.Combine(Output,"checks.txt"),"FAIL "+e+"\n");Debug.LogException(e);Finish(1);}
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
        var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    static void Capture(string name,params Canvas[] canvases)
    {
        canvases=canvases.Distinct().ToArray();
        var modes=canvases.Select(x=>x.renderMode).ToArray();var cameras=canvases.Select(x=>x.worldCamera).ToArray();var distances=canvases.Select(x=>x.planeDistance).ToArray();
        var objects=canvases.SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).Select(x=>x.gameObject).Distinct().ToArray();var layers=objects.Select(x=>x.layer).ToArray();
        var camera=new GameObject("CaptureHover").AddComponent<Camera>();camera.orthographic=true;camera.cullingMask=1<<30;
        camera.transform.position=new Vector3(0,0,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.30f,.32f,.30f);
        var rt=new RenderTexture(Screen.width * 3,Screen.height * 3,24);camera.targetTexture=rt;
        foreach(var obj in objects)obj.layer=30;
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;}
        Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Output,name),texture.EncodeToPNG());RenderTexture.active=previous;
        for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
        for(int i=0;i<objects.Length;i++)objects[i].layer=layers[i];
        camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(camera.gameObject);Canvas.ForceUpdateCanvases();
    }
}
