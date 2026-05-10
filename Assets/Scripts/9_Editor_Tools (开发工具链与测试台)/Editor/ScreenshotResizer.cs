// --- ScreenshotResizer.cs (存放在 Editor 文件夹下) ---
using UnityEngine;
using UnityEditor;
using System.IO;

public class ScreenshotResizer : EditorWindow
{
    [MenuItem("Assets/强制转换为 1920x1080 (HD)")]
    public static void ResizeSelectedImages()
    {
        // 1. 获取选中的所有图片
        Object[] selectedObjects = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);

        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在 Project 窗口选中一张或多张图片素材！", "好的");
            return;
        }

        string folderPath = Path.Combine(Application.dataPath, "../ExportedScreenshots");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        int count = 0;
        foreach (Object obj in selectedObjects)
        {
            Texture2D source = obj as Texture2D;
            if (source == null) continue;

            // 确保图片可读
            string assetPath = AssetDatabase.GetAssetPath(source);
            MakeTextureReadable(assetPath);

            // 2. 执行 HD 转换
            Texture2D result = ResizeToHD(source, 1920, 1080);

            // 3. 保存文件
            byte[] bytes = result.EncodeToPNG();
            string fileName = $"HD_Shot_{source.name}_{System.DateTime.Now.Ticks}.png";
            File.WriteAllBytes(Path.Combine(folderPath, fileName), bytes);

            count++;
        }

        Debug.Log($"<color=green>【处理完成】</color> 已成功将 {count} 张图片转换为 1920x1080。");
        EditorUtility.RevealInFinder(folderPath);
    }

    private static Texture2D ResizeToHD(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);

        // --- 核心比例逻辑：计算 Cover 覆盖缩放 ---
        float sourceAspect = (float)source.width / source.height;
        float targetAspect = (float)targetWidth / targetHeight;

        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;

        if (sourceAspect > targetAspect) // 原图太宽了
        {
            scale.x = targetAspect / sourceAspect;
            offset.x = (1 - scale.x) / 2f;
        }
        else // 原图太高了
        {
            scale.y = sourceAspect / targetAspect;
            offset.y = (1 - scale.y) / 2f;
        }

        // 使用 Material 的偏移和缩放来绘制图片，防止拉伸变形
        Graphics.Blit(source, rt, new Vector2(1f / scale.x, 1f / scale.y), new Vector2(-offset.x / scale.x, -offset.y / scale.y));

        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    private static void MakeTextureReadable(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed))
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            AssetDatabase.ImportAsset(path);
        }
    }
}