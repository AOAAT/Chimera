using UnityEngine;
using UnityEngine.UI;

// World density, independent of UI canvas reference PPU or map cell dimensions.
public static class WorldPixelMetrics
{
    public const float PixelsPerUnit = 50f;
    public const float PixelSize = 1f / PixelsPerUnit;

    public static int ComponentOrder(int slotIndex) => 10 + slotIndex;

    public static void SizePreview(Image image, float uiUnitsPerWorldUnit)
    {
        if (image == null || image.sprite == null) return;
        var sprite = image.sprite;
        image.rectTransform.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * uiUnitsPerWorldUnit;
        image.rectTransform.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
    }

    public static void FitPreview(RectTransform viewport, Transform content, float preferredScale)
    {
        if (viewport == null || content == null || viewport.rect.width <= 0 || viewport.rect.height <= 0) return;
        content.localScale = Vector3.one;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, content);
        float fit = Mathf.Min((viewport.rect.width - 24) / Mathf.Max(1, bounds.size.x),
            (viewport.rect.height - 24) / Mathf.Max(1, bounds.size.y));
        float scale = Mathf.Max(.01f, Mathf.Min(preferredScale, fit));
        content.localScale = Vector3.one * scale;
        content.localPosition = -bounds.center * scale;
    }
}
