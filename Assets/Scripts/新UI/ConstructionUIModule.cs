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
            btnObj.GetComponent<Image>().sprite = data.Icon;
            btnObj.GetComponent<Button>().onClick.AddListener(() => {
                BuildingManager.Instance.StartPlacement(data);
            });
        }
    }
}