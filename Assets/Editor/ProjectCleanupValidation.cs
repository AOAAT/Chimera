using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run in an isolated project copy: -batchmode -executeMethod ProjectCleanupValidation.Run
// This validator never saves scenes or assets.
public static class ProjectCleanupValidation
{
    private static int errors;
    private static int phase;
    private static double phaseStarted;
    private static int sceneFrames;
    private static EnemyBrain spawnedTestEnemy;

    public static void Run()
    {
        try
        {
            Require(LayerMask.NameToLayer("Building") >= 0 && LayerMask.NameToLayer("Enemy_Body") >= 0, "Validation copy is missing project layer settings");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Require(scenes.SequenceEqual(new[] { "Assets/Scenes/Scene_MainMenu.unity", "Assets/Scenes/RTS_World_Master.unity" }), "Unexpected build scene order");
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/") || path.StartsWith("Assets/TextMesh Pro/")) continue;
                if (path.EndsWith(".prefab"))
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try { CheckHierarchy(root, path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                else if (path.EndsWith(".asset"))
                {
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (asset != null) CheckReferences(asset, path);
                }
            }
            foreach (string path in scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects()) CheckHierarchy(root, path);
            }
            Require(errors == 0, "Serialized asset validation failed");
            EditorSceneManager.OpenScene(scenes[0], OpenSceneMode.Single);
            // Only used in the isolated validation copy; keep this runner alive across play entry.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
            phase = 0;
            phaseStarted = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void CheckHierarchy(GameObject root, string path)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                path + ": missing script on " + transform.name);
            foreach (var component in transform.GetComponents<Component>())
                if (component != null) CheckReferences(component, path + "/" + transform.name);
        }
    }

    private static void CheckReferences(UnityEngine.Object obj, string path)
    {
        var serialized = new SerializedObject(obj);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                Require(false, path + ": missing reference " + property.propertyPath);
            if (property.name != "m_MethodName" || !property.propertyPath.Contains("m_PersistentCalls")) continue;
            string prefix = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.'));
            var target = serialized.FindProperty(prefix + ".m_Target")?.objectReferenceValue;
            string method = property.stringValue;
            Require(target != null && !string.IsNullOrEmpty(method), path + ": empty persistent event");
            if (target != null)
                Require(target.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Any(m => m.Name == method), path + ": missing event method " + method);
        }
    }

    private static void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) { phase = 1; phaseStarted = EditorApplication.timeSinceStartup; }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - phaseStarted > 90) { Fail(new Exception("Play smoke test timed out at phase " + phase)); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            if (phase == 1 && Time.frameCount > 2)
            {
                CheckGridShapes();
                var menu = UnityEngine.Object.FindObjectOfType<MainMenuUI>();
                Require(menu != null && menu.NewGameButton != null, "Main menu entry unavailable");
                menu.NewGameButton.onClick.Invoke();
                phase = 2;
                sceneFrames = Time.frameCount;
            }
            else if (phase == 2 && SceneManager.GetActiveScene().name == "RTS_World_Master" && Time.frameCount > sceneFrames + 15)
            {
                Require(RTSGridSystem.Instance != null, "Grid did not initialize");
                Require(BuildingBase.AllPlacedBuildings.Count == 4, "Preplaced buildings did not register exactly once");
                Require(PlayerInventoryManager.Instance != null && PopulationManager.Instance != null && CombatDirector.Instance != null, "Base services missing");
                CheckCameraAndEnemySpawn();
                foreach (var building in BuildingBase.AllPlacedBuildings)
                {
                    var grid = RTSGridSystem.Instance;
                    foreach (var offset in building.FootprintOffsets)
                    {
                        Vector3 position = building.transform.position + new Vector3(offset.x, offset.y) * grid.CellSize;
                        Require(grid.TryWorldToGrid(position, out Vector2Int index), "Preplaced building outside grid: " + building.name);
                        Require(grid.GetCell(index.x, index.y).Occupant == building.gameObject, "Building occupancy mismatch: " + building.name);
                    }
                }
                var headquarters = UnityEngine.Object.FindObjectOfType<HeadquartersBuilding>();
                SelectionContextHUD.Instance.Refresh(headquarters);
                Require(UnityEngine.Object.FindObjectOfType<ConstructionUIModule>() != null, "Construction UI did not open");
                // Let normal scene updates exercise population and UI subscriptions.
                phase = 3;
                phaseStarted = EditorApplication.timeSinceStartup;
            }
            else if (phase == 3 && EditorApplication.timeSinceStartup - phaseStarted > 3)
            {
                Require(PopulationManager.Instance.TotalResidents.Count > 0, "Headquarters did not recruit");
                Require(spawnedTestEnemy != null && spawnedTestEnemy.GetComponent<DamageReceiver>().CurrentHP > 0,
                    "Test enemy did not initialize successfully");
                UnityEngine.Object.Destroy(spawnedTestEnemy.gameObject);
                Debug.Log("CAMERA_AND_TEST_ENEMY_VALIDATED");
                CheckUIInteractions();
                PauseMenuUI.Instance.PauseGame();
                Require(Time.timeScale == 0f, "Pause failed");
                PauseMenuUI.Instance.ResumeGame();
                Require(Time.timeScale == 1f, "Resume failed");
                PauseMenuUI.Instance.MainMenuButton.onClick.Invoke();
                phase = 4;
                sceneFrames = Time.frameCount;
            }
            else if (phase == 4 && SceneManager.GetActiveScene().name == "Scene_MainMenu" && Time.frameCount > sceneFrames + 3)
            {
                Require(BuildingBase.AllPlacedBuildings.Count == 0, "Buildings leaked across scene unload");
                UnityEngine.Object.FindObjectOfType<MainMenuUI>().NewGameButton.onClick.Invoke();
                phase = 5;
                sceneFrames = Time.frameCount;
            }
            else if (phase == 5 && SceneManager.GetActiveScene().name == "RTS_World_Master" && Time.frameCount > sceneFrames + 10)
            {
                Require(BuildingBase.AllPlacedBuildings.Count == 4, "Base re-entry failed");
                Debug.Log("CLEANUP_VALIDATION_COMPLETE errors=" + errors);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(errors == 0 ? 0 : 1);
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckUIInteractions()
    {
        var hud = SelectionContextHUD.Instance;
        int residents = PopulationManager.Instance.TotalResidents.Count;
        var logButton = hud.ResidentRoot.GetComponentsInChildren<Button>(true).Single(b => b.name == "日志按钮");
        Require(logButton.onClick.GetPersistentMethodName(0) == "OnClickResidentLog", "Resident log still invokes exile");
        logButton.onClick.Invoke();
        Require(UIFeedback.CurrentMessage.Contains("尚未实现") && PopulationManager.Instance.TotalResidents.Count == residents, "Resident log did not safely show feedback");
        hud.BuildingUpgradeButton.onClick.Invoke();
        Require(UIFeedback.CurrentMessage.Contains("建筑升级"), "Upgrade feedback missing");
        hud.BuildingDismantleButton.onClick.Invoke();
        Require(UIFeedback.CurrentMessage.Contains("建筑拆除"), "Dismantle feedback missing");
        var factory = UnityEngine.Object.FindObjectOfType<FactoryBuilding>();
        int tasks = factory.TaskQueue.Count;
        var resources = GlobalResourceManager.Instance;
        float scrap = resources.CurrentScrap;
        factory.AddToQueue(null, "Validation", null, 10, new ResourceSet(scrap + 100, 0, 0));
        Require(factory.TaskQueue.Count == tasks && resources.CurrentScrap == scrap && UIFeedback.CurrentMessage.Contains("废料 100"), "Insufficient funds must provide feedback without mutation");
        GlobalWarehouseUI.Instance.OpenWarehouse();
        AssemblyWorkshopUI.Instance.OpenEmptyWorkshop(UnityEngine.Object.FindObjectOfType<AssemblerBuilding>());
        PauseMenuUI.Instance.HandleBack();
        Require(!AssemblyWorkshopUI.Instance.gameObject.activeSelf && GlobalWarehouseUI.Instance.gameObject.activeSelf && Time.timeScale == 1, "Back did not close only the latest window");
        PauseMenuUI.Instance.HandleBack();
        Require(!GlobalWarehouseUI.Instance.gameObject.activeSelf && Time.timeScale == 1, "Back failed to close warehouse");
        PauseMenuUI.Instance.HandleBack();
        Require(Time.timeScale == 0, "Back should pause after windows close");
        UIFeedback.Show("暂停时的提示");
        Require(UIFeedback.CurrentMessage == "暂停时的提示", "Feedback unavailable while paused");
        PauseMenuUI.Instance.HandleBack();
        Require(Time.timeScale == 1, "Back should resume paused game");
        Debug.Log("UI_INTERACTIONS_VALIDATED");
    }

    private static void CheckGridShapes()
    {
        foreach (Vector2Int size in new[] { new Vector2Int(3, 11), new Vector2Int(11, 3), new Vector2Int(7, 7) })
        {
            var go = new GameObject("GridValidation");
            go.SetActive(false);
            var grid = go.AddComponent<RTSGridSystem>();
            grid.MapWidth = size.x;
            grid.MapHeight = size.y;
            grid.GridOrigin = new Vector2(-23.5f, 14.25f);
            grid.CellSize = 1.75f;
            go.SetActive(true);
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                Require(grid.WorldToGrid(grid.GetCell(x, y).WorldPos) == new Vector2Int(x, y), "Grid coordinate round trip failed");
            Require(!grid.TryWorldToGrid(grid.GetCell(0, 0).WorldPos - Vector3.right * grid.CellSize, out _), "Out of bounds accepted");
            for (int y = 0; y < size.y; y++) grid.GetCell(size.x - 1, y).IsOccupied = true;
            var accessible = ConnectivityManager.GetAccessibleArea(new HashSet<Vector2Int>());
            Require(accessible.Contains(new Vector2Int(0, size.y / 2)), "Connectivity still depends on the right boundary");
            UnityEngine.Object.DestroyImmediate(go);
        }
        Debug.Log("GRID_SHAPES_VALIDATED portrait, landscape, square");
    }

    private static void CheckCameraAndEnemySpawn()
    {
        var mover = UnityEngine.Object.FindObjectOfType<RTSCameraMover>();
        var effects = ScreenEffectManager.Instance;
        var camera = Camera.main;
        Require(mover != null && effects != null && camera != null, "Camera services missing");
        Require(mover.transform != effects.CameraTransform && effects.CameraTransform.IsChildOf(mover.transform),
            "Movement and shake must own separate transforms");
        Vector3 start = camera.transform.position;
        Vector3 localRest = camera.transform.localPosition;
        // Exercise the same movement path used by arrow keys, interleaved with the shake owner's updates.
        for (int i = 0; i < 10; i++)
        {
            mover.Pan(new Vector2(0.1f, 0.05f));
            effects.SendMessage("Update");
        }
        Require(Vector3.Distance(camera.transform.position, start + new Vector3(1f, 0.5f, 0)) < 0.001f,
            "Camera movement was reset between frames");
        Vector3 beforeDrag = camera.transform.position;
        mover.PanScreenDelta(new Vector2(30, 15));
        effects.SendMessage("Update");
        Require(Vector3.Distance(camera.transform.position, beforeDrag) > 0.001f, "Mouse pan did not persist");
        effects.TriggerShake(0.1f, 1f);
        effects.SendMessage("Update");
        mover.Pan(new Vector2(0.2f, 0.1f));
        Vector3 movedRoot = mover.transform.position;
        effects.TriggerShake(0, 0);
        effects.SendMessage("Update");
        Require(mover.transform.position == movedRoot && camera.transform.localPosition == localRest,
            "Shake recovery changed camera navigation position");
        mover.Pan(new Vector2(10000, 10000));
        var bounds = RTSGridSystem.Instance.WorldBounds;
        Require(Mathf.Abs(camera.transform.position.x - bounds.max.x) < 0.001f &&
            Mathf.Abs(camera.transform.position.y - bounds.max.y) < 0.001f, "Camera bounds failed");
        mover.Pan(new Vector2(start.x - camera.transform.position.x, start.y - camera.transform.position.y));

        var bench = UnityEngine.Object.FindObjectOfType<RTSTestBench>();
        Require(bench != null && bench.enabled && bench.BaseEnemyPrefab != null && bench.EnemyPool.Count > 0,
            "E-key test spawner missing or unconfigured");
        var before = UnityEngine.Object.FindObjectsOfType<EnemyBrain>();
        // Invoke the exact action dispatched by RTSTestBench.Update on E.
        bench.SendMessage("SpawnEnemyAtMouse");
        spawnedTestEnemy = UnityEngine.Object.FindObjectsOfType<EnemyBrain>().Single(e => !before.Contains(e));
        Require(spawnedTestEnemy.MyData != null, "Spawned enemy has no blueprint");
    }

    private static void Fail(Exception exception)
    {
        Debug.LogException(exception);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(1);
    }
}
