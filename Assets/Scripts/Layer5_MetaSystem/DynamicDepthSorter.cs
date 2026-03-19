using UnityEngine;
using UnityEngine.Rendering; // 【极其重要】：必须引入这个命名空间才能使用 SortingGroup

[RequireComponent(typeof(SortingGroup))]
public class DynamicDepthSorter : MonoBehaviour
{
    private SortingGroup sortingGroup;

    [Tooltip("如果是静态场景物件（如石头），勾选此项，只在Start算一次，节省性能")]
    public bool IsStatic = false;

    [Tooltip("Y轴基准点偏移：通常我们的锚点在图片中心，但视觉遮挡是看‘脚底板’的，所以这里要往下偏")]
    public float YOffset = 0f;

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
    }

    private void Start()
    {
        UpdateSorting();
    }

    private void LateUpdate() // 使用 LateUpdate 确保在走位逻辑结算完后再更新画面
    {
        if (!IsStatic)
        {
            UpdateSorting();
        }
    }

    private void UpdateSorting()
    {
        // 【核心算法】：Y 轴越往下（坐标越小），说明离屏幕越近，SortingOrder 应该越大。
        // 乘以 100 是为了把小数点后的精度转化为整数（配合你的 PPU 设置）。
        // 取反（-）是因为 Unity 坐标系向上 Y 为正。
        sortingGroup.sortingOrder = -(int)((transform.position.y + YOffset) * 100f);
    }
}