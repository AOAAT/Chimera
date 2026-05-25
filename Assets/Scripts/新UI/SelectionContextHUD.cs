using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// 建筑子模式：生产货架、岗位列表、员工详情透视
public enum HUDSubMode { Production, StaffList, StaffDetail }

public class SelectionContextHUD : MonoBehaviour
{
    // 🌟 全局单例
    public static SelectionContextHUD Instance;

    // --- 运行时逻辑目标引用 ---
    public BuildingBase CurrentTargetBuilding { get; private set; }
    public MechUnit2D CurrentTargetMech { get; private set; }
    public ResidentEntity CurrentTargetResident { get; private set; }

    [Header("=== 1. 顶级容器 (三态隔离) ===")]
    public GameObject BuildingRoot;
    public GameObject MechRoot;
    public GameObject ResidentRoot; // 🌟 核心：独立居民页/覆盖层

    [Header("=== 2. 建筑看板 - 舞台管理 ===")]
    public TMP_Text BuildingNameDisplay;     // 建筑名称文本
    public Image BuildingIconImage;          // 建筑图标
    public RectTransform FunctionStage;      // 动态预制体（货架）挂载点
    public GameObject StaffListContainer;    // 岗位列表挂载点
    public GameObject StaffAvatarPrefab;     // 员工小头像预制体

    [Header("=== 3. 建筑看板 - 右侧控制外壳 ===")]
    public Button StaffToggleButton;         // [工作人员/回到建筑] 切换按钮
    public TMP_Text StaffToggleText;         // 切换按钮的文字
    public GameObject DismissAllButton;      // [全部遣散] 按钮 (智能显隐)
    public Button BuildingUpgradeButton;     // [升级] 按钮 (预留)
    public Button BuildingDismantleButton;   // [拆除] 按钮 (预留)

    [Header("=== 4. 居民看板组件 (通用渲染) ===")]
    public TMP_Text ResNameText;
    public Slider ResHPBar;
    public TMP_Text ResStatusText;           // 状态文字：工作中/赋闲
    public Button OffDutyButton;             // [下岗] 按钮 (仅工作中显示)
    public Image ResIconImage;               // 居民大头像

    [Header("=== 5. 机甲看板组件 ===")]
    public TMP_Text MechNameText;
    public RectTransform MechPreviewContainer;
    public Slider MechHPBar;
    public Slider MechAPBar;
    public TMP_Text MechHPValueDisplay;      // 显示 "850/1000"
    public Button MechDetailButton;
    public Button MechRefitButton;
    public Button MechRecycleButton;
    public float InteractionRadius = 5.0f;   // 维修/改装所需的物理距离

    // 内部私有状态
    private HUDSubMode currentSubMode = HUDSubMode.Production;
    private ResidentData inspectingStaffData; // 当前正在“窥探”的员工数据

    private void Awake()
    {
        Instance = this;

        // 初始化时强制关闭所有根节点，防止界面重叠
        if (BuildingRoot) BuildingRoot.SetActive(false);
        if (MechRoot) MechRoot.SetActive(false);
        if (ResidentRoot) ResidentRoot.SetActive(false);
    }

    // ==========================================
    // 🚀 核心入口：Refresh (由指挥官系统在单选/框选时调用)
    // ==========================================
    public void Refresh(object target)
    {
        // 1. 清理当前所有状态
        HideAllRoots();
        ClearLogicReferences();

        // 隐藏详情面板 (UnitDetailPanelUI)，防止残留
        if (ItemDetailPanelUI.Instance != null) ItemDetailPanelUI.Instance.HidePanel();

        if (target == null) return;

        // 2. 根据类型进行多态分流
        if (target is BuildingBase building)
        {
            CurrentTargetBuilding = building;
            BuildingRoot.SetActive(true);
            currentSubMode = HUDSubMode.Production; // 每次点击建筑默认回生产页面
            InitBuildingPanel(building);
        }
        else if (target is MechUnit2D mech)
        {
            CurrentTargetMech = mech;
            MechRoot.SetActive(true);
            InitMechPanel(mech);
        }
        else if (target is ResidentEntity resident)
        {
            CurrentTargetResident = resident;
            ResidentRoot.SetActive(true);
            // 直接点选世界实体，显示实时血量，状态默认为赋闲
            FillResidentDetail(resident.MyData, resident.GetComponent<DamageReceiver>());
        }
    }

    private void HideAllRoots()
    {
        if (BuildingRoot) BuildingRoot.SetActive(false);
        if (MechRoot) MechRoot.SetActive(false);
        if (ResidentRoot) ResidentRoot.SetActive(false);
    }

    private void ClearLogicReferences()
    {
        CurrentTargetBuilding = null;
        CurrentTargetMech = null;
        CurrentTargetResident = null;
        inspectingStaffData = null;
    }

