// Isolated batch export only: -executeMethod RotationStudy.Run -quit
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RotationStudy
{
    const string Output = "RotationStudyResults";
    [Serializable] public class Layer
    {
        public string name, file;
        public float ppu, width, height, pivotX, pivotY, slotX, slotY, angle, scale, anchorX, anchorY, muzzleX, muzzleY;
        public bool weapon;
    }
    [Serializable] public class Model { public Layer[] layers; public float worldSize = 6, density = 50; public VertexCheck[] checks; }
    [Serializable] public class VertexCheck { public int layer; public float angle; public Vector3 corner, pivot, muzzle; }
    static T Find<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.FindAssets("t:" + typeof(T).Name)
        .Select(AssetDatabase.GUIDToAssetPath).Single(p => Path.GetFileNameWithoutExtension(p) == name));
    static Layer Export(Sprite sprite, string name, string file)
    {
        if (sprite.pixelsPerUnit != 50) throw new Exception("Unexpected source PPU: " + name);
        var raw = new Texture2D(2, 2); raw.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
        var r = sprite.rect; var crop = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
        crop.SetPixels(raw.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height)); crop.Apply();
        File.WriteAllBytes(Output + "/" + file, crop.EncodeToPNG()); Object.DestroyImmediate(raw); Object.DestroyImmediate(crop);
        return new Layer { name = name, file = file, ppu = sprite.pixelsPerUnit, width = r.width, height = r.height,
            pivotX = sprite.pivot.x, pivotY = sprite.pivot.y, scale = 1 };
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var chassis = Find<ChassisDataSO>("CH1_禁军");
        var parts = new[] { Find<ComponentDataSO>("CORE1_陷阵核心"), Find<ComponentDataSO>("SUP1_厚实护甲"),
            Find<ComponentDataSO>("WPN4_狙击枪"), Find<ComponentDataSO>("WPN5_链锯"), Find<ComponentDataSO>("MOVE3_刀锋") };
        var sprites = new[] { chassis.ChassisSprite }.Concat(parts.Select(p => p.ComponentIcon)).ToArray();
        var layers = new List<Layer> { Export(chassis.ChassisSprite, chassis.ChassisName, "chassis.png") };
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i]; var slot = chassis.Sockets[i];
            if (!slot.AllowedTypes.Contains(part.Type)) throw new Exception("Incompatible part");
            var l = Export(part.ComponentIcon, part.ComponentName, "part-" + i + ".png");
            l.slotX = slot.LocalPosition.x; l.slotY = slot.LocalPosition.y;
            l.angle = slot.MountAngle + part.BaseRotationOffset; l.scale = slot.DefaultComponentScale * part.VisualScaleMultiplier;
            l.anchorX = part.AnchorOffset.x; l.anchorY = part.AnchorOffset.y;
            l.muzzleX = part.MuzzleOffset.x; l.muzzleY = part.MuzzleOffset.y; l.weapon = part.Type == ComponentType.Weapon;
            if (l.scale != 1) throw new Exception("Sample requires uniform physical pixel density");
            layers.Add(l);
        }
        var root = new GameObject("Disposable rotation study");
        var hinges = new List<Transform>(); var visuals = new List<Transform>();
        for (int i = 0; i < layers.Count; i++)
        {
            var l = layers[i]; var hinge = new GameObject(l.name).transform; hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(l.slotX, l.slotY); hinge.localScale = Vector3.one * l.scale;
            var sr = new GameObject("Sprite").AddComponent<SpriteRenderer>(); sr.transform.SetParent(hinge, false);
            // Explicit order makes the comparison repeatable; production currently gives all components the same order.
            sr.transform.localPosition = new Vector3(-l.anchorX, -l.anchorY); sr.sprite = sprites[i]; sr.sortingOrder = i;
            hinges.Add(hinge); visuals.Add(sr.transform);
        }
        var checks = new List<VertexCheck>();
        var cam = new GameObject("Study camera").AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 3;
        cam.transform.position = new Vector3(0, 0, -10); cam.backgroundColor = new Color(51f / 255, 61f / 255, 64f / 255);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.allowHDR = false; cam.allowMSAA = false;
        foreach (float angle in new[] { 0f, 22.5f, 45f, 90f, 180f, 270f, 359f })
        {
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i]; hinges[i].localRotation = Quaternion.Euler(0, 0, l.angle + (l.weapon ? angle : 0));
                checks.Add(new VertexCheck { layer = i, angle = angle,
                    corner = visuals[i].TransformPoint(new Vector3(-l.pivotX / l.ppu, -l.pivotY / l.ppu)),
                    pivot = hinges[i].position, muzzle = hinges[i].TransformPoint(new Vector3(l.muzzleX, l.muzzleY)) });
            }
            if (angle != 22.5f && angle != 45f) continue;
            foreach (int size in new[] { 300, 600 })
            {
                var rt = new RenderTexture(size, size, 24) { antiAliasing = 1 }; cam.targetTexture = rt; cam.Render();
                var old = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
                File.WriteAllBytes(Output + "/unity-" + angle.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + size + ".png", tex.EncodeToPNG());
                RenderTexture.active = old; cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
        }
        File.WriteAllText(Output + "/model.json", JsonUtility.ToJson(new Model { layers = layers.ToArray(), checks = checks.ToArray() }, true));
        Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(root);
        File.WriteAllText(Output + "/checks.txt", "PASS six source sprites use 50 PPU\nPASS five components fit chassis slot types\nPASS sample scale multipliers are 1\nPASS exported 42 Unity transform references\nCOMPLETE\n");
        Debug.Log("RotationStudy COMPLETE");
    }
}
