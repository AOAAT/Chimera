using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MapEditorChecks
{
    const string Key="MapEditorChecks";
    static string Output=>Path.Combine(Directory.GetCurrentDirectory(),"MapResults");
    static double started,next;
    static int stage;
    static void Check(bool valid,string message)
    {
        if(!valid)throw new Exception(message);
        File.AppendAllText(Path.Combine(Output,"checks.txt"),"PASS "+message+"\n");
    }
    static Bounds RenderBounds(RTSMapVisuals visuals)
    {
        var renderers=visuals.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>();var bounds=renderers[0].bounds;
        foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    static void VerifyBounds(RTSGridSystem grid)
    {
        var actual=RenderBounds(grid.GetComponent<RTSMapVisuals>());var expected=grid.WorldBounds;
        Check(Vector2.Distance(actual.center,expected.center)<.002f&&Vector2.Distance(actual.size,expected.size)<.002f,
            $"rendered bounds match logical {grid.MapWidth}x{grid.MapHeight}, cell {grid.CellSize}, origin {grid.GridOrigin}");
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"checks.txt"),"");
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
            var grid=Object.FindObjectOfType<RTSGridSystem>();var visuals=grid.GetComponent<RTSMapVisuals>();
            Check(visuals.PreviewMatches(grid),"map preview is already serialized and visible before play");
            var buildings=Object.FindObjectsOfType<BuildingBase>();var positions=buildings.Select(x=>x.transform.position).ToArray();
            var authored=new GameObject("AuthoredMapDecoration");authored.transform.SetParent(grid.transform,false);
            authored.transform.position=new Vector3(-8,3,0);
            foreach(var size in new[]{new Vector2Int(3,11),new Vector2Int(11,3),new Vector2Int(33,65),new Vector2Int(64,64),new Vector2Int(512,512)})
            {
                grid.MapWidth=size.x;grid.MapHeight=size.y;grid.CellSize=1.75f;grid.GridOrigin=new Vector2(-23.5f,14.25f);
                RTSMapEditor.UpdatePreview(grid);VerifyBounds(grid);
                int expected=Mathf.CeilToInt(size.x/(float)RTSMapVisuals.ChunkSize)*Mathf.CeilToInt(size.y/(float)RTSMapVisuals.ChunkSize);
                Check(visuals.GeneratedRoot.childCount==expected,"chunk count is bounded for "+size);
                for(int x=0;x<size.x;x+=Mathf.Max(1,size.x-1))for(int y=0;y<size.y;y+=Mathf.Max(1,size.y-1))
                    Check(grid.WorldToGrid(grid.GridToWorld(x,y))==new Vector2Int(x,y),"editor coordinate round trip at "+x+","+y);
                Check(Vector3.Distance(grid.GetSnappedWorldPos(grid.GridToWorld(size.x-1,size.y-1)),grid.GridToWorld(size.x-1,size.y-1))<.001f,"editor snapping needs no play mode grid allocation");
            }
            grid.MapWidth=1;grid.MapHeight=1;grid.CellSize=1;grid.GridOrigin=new Vector2(1000,1000);
            RTSMapEditor.UpdatePreview(grid);
            Check(RTSMapEditor.CountOutsideBuildings(grid)==buildings.Length,"shrinking warns about every out-of-bounds existing building");
            Check(buildings.Select((x,i)=>x!=null&&x.transform.position==positions[i]).All(x=>x)&&authored!=null&&authored.transform.position==new Vector3(-8,3,0),"rebuild preserves authored buildings and unrelated children");
            grid.MapWidth=-5;grid.MapHeight=99999;grid.CellSize=0;grid.ValidateSettings();
            Check(grid.MapWidth==1&&grid.MapHeight==512&&grid.CellSize==.01f,"invalid dimensions and zero cell size are sanitized");
            grid.MapWidth=24;grid.MapHeight=16;grid.CellSize=1.5f;grid.GridOrigin=new Vector2(-12,-8);
            RTSMapEditor.UpdatePreview(grid);Undo.FlushUndoRecordObjects();var originalBounds=RenderBounds(visuals);
            grid.MapWidth=47;grid.MapHeight=25;RTSMapEditor.UpdatePreview(grid);Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Check(Vector2.Distance(RenderBounds(visuals).size,originalBounds.size)<.002f&&grid.transform.Cast<Transform>().Count(x=>x.name==RTSMapVisuals.GeneratedRootName)==1,"undo restores previous preview without duplicates");
            Undo.PerformRedo();VerifyBounds(grid);
            grid.MapWidth=24;grid.MapHeight=16;grid.CellSize=1.5f;grid.GridOrigin=new Vector2(-12,-8);grid.DefaultFlavor=TileFlavor.TechBase;
            RTSMapEditor.UpdatePreview(grid);VerifyBounds(grid);
            Check(visuals.GeneratedRoot.GetComponentsInChildren<SpriteRenderer>().All(x=>x.color==visuals.ColorTech),"selected ground palette is applied to preview");
            Capture(grid,"rectangular-map-editor.png");
            // Reload the baked default scene to prove editor-only changes did not leak into assets.
            EditorSceneManager.OpenScene(scene.path);
            grid=Object.FindObjectOfType<RTSGridSystem>();visuals=grid.GetComponent<RTSMapVisuals>();
            Check(grid.MapWidth==100&&grid.MapHeight==12&&visuals.PreviewMatches(grid),"saved scene retains its chosen dimensions and authored preview");
            grid.MapWidth=70;grid.MapHeight=40;RTSMapEditor.UpdatePreview(grid);
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        catch(Exception e){File.AppendAllText(Path.Combine(Output,"checks.txt"),"FAIL "+e+"\n");Debug.LogException(e);EditorApplication.Exit(1);}
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if(!SessionState.GetBool(Key,false))return;
        started=EditorApplication.timeSinceStartup;next=started+6;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.5;
        try
        {
            if(EditorApplication.timeSinceStartup-started>90)throw new Exception("Runtime timeout");
            var grid=RTSGridSystem.Instance;
            if(grid==null)return;
            var visuals=grid.GetComponent<RTSMapVisuals>();
            switch(stage++)
            {
                case 0:
                    Check(grid.MapWidth==70&&grid.MapHeight==40&&grid.GetCell(69,39)!=null&&grid.GetCell(70,39)==null,"runtime allocates configured rectangular grid");
                    VerifyBounds(grid);
                    Check(visuals.GeneratedRoot.childCount==6&&grid.transform.Cast<Transform>().Count(x=>x.name==RTSMapVisuals.GeneratedRootName)==1,"play mode reuses authored chunks without a second map");
                    Check(!grid.TryWorldToGrid(grid.GridToWorld(70,39),out _),"building placement rejects positions outside new boundary");
                    var path=GridPathfinder.FindPath(grid.GridToWorld(60,30),grid.GridToWorld(69,39),false);
                    Check(path!=null&&path.Count>0&&Vector3.Distance(path.Last(),grid.GridToWorld(69,39))<.001f,"pathfinding reaches the expanded map region");
                    var mover=Object.FindObjectOfType<RTSCameraMover>();mover.Pan(new Vector2(10000,10000));
                    Check(Vector2.Distance(Camera.main.transform.position,grid.WorldBounds.max)<.001f,"camera movement clamps to configured map bounds");
                    grid.GetCell(20,20).IsOccupied=true;visuals.Rebuild(grid);
                    Check(grid.GetCell(20,20).IsOccupied,"visual refresh does not reset occupancy or gameplay grid");break;
                case 1:
                    Check(grid.transform.Cast<Transform>().Count(x=>x.name==RTSMapVisuals.GeneratedRootName)==1&&!grid.transform.Cast<Transform>().Any(x=>x.name=="Retired_Grid_Tiles"),"runtime visual rebuild removes retired root");
                    File.AppendAllText(Path.Combine(Output,"checks.txt"),"COMPLETE\n");Finish(0);break;
            }
        }
        catch(Exception e){File.AppendAllText(Path.Combine(Output,"checks.txt"),"FAIL "+e+"\n");Debug.LogException(e);Finish(1);}
    }
    static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    static void Capture(RTSGridSystem grid,string name)
    {
        var root=grid.GetComponent<RTSMapVisuals>().GeneratedRoot;
        // Include the scene's 2D lighting when isolating the map for capture.
        var objects=root.GetComponentsInChildren<Transform>().Select(x=>x.gameObject)
            .Concat(Object.FindObjectsOfType<UnityEngine.Rendering.Universal.Light2D>().Select(x=>x.gameObject)).Distinct().ToArray();
        var layers=objects.Select(x=>x.layer).ToArray();
        foreach(var obj in objects)obj.layer=30;
        var camera=new GameObject("MapPreviewCapture").AddComponent<Camera>();camera.orthographic=true;camera.cullingMask=1<<30;
        var bounds=grid.WorldBounds;camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-10);
        camera.orthographicSize=Mathf.Max(bounds.size.y/2,bounds.size.x/2/(4f/3))*1.06f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.12f,.14f);
        var rt=new RenderTexture(1024,768,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(1024,768,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1024,768),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Output,name),texture.EncodeToPNG());RenderTexture.active=previous;
        for(int i=0;i<objects.Length;i++)objects[i].layer=layers[i];
        camera.targetTexture=null;rt.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
}
