using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RTSGridSystem))]
public sealed class RTSGridSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var grid=(RTSGridSystem)target;
        serializedObject.Update();
        EditorGUILayout.HelpBox("宽、高以格子计。调整后点击“更新地图预览”，无需进入运行模式。",MessageType.Info);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("MapWidth"),new GUIContent("地图宽度（格）"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("MapHeight"),new GUIContent("地图高度（格）"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("CellSize"),new GUIContent("每格世界尺寸"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("GridOrigin"),new GUIContent("左下角格子中心"));
            var flavor=serializedObject.FindProperty("DefaultFlavor");
            flavor.enumValueIndex=EditorGUILayout.Popup("默认地面",flavor.enumValueIndex,new[]{"科技地面","荒地","血肉地面"});
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.LabelField("实际尺寸",$"{grid.MapWidth*grid.CellSize:0.##} × {grid.MapHeight*grid.CellSize:0.##} 世界单位（{(long)grid.MapWidth*grid.MapHeight:N0} 格）");
            EditorGUILayout.LabelField("单边范围",$"1～{RTSGridSystem.MaxMapDimension} 格");
            if(GUILayout.Button("更新地图预览",GUILayout.Height(30)))RTSMapEditor.UpdatePreview(grid);
            if(GUILayout.Button("将地图中心设为世界原点"))
            {
                Undo.RecordObject(grid,"居中地图原点");
                grid.GridOrigin=new Vector2(-(grid.MapWidth-1)*grid.CellSize*.5f,-(grid.MapHeight-1)*grid.CellSize*.5f);
                EditorUtility.SetDirty(grid);EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
            }
        }
        if(GUILayout.Button("在 Scene 窗口聚焦地图"))RTSMapEditor.Frame(grid);
        var visuals=grid.GetComponent<RTSMapVisuals>();
        if(visuals==null||!visuals.PreviewMatches(grid))EditorGUILayout.HelpBox("预览尚未生成或参数已变化，请更新地图预览。",MessageType.Warning);
        int outside=RTSMapEditor.CountOutsideBuildings(grid);
        if(outside>0)EditorGUILayout.HelpBox($"有 {outside} 座现有建筑的占地超出地图，请移动建筑或扩大地图。更新预览不会移动或删除建筑。",MessageType.Warning);
        if(EditorApplication.isPlayingOrWillChangePlaymode)EditorGUILayout.HelpBox("地图规格在编辑模式下设置，退出运行模式后再修改。",MessageType.Info);
    }
}

[CustomEditor(typeof(RTSMapVisuals))]
public sealed class RTSMapVisualsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var grid=((RTSMapVisuals)target).GetComponent<RTSGridSystem>();
        using(new EditorGUI.DisabledScope(grid==null||EditorApplication.isPlayingOrWillChangePlaymode))
            if(GUILayout.Button("更新地块图像与颜色"))RTSMapEditor.UpdatePreview(grid);
    }
}

public static class RTSMapEditor
{
    [MenuItem("Tools/Chimera/地图/地图设置")]
    public static void SelectMap()
    {
        var grid=Object.FindObjectsOfType<RTSGridSystem>(true).FirstOrDefault(x=>x.gameObject.scene==EditorSceneManager.GetActiveScene());
        if(grid==null){Debug.LogWarning("当前场景没有 RTSGridSystem，请打开 RTS_World_Master 场景。");return;}
        Selection.activeGameObject=grid.gameObject;EditorGUIUtility.PingObject(grid);
    }
    public static GameObject UpdatePreview(RTSGridSystem grid)
    {
        if(grid==null||EditorApplication.isPlayingOrWillChangePlaymode||EditorUtility.IsPersistent(grid))return null;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("更新矩形地图预览");
        Undo.RecordObject(grid,"地图规格校验");grid.ValidateSettings();
        var visuals=grid.GetComponent<RTSMapVisuals>()??Undo.AddComponent<RTSMapVisuals>(grid.gameObject);
        Undo.RecordObject(visuals,"更新地图预览参数");
        if(visuals.BaseTileSprite==null)visuals.BaseTileSprite=AssetDatabase.LoadAssetAtPath<Sprite>(WorldPixelArtAuthoring.TilePath)
            ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/World/Source/地块背景图.png");
        if(visuals.BaseTileSprite==null){Debug.LogError("请先为 RTSMapVisuals 指定地块图片。",visuals);return null;}
        // Tiled SpriteRenderer needs a full rectangle; the sprite and its GUID remain unchanged.
        var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(visuals.BaseTileSprite)) as TextureImporter;
        if(importer!=null)
        {
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            if(settings.spriteMeshType!=SpriteMeshType.FullRect)
            {settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();}
        }
        if(visuals.GeneratedRoot!=null)Undo.DestroyObjectImmediate(visuals.GeneratedRoot.gameObject);
        var root=visuals.Rebuild(grid);
        Undo.RegisterCreatedObjectUndo(root,"生成地图地块");
        EditorUtility.SetDirty(visuals);EditorUtility.SetDirty(grid);
        if(PrefabUtility.IsPartOfPrefabInstance(grid))PrefabUtility.RecordPrefabInstancePropertyModifications(grid);
        if(PrefabUtility.IsPartOfPrefabInstance(visuals))PrefabUtility.RecordPrefabInstancePropertyModifications(visuals);
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();
        return root;
    }
    public static int CountOutsideBuildings(RTSGridSystem grid)
    {
        int count=0;
        foreach(var building in Object.FindObjectsOfType<BuildingBase>(true))
        {
            if(building.gameObject.scene!=grid.gameObject.scene)continue;
            if(building.FootprintOffsets==null||building.FootprintOffsets.Count==0)
            {if(!grid.TryWorldToGrid(building.transform.position,out _))count++;continue;}
            if(building.FootprintOffsets.Any(offset=>!grid.TryWorldToGrid(building.transform.position+
                new Vector3(offset.x,offset.y,0)*grid.CellSize,out _)))count++;
        }
        return count;
    }
    public static void Frame(RTSGridSystem grid)
    {
        if(SceneView.lastActiveSceneView==null)return;
        var bounds=grid.WorldBounds;bounds.size=new Vector3(bounds.size.x,bounds.size.y,1);
        SceneView.lastActiveSceneView.in2DMode=true;SceneView.lastActiveSceneView.Frame(bounds,false);
    }
    public static void BakeCurrentMap()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
        foreach(var grid in Object.FindObjectsOfType<RTSGridSystem>(true))UpdatePreview(grid);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
}
