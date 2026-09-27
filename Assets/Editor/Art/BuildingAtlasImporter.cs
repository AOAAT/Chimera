using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>Keep the generated atlas crisp, transparent and readable for runtime sprite slicing.</summary>
public class BuildingAtlasImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/Buildings/ColonyBuildings.png") return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 64;
        importer.isReadable = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        // Persistent sub-assets can be referenced by scenes and prefabs, unlike Sprite.Create at runtime.
        var source = new Texture2D(2, 2);
        try
        {
            source.LoadImage(File.ReadAllBytes(assetPath));
            int width = source.width / 3, height = source.height / 2;
            string[] names = { "Command", "Factory", "Assembly", "Habitat", "Warehouse" };
            var slices = new List<SpriteMetaData>();
            for (int index = 0; index < names.Length; index++)
            {
                int left = index % 3 * width, bottom = (1 - index / 3) * height;
                var pixels = source.GetPixels(left, bottom, width, height);
                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < .1f) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
                slices.Add(new SpriteMetaData { name = names[index], alignment = 0, pivot = new Vector2(.5f, .5f),
                    rect = new Rect(left + minX, bottom + minY, maxX - minX + 1, maxY - minY + 1) });
            }
            importer.spritesheet = slices.ToArray();
        }
        finally { Object.DestroyImmediate(source); }
    }
}
