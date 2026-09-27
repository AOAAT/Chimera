using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit serialized theme migration. Logistics is excluded, including its palette.</summary>
[InitializeOnLoad]
public static class ChimeraIvoryAuthoring
{
    public const string Stamp = "Chimera_UI_Ivory_v2";
    public const string ReadyPath = "Assets/Resources/UI/IvoryThemeReady.txt";
    static ChimeraIvoryAuthoring()
    {
        EditorApplication.delayCall += UpgradeLoadedScenes;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += UpgradeLoadedScenes;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += UpgradeLoadedScenes;
        };
    }
    public static void Schedule() => EditorApplication.delayCall += UpgradeLoadedScenes;
    private static void UpgradeLoadedScenes()
    {
        if (File.Exists(ChimeraUISkinAuthoring.ConfigPath) || Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(ReadyPath)) return;
        for (int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);
            if(!scene.isLoaded || !scene.path.StartsWith("Assets/Scenes/") || SceneOrganization.HasMarker(scene, Stamp)) continue;
            bool dirty=scene.isDirty;
            foreach(var root in scene.GetRootGameObjects())Undo.RegisterFullObjectHierarchyUndo(root,"应用米白主题");
            UpgradeScene(scene);
            if(!dirty)EditorSceneManager.SaveScene(scene);
        }
    }
    private static readonly HashSet<string> LegacyNames = new HashSet<string> {
        "仓库页背景图", "装配页背景测试", "机甲仓库背景图", "物品背景图", "标签背景图", "机甲小图背景图片",
        "全局信息", "按钮", "升级按钮", "升级页背景图"
    };
    public static bool IsLegacy(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return path.StartsWith("Assets/Art/") && (LegacyNames.Contains(name) || name.Contains("详情页"));
    }
    private static bool Protected(Component c) => c.GetComponentInParent<LogisticsPanelUI>(true) != null;
    public static void Apply(GameObject root)
    {
        if (root.GetComponentInParent<LogisticsPanelUI>(true) != null) return;
        if (root.GetComponent<RectTransform>() != null) ChimeraUITheme.ApplyPanel(root, false);
        ChimeraUIThemeController.ApplyToRoot(root);
        foreach (var hud in root.GetComponentsInChildren<SelectionContextHUD>(true))
        {
            hud.PrepareEditorLayout(true);
            LayoutHUD(hud);
        }
        foreach (var factory in root.GetComponentsInChildren<FactoryUIModule>(true)) LayoutFactory(factory);
        foreach (var workshop in root.GetComponentsInChildren<AssemblyWorkshopUI>(true)) LayoutWorkshop(workshop);
        foreach (var pause in root.GetComponentsInChildren<PauseMenuUI>(true))
            if (pause.PausePanel != null)
                foreach (var label in pause.PausePanel.GetComponentsInChildren<TMP_Text>(true))
                    if (label.GetComponentInParent<Button>() == null && label.text.Contains("操作说明"))
                    {
                        label.enableAutoSizing = false; label.fontSize = 28;
                        label.alignment = TextAlignmentOptions.TopLeft; label.lineSpacing = 12;
                        label.margin = new Vector4(24,24,24,24);
                    }
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (Protected(image)) continue;
            if (image.name == "Backdrop" || image.name == "Blocker_Background")
            { image.sprite = null; image.color = ChimeraUITheme.Backdrop; }
        }
        foreach (var c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null || Protected(c)) continue;
            EditorUtility.SetDirty(c);
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
    }
    [MenuItem("Tools/Chimera/UI主题/维护/清理当前场景的已知旧版UI引用")]
    public static void CleanupLegacyReferences()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects())
        {
            Undo.RegisterFullObjectHierarchyUndo(root,"清理旧版 UI 引用");
        foreach (var c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null || Protected(c)) continue;
            // Also remove serialized background libraries so Play mode cannot resurrect old art.
            var serialized = new SerializedObject(c);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;
                if (!(property.objectReferenceValue is Sprite) && !(property.objectReferenceValue is Texture)) continue;
                if (!IsLegacy(AssetDatabase.GetAssetPath(property.objectReferenceValue))) continue;
                property.objectReferenceValue = null; changed = true;
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }
    private static void Fit(RectTransform rect, Transform parent, float x0, float y0, float x1, float y1)
    {
        if (rect == null) return;
        if (parent != null && rect.parent != parent) rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x0,y0); rect.anchorMax = new Vector2(x1,y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    private static void LayoutHUD(SelectionContextHUD hud)
    {
        var background = hud.GetComponent<Image>();
        if (background != null) { background.sprite = null; background.color = Color.clear; background.raycastTarget = false; }
        foreach (var root in new[] { hud.BuildingRoot, hud.ResidentRoot, hud.MechRoot }.Where(x=>x!=null))
        {
            var rect = (RectTransform)root.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);
            rect.anchoredPosition=new Vector2(0,18);rect.sizeDelta=new Vector2(1480,260);rect.localScale=Vector3.one;
        }
        Fit(hud.BuildingNameDisplay?.rectTransform,hud.BuildingRoot.transform,.02f,.75f,.17f,.96f);
        Fit(hud.BuildingIconImage?.rectTransform,hud.BuildingRoot.transform,.035f,.10f,.15f,.70f);
        Fit(hud.FunctionStage,hud.BuildingRoot.transform,.19f,.05f,.85f,.95f);
        Fit(hud.StaffListContainer?.transform as RectTransform,hud.BuildingRoot.transform,.19f,.07f,.85f,.93f);
        var buttons = new[] { hud.StaffToggleButton, hud.BuildingUpgradeButton, hud.BuildingDismantleButton,
            hud.DismissAllButton != null ? hud.DismissAllButton.GetComponent<Button>() : null };
        DockButtons(hud.BuildingRoot, buttons);
        Fit(hud.ResNameText?.rectTransform,hud.ResidentRoot.transform,.02f,.75f,.17f,.96f);
        Fit(hud.ResIconImage?.rectTransform,hud.ResidentRoot.transform,.035f,.10f,.15f,.70f);
        Fit(hud.ResStatusText?.rectTransform,hud.ResidentRoot.transform,.20f,.40f,.84f,.91f);
        Fit(hud.ResHPBar?.transform as RectTransform,hud.ResidentRoot.transform,.20f,.14f,.84f,.22f);
        var residentButtons=hud.ResidentRoot.GetComponentsInChildren<Button>(true).OrderBy(x=>x==hud.OffDutyButton?0:1).ToArray();
        DockButtons(hud.ResidentRoot, residentButtons);
        Fit(hud.MechNameText?.rectTransform,hud.MechRoot.transform,.02f,.75f,.17f,.96f);
        Fit(hud.MechPreviewContainer,hud.MechRoot.transform,.035f,.10f,.15f,.70f);
        Fit(hud.MechHPBar?.transform as RectTransform,hud.MechRoot.transform,.20f,.55f,.84f,.64f);
        Fit(hud.MechAPBar?.transform as RectTransform,hud.MechRoot.transform,.20f,.22f,.84f,.31f);
        Fit(hud.MechHPValueDisplay?.rectTransform,hud.MechRoot.transform,.20f,.67f,.84f,.80f);
        var mechButtons=new[]{hud.MechDetailButton,hud.MechRefitButton,hud.MechRecycleButton};
        DockButtons(hud.MechRoot, mechButtons);
    }
    private static void DockButtons(GameObject root, Button[] buttons)
    {
        var column = root.transform.Find("IvoryActions") as RectTransform;
        if(column==null)column=new GameObject("IvoryActions",typeof(RectTransform)).GetComponent<RectTransform>();
        Fit(column,root.transform,.87f,.05f,.99f,.95f);
        for(int i=0;i<buttons.Length;i++)
        {
            if(buttons[i]==null)continue;
            var rect=(RectTransform)buttons[i].transform;rect.SetParent(column,false);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=new Vector2(0,-24-i*48);rect.sizeDelta=new Vector2(160,40);
        }
    }
    private static void LayoutFactory(FactoryUIModule factory)
    {
        var root=(RectTransform)factory.transform;
        Fit(root,null,0,0,1,1);
        var shelf=factory.ShelfGrid?.GetComponentInParent<ScrollRect>(true);
        var queue=factory.TaskQueueContainer?.GetComponentInParent<ScrollRect>(true);
        if(shelf!=null)Fit((RectTransform)shelf.transform,root,.20f,.52f,.99f,.99f);
        if(queue!=null)Fit((RectTransform)queue.transform,root,.20f,.01f,.99f,.49f);
        foreach(var button in factory.GetComponentsInChildren<Button>(true))
        {
            var label=button.GetComponentInChildren<TMP_Text>(true);
            if(label==null)continue;
            if(label.text=="底盘")Fit((RectTransform)button.transform,root,.01f,.81f,.18f,.98f);
            if(label.text=="组件")Fit((RectTransform)button.transform,root,.01f,.02f,.18f,.19f);
        }
    }
    private static RectTransform Surface(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var rect=parent.Find(name) as RectTransform;
        if(rect==null)rect=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
        Fit(rect,parent,x0,y0,x1,y1);
        ChimeraUITheme.StyleSurface(rect.GetComponent<Image>(),ChimeraUITheme.Window);
        return rect;
    }
    private static void Caption(string name,string value,Transform parent,TMP_FontAsset font,float x0,float y0,float x1,float y1)
    {
        var child=parent.Find(name);
        var label=child!=null?child.GetComponent<TMP_Text>():new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        Fit(label.rectTransform,parent,x0,y0,x1,y1);label.font=font;label.text=value;label.fontSize=26;
        label.color=ChimeraUITheme.PrimaryText;label.fontStyle=FontStyles.Bold;label.raycastTarget=false;
    }
    private static void LayoutWorkshop(AssemblyWorkshopUI workshop)
    {
        var root=(RectTransform)workshop.transform;
        if(root.GetComponent<Canvas>()==null)Fit(root,null,0,0,1,1);
        var backdrop=root.GetComponent<Image>()??root.gameObject.AddComponent<Image>();
        backdrop.sprite=null;backdrop.color=ChimeraUITheme.Backdrop;backdrop.raycastTarget=true;
        var window=Surface("IvoryWorkshopWindow",root,.07f,.07f,.93f,.93f);
        var font=workshop.HPText.font;
        Caption("WorkshopTitle","机甲装配",window,font,.025f,.92f,.80f,.985f);
        Fit((RectTransform)workshop.LeftStatsPanel.transform,window,.025f,.13f,.25f,.90f);
        Fit((RectTransform)workshop.CenterPreviewArea.transform,window,.265f,.13f,.685f,.90f);
        var right=Surface("ComponentSelectionPane",window,.70f,.13f,.975f,.90f);
        Caption("SelectorTitle","适配组件",right,font,.04f,.91f,.96f,.98f);
        Caption("SelectorHint","点击机甲节点选择组件",right,font,.08f,.40f,.92f,.65f);
        Fit((RectTransform)workshop.RightInventoryPanel.transform,right,.02f,.02f,.98f,.89f);
        var stats=workshop.LeftStatsPanel.transform;
        Caption("StatsTitle","机甲信息",stats,font,.06f,.91f,.94f,.98f);
        Fit(workshop.UnitNameInput.transform as RectTransform,stats,.06f,.79f,.94f,.88f);
        workshop.UnitNameInput.textComponent.color=ChimeraUITheme.PrimaryText;
        workshop.UnitNameInput.textComponent.fontSize=23;
        var labels=new[]{workshop.HPText,workshop.APText,workshop.BlockText,workshop.MassText,workshop.SpeedText,workshop.PowerText};
        for(int i=0;i<labels.Length;i++)if(labels[i]!=null)
        {
            Fit(labels[i].rectTransform,stats,.08f,.64f-i*.10f,.94f,.72f-i*.10f);
            labels[i].fontSize=22;labels[i].alignment=TextAlignmentOptions.MidlineLeft;labels[i].color=ChimeraUITheme.PrimaryText;
        }
        var center=workshop.CenterPreviewArea.transform;
        Caption("PreviewTitle","装配预览",center,font,.04f,.91f,.96f,.98f);
        Fit(workshop.ChassisVisualRoot as RectTransform,center,.05f,.06f,.95f,.88f);
        Fit(workshop.GhostChassisPrompt.transform as RectTransform,center,.20f,.36f,.80f,.57f);
        workshop.PreviewScale=2.6f;workshop.SlotButtonSize=16f;
        foreach(var button in workshop.GetComponentsInChildren<Button>(true))
            for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
            {
                string method=button.onClick.GetPersistentMethodName(i);
                if(method=="SaveAndExitWorkshop")Fit((RectTransform)button.transform,window,.79f,.035f,.975f,.10f);
                if(method=="CancelAndExitWorkshop")Fit((RectTransform)button.transform,window,.025f,.035f,.20f,.10f);
            }
        if(workshop.ValidationMessageText!=null)Fit(workshop.ValidationMessageText.rectTransform,window,.23f,.035f,.76f,.10f);
        var selector=workshop.RightInventoryPanel.GetComponent<RightInventoryPanelUI>();
        if(selector!=null && selector.ContentRoot!=null)
        {
            var scroll=selector.ContentRoot.GetComponentInParent<ScrollRect>(true);
            if(scroll!=null)Fit((RectTransform)scroll.transform,selector.transform,.02f,.02f,.98f,.98f);
            var grid=selector.ContentRoot.GetComponent<GridLayoutGroup>();
            if(grid!=null){grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.cellSize=new Vector2(172,170);grid.spacing=new Vector2(10,10);}
        }
    }
    public static void UpgradeScene(Scene scene, bool force = false)
    {
        bool stamped = SceneOrganization.HasMarker(scene, Stamp);
        if (stamped && !force) return;
        // Rebuild the warehouse first: only its authored generated tree is replaced.
        foreach (var warehouse in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GlobalWarehouseUI>(true)).ToArray())
            warehouse.PrepareEditorView();
        foreach (var root in scene.GetRootGameObjects()) Apply(root);
        if (!stamped) { var stamp = new GameObject(Stamp); SceneManager.MoveGameObjectToScene(stamp, scene); }
        EditorSceneManager.MarkSceneDirty(scene);
    }
    [MenuItem("Tools/Chimera/美术与UI/应用米白主题到当前场景")]
    public static void UpgradeCurrent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        UpgradeScene(SceneManager.GetActiveScene());
    }
    public static void BakeProject()
    {
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] {"Assets/Prefabs", "Assets/Resources/UI"})
            .Select(AssetDatabase.GUIDToAssetPath).ToArray())
        {
            if (path.EndsWith("/LogisticsPanel.prefab")) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<RectTransform>() == null) continue;
                var warehouse = root.GetComponent<GlobalWarehouseUI>();
                if (warehouse != null) warehouse.PrepareEditorView();
                Apply(root); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in new[] {"Assets/Scenes/RTS_World_Master.unity", "Assets/Scenes/Scene_MainMenu.unity"})
        {
            var scene = EditorSceneManager.OpenScene(path);
            UpgradeScene(scene, true); EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[米白主题] 场景与预制体已保存；物流界面保持原样。");
    }
}

public class ChimeraIvoryReadyImport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
    {
        if(imported.Contains(ChimeraIvoryAuthoring.ReadyPath))ChimeraIvoryAuthoring.Schedule();
    }
}
