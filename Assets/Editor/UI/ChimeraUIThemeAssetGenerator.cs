using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ChimeraUIThemeAssetGenerator
{
    private const string AssetPath = "Assets/Resources/UI/ChimeraRounded.png";
    private const string AssetVersion = "chimera-ui-rounded-v2";

    static ChimeraUIThemeAssetGenerator()
    {
        EditorApplication.delayCall += GenerateIfMissing;
    }

    [MenuItem("Tools/Chimera/重新生成UI主题底图")]
    public static void Regenerate()
    {
        Generate(true);
    }

    private static void GenerateIfMissing()
    {
        TextureImporter importer = AssetImporter.GetAtPath(AssetPath) as TextureImporter;
        if (AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath) != null && importer != null &&
            importer.userData == AssetVersion) return;
        Generate(false);
    }

    private static void Generate(bool reportResult)
    {
        const int size = 32;
        const float radius = 6.5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ChimeraRounded";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nearestX = x < radius ? radius : x > size - 1f - radius ? size - 1f - radius : x;
                float nearestY = y < radius ? radius : y > size - 1f - radius ? size - 1f - radius : y;
                float dx = x - nearestX;
                float dy = y - nearestY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        File.WriteAllBytes(AssetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(AssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.spriteBorder = new Vector4(7f, 7f, 7f, 7f);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.userData = AssetVersion;
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        if (reportResult) Debug.Log($"UI 主题底图已重新生成：{AssetPath}");
    }
}
