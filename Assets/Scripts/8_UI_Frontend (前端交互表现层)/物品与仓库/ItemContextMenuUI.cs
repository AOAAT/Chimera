using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemContextMenuUI : MonoBehaviour
{
    public static ItemContextMenuUI Instance;

    public RectTransform MenuRect;
    public Button UpgradeButton;
    public Button DismantleButton; // 👇【新增】：拆解按钮
    public TMP_Text ErrorPromptText;

    private InstancedComponent currentComponent;
    private InstancedChassis currentChassis;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        gameObject.SetActive(false);
    }

    private void Start()
    {
        UpgradeButton.onClick.AddListener(OnUpgradeClicked);
        DismantleButton.onClick.AddListener(OnDismantleClicked);
    }

    // 接收右键点击，在鼠标位置展开菜单
    public void ShowMenu(InstancedComponent comp, InstancedChassis chassis, Vector2 screenPos)
    {
        currentComponent = comp;
        currentChassis = chassis;
        gameObject.SetActive(true);
        ErrorPromptText.text = "";
        MenuRect.position = screenPos;

        // 动态显示：只有组件可以强化！底盘点右键，强化按钮会直接隐藏
        UpgradeButton.gameObject.SetActive(currentComponent != null);

        int scrapVal = 0;
        if (comp != null)
        {
            var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
            scrapVal = lvData != null ? lvData.ScrapValue : 5;
        }
        else if (chassis != null)
        {
            scrapVal = chassis.BaseData.ScrapValue;
        }

        DismantleButton.GetComponentInChildren<TMP_Text>().text = $"就地拆解 (+{scrapVal}废料)";
    }

    public void HideMenu()
    {
        gameObject.SetActive(false);
        currentComponent = null;
        currentChassis = null;
    }

    // === 强化逻辑 (保持不变) ===
    private void OnUpgradeClicked()
    {
        if (currentComponent == null) return;

        bool canUpgrade = ComponentUpgradeManager.Instance.TryInitiateUpgrade(currentComponent, out UpgradePreviewData previewData, out string errorMsg);

        if (canUpgrade)
        {
            UpgradePreviewPanelUI.Instance.OpenPreview(previewData);
            HideMenu();
        }
        else
        {
            ShowError(errorMsg);
        }
    }

    // ==========================================
    // 👇【核心新增】：拆解回收逻辑
    // ==========================================
    private void OnDismantleClicked()
    {
        int scrapValue = 0;
        string itemName = "";

        // 1. 如果拆的是组件
        if (currentComponent != null)
        {
            if (currentComponent.IsEquipped) { ShowError("操作拒绝：必须先从机甲上卸下该组件！"); return; }

            var lvData = currentComponent.BaseData.GetLevelData(currentComponent.CurrentLevel);
            scrapValue = lvData != null ? lvData.ScrapValue : 5; // 拿星级对应的废料钱
            itemName = currentComponent.BaseData.ComponentName;

            // 从真实库存里抹杀它！
            PlayerInventoryManager.Instance.ComponentInventory.Remove(currentComponent);
        }
        // 2. 如果拆的是底盘
        else if (currentChassis != null)
        {
            if (currentChassis.IsEquipped) { ShowError("操作拒绝：该底盘正在服役中，无法拆解！"); return; }

            scrapValue = currentChassis.BaseData.ScrapValue; // 底盘无星级，直接拿基础废料钱
            itemName = currentChassis.BaseData.ChassisName;

            // 从真实库存里抹杀它！
            PlayerInventoryManager.Instance.ChassisInventory.Remove(currentChassis);
        }

        // 3. 把钱打到账上！
        if (GlobalResourceManager.Instance != null)
        {
            GlobalResourceManager.Instance.ModifyMaterials(scrapValue);
            Debug.Log($"<color=#FFD700>【废品回收】</color> 成功熔毁了 [{itemName}]，获得了 {scrapValue} 点废料！");
        }
        if (JuicyLootEffectManager.Instance != null)
        {
            // 直接在鼠标点击位置喷发
            JuicyLootEffectManager.Instance.SpawnScrapExplosion(MenuRect.position, scrapValue);
        }
        // 4. 强制刷新大仓库 UI 并关闭菜单
        PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();
        HideMenu();
    }

    private void ShowError(string msg)
    {
        ErrorPromptText.text = $"<color=#FF0000>{msg}</color>";
        Debug.LogWarning(msg);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(MenuRect, Input.mousePosition))
        {
            HideMenu();
        }
    }
}