    // ==========================================
    // 🏗️ 建筑面板逻辑
    // ==========================================
    private void InitBuildingPanel(BuildingBase building)
    {
        // 设置名称与图标
        if (BuildingNameDisplay) BuildingNameDisplay.text = building.BuildingName;
        if (BuildingIconImage) BuildingIconImage.sprite = building.BuildingIcon;

        // 🌟 核心：物理清空舞台，并根据预制体生成对应的功能模块（如货架）
        foreach (Transform child in FunctionStage) Destroy(child.gameObject);
        if (building.FunctionUIPrefab != null)
        {
            GameObject module = Instantiate(building.FunctionUIPrefab, FunctionStage);

            // 执行业务逻辑握手
            var assemblerUI = module.GetComponent<AssemblerUIModule>();
            if (assemblerUI != null && building is AssemblerBuilding ab) assemblerUI.Initialize(ab);

            var factoryUI = module.GetComponent<FactoryUIModule>();
            if (factoryUI != null) factoryUI.Initialize();
        }

        // 岗位系统权限：只有 SupportsStaff 为 true 的建筑才显示工作人员按钮
        StaffToggleButton.gameObject.SetActive(building.SupportsStaff);

        RefreshBuildingSubVisibility();
    }

    public void OnClickStaffToggle()
    {
        // 模式切换：Production <-> StaffList
        if (currentSubMode == HUDSubMode.Production)
        {
            currentSubMode = HUDSubMode.StaffList;
            RefreshStaffListAvatars();
        }
        else
        {
            // 如果是在列表页或详情透视页，点击此按钮都回到生产主页
            currentSubMode = HUDSubMode.Production;
        }

        RefreshBuildingSubVisibility();
    }

    private void RefreshBuildingSubVisibility()
    {
        // 舞台物理切换
        FunctionStage.gameObject.SetActive(currentSubMode == HUDSubMode.Production);
        StaffListContainer.SetActive(currentSubMode == HUDSubMode.StaffList || currentSubMode == HUDSubMode.StaffDetail);

        // 🌟 重点：看详情时，激活 ResidentRoot 遮盖层（由于其宽度较窄，右侧按钮列会露出来）
        ResidentRoot.SetActive(currentSubMode == HUDSubMode.StaffDetail);

        // 更新按钮文案
        if (StaffToggleText)
            StaffToggleText.text = (currentSubMode == HUDSubMode.Production) ? "工作人员" : "回到建筑";

        // 遣散按钮智能显隐
        if (DismissAllButton)
            DismissAllButton.SetActive(currentSubMode != HUDSubMode.Production);
    }

    // --- 岗位列表头像生成 ---
    private void RefreshStaffListAvatars()
    {
        foreach (Transform child in StaffListContainer.transform) Destroy(child.gameObject);
        if (CurrentTargetBuilding == null) return;

        foreach (var data in CurrentTargetBuilding.GetStaffList())
        {
            GameObject avatar = Instantiate(StaffAvatarPrefab, StaffListContainer.transform);

            // 自动寻找组件并初始化
            Button btn = avatar.GetComponent<Button>();
            if (btn) btn.onClick.AddListener(() => InspectStaffMember(data));

            TMP_Text label = avatar.GetComponentInChildren<TMP_Text>();
            if (label) label.text = data.ResidentName;
        }
    }

    public void InspectStaffMember(ResidentData data)
    {
        inspectingStaffData = data;
        currentSubMode = HUDSubMode.StaffDetail;

        // 填充详情（注意：在建筑内工作时，DamageReceiver 为 null，血量显满）
        FillResidentDetail(data, null);
        RefreshBuildingSubVisibility();
    }

    // ==========================================
    // 👨‍🌾 居民详情通用填充逻辑 (点选实体或透视员工共用)
    // ==========================================
    private void FillResidentDetail(ResidentData data, DamageReceiver dr)
    {
        if (ResNameText) ResNameText.text = data.ResidentName;

        if (dr != null) // 处理世界中的赋闲实体
        {
            if (ResHPBar) { ResHPBar.maxValue = dr.MaxHP; ResHPBar.value = dr.CurrentHP; }
            if (ResStatusText) ResStatusText.text = "状态：赋闲在基地";
            if (OffDutyButton) OffDutyButton.gameObject.SetActive(false);
        }
        else // 处理建筑内的在职人员
        {
            if (ResHPBar) { ResHPBar.maxValue = 100; ResHPBar.value = 100; }
            string bName = CurrentTargetBuilding != null ? CurrentTargetBuilding.BuildingName : "工厂";
            if (ResStatusText) ResStatusText.text = $"状态：正在 {bName} 工作";
            if (OffDutyButton) OffDutyButton.gameObject.SetActive(true);
        }
    }

    // --- 交互按钮：下岗与遣散 ---
    public void OnClickOffDuty()
    {
        if (inspectingStaffData != null && CurrentTargetBuilding != null)
        {
            CurrentTargetBuilding.RemoveStaff(inspectingStaffData);
            inspectingStaffData = null;
            OnClickStaffToggle(); // 回到生产列表页
        }
    }

