// --- 替换 DynamicDepthSorter.cs 全量代码 ---
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SortingGroup))]
public class DynamicDepthSorter : MonoBehaviour
{
    private SortingGroup sortingGroup;
    public bool IsStatic = false;
    public float YOffset = 0f;
    private float lastY; // 【新增】缓存上次的 Y 坐标

    [SerializeField] private string targetSortingLayer = "Entities";

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
        sortingGroup.sortingLayerName = targetSortingLayer;
    }

    private void Start()
    {
        UpdateSorting(true); // 初始强制刷一次
    }

    private void LateUpdate()
    {
        // 如果是静态物体，或者 Y 轴没动，直接跳过计算
        if (IsStatic) return;

        if (Mathf.Abs(transform.position.y - lastY) > 0.01f) // 【性能闸门】
        {
            UpdateSorting(false);
        }
    }

    private void UpdateSorting(bool force)
    {
        lastY = transform.position.y;
        sortingGroup.sortingOrder = -(int)((lastY + YOffset) * 100f);
    }
}