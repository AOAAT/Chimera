using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Skin application never runs the legacy artwork cleanup or reconstructs UI trees.
public static class ChimeraUISkinAuthoring
{
    public const string ConfigPath = "Assets/Resources/UI/ChimeraTheme.asset";
    public const string Stamp = "Chimera_UI_Skin_v2";
    public const string ReadyPath = "Assets/Resources/UI/SkinAuthoringReady.txt";
    public static ChimeraUIThemeConfig EnsureConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<ChimeraUIThemeConfig>(ConfigPath);
        if(config == null)
        {
            config = ScriptableObject.CreateInstance<ChimeraUIThemeConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        ChimeraUITheme.ReloadConfig();
        return config;
    }
    [MenuItem("Tools/Chimera/UI主题/打开主题配置")]
    public static void SelectConfig() { Selection.activeObject = EnsureConfig(); }

    [MenuItem("Tools/Chimera/UI主题/应用到当前打开的场景")]
    public static void ApplyLoaded()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureConfig();
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("应用 UI 主题");
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if(!scene.isLoaded) continue;
            foreach(var root in scene.GetRootGameObjects())
            {
                Undo.RegisterFullObjectHierarchyUndo(root,"应用 UI 主题");
                Apply(root);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }
        Undo.CollapseUndoOperations(group);
        SceneView.RepaintAll();
    }
    public static void Apply(GameObject root)
    {
        foreach(var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if(dropdown.GetComponentInParent<LogisticsPanelUI>(true)!=null)continue;
            UIThemeBinding.Bind(dropdown.targetGraphic,UIThemeRole.Button,true);
        }
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if(graphic.GetComponentInParent<LogisticsPanelUI>(true) != null) continue;
            var binding = graphic.GetComponent<UIThemeBinding>();
            if(binding != null) { binding.Apply(); continue; }
            var text = graphic as TMP_Text;
            var image = graphic as Image;
            var role = ChimeraUITheme.RoleForColor(graphic.color);
            if(text != null)
            {
                var item = text.GetComponentInParent<InventoryItemSlotUI>(true);
                bool dynamicQuality = item != null && text == item.ItemLevelText;
                UIThemeBinding.Bind(text, dynamicQuality ? UIThemeRole.None : role);
                continue;
            }
            if(image == null || image.color.a == 0 || image.GetComponent<Mask>() != null) continue;
            string key = image.name.ToLowerInvariant();
            if(key.Contains("icon") || key.Contains("portrait") || key.Contains("quality") || key.Contains("checkmark") || key.Contains("图标")) continue;
            string path = AssetDatabase.GetAssetPath(image.sprite);
            // Content art and deliberately authored sprites are never classified by name.
            if(image.sprite != null && path.StartsWith("Assets/") && !path.EndsWith("ChimeraRounded.png")) continue;
            if(role == UIThemeRole.None) continue;
            bool button = image.GetComponent<Button>()?.targetGraphic == image;
            if(button && role == UIThemeRole.Accent) role = UIThemeRole.AccentButton;
            UIThemeBinding.Bind(image, role, image.GetComponent<Outline>() != null);
        }
        foreach(var component in root.GetComponentsInChildren<Component>(true))
        {
            if(component == null || component.GetComponentInParent<LogisticsPanelUI>(true) != null) continue;
            EditorUtility.SetDirty(component);
            if(PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }
    public static void PrepareLayout(GameObject root)
    {
        foreach(var workshop in root.GetComponentsInChildren<AssemblyWorkshopUI>(true)) workshop.PrepareEditorFeedback();
        foreach(var task in root.GetComponentsInChildren<ProductionTaskUIItem>(true)) task.PrepareEditorLayout();
        foreach(var slot in root.GetComponentsInChildren<InventoryItemSlotUI>(true)) slot.PrepareEditorLayout();
        foreach(var factory in root.GetComponentsInChildren<FactoryUIModule>(true)) factory.PrepareEditorView();
        foreach(var inventory in root.GetComponentsInChildren<RightInventoryPanelUI>(true))
        {
            if(inventory.ContentRoot==null || inventory.ContentRoot.GetComponent<GridLayoutGroup>()==null)continue;
            var columns=inventory.ContentRoot.GetComponent<UIGridColumns>()??inventory.ContentRoot.gameObject.AddComponent<UIGridColumns>();
            columns.Columns=2;columns.MaxCellWidth=172;columns.HeightRatio=170f/172;columns.Refresh();
        }
        foreach(var factory in root.GetComponentsInChildren<FactoryUIModule>(true))
        {
            var grid=factory.ShelfGrid?.GetComponent<GridLayoutGroup>();
            if(grid!=null){grid.cellSize=new Vector2(100,98);grid.spacing=new Vector2(6,6);grid.padding=new RectOffset(6,6,6,6);}
            var queue=factory.TaskQueueContainer?.GetComponent<VerticalLayoutGroup>();
            if(queue!=null){queue.childControlWidth=true;queue.childForceExpandWidth=true;queue.spacing=6;queue.padding=new RectOffset(6,6,6,6);}
        }
    }
    private static void PrepareRecipe(GameObject root)
    {
        var icon=root.transform.Find("Icon") as RectTransform;
        if(icon==null)return;
        icon.anchorMin=new Vector2(.18f,.28f);icon.anchorMax=new Vector2(.82f,.96f);icon.offsetMin=icon.offsetMax=Vector2.zero;
        var label=root.transform.Find("RecipeName")?.GetComponent<TMP_Text>();
        if(label==null)
        {
            label=new GameObject("RecipeName",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.transform.SetParent(root.transform,false);
            var font=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/ItemHoverTooltip.prefab")?.GetComponentInChildren<TMP_Text>(true)?.font;
            if(font!=null)label.font=font;
        }
        label.text="生产配方";label.fontSize=15;label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=15;
        label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        label.rectTransform.anchorMin=new Vector2(.04f,.02f);label.rectTransform.anchorMax=new Vector2(.96f,.28f);
        label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
        UIThemeBinding.Bind(label,UIThemeRole.PrimaryText);
    }
    [MenuItem("Tools/Chimera/UI主题/同步主题到UI预制体")]
    public static void BakePrefabs() => BakePrefabs(false);
    private static void BakePrefabs(bool prepare)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureConfig();
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs","Assets/Resources/UI"}).Select(AssetDatabase.GUIDToAssetPath).ToArray())
        {
            if(path.EndsWith("/LogisticsPanel.prefab")) continue;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset == null || asset.GetComponent<RectTransform>() == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(root.GetComponent<RectTransform>() == null) continue;
                if(prepare)
                {
                    PrepareLayout(root);
                    if(path=="Assets/Prefabs/UI/Colony/物品小图.prefab")PrepareRecipe(root);
                }
                Apply(root); PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }
    // Batch entry for isolated validation; never opens/replaces a user's unsaved scene.
    public static void BakeProject()
    {
        BakePrefabs(true);
        foreach(string path in new[]{"Assets/Scenes/RTS_World_Master.unity","Assets/Scenes/Scene_MainMenu.unity"})
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach(var root in scene.GetRootGameObjects()) { PrepareLayout(root); Apply(root); }
            if(!SceneOrganization.HasMarker(scene, Stamp)) SceneManager.MoveGameObjectToScene(new GameObject(Stamp),scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }
}

// Upgrade an already open scene in place, preserving unsaved map/building edits.
// Only enabled after the validated assets and ready marker are delivered.
[InitializeOnLoad]
public static class ChimeraUISkinReady
{
    static ChimeraUISkinReady()
    {
        EditorApplication.delayCall += Upgrade;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += Upgrade;
        EditorApplication.playModeStateChanged += state =>
        { if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Upgrade; };
    }
    public static void Upgrade()
    {
        if(Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            !File.Exists(ChimeraUISkinAuthoring.ReadyPath) || !File.Exists(ChimeraUISkinAuthoring.ConfigPath)) return;
        if(!File.ReadAllText(ChimeraUISkinAuthoring.ReadyPath).Contains(ChimeraUISkinAuthoring.Stamp)) return;
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);
            if(!scene.isLoaded || !scene.path.StartsWith("Assets/Scenes/") || SceneOrganization.HasMarker(scene, ChimeraUISkinAuthoring.Stamp))continue;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("完善 UI 与接入主题配置");
            bool wasDirty=scene.isDirty;
            ChimeraUISkinAuthoring.EnsureConfig();
            foreach(var root in scene.GetRootGameObjects())
            {
                Undo.RegisterFullObjectHierarchyUndo(root,"完善 UI 与接入主题配置");
                ChimeraUISkinAuthoring.PrepareLayout(root);ChimeraUISkinAuthoring.Apply(root);
            }
            var stamp=new GameObject(ChimeraUISkinAuthoring.Stamp);SceneManager.MoveGameObjectToScene(stamp,scene);
            Undo.RegisterCreatedObjectUndo(stamp,"完成 UI 升级");Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!wasDirty)EditorSceneManager.SaveScene(scene);
        }
    }
}
public sealed class ChimeraUISkinReadyImport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
    {
        if(imported.Contains(ChimeraUISkinAuthoring.ReadyPath))EditorApplication.delayCall += ChimeraUISkinReady.Upgrade;
    }
}

[CustomEditor(typeof(ChimeraUIThemeConfig))]
public sealed class ChimeraUIThemeConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("保留当前米白配色。美术留空时使用简洁底板；彩色图建议关闭 Tint Artwork。九宫格边距在 Sprite Editor 中设置。应用主题不会清理素材、改变布局或修改物流界面。",MessageType.Info);
        DrawDefaultInspector();
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("预览 / 应用到打开的场景（可撤销）")) ChimeraUISkinAuthoring.ApplyLoaded();
            if(GUILayout.Button("同步到 UI 预制体")) ChimeraUISkinAuthoring.BakePrefabs();
        }
    }
}
