using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SortingGroup))]
public class DynamicDepthSorter : MonoBehaviour
{
    private SortingGroup sortingGroup;
    public bool IsStatic = false;
    public float YOffset = 0f;

    // 【新增】：定义该物件所属的绝对图层
    [SerializeField] private string targetSortingLayer = "Entities";

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();

        // --- 核心修复：强制注入图层，无视编辑器的手误 ---
        sortingGroup.sortingLayerName = targetSortingLayer; //
    }

    private void Start() { UpdateSorting(); }

    private void LateUpdate()
    {
        if (!IsStatic) UpdateSorting();
    }

    private void UpdateSorting()
    {
        // 依然保留 Y 轴排序逻辑，但它现在只在 Entities 图层内部进行“内战”
        // 即使这里的数值跌破 0，也不会被 Floor 图层的地块遮挡
        sortingGroup.sortingOrder = -(int)((transform.position.y + YOffset) * 100f); //
    }
}