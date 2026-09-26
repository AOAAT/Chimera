using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit, idempotent migration from runtime-only presentation to editable assets.</summary>
[InitializeOnLoad]
public static class ChimeraVisualAuthoring
{
    private const string Stamp = "Chimera_AuthoredVisuals_v1";
    private const string Ready = "Assets/Resources/UI/EditorVisualsReady.txt";
    private const string Atlas = "Assets/Resources/Buildings/ColonyBuildings.png";

    static ChimeraVisualAuthoring()
    {
        EditorApplication.delayCall += UpgradeLoadedScenes;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += UpgradeLoadedScenes;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += UpgradeLoadedScenes;
        };
    }

    // A currently open scene may still contain the previous in-memory version after asset import.
    // Migrate that scene in place; never reload it or save unrelated unsaved user edits.
    private static void UpgradeLoadedScenes()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || !File.Exists(Ready)) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !scene.path.StartsWith("Assets/Scenes/") || IsAuthored(scene)) continue;
            bool wasDirty = scene.isDirty;
            foreach (GameObject root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "迁移可编辑界面与建筑");
            UpgradeScene(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            else Debug.Log("[可编辑美术] 已更新当前场景；保留了未保存修改，请照常保存场景。");
        }
    }

    public static void ScheduleOpenSceneUpgrade() => EditorApplication.delayCall += UpgradeLoadedScenes;

    private static bool IsAuthored(Scene scene) => scene.GetRootGameObjects().Any(x => x.name == Stamp);
    private static T[] InScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("Tools/Chimera/美术与UI/预览仓库界面（编辑模式）")]
    public static void PreviewWarehouse() => Preview(Object.FindObjectOfType<GlobalWarehouseUI>(true)?.gameObject);

    [MenuItem("Tools/Chimera/美术与UI/预览物流界面（编辑模式）")]
    public static void PreviewLogistics()
    {
        var panel = Object.FindObjectOfType<LogisticsPanelUI>(true);
        if (panel != null) Preview(panel.transform.Find("LogisticsWindow")?.gameObject);
    }

    [MenuItem("Tools/Chimera/美术与UI/预览居民名册（编辑模式）")]
    public static void PreviewRoster() => Preview(Object.FindObjectOfType<ResidentRosterPanelUI>(true)?.gameObject);

    private static void Preview(GameObject root)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || root == null) return;
        Undo.RegisterFullObjectHierarchyUndo(root, "预览界面");
        root.SetActive(true);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Chimera/美术与UI/同步当前场景为可编辑资源")]
    public static void UpgradeCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        UpgradeScene(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Chimera/美术与UI/对选中对象重新应用统一样式")]
    public static void RestyleSelection()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (var root in Selection.gameObjects)
        {
            Undo.RegisterFullObjectHierarchyUndo(root, "重新应用统一样式");
            foreach (var building in root.GetComponentsInChildren<BuildingBase>(true)) BuildingVisualTheme.Apply(building, true);
            if (root.GetComponent<RectTransform>() != null) ChimeraUITheme.ApplyPanel(root, false);
            ChimeraUIThemeController.ApplyToRoot(root);
            Record(root);
            if (root.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.scene);
        }
    }

    public static void UpgradeScene(Scene scene)
    {
        if (IsAuthored(scene)) return;
        foreach (var root in scene.GetRootGameObjects()) ChimeraUIThemeController.ApplyToRoot(root);
        foreach (var hud in InScene<SelectionContextHUD>(scene)) hud.PrepareEditorLayout();
        foreach (var warehouse in InScene<GlobalWarehouseUI>(scene)) warehouse.PrepareEditorView();
        var grid = InScene<RTSGridSystem>(scene).FirstOrDefault();
        // Old scene instances can override GhostRenderer/BuildingIcon even after the prefab is migrated.
        foreach (var building in InScene<BuildingBase>(scene)) BuildingVisualTheme.Apply(building, true, grid != null ? grid.CellSize : 1);
        if (grid != null && InScene<LogisticsPanelUI>(scene).Length == 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/LogisticsPanel.prefab");
            if (prefab != null) PrefabUtility.InstantiatePrefab(prefab, scene);
        }
        if (grid != null && InScene<ResidentRosterPanelUI>(scene).Length == 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/ResidentRosterPanel.prefab");
            var roster = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            roster.SetActive(false);
        }
        var stamp = new GameObject(Stamp);
        SceneManager.MoveGameObjectToScene(stamp, scene);
        foreach (var root in scene.GetRootGameObjects()) Record(root);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void Record(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    // Invoked in the isolated project by the regression runner. Only known game assets are migrated.
    public static void BakeProject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before baking.");
        AssetDatabase.ImportAsset(Atlas, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool building = root.GetComponentInChildren<BuildingBase>(true) != null;
                bool ui = root.GetComponent<RectTransform>() != null && root.GetComponentInChildren<Graphic>(true) != null;
                if (!building && !ui) continue;
                foreach (var item in root.GetComponentsInChildren<BuildingBase>(true)) BuildingVisualTheme.Apply(item);
                if (ui) ChimeraUITheme.ApplyPanel(root, false);
                ChimeraUIThemeController.ApplyToRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingDataSO", new[] { "Assets" }))
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            var sprite = data.Prefab != null ? BuildingVisualTheme.GetIcon(data.Prefab.GetComponent<BuildingBase>()) : null;
            if (sprite == null) continue;
            data.Icon = sprite; EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        }
        const string depotPath = "Assets/Resources/Buildings/Warehouse.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(depotPath) == null)
        {
            var go = new GameObject("综合仓库", typeof(WarehouseBuilding));
            try
            {
                go.GetComponent<WarehouseBuilding>().BuildingName = "综合仓库";
                BuildingVisualTheme.Apply(go.GetComponent<WarehouseBuilding>());
                PrefabUtility.SaveAsPrefabAsset(go, depotPath);
            }
            finally { Object.DestroyImmediate(go); }
        }
        const string logisticsPath = "Assets/Resources/UI/LogisticsPanel.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(logisticsPath) == null)
        {
            var go = new GameObject("物流界面", typeof(RectTransform), typeof(LogisticsPanelUI));
            try { go.GetComponent<LogisticsPanelUI>().PrepareEditorView(); PrefabUtility.SaveAsPrefabAsset(go, logisticsPath); }
            finally { Object.DestroyImmediate(go); }
        }
        else
        {
            var go = PrefabUtility.LoadPrefabContents(logisticsPath);
            try { go.GetComponent<LogisticsPanelUI>().PrepareEditorView(); PrefabUtility.SaveAsPrefabAsset(go, logisticsPath); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        foreach (string path in new[] { "Assets/Scenes/RTS_World_Master.unity", "Assets/Scenes/Scene_MainMenu.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path);
            UpgradeScene(scene);
            foreach (var warehouse in InScene<GlobalWarehouseUI>(scene))
            {
                const string warehousePath = "Assets/Resources/UI/GlobalWarehousePanel.prefab";
                PrefabUtility.SaveAsPrefabAsset(warehouse.gameObject, warehousePath);
            }
            EditorSceneManager.SaveScene(scene);
        }
        File.WriteAllText(Ready, "Authored visual assets v1. Styles are applied explicitly in the editor, not periodically in Play mode.\n");
        AssetDatabase.ImportAsset(Ready);
        Debug.Log("[可编辑美术] 建筑、图纸、UI 预制体与场景已保存。");
    }
}

public class ChimeraVisualReadyImport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Any(x => x == "Assets/Resources/UI/EditorVisualsReady.txt" || x.StartsWith("Assets/Scenes/") && x.EndsWith(".unity")))
            ChimeraVisualAuthoring.ScheduleOpenSceneUpgrade();
    }
}
