using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class ConstructionUIModule : MonoBehaviour
{
    [Header("=== 数据配置 ===")]
    public List<BuildingDataSO> AllBuildingBlueprints; // 在 Inspector 里拖入所有可选建筑

    [Header("=== UI 引用 ===")]
    public Transform GridRoot;
    public GameObject BuildButtonPrefab;

    public void Initialize()
    {
        ShowCategory(BuildingCategory.Unit); // 默认显示单位建筑
    }

    public void FilterCategory(int categoryIndex)
    {
        ShowCategory((BuildingCategory)categoryIndex);
    }

    private void ShowCategory(BuildingCategory category)
    {
        foreach (Transform child in GridRoot) Destroy(child.gameObject);

        var filtered = AllBuildingBlueprints.Where(b => b.Category == category).ToList();

        foreach (var data in filtered)
        {
            GameObject btnObj = Instantiate(BuildButtonPrefab, GridRoot);

            // 🌟 核心修复：寻找名为 "BuildingIcon" 的子物体并给它赋值
            // 这样不会影响按钮根节点的背景图
            Transform iconTransform = btnObj.transform.Find("BuildingIcon");
            if (iconTransform != null)
            {
                Image iconImage = iconTransform.GetComponent<Image>();
                iconImage.sprite = data.Icon;

                // 💡 建议：将这个 iconImage 的 Raycast Target 勾选去掉
                // 这样点击事件才会穿透到下方的 Button 组件上
                iconImage.raycastTarget = false;
            }

            btnObj.GetComponent<Button>().onClick.AddListener(() => {
                BuildingManager.Instance.StartPlacement(data);
            });
        }
    }
}