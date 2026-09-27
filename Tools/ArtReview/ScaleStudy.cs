// Run only in an isolated Unity project: -batchmode -executeMethod ScaleStudy.Run -quit
// Creates disposable render objects. Never edits asset import settings or saves a scene.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ScaleStudy
{
    const string Output = "ScaleStudyResults";
    static GameObject root;
    static Sprite square;
    static T Find<T>(string filename) where T : Object
    {
        string path = AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath).Single(p => Path.GetFileNameWithoutExtension(p) == filename);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
    static SpriteRenderer Draw(string name, Sprite sprite, Transform parent, int order)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = order;
        return sr;
    }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<SpriteRenderer>();
        var bounds = renderers[0].bounds;
        foreach (var sr in renderers.Skip(1)) bounds.Encapsulate(sr.bounds);
        return bounds;
    }
    static void Ground(GameObject go, float x)
    {
        Bounds b = BoundsOf(go);
        go.transform.position += new Vector3(x - b.center.x, 1 - b.min.y, 0);
    }
    static Sprite DensitySample(Sprite source, float worldScale)
    {
        // A native-resolution SAMPLE, not production art. Source assets stay untouched.
        var raw = new Texture2D(2, 2); raw.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source.texture)));
        Rect r = source.rect;
        int w = Mathf.Max(1, Mathf.RoundToInt(source.bounds.size.x * worldScale * 32));
        int h = Mathf.Max(1, Mathf.RoundToInt(source.bounds.size.y * worldScale * 32));
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color32[w * h]; var src = raw.GetPixels32();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int sx = (int)r.x + Mathf.Min((int)r.width - 1, Mathf.FloorToInt((x + .5f) * r.width / w));
            int sy = (int)r.y + Mathf.Min((int)r.height - 1, Mathf.FloorToInt((y + .5f) * r.height / h));
            pixels[y * w + x] = src[sy * raw.width + sx];
        }
        tex.SetPixels32(pixels); tex.Apply(); Object.DestroyImmediate(raw);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var chassis = Find<ChassisDataSO>("CH1_禁军");
        var parts = new[] { Find<ComponentDataSO>("CORE1_陷阵核心"), Find<ComponentDataSO>("SUP1_厚实护甲"),
            Find<ComponentDataSO>("WPN4_狙击枪"), Find<ComponentDataSO>("WPN5_链锯"), Find<ComponentDataSO>("MOVE3_刀锋") };
        if (chassis.Sockets.Count != parts.Length) throw new Exception("Unexpected chassis socket count");
        for (int i = 0; i < parts.Length; i++)
            if (!chassis.Sockets[i].AllowedTypes.Contains(parts[i].Type)) throw new Exception("Incompatible sample part");
        Sprite resident = Resources.Load<ResidentSpriteSet>("Residents/WorkerSprites").Portrait;
        Sprite factory = Resources.LoadAll<Sprite>("Buildings/ColonyBuildings").Single(s => s.name == "Factory");
        var pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
        square = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        var report = "Isolated static art study. No collision/pathfinding validation; no production assets changed.\n";
        float[] mechScales = { .7f, 1f, 1.5625f };
        int[] factoryTiles = { 2, 3, 4 };
        for (int option = 0; option < 3; option++) for (int density = 0; density < 2; density++)
        {
            root = new GameObject("Disposable scale study");
            for (int x = 0; x <= 14; x++)
            {
                var sr = Draw("Grid", square, root.transform, -20); sr.color = new Color(.28f, .32f, .33f);
                sr.transform.position = new Vector3(x, 3, 0); sr.transform.localScale = new Vector3(1f / 64, 6, 1);
            }
            for (int y = 0; y <= 6; y++)
            {
                var sr = Draw("Grid", square, root.transform, -20); sr.color = new Color(.28f, .32f, .33f);
                sr.transform.position = new Vector3(7, y, 0); sr.transform.localScale = new Vector3(14, 1f / 64, 1);
            }
            var human = Draw("Resident", resident, root.transform, 2); Ground(human.gameObject, 1.5f);
            var mech = new GameObject("Assembled mech"); mech.transform.SetParent(root.transform, false);
            Draw("Chassis", chassis.ChassisSprite, mech.transform, 2);
            for (int i = 0; i < parts.Length; i++)
            {
                var slot = chassis.Sockets[i]; var part = parts[i];
                var hinge = new GameObject(slot.SlotName); hinge.transform.SetParent(mech.transform, false);
                hinge.transform.localPosition = slot.LocalPosition;
                hinge.transform.localRotation = Quaternion.Euler(0, 0, slot.MountAngle + part.BaseRotationOffset);
                hinge.transform.localScale = Vector3.one * slot.DefaultComponentScale * part.VisualScaleMultiplier;
                var sr = Draw(part.ComponentName, part.ComponentIcon, hinge.transform, 3);
                sr.transform.localPosition = -part.AnchorOffset;
            }
            mech.transform.localScale = Vector3.one * mechScales[option]; Ground(mech, 5.3f);
            float scale = factoryTiles[option] * .94f / Mathf.Max(factory.bounds.size.x, factory.bounds.size.y);
            Sprite sampled = density == 1 ? DensitySample(factory, scale) : factory;
            var building = Draw("Factory", sampled, root.transform, 2);
            building.transform.localScale = density == 0 ? Vector3.one * scale : new Vector3(
                factory.bounds.size.x * scale / sampled.bounds.size.x, factory.bounds.size.y * scale / sampled.bounds.size.y, 1);
            Ground(building.gameObject, 10.5f);
            var bounds = BoundsOf(mech);
            if (bounds.min.x < 2.1f || bounds.max.x > 8.5f || bounds.max.y > 6) throw new Exception("Study objects overlap or clip");
            var cam = new GameObject("Study Camera").AddComponent<Camera>();
            cam.transform.position = new Vector3(7, 3, -10); cam.orthographic = true; cam.orthographicSize = 3;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.20f, .24f, .25f);
            cam.allowMSAA = false; cam.allowHDR = false;
            var rt = new RenderTexture(896, 384, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt; cam.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(896, 384, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 896, 384), 0, 0); image.Apply();
            string name = ((char)('A' + option)).ToString() + (density == 0 ? "-source" : "-32px");
            File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            report += name + ": mech scale=" + mechScales[option] + ", mech frame bounds=" + bounds.size +
                ", mech source pixels/cell=" + 50 / mechScales[option] + ", factory footprint=" + factoryTiles[option] + "x" + factoryTiles[option] + "\n";
            RenderTexture.active = old; cam.targetTexture = null; rt.Release();
            Object.DestroyImmediate(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(root);
            if (density == 1) { Object.DestroyImmediate(sampled.texture); Object.DestroyImmediate(sampled); }
        }
        File.WriteAllText(Output + "/measurements.txt", report + "COMPLETE\n");
        Debug.Log("Scale study complete: " + Output);
    }
}
