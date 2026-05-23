using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainBuildingHUD : MonoBehaviour
{
    public static MainBuildingHUD Instance;
    public BuildingBase CurrentTargetBuilding { get; private set; }

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
        // 🌟 核心修复 1：物理清空舞台，确保没有任何残余
        foreach (Transform child in FunctionStage)
        {
            Destroy(child.gameObject);
        }
        currentModule = null;

        // 记录当前选中的建筑
        CurrentTargetBuilding = building;

        if (building == null)
        {
            InfoRoot.SetActive(false);
            // 隐藏详情页
            ItemDetailPanelUI.Instance.SetFixedAnchor(null);
            ItemDetailPanelUI.Instance.HidePanel();
            return;
        }

        InfoRoot.SetActive(true);
        BuildingName.text = building.BuildingName;
        BuildingIcon.sprite = building.BuildingIcon;

        if (building.FunctionUIPrefab != null)
        {
            // 🌟 核心修复 2：将生成的实例记录在 currentModule
            currentModule = Instantiate(building.FunctionUIPrefab, FunctionStage);

            // 尝试初始化不同模块
            var factory = currentModule.GetComponent<FactoryUIModule>();
            if (factory != null) factory.Initialize();

            var assembler = currentModule.GetComponent<AssemblerUIModule>();
            if (assembler != null) assembler.Initialize(building as AssemblerBuilding);
        }
    }
}