    public void OnClickDismissAll()
    {
        if (CurrentTargetBuilding != null)
        {
            CurrentTargetBuilding.DismissAllStaff();
            currentSubMode = HUDSubMode.Production; // 遣散后重置回主视图
            RefreshBuildingSubVisibility();
        }
    }

    // ==========================================
    // 🤖 机甲面板处理
    // ==========================================
    private void InitMechPanel(MechUnit2D mech)
    {
        var profile = mech.GetProfile();
        if (MechNameText) MechNameText.text = profile.UnitName;

        BuildMechPreview(profile); // 🌟 执行动态拼接渲染
        UpdateMechBars();
    }

    private void Update()
    {
        // 实时刷新：如果机甲看板亮着，同步血量与距离感应
        if (MechRoot.activeSelf && CurrentTargetMech != null)
        {
            UpdateMechBars();
            CheckMechProximity();
        }

        // 实时刷新：如果选中的是世界小人，同步血量
        if (ResidentRoot.activeSelf && CurrentTargetResident != null)
        {
            var dr = CurrentTargetResident.GetComponent<DamageReceiver>();
            if (dr && ResHPBar) { ResHPBar.maxValue = dr.MaxHP; ResHPBar.value = dr.CurrentHP; }
        }
    }

    private void UpdateMechBars()
    {
        var receiver = CurrentTargetMech.GetComponent<DamageReceiver>();
        if (receiver != null)
        {
            if (MechHPBar) { MechHPBar.maxValue = receiver.MaxHP; MechHPBar.value = receiver.CurrentHP; }
            if (MechAPBar) { MechAPBar.maxValue = receiver.MaxAP; MechAPBar.value = receiver.CurrentAP; }
            if (MechHPValueDisplay) MechHPValueDisplay.text = $"{receiver.CurrentHP:F0} / {receiver.MaxHP:F0}";
        }
    }

    private void CheckMechProximity()
    {
        // 距离判定逻辑：是否靠近组装厂
        Collider2D[] hits = Physics2D.OverlapCircleAll(CurrentTargetMech.transform.position, InteractionRadius, LayerMask.GetMask("Building"));
        bool nearAssembler = false;
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<AssemblerBuilding>() != null) { nearAssembler = true; break; }
        }
        if (MechRefitButton) MechRefitButton.interactable = nearAssembler;
        if (MechRecycleButton) MechRecycleButton.interactable = nearAssembler;
    }

    // 🌟 [机甲视觉渲染引擎]：根据实物档案在 UI 上“拼装”机甲
    private void BuildMechPreview(SavedUnitProfile profile)
    {
        if (MechPreviewContainer == null) return;

        // 1. 清理旧图
        foreach (Transform child in MechPreviewContainer) Destroy(child.gameObject);

        // 2. 生成底盘基础
        GameObject chassisObj = new GameObject("UI_Chassis_Visual");
        chassisObj.transform.SetParent(MechPreviewContainer, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = profile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();
        chassisImg.raycastTarget = false;

        // 3. 循环挂载零件
        for (int i = 0; i < profile.SlotIndices.Count; i++)
        {
            int slotIdx = profile.SlotIndices[i];
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == profile.EquippedComponentIDs[i]);
            if (comp == null) continue;

            var slotDef = profile.ChassisData.Sockets[slotIdx];

            // 创建插槽挂载点
            GameObject slotObj = new GameObject("UISlot");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * 100f; // 对齐 1.0m = 100px 标准

            // 创建零件图片
            GameObject visObj = new GameObject("CompVis");
            visObj.transform.SetParent(slotObj.transform, false);
            Image compImg = visObj.AddComponent<Image>();
            compImg.sprite = comp.BaseData.ComponentIcon;
            compImg.SetNativeSize();
            compImg.raycastTarget = false;

            // 应用偏移与旋转
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset * 100f;
            visObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
        }

        // 4. 自适应缩放预览图
        float targetLayoutWidth = 180f;
        float spriteWidth = chassisImg.rectTransform.rect.width;
        if (spriteWidth > 0) chassisObj.transform.localScale = Vector3.one * (targetLayoutWidth / spriteWidth);
    }

    // ==========================================
    // 🖱️ 统一外壳按钮回调 (供 Inspector 绑定)
    // ==========================================
    public void OnClickMechDetail() { if (CurrentTargetMech) UnitDetailPanelUI.Instance.OpenDetail(CurrentTargetMech, true); }
    public void OnClickMechRefit() { if (CurrentTargetMech) AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(CurrentTargetMech); }
    public void OnClickMechRecycle() { if (CurrentTargetMech) { CurrentTargetMech.RecycleToWarehouse(); Refresh(null); } }
    public void OnClickExile() { if (CurrentTargetResident) { PopulationManager.Instance.ExileResident(CurrentTargetResident); Refresh(null); } }
}