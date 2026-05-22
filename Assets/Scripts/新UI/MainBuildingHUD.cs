using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainBuildingHUD : MonoBehaviour
{
    public static MainBuildingHUD Instance;

    [Header("=== 左侧：固定信息区 ===")]
    public GameObject InfoRoot;
    public Image BuildingIcon;
    public TMP_Text BuildingName;

    [Header("=== 右侧：动态功能舞台 ===")]
    public RectTransform FunctionStage;

    private GameObject currentModule;

    private void Awake() => Instance = this;

    /// <summary>
    /// 当玩家选中任何建筑时调用
    /// </summary>
    public void Refresh(BuildingBase building)
    {
        if (currentModule != null) Destroy(currentModule);

        if (building == null)
        {
            InfoRoot.SetActive(false);

            // 🌟 确保隐藏详情页并清理标签
            ItemDetailPanelUI.Instance.SetFixedAnchor(null);
            ItemDetailPanelUI.Instance.HidePanel();

            return;
        }
        // 3. 填充左侧基础信息
        InfoRoot.SetActive(true);
        BuildingName.text = building.BuildingName;
        BuildingIcon.sprite = building.BuildingIcon;

        if (building.FunctionUIPrefab != null)
        {
            currentModule = Instantiate(building.FunctionUIPrefab, FunctionStage);

            // --- 尝试进行模块初始化 ---
            var factoryModule = currentModule.GetComponent<FactoryUIModule>();
            if (factoryModule != null) factoryModule.Initialize();

            // 🌟 这里对应你之前的报错点：现在有了 AssemblerUIModule 脚本，这里就不会报错了
            var assemblerModule = currentModule.GetComponent<AssemblerUIModule>();
            if (assemblerModule != null)
            {
                assemblerModule.Initialize(building as AssemblerBuilding);
            }
        }
    }
}