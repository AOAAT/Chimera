using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time, GUID-preserving migration plus a reusable, undoable scene organizer.</summary>
[InitializeOnLoad]
public static class ProjectOrganizationAuthoring
{
    public const string PlanPath = "Tools/ProjectOrganization/migration.json";
    public const string ReadyPath = "Assets/Resources/ProjectOrganizationReady.txt";
    public const string DonePath = "Tools/ProjectOrganization/applied.json";
    public const string Stamp = "Chimera_Organization_v1";
    [Serializable] public class Move { public string source; public string destination; }
    [Serializable] public class AssetRecord { public string source, destination, sha256, metaSha256; }
    [Serializable] public class Plan { public int version; public Move[] moves, replacements; public AssetRecord[] assets; }
    static bool busy;

    static ProjectOrganizationAuthoring()
    {
        EditorApplication.delayCall += ApplyWhenReady;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += ApplyWhenReady;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyWhenReady;
        };
    }

    public static Plan LoadPlan() => JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath, Encoding.UTF8));

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
            throw new IOException("Cannot create asset folder: " + path);
    }

    public static void MigrateAssets()
    {
        if (File.Exists(DonePath)) return;
        var plan = LoadPlan();
        // A previous process may have completed all asset moves before a text update failed.
        // Resume only when every destination and original importer hash can be verified.
        if (!Directory.Exists(plan.moves[0].source) && plan.assets.All(a => File.Exists(a.destination)))
        {
            foreach (var asset in plan.assets)
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(asset.destination + ".meta"))).Replace("-", "").ToLowerInvariant() != asset.metaSha256)
                        throw new IOException("Cannot resume: importer differs for " + asset.destination);
            RewritePathReferences(plan);
            File.WriteAllText(DonePath, "{\"version\":1,\"movedAssets\":" + plan.assets.Count(a => a.source != a.destination) + "}", Encoding.UTF8);
            AssetDatabase.Refresh();
            return;
        }
        // Preflight the whole plan before mutating any asset, including intermediate moves.
        var virtualPaths = new HashSet<string>(AssetDatabase.GetAllAssetPaths());
        foreach (var move in plan.moves)
        {
            if (!virtualPaths.Contains(move.source) || virtualPaths.Contains(move.destination))
                throw new IOException("Migration path conflict: " + move.source + " -> " + move.destination);
            var affected = virtualPaths.Where(p => p == move.source || p.StartsWith(move.source + "/", StringComparison.Ordinal)).ToArray();
            foreach (var path in affected) { virtualPaths.Remove(path); virtualPaths.Add(move.destination + path.Substring(move.source.Length)); }
        }
        var guidMap = plan.assets.ToDictionary(a => a.destination, a => AssetDatabase.AssetPathToGUID(a.source));
        var completed = new List<Move>();
        EditorApplication.LockReloadAssemblies();
        try
        {
            foreach (var move in plan.moves)
            {
                EnsureFolder(Path.GetDirectoryName(move.destination).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(move.source, move.destination);
                if (!string.IsNullOrEmpty(error)) throw new IOException(move.source + " -> " + move.destination + ": " + error);
                completed.Add(move);
            }
            RewritePathReferences(plan);
            // Empty former UI folders carry no content and are safe to remove after the moves.
            foreach (string path in Directory.GetDirectories("Assets/Scripts", "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
                if (!Directory.EnumerateFileSystemEntries(path).Any()) AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
            foreach (var item in guidMap)
                if (AssetDatabase.AssetPathToGUID(item.Key) != item.Value) throw new IOException("GUID changed: " + item.Key);
            File.WriteAllText(DonePath, "{\"version\":1,\"movedAssets\":" + plan.assets.Count(a => a.source != a.destination) + "}", Encoding.UTF8);
        }
        catch
        {
            // Keep partial progress inspectable if Unity itself refuses a move.
            File.WriteAllText("Tools/ProjectOrganization/partial-migration.txt", string.Join("\n", completed.Select(m => m.source + " -> " + m.destination)));
            throw;
        }
        finally { EditorApplication.UnlockReloadAssemblies(); }
        AssetDatabase.Refresh();
    }

    static void RewritePathReferences(Plan plan)
    {
        var extensions = new HashSet<string> { ".cs", ".py", ".md", ".html", ".json" };
        var files = new[] { "Assets", "Tools", "Docs", "ArtSource", "ArtReview" }
            .Where(Directory.Exists).SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            .Where(p => extensions.Contains(Path.GetExtension(p)) && !p.Replace('\\', '/').Contains("Tools/ProjectOrganization/"))
            .Concat(new[] { "README.md" }).Where(File.Exists).ToArray();
        var map = plan.replacements.ToDictionary(m => m.source, m => m.destination);
        // Longest path first and a token boundary prevent e.g. moving Foo from corrupting FooBar.
        var pattern = new Regex(string.Join("|", map.Keys.OrderByDescending(p => p.Length).Select(p => Regex.Escape(p) + "(?=$|[/\\\\\"'`\\s):,;])")));
        foreach (string path in files)
        {
            string before = File.ReadAllText(path, Encoding.UTF8);
            string after = pattern.Replace(before, match => map[match.Value]);
            if (path.EndsWith(".cs", StringComparison.Ordinal))
                after = Regex.Replace(after, @"scene\.GetRootGameObjects\(\)\.Any\(x\s*=>\s*x\.name\s*==\s*(ChimeraUISkinAuthoring\.Stamp|Stamp)\)",
                    "SceneOrganization.HasMarker(scene, $1)");
            if (before != after) File.WriteAllText(path, after, new UTF8Encoding(false));
        }
    }

    public static bool IsPersistentRoot(GameObject root) => root.GetComponent<GlobalAudioManager>() != null ||
        root.GetComponent<MusicManager>() != null || root.GetComponent<IngameConsole>() != null || root.GetComponent<SaveGameManager>() != null;

    static string Category(GameObject root)
    {
        if (root.name.StartsWith("Chimera_", StringComparison.Ordinal)) return SceneOrganization.Editor;
        if (root.GetComponent<BuildingBase>() != null) return SceneOrganization.Buildings;
        if (root.GetComponent<RTSGridSystem>() != null || root.GetComponentInChildren<Camera>(true) != null || root.name == "Global Light 2D") return SceneOrganization.World;
        if (root.GetComponent<ChassisSetupHelper>() != null || root.GetComponent<EnemyTestBench>() != null ||
            root.GetComponent<RTSTestBench>() != null || root.name == "Enemy") return SceneOrganization.Debug;
        if (root.GetComponent<ResidentEntity>() != null || root.GetComponent<MechUnit2D>() != null || root.GetComponent<EnemyBrain>() != null) return SceneOrganization.Units;
        if (root.GetComponent<RectTransform>() != null || root.GetComponentInChildren<Canvas>(true) != null || root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) return SceneOrganization.UI;
        return SceneOrganization.Systems;
    }

    public static void Organize(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        string[] categories = { SceneOrganization.Systems, SceneOrganization.World, SceneOrganization.Buildings,
            SceneOrganization.Units, SceneOrganization.UI, SceneOrganization.Debug, SceneOrganization.Editor };
        var roots = scene.GetRootGameObjects();
        var groups = new Dictionary<string, Transform>();
        foreach (var name in categories)
        {
            var existing = roots.FirstOrDefault(r => r.name == name);
            if (existing != null && (existing.transform.position != Vector3.zero || existing.transform.rotation != Quaternion.identity ||
                existing.transform.localScale != Vector3.one || !existing.activeSelf))
                throw new InvalidOperationException("Scene folder must remain active at identity transform: " + name);
            var group = existing ?? new GameObject(name);
            if (existing == null) { SceneManager.MoveGameObjectToScene(group, scene); Undo.RegisterCreatedObjectUndo(group, "整理场景层级"); }
            groups.Add(name, group.transform);
        }
        foreach (var root in roots)
        {
            if (categories.Contains(root.name) || IsPersistentRoot(root)) continue;
            Undo.SetTransformParent(root.transform, groups[Category(root)], "整理场景层级");
        }
        if (!SceneOrganization.HasMarker(scene, Stamp))
        {
            var marker = new GameObject(Stamp); SceneManager.MoveGameObjectToScene(marker, scene);
            marker.transform.SetParent(groups[SceneOrganization.Editor], false); Undo.RegisterCreatedObjectUndo(marker, "记录场景整理");
        }
        foreach (var name in categories) groups[name].SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Chimera/项目整理/整理当前场景层级（可撤销）")]
    public static void OrganizeCurrent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("整理场景层级");
        Organize(SceneManager.GetActiveScene()); Undo.CollapseUndoOperations(group);
    }

    // Batch entry is used only in the isolated validation copy, never to reload the user's scene.
    public static void BakeProject()
    {
        MigrateAssets();
        foreach (var path in new[] { "Assets/Scenes/Scene_MainMenu.unity", "Assets/Scenes/RTS_World_Master.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path); Organize(scene); EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    public static void ApplyWhenReady()
    {
        if (busy || Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            !File.Exists(ReadyPath) || !File.Exists(PlanPath)) return;
        busy = true;
        try
        {
            MigrateAssets();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !scene.path.StartsWith("Assets/Scenes/", StringComparison.Ordinal) || SceneOrganization.HasMarker(scene, Stamp)) continue;
                bool wasDirty = scene.isDirty; Organize(scene);
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            OrganizeUnloadedScenes();
        }
        catch (Exception error) { UnityEngine.Debug.LogException(error); }
        finally { busy = false; }
    }

    public static void OrganizeUnloadedScenes()
    {
        var activeScene = SceneManager.GetActiveScene();
        foreach (string path in new[] { "Assets/Scenes/Scene_MainMenu.unity", "Assets/Scenes/RTS_World_Master.unity" })
        {
            if (SceneManager.GetSceneByPath(path).isLoaded) continue;
            // sceneOpened queues another ready callback. Inspect the serialized stamp first so
            // an already organized, closed scene is not reopened on every editor update.
            if (File.ReadAllText(path).Contains("m_Name: " + Stamp)) continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (!SceneOrganization.HasMarker(scene, Stamp)) { Organize(scene); EditorSceneManager.SaveScene(scene); }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
    }
}

public sealed class ProjectOrganizationReadyImport : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
    {
        if (imported.Contains(ProjectOrganizationAuthoring.ReadyPath)) EditorApplication.delayCall += ProjectOrganizationAuthoring.ApplyWhenReady;
    }
}
