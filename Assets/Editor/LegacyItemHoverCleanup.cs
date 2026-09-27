using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Repairs an already-open scene too, including unsaved scenes retained during a script reload.
[InitializeOnLoad]
public static class LegacyItemHoverCleanup
{
    public const string ReadyPath = "Assets/Resources/UI/LegacyItemHoverRemoved.txt";
    const string OldPrefab = "Assets/Prefabs/物品详情ItemDetailPanelUI.prefab";
    static LegacyItemHoverCleanup()
    {
        Schedule();
        EditorSceneManager.sceneOpened += (_, __) => Schedule();
        EditorApplication.playModeStateChanged += state => { if(state==PlayModeStateChange.EnteredEditMode)Schedule(); };
    }
    public static void Schedule() => EditorApplication.delayCall += CleanOpenScenes;
    private static bool Clean(GameObject root,bool undo)
    {
        bool changed=false;
        foreach(var node in root.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if(node==null)continue;
            bool legacy=node.name=="物品详情ItemDetailPanelUI" || node.name=="物品详情ItemDetailPanelUI(Clone)" ||
                node.GetComponents<MonoBehaviour>().Any(x=>x!=null&&x.GetType().Name=="ItemDetailPanelUI");
            bool anchor=node.name=="详情页锚点"&&node.GetComponentInParent<FactoryUIModule>(true)!=null;
            if(!legacy&&!anchor)continue;
            if(undo)Undo.DestroyObjectImmediate(node.gameObject);else Object.DestroyImmediate(node.gameObject);
            changed=true;
        }
        if(root!=null && root.name=="Canvas_Overlay" && root.transform.childCount==0 && root.GetComponent<Canvas>()!=null &&
            root.GetComponents<MonoBehaviour>().All(x=>x is CanvasScaler || x is GraphicRaycaster))
        {
            if(undo)Undo.DestroyObjectImmediate(root);else Object.DestroyImmediate(root);
            changed=true;
        }
        return changed;
    }
    private static void CleanOpenScenes()
    {
        if(Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||!File.Exists(ReadyPath))return;
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);
            if(!scene.isLoaded||!scene.path.StartsWith("Assets/Scenes/"))continue;
            bool wasDirty=scene.isDirty,changed=false;
            foreach(var root in scene.GetRootGameObjects())changed|=Clean(root,true);
            if(changed){EditorSceneManager.MarkSceneDirty(scene);if(!wasDirty)EditorSceneManager.SaveScene(scene);}
        }
    }
    public static void Bake()
    {
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs","Assets/Resources/UI"}).Select(AssetDatabase.GUIDToAssetPath).ToArray())
        {
            if(path==OldPrefab)continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try {if(Clean(root,false))PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(var path in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var scene=EditorSceneManager.OpenScene(path);bool changed=false;
            foreach(var root in scene.GetRootGameObjects())changed|=Clean(root,false);
            if(changed)EditorSceneManager.SaveScene(scene);
        }
        if(File.Exists(OldPrefab))AssetDatabase.DeleteAsset(OldPrefab);
        AssetDatabase.SaveAssets();
        Debug.Log("Legacy item hover objects and factory anchor removed.");
    }
}
public sealed class LegacyItemHoverCleanupImport : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
    {if(imported.Contains(LegacyItemHoverCleanup.ReadyPath))LegacyItemHoverCleanup.Schedule();}
}
