// --- IconExporter.cs (存放在 Editor 文件夹下) ---
using UnityEngine;
using UnityEditor;
using System.IO;

public class IconExporter : EditorWindow
{
    [MenuItem("Assets/Export Game Icons (28, 55, 110)")]
    public static void ExportIcons()
    {
        // 1. 获取当前选中的图片
        Texture2D selectedTexture = Selection.activeObject as Texture2D;

        if (selectedTexture == null)
        {
            EditorUtility.DisplayDialog("导出失败", "请先在 Project 窗口点击选中一张图标图片！", "知道了");
            return;
        }

        // 确保图片是可读的
        string path = AssetDatabase.GetAssetPath(selectedTexture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            AssetDatabase.ImportAsset(path);
        }

        // 2. 定义需要的尺寸
        int[] targetSizes = { 28, 55, 110 };
        string folderPath = Path.Combine(Application.dataPath, "../ExportedIcons");

        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        foreach (int size in targetSizes)
        {
            // 3. 执行缩放逻辑
            Texture2D resized = Resize(selectedTexture, size, size);
            byte[] bytes = resized.EncodeToPNG();

            string fileName = $"Icon_{size}x{size}.png";
            File.WriteAllBytes(Path.Combine(folderPath, fileName), bytes);

            Debug.Log($"<color=green>【导出成功】</color> 已保存: {fileName}");
        }

        EditorUtility.RevealInFinder(folderPath);
    }

    // 核心缩放算法：针对像素图优化
    private static Texture2D Resize(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        // 重点：设置 FilterMode 为 Point，保留像素风格，不产生模糊
        source.filterMode = FilterMode.Point;

        Graphics.Blit(source, rt);
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);

        RenderTexture.active = rt;
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}