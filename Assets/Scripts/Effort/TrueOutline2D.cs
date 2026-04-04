// --- START OF FILE TrueOutline2D.cs ---
using System.Collections.Generic;
using UnityEngine;

public class TrueOutline2D : MonoBehaviour
{
    [Header("=== 描边配置 ===")]
    public bool EnableOutline = true;
    public Color OutlineColor = new Color(1f, 0f, 0f, 1f); // 默认红色描边
    [Range(0.01f, 0.2f)] public float OutlineThickness = 0.05f;
    public Material OutlineMaterial; // 👈 拖入你刚才创建的 Mat_TrueOutline！

    private Transform visualRoot;
    private Transform outlineRoot;

    // 记录原始件和克隆体的映射，以便在动画或旋转时实时同步
    private class OutlineLink
    {
        public SpriteRenderer Original;
        public SpriteRenderer[] Clones = new SpriteRenderer[4];
    }
    private List<OutlineLink> links = new List<OutlineLink>();

    // 呼叫这个方法，一键生成完美描边！
    public void BuildOutline(Transform targetVisualRoot, string sortingLayerName, int baseSortingOrder)
    {
        if (OutlineMaterial == null) return;

        visualRoot = targetVisualRoot;
        ClearOutline();

        // 1. 创建一个容纳所有描边的根节点，放在最顶层 (但在渲染排序上是最底层)
        GameObject rootObj = new GameObject("[TrueOutline_Root]");
        rootObj.transform.SetParent(visualRoot, false);
        rootObj.transform.SetAsFirstSibling();
        outlineRoot = rootObj.transform;

        // 2. 找到身上所有的贴图组件
        SpriteRenderer[] allRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>();

        // 4 个偏移方向：上下左右
        Vector3[] offsets = { Vector3.up, Vector3.down, Vector3.left, Vector3.right };

        foreach (var originalSR in allRenderers)
        {
            // 跳过已经是克隆体的组件
            if (originalSR.transform.IsChildOf(outlineRoot)) continue;

            OutlineLink link = new OutlineLink { Original = originalSR };

            for (int i = 0; i < 4; i++)
            {
                GameObject cloneObj = new GameObject($"OutlineClone_{originalSR.name}");
                cloneObj.transform.SetParent(outlineRoot, false);

                SpriteRenderer cloneSR = cloneObj.AddComponent<SpriteRenderer>();
                cloneSR.sprite = originalSR.sprite;
                cloneSR.material = OutlineMaterial;
                cloneSR.color = OutlineColor;

                // 👇 极其关键：描边层必须比机甲本身的最低层还要低！这样内部交错的线会被完全遮挡！
                cloneSR.sortingLayerName = sortingLayerName;
                cloneSR.sortingOrder = baseSortingOrder - 10;

                cloneObj.transform.localPosition = originalSR.transform.localPosition + offsets[i] * OutlineThickness;
                cloneObj.transform.localRotation = originalSR.transform.localRotation;
                cloneObj.transform.localScale = originalSR.transform.localScale;

                link.Clones[i] = cloneSR;
            }
            links.Add(link);
        }

        outlineRoot.gameObject.SetActive(EnableOutline);
    }

    // 实时同步贴图变化（兼容动画系统和武器旋转！）
    private void LateUpdate()
    {
        if (!EnableOutline || outlineRoot == null || links.Count == 0) return;

        foreach (var link in links)
        {
            if (link.Original == null) continue;

            for (int i = 0; i < 4; i++)
            {
                var clone = link.Clones[i];
                if (clone == null) continue;

                clone.sprite = link.Original.sprite;
                clone.flipX = link.Original.flipX;
                clone.flipY = link.Original.flipY;
                clone.color = OutlineColor;
            }
        }
    }

    public void ClearOutline()
    {
        if (outlineRoot != null) Destroy(outlineRoot.gameObject);
        links.Clear();
    }
}