using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MainBuildingHUD : MonoBehaviour
{
    public static MainBuildingHUD Instance;

    // --- 运行时逻辑目标 ---
    public BuildingBase CurrentTargetBuilding { get; private set; }
    public MechUnit2D CurrentTargetMech { get; private set; }
    public ResidentEntity CurrentTargetResident { get; private set; }

    [Header("=== 1. 容器根节点 (三态强制隔离) ===")]
    public GameObject BuildingRoot;
    public GameObject MechRoot;
    public GameObject ResidentRoot;

    [Header("=== 2. 建筑面板组件 (Building) ===")]
    public Image BuildingIcon;
    public TMP_Text BuildingNameText;
    public RectTransform FunctionStage; // 用于放置工厂货架或组装按钮

    [Header("=== 3. 机甲面板组件 (Mech - 你的第一张草图) ===")]
    public TMP_Text MechNameText;
    public RectTransform MechPreviewContainer;
    public Slider MechHPBar;
    public Slider MechAPBar;
    public TMP_Text MechHPValueDisplay; // 显示 "850 / 1000"
    public Button MechDetailButton;
    public Button MechRefitButton;
    public Button MechRecycleButton;
    public float InteractionRadius = 5.0f; // 靠近装配厂的判定距离

    [Header("=== 4. 居民面板组件 (Resident - 你的第二张草图) ===")]
    public TMP_Text ResNameText;
    public Image ResIconImage;
    public Slider ResHPBar;
    public Button ResExileButton;
    public Button ResLogButton;

    private void Awake()
    {
        Instance = this;

        // 初始关闭所有界面，防止穿帮
        if (BuildingRoot) BuildingRoot.SetActive(false);
        if (MechRoot) MechRoot.SetActive(false);
        if (ResidentRoot) ResidentRoot.SetActive(false);
    }

    // ==========================================
    // 🚀 核心入口：Refresh (由指挥官系统调用)
    // ==========================================
    public void Refresh(object target)
    {
        // 第一步：全部隐藏并清理引用
        HideAllRoots();
        ClearTargetReferences();

        // 隐藏全局详情页，防止数据残留
        if (ItemDetailPanelUI.Instance != null) ItemDetailPanelUI.Instance.HidePanel();

        if (target == null) return;

        // 第二步：根据类型分流
        if (target is BuildingBase building)
        {
            CurrentTargetBuilding = building;
            BuildingRoot.SetActive(true);
            UpdateBuildingPanel(building);
        }
        else if (target is MechUnit2D mech)
        {
            CurrentTargetMech = mech;
            MechRoot.SetActive(true);
            UpdateMechPanel(mech);
        }
        else if (target is ResidentEntity resident)
        {
            CurrentTargetResident = resident;
            ResidentRoot.SetActive(true);
            UpdateResidentPanel(resident);
        }
    }

    private void HideAllRoots()
    {
        if (BuildingRoot) BuildingRoot.SetActive(false);
        if (MechRoot) MechRoot.SetActive(false);
        if (ResidentRoot) ResidentRoot.SetActive(false);
    }

    private void ClearTargetReferences()
    {
        CurrentTargetBuilding = null;
        CurrentTargetMech = null;
        CurrentTargetResident = null;
    }

    // ==========================================
    // 🏗️ 建筑逻辑分区
    // ==========================================
    private void UpdateBuildingPanel(BuildingBase building)
    {
        BuildingNameText.text = building.BuildingName;
        BuildingIcon.sprite = building.BuildingIcon;

        // 物理清空旧模块
        foreach (Transform child in FunctionStage) Destroy(child.gameObject);

        // 实例化新模块
        if (building.FunctionUIPrefab != null)
        {
            GameObject moduleObj = Instantiate(building.FunctionUIPrefab, FunctionStage);

            // 尝试绑定组装厂逻辑
            var assemblerUI = moduleObj.GetComponent<AssemblerUIModule>();
            if (assemblerUI != null && building is AssemblerBuilding ab)
                assemblerUI.Initialize(ab);

            // 尝试初始化工厂生产逻辑
            var factoryUI = moduleObj.GetComponent<FactoryUIModule>();
            if (factoryUI != null)
                factoryUI.Initialize();
        }
    }

    // ==========================================
    // 🤖 机甲逻辑分区
    // ==========================================
    private void UpdateMechPanel(MechUnit2D mech)
    {
        var profile = mech.GetProfile();
        MechNameText.text = profile.UnitName;

        // 渲染缩略图
        BuildMechPreview(profile);
        // 立即更新一次数值
        UpdateMechBars();
    }

    private void UpdateMechBars()
    {
        if (CurrentTargetMech == null) return;
        var receiver = CurrentTargetMech.GetComponent<DamageReceiver>();
        if (receiver != null)
        {
            MechHPBar.maxValue = receiver.MaxHP;
            MechHPBar.value = receiver.CurrentHP;
            MechAPBar.maxValue = receiver.MaxAP;
            MechAPBar.value = receiver.CurrentAP;
            if (MechHPValueDisplay) MechHPValueDisplay.text = $"{receiver.CurrentHP:F0} / {receiver.MaxHP:F0}";
        }
    }

    private void CheckMechProximity()
    {
        if (CurrentTargetMech == null) return;
        Collider2D[] hits = Physics2D.OverlapCircleAll(CurrentTargetMech.transform.position, InteractionRadius, LayerMask.GetMask("Building"));
        bool nearAssembler = false;
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<AssemblerBuilding>() != null) { nearAssembler = true; break; }
        }
        MechRefitButton.interactable = nearAssembler;
        MechRecycleButton.interactable = nearAssembler;
    }

    // ==========================================
    // 👨‍🌾 居民逻辑分区
    // ==========================================
    private void UpdateResidentPanel(ResidentEntity resident)
    {
        ResNameText.text = resident.MyData.ResidentName;
        UpdateResidentBars();
    }

    private void UpdateResidentBars()
    {
        if (CurrentTargetResident == null) return;
        var receiver = CurrentTargetResident.GetComponent<DamageReceiver>();
        if (receiver != null)
        {
            ResHPBar.maxValue = receiver.MaxHP;
            ResHPBar.value = receiver.CurrentHP;
        }
    }

    // ==========================================
    // 🔄 实时监视
    // ==========================================
    private void Update()
    {
        if (MechRoot.activeSelf && CurrentTargetMech != null)
        {
            UpdateMechBars();
            CheckMechProximity();
        }

        if (ResidentRoot.activeSelf && CurrentTargetResident != null)
        {
            UpdateResidentBars();
        }
        if (ResidentRoot.activeSelf && CurrentTargetResident == null)
        {
            Refresh(null);
        }
    }

    // ==========================================
    // 🎨 视觉辅助：机甲预览渲染 (无省略)
    // ==========================================
    private void BuildMechPreview(SavedUnitProfile profile)
    {
        foreach (Transform child in MechPreviewContainer) Destroy(child.gameObject);

        GameObject chassisObj = new GameObject("UI_Chassis_Visual");
        chassisObj.transform.SetParent(MechPreviewContainer, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = profile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();
        chassisImg.raycastTarget = false;

        for (int i = 0; i < profile.SlotIndices.Count; i++)
        {
            int slotIdx = profile.SlotIndices[i];
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == profile.EquippedComponentIDs[i]);
            if (comp == null) continue;

            var slotDef = profile.ChassisData.Sockets[slotIdx];
            GameObject slotObj = new GameObject("Slot");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * 100f;

            GameObject visObj = new GameObject("Comp_Visual");
            visObj.transform.SetParent(slotObj.transform, false);
            Image img = visObj.AddComponent<Image>();
            img.sprite = comp.BaseData.ComponentIcon;
            img.SetNativeSize();
            img.raycastTarget = false;
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset * 100f;
            visObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
        }

        // 预览框适配缩放
        float maxSize = 150f;
        float spriteSize = Mathf.Max(chassisImg.rectTransform.rect.width, chassisImg.rectTransform.rect.height);
        chassisObj.transform.localScale = Vector3.one * (maxSize / spriteSize);
    }

    // ==========================================
    // 🖱️ 按钮交互回调 (请在 Inspector 检查这些名字)
    // ==========================================

    // --- 机甲按钮 ---
    public void OnClickMechDetail() { if (CurrentTargetMech) UnitDetailPanelUI.Instance.OpenDetail(CurrentTargetMech, true); }
    public void OnClickMechRefit() { if (CurrentTargetMech) AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(CurrentTargetMech); }
    public void OnClickMechRecycle() { if (CurrentTargetMech) { CurrentTargetMech.RecycleToWarehouse(); Refresh(null); } }

    // --- 居民按钮 ---
    public void OnClickExile() { if (CurrentTargetResident) { PopulationManager.Instance.ExileResident(CurrentTargetResident); Refresh(null); } }
    public void OnClickResLog() { Debug.Log("日志系统暂未开放"); }
}