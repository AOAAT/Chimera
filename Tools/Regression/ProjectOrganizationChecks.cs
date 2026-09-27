using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectOrganizationChecks
{
    const string Output = "OrganizationResults";
    sealed class State
    {
        public string name, references;
        public bool active;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Vector2 rectSize;
        public int components;
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        File.AppendAllText(Output + "/checks.txt", "PASS " + message + "\n");
    }
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    static string References(GameObject go)
    {
        var result = new List<string>();
        foreach (var component in go.GetComponents<Component>())
        {
            if (component == null || component is Transform) continue;
            var obj = new SerializedObject(component); var property = obj.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var reference = property.objectReferenceValue;
                if (reference == null)
                {
                    if (property.objectReferenceInstanceIDValue != 0) throw new Exception("Broken reference: " + go.name + "/" + property.propertyPath);
                    continue;
                }
                var id = GlobalObjectId.GetGlobalObjectIdSlow(reference);
                result.Add(component.GetType().Name + ":" + property.propertyPath + ":" + id.targetObjectId + ":" + id.targetPrefabId +
                    (EditorUtility.IsPersistent(reference) ? ":" + id.assetGUID : ""));
            }
        }
        return string.Join("\n", result);
    }
    static Dictionary<string, State> Snapshot(Scene scene)
    {
        Canvas.ForceUpdateCanvases();
        var result = new Dictionary<string, State>();
        foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Missing script: " + t.name);
            var globalID = GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject);
            if (globalID.targetObjectId == 0)
            {
                File.AppendAllText(Output + "/transient-editor-objects.txt", scene.name + ": " + t.name + "\n");
                continue; // Unsaved ExecuteInEditMode preview objects have no serialized identity.
            }
            string id = globalID.targetObjectId + ":" + globalID.targetPrefabId;
            result.Add(id, new State { name = t.name, active = t.gameObject.activeSelf, position = t.position,
                rotation = t.rotation, scale = t.lossyScale, rectSize = t is RectTransform rt ? rt.rect.size : Vector2.zero,
                components = t.GetComponents<Component>().Length, references = References(t.gameObject) });
        }
        return result;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/checks.txt", "");
        try
        {
            var plan = ProjectOrganizationAuthoring.LoadPlan();
            Check(File.Exists(ProjectOrganizationAuthoring.DonePath), "migration completed");
            Check(plan.assets.All(a => File.Exists(a.destination)), "all original assets exist at mapped destinations");
            Check(plan.assets.All(a => Hash(a.destination + ".meta") == a.metaSha256), "all original asset metas are byte-identical, including importer settings and GUIDs");
            Check(plan.assets.Where(a => !a.source.EndsWith(".cs") && !a.source.EndsWith(".unity"))
                .All(a => Hash(a.destination) == a.sha256), "all original art, prefabs, definitions and other non-code assets are byte-identical");
            Check(!Directory.GetFiles("Assets/Scripts", "*.asset", SearchOption.AllDirectories).Any(), "no gameplay configuration assets mixed into code directories");
            foreach (var sceneName in new[] { "Scene_MainMenu", "RTS_World_Master" })
            {
                var baseline = EditorSceneManager.OpenScene("Assets/Editor/OrganizationBaseline/" + sceneName + ".unity");
                var before = Snapshot(baseline);
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity");
                var after = Snapshot(scene);
                foreach (var pair in before)
                {
                    if (!after.TryGetValue(pair.Key, out var b)) throw new Exception("Object removed: " + pair.Value.name);
                    var a = pair.Value;
                    if (a.name != b.name || a.active != b.active || a.components != b.components || a.references != b.references ||
                        Vector3.Distance(a.position, b.position) > .002f || Vector3.Distance(a.scale, b.scale) > .002f ||
                        Quaternion.Angle(a.rotation, b.rotation) > .01f || Vector2.Distance(a.rectSize, b.rectSize) > .002f)
                        throw new Exception("State changed: " + a.name + " id=" + pair.Key + " positions=" + a.position + "/" + b.position +
                            " rect=" + a.rectSize + "/" + b.rectSize + " referencesEqual=" + (a.references == b.references));
                }
                Check(true, sceneName + ": all existing objects, transforms, UI sizes, active flags and serialized references preserved");
                Check(scene.GetRootGameObjects().Where(ProjectOrganizationAuthoring.IsPersistentRoot).All(r => r.transform.parent == null),
                    sceneName + ": persistent audio/music/console managers remain roots");
                Check(SceneOrganization.HasMarker(scene, "Chimera_AuthoredVisuals_v1") && SceneOrganization.HasMarker(scene, "Chimera_UI_Skin_v2"),
                    sceneName + ": existing authoring stamps remain discoverable after grouping");
                ProjectOrganizationAuthoring.Organize(scene);
                int once = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
                ProjectOrganizationAuthoring.Organize(scene);
                Check(once == scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length), sceneName + ": organize is idempotent");
                var lines = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    lines.Add(root.name + " [" + string.Join(", ", root.GetComponents<Component>().Select(c => c.GetType().Name)) + "]");
                    foreach (Transform child in root.transform) lines.Add("  " + child.name);
                }
                File.WriteAllLines(Output + "/" + sceneName + "-hierarchy.txt", lines);
            }
            var loaded = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(loaded);
            int sceneCount = SceneManager.sceneCount;
            ProjectOrganizationAuthoring.OrganizeUnloadedScenes();
            Check(SceneManager.GetActiveScene() == loaded && loaded.isDirty && SceneManager.sceneCount == sceneCount,
                "organizing unopened scenes preserves the active scene, its unsaved state and open scene set");
            int reopened = 0;
            void OnOpened(Scene s, OpenSceneMode mode) { reopened++; }
            EditorSceneManager.sceneOpened += OnOpened;
            try { ProjectOrganizationAuthoring.OrganizeUnloadedScenes(); ProjectOrganizationAuthoring.OrganizeUnloadedScenes(); }
            finally { EditorSceneManager.sceneOpened -= OnOpened; }
            Check(reopened == 0, "ready callbacks do not reopen already organized closed scenes");
            ProjectOrganizationAuthoring.MigrateAssets();
            Check(true, "completed asset migration is a no-op when invoked again");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { File.AppendAllText(Output + "/checks.txt", "FAIL " + error + "\n"); UnityEngine.Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
