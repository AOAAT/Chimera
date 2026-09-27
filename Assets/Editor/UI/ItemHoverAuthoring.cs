using TMPro;
using UnityEditor;
using UnityEngine;

public static class ItemHoverAuthoring
{
    [MenuItem("Tools/Chimera/美术与UI/生成统一悬停详情预制体")]
    public static void Bake()
    {
        var root = new GameObject("ItemHoverTooltip", typeof(RectTransform));
        try
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/msyh SDF.asset");
            root.AddComponent<ItemHoverTooltip>().PrepareView(font);
            root.GetComponent<CanvasGroup>().alpha = 1; // Previewable in Prefab Mode; Awake hides it in play.
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/UI/ItemHoverTooltip.prefab");
            AssetDatabase.SaveAssets();
        }
        finally { Object.DestroyImmediate(root); }
    }
}
