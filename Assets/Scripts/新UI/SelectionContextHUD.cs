using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

    [Header("=== 6. 统一界面动效 ===")]
    [Min(0f)] public float PanelFadeDuration = 0.12f;

    // 内部私有状态
    private HUDSubMode currentSubMode = HUDSubMode.Production;
    private ResidentData inspectingStaffData; // 当前正在“窥探”的员工数据
    private Coroutine rootFadeRoutine;
    private Button staffDetailBackButton;
    private TMP_Text residentHPValueText;

    private void Awake()
    {
        Instance = this;
        if (BuildingUpgradeButton) BuildingUpgradeButton.onClick.AddListener(OnClickBuildingUpgrade);
        if (BuildingDismantleButton) BuildingDismantleButton.onClick.AddListener(OnClickBuildingDismantle);
        EnsureStaffDetailBackButton();
        ConfigureStaffListLayout();
        ConfigureResidentInfoLayout();

        // 初始化时强制关闭所有根节点，防止界面重叠
        if (BuildingRoot) BuildingRoot.SetActive(false);
        if (MechRoot) MechRoot.SetActive(false);
        if (ResidentRoot) ResidentRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (CurrentTargetBuilding != null)
            CurrentTargetBuilding.OnStaffChanged -= HandleStaffChanged;
        if (Instance == this) Instance = null;
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
            CurrentTargetBuilding.OnStaffChanged += HandleStaffChanged;
            PrepareRootForReveal(BuildingRoot);
            currentSubMode = HUDSubMode.Production; // 每次点击建筑默认回生产页面
            InitBuildingPanel(building);
            RevealRoot(BuildingRoot);
        }
        else if (target is MechUnit2D mech)
        {
            CurrentTargetMech = mech;
            PrepareRootForReveal(MechRoot);
            InitMechPanel(mech);
            RevealRoot(MechRoot);
        }
        else if (target is ResidentEntity resident)
        {
            CurrentTargetResident = resident;
            PrepareRootForReveal(ResidentRoot);
            // 直接点选世界实体，显示实时血量，状态默认为赋闲
            FillResidentDetail(resident.MyData, resident.GetComponent<DamageReceiver>());
            RevealRoot(ResidentRoot);
        }
    }

    private void HideAllRoots()
    {
        if (rootFadeRoutine != null)
        {
            StopCoroutine(rootFadeRoutine);
            rootFadeRoutine = null;
        }

        HideRoot(BuildingRoot);
        HideRoot(MechRoot);
        HideRoot(ResidentRoot);
    }

    private static CanvasGroup GetOrCreateCanvasGroup(GameObject root)
    {
        if (root == null) return null;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        return group != null ? group : root.AddComponent<CanvasGroup>();
    }

    private static void HideRoot(GameObject root)
    {
        if (root == null) return;
        CanvasGroup group = GetOrCreateCanvasGroup(root);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        root.SetActive(false);
    }

    private static void PrepareRootForReveal(GameObject root)
    {
        if (root == null) return;
        root.SetActive(true);
        CanvasGroup group = GetOrCreateCanvasGroup(root);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void RevealRoot(GameObject root)
    {
        if (root == null) return;
        CanvasGroup group = GetOrCreateCanvasGroup(root);
        if (PanelFadeDuration <= 0f)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            return;
        }

        rootFadeRoutine = StartCoroutine(FadeRootIn(group));
    }

    private IEnumerator FadeRootIn(CanvasGroup group)
    {
        float elapsed = 0f;
        while (elapsed < PanelFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / PanelFadeDuration);
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        rootFadeRoutine = null;
    }

    private void ClearLogicReferences()
    {
        if (CurrentTargetBuilding != null)
            CurrentTargetBuilding.OnStaffChanged -= HandleStaffChanged;
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
        // 设置名称、员工与产能摘要
        RefreshBuildingSummary();
        if (BuildingIconImage) BuildingIconImage.sprite = building.BuildingIcon;

        // 🌟 核心：物理清空舞台，并根据预制体生成对应的功能模块（如货架）
        foreach (Transform child in FunctionStage) Destroy(child.gameObject);
        if (building.FunctionUIPrefab != null)
        {
            GameObject module = Instantiate(building.FunctionUIPrefab, FunctionStage);
            StretchToParent(module.transform as RectTransform);

            // 执行业务逻辑握手
            var assemblerUI = module.GetComponent<AssemblerUIModule>();
            if (assemblerUI != null && building is AssemblerBuilding ab) assemblerUI.Initialize(ab);

            var factoryUI = module.GetComponent<FactoryUIModule>();
            if (factoryUI != null && building is FactoryBuilding factory) factoryUI.Initialize(factory);
            var constructionUI = module.GetComponent<ConstructionUIModule>();
            if (constructionUI != null) constructionUI.Initialize();
        }

        // 岗位系统权限：只有 SupportsStaff 为 true 的建筑才显示工作人员按钮
        if (StaffToggleButton) StaffToggleButton.gameObject.SetActive(building.SupportsStaff);

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
        if (FunctionStage) FunctionStage.gameObject.SetActive(currentSubMode == HUDSubMode.Production);
        if (StaffListContainer) StaffListContainer.SetActive(currentSubMode == HUDSubMode.StaffList || currentSubMode == HUDSubMode.StaffDetail);

        // 查看员工详情时复用居民看板；返回岗位与下岗操作在居民右栏中提供。
        if (ResidentRoot) ResidentRoot.SetActive(currentSubMode == HUDSubMode.StaffDetail);

        // 更新按钮文案
        if (StaffToggleText && CurrentTargetBuilding != null)
        {
            int staffCount = CurrentTargetBuilding.GetStaffList().Count;
            string action = currentSubMode == HUDSubMode.Production ? "工作人员" : "回到建筑";
            StaffToggleText.text = $"{action} {staffCount}/{CurrentTargetBuilding.MaxStaffCapacity}";
        }

        // 遣散按钮智能显隐
        if (DismissAllButton)
            DismissAllButton.SetActive(currentSubMode != HUDSubMode.Production &&
                CurrentTargetBuilding != null && CurrentTargetBuilding.GetStaffList().Count > 0);

        LayoutActionColumn(BuildingUpgradeButton != null ? BuildingUpgradeButton.transform.parent : null);
        RefreshBuildingSummary();
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ConfigureStaffListLayout()
    {
        if (StaffListContainer == null) return;
        HorizontalLayoutGroup layout = StaffListContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) return;
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static void LayoutActionColumn(Transform column)
    {
        if (column == null) return;

        List<RectTransform> activeButtons = new List<RectTransform>();
        for (int i = 0; i < column.childCount; i++)
        {
            Transform child = column.GetChild(i);
            if (!child.gameObject.activeSelf || child.GetComponent<Button>() == null) continue;
            if (child is RectTransform rect) activeButtons.Add(rect);
        }

        const float buttonHeight = 32f;
        const float spacing = 8f;
        float step = buttonHeight + spacing;
        float top = (activeButtons.Count - 1) * step * 0.5f;
        float columnWidth = column is RectTransform columnRect ? columnRect.rect.width : 168f;
        float buttonWidth = Mathf.Max(80f, Mathf.Min(160f, columnWidth - 8f));
        for (int i = 0; i < activeButtons.Count; i++)
        {
            RectTransform rect = activeButtons[i];
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, top - i * step);
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        }
    }

    private void EnsureStaffDetailBackButton()
    {
        if (staffDetailBackButton != null || OffDutyButton == null) return;

        GameObject backObject = Instantiate(OffDutyButton.gameObject, OffDutyButton.transform.parent);
        backObject.name = "返回岗位列表";
        staffDetailBackButton = backObject.GetComponent<Button>();
        staffDetailBackButton.onClick = new Button.ButtonClickedEvent();
        staffDetailBackButton.onClick.AddListener(OnClickStaffDetailBack);
        TMP_Text text = backObject.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = "返回岗位";
        backObject.SetActive(false);
    }

    private void ConfigureResidentActions(bool inspectingStaff)
    {
        EnsureStaffDetailBackButton();
        Transform column = OffDutyButton != null ? OffDutyButton.transform.parent : null;
        if (column == null) return;

        for (int i = 0; i < column.childCount; i++)
        {
            Button button = column.GetChild(i).GetComponent<Button>();
            if (button == null || button == OffDutyButton || button == staffDetailBackButton) continue;
            button.gameObject.SetActive(!inspectingStaff);
        }

        if (OffDutyButton != null) OffDutyButton.gameObject.SetActive(inspectingStaff);
        if (staffDetailBackButton != null) staffDetailBackButton.gameObject.SetActive(inspectingStaff);
        LayoutActionColumn(column);
    }

    private void ConfigureResidentInfoLayout()
    {
        if (ResStatusText != null)
        {
            RectTransform statusRect = ResStatusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0f, 24f);
            statusRect.sizeDelta = new Vector2(600f, 112f);
            ResStatusText.fontSize = 18f;
            ResStatusText.enableAutoSizing = false;
            ResStatusText.alignment = TextAlignmentOptions.TopLeft;
            ResStatusText.enableWordWrapping = true;
            ResStatusText.overflowMode = TextOverflowModes.Ellipsis;
            ResStatusText.lineSpacing = 5f;
            ResStatusText.color = ChimeraUITheme.SecondaryText;
            ResStatusText.raycastTarget = false;
        }

        if (ResHPBar == null) return;
        RectTransform barRect = ResHPBar.transform as RectTransform;
        if (barRect != null)
        {
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = new Vector2(0f, -66f);
            barRect.sizeDelta = new Vector2(600f, 24f);
        }

        ResHPBar.interactable = false;
        HealthBarGrid grid = ResHPBar.GetComponent<HealthBarGrid>();
        if (grid != null) grid.enabled = false;

        Transform background = ResHPBar.transform.Find("Background");
        if (background is RectTransform backgroundRect)
        {
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            Image backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = ChimeraUITheme.SurfaceDark;
                backgroundImage.raycastTarget = false;
            }
        }

        if (ResHPBar.fillRect != null)
        {
            RectTransform fillArea = ResHPBar.fillRect.parent as RectTransform;
            if (fillArea != null)
            {
                fillArea.anchorMin = Vector2.zero;
                fillArea.anchorMax = Vector2.one;
                fillArea.offsetMin = new Vector2(3f, 3f);
                fillArea.offsetMax = new Vector2(-3f, -3f);
            }
            Image fillImage = ResHPBar.fillRect.GetComponent<Image>();
            if (fillImage != null) fillImage.raycastTarget = false;
        }

        GameObject valueObject = new GameObject("HP_Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        valueObject.transform.SetParent(ResHPBar.transform, false);
        RectTransform valueRect = valueObject.GetComponent<RectTransform>();
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;
        residentHPValueText = valueObject.GetComponent<TextMeshProUGUI>();
        residentHPValueText.alignment = TextAlignmentOptions.Center;
        residentHPValueText.fontSize = 14f;
        residentHPValueText.fontStyle = FontStyles.Bold;
        residentHPValueText.color = Color.white;
        residentHPValueText.raycastTarget = false;
        if (ResStatusText != null && ResStatusText.font != null)
            residentHPValueText.font = ResStatusText.font;
    }

    private void SetResidentHealth(float current, float maximum)
    {
        maximum = Mathf.Max(1f, maximum);
        current = Mathf.Clamp(current, 0f, maximum);
        if (ResHPBar != null)
        {
            ResHPBar.maxValue = maximum;
            ResHPBar.value = current;
            Image fillImage = ResHPBar.fillRect != null ? ResHPBar.fillRect.GetComponent<Image>() : null;
            if (fillImage != null)
            {
                float ratio = current / maximum;
                fillImage.color = ratio > 0.6f
                    ? ChimeraUITheme.HP
                    : ratio > 0.3f
                        ? new Color32(224, 170, 63, 255)
                        : new Color32(210, 76, 76, 255);
            }
        }
        if (residentHPValueText != null)
            residentHPValueText.text = $"生命  {current:0} / {maximum:0}";
    }

    // --- 岗位列表头像生成 ---
    private void RefreshStaffListAvatars()
    {
        if (StaffListContainer == null || StaffAvatarPrefab == null) return;
        ConfigureStaffListLayout();
        foreach (Transform child in StaffListContainer.transform)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        if (CurrentTargetBuilding == null) return;

        List<ResidentData> staff = CurrentTargetBuilding.GetStaffList();
        List<ResidentData> pendingStaff = PopulationManager.Instance == null
            ? new List<ResidentData>()
            : PopulationManager.Instance.TotalResidents.Where(data =>
                data != null &&
                data.Status == ResidentStatus.TravelingToWork &&
                data.CurrentCarrierID == CurrentTargetBuilding.PersistentID).ToList();
        for (int i = 0; i < CurrentTargetBuilding.MaxStaffCapacity; i++)
        {
            GameObject avatar = Instantiate(StaffAvatarPrefab, StaffListContainer.transform);
            LayoutElement layoutElement = avatar.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = avatar.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 140f;
            layoutElement.preferredHeight = 156f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            Transform icon = avatar.transform.Find("Icon");
            if (icon is RectTransform iconRect)
            {
                iconRect.sizeDelta = new Vector2(76f, 76f);
                iconRect.anchoredPosition = new Vector2(0f, 20f);
            }

            Button btn = avatar.GetComponent<Button>();
            TMP_Text label = GetOrCreateStaffLabel(avatar);
            bool occupied = i < staff.Count;
            if (occupied)
            {
                ResidentData data = staff[i];
                float contribution = GetDisplayedContribution(data);
                if (label) label.text = $"{data.ResidentName}\n生产力 {contribution:0.00}";
                if (btn)
                {
                    btn.interactable = true;
                    btn.onClick.AddListener(() => InspectStaffMember(data));
                }
            }
            else if (i - staff.Count < pendingStaff.Count)
            {
                ResidentData data = pendingStaff[i - staff.Count];
                if (label) label.text = $"{data.ResidentName}\n前往岗位中";
                if (btn) btn.interactable = false;
            }
            else
            {
                if (label) label.text = "＋ 派遣居民";
                if (btn)
                {
                    btn.interactable = true;
                    btn.onClick.AddListener(() => ResidentRosterPanelUI.OpenForBuilding(CurrentTargetBuilding));
                }
                if (icon != null) icon.gameObject.SetActive(false);
            }
        }
    }

    private TMP_Text GetOrCreateStaffLabel(GameObject avatar)
    {
        TMP_Text existing = avatar.GetComponentInChildren<TMP_Text>(true);
        if (existing != null) return existing;

        GameObject labelObject = new GameObject("StaffLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(avatar.transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 6f);
        rect.offsetMax = new Vector2(-6f, -6f);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Bottom;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 15f;
        label.color = Color.white;
        if (StaffToggleText != null && StaffToggleText.font != null) label.font = StaffToggleText.font;
        label.raycastTarget = false;
        return label;
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
            SetResidentHealth(dr.CurrentHP, dr.MaxHP);
            ResidentIdentityLibrarySO library = PopulationManager.Instance != null
                ? PopulationManager.Instance.IdentityLibrary : null;
            string traits = ResidentWorkCalculator.GetTraitSummary(data, library);
            float techPotential = ResidentWorkCalculator.CalculateContribution(data, ResidentWorkDomain.Tech, library);
            if (ResStatusText) ResStatusText.text =
                $"<color=#5BBE6F>●</color> <size=22><b>赋闲</b></size>  <size=16><color=#516273>基地活动</color></size>\n" +
                $"<size=17>工业潜力 <b>{techPotential:0.00}</b>   ·   纪律 <b>{data.Discipline:P0}</b></size>\n" +
                $"<size=15><color=#516273>特性</color>  {traits}</size>";
            ConfigureResidentActions(false);
        }
        else // 处理建筑内的在职人员
        {
            float maxHP = PopulationManager.Instance != null && PopulationManager.Instance.IdentityLibrary != null
                ? PopulationManager.Instance.IdentityLibrary.DefaultResidentHP : Mathf.Max(1f, data.CurrentHP);
            float currentHP = data.CurrentHP > 0f ? data.CurrentHP : maxHP;
            SetResidentHealth(currentHP, maxHP);
            string bName = CurrentTargetBuilding != null ? CurrentTargetBuilding.BuildingName : "工厂";
            string traits = ResidentWorkCalculator.GetTraitSummary(data,
                PopulationManager.Instance != null ? PopulationManager.Instance.IdentityLibrary : null);
            if (ResStatusText) ResStatusText.text =
                $"<color=#4F8FD8>●</color> <size=22><b>工作中</b></size>  <size=16><color=#516273>{bName}</color></size>\n" +
                $"<size=17>生产力 <b>{GetDisplayedContribution(data):0.00}</b>   ·   纪律 <b>{data.Discipline:P0}</b></size>\n" +
                $"<size=15><color=#516273>特性</color>  {traits}</size>";
            ConfigureResidentActions(true);
        }
    }

    // --- 交互按钮：下岗与遣散 ---
    public void OnClickOffDuty()
    {
        if (inspectingStaffData != null && CurrentTargetBuilding != null)
        {
            ResidentData leaving = inspectingStaffData;
            inspectingStaffData = null;
            currentSubMode = HUDSubMode.StaffList;
            CurrentTargetBuilding.RemoveStaff(leaving);
        }
    }

    public void OnClickDismissAll()
    {
        if (CurrentTargetBuilding != null)
        {
            inspectingStaffData = null;
            currentSubMode = HUDSubMode.StaffList;
            CurrentTargetBuilding.DismissAllStaff();
        }
    }

    private void OnClickStaffDetailBack()
    {
        if (CurrentTargetBuilding == null) return;
        inspectingStaffData = null;
        currentSubMode = HUDSubMode.StaffList;
        RefreshStaffListAvatars();
        RefreshBuildingSubVisibility();
    }

    private void HandleStaffChanged(BuildingBase building)
    {
        if (building == null || building != CurrentTargetBuilding) return;
        if (inspectingStaffData != null && !building.GetStaffList().Contains(inspectingStaffData))
        {
            inspectingStaffData = null;
            currentSubMode = HUDSubMode.StaffList;
        }
        if (currentSubMode != HUDSubMode.Production) RefreshStaffListAvatars();
        RefreshBuildingSubVisibility();
    }

    private void RefreshBuildingSummary()
    {
        if (BuildingNameDisplay == null || CurrentTargetBuilding == null) return;
        BuildingNameDisplay.text = CurrentTargetBuilding.BuildingName;
    }

    private float GetDisplayedContribution(ResidentData data)
    {
        if (CurrentTargetBuilding is FactoryBuilding factory) return factory.GetResidentContribution(data);
        ResidentIdentityLibrarySO library = PopulationManager.Instance != null
            ? PopulationManager.Instance.IdentityLibrary : null;
        return ResidentWorkCalculator.CalculateContribution(data, ResidentWorkDomain.General, library);
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
        LayoutActionColumn(MechDetailButton != null ? MechDetailButton.transform.parent : null);
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
            if (dr) SetResidentHealth(dr.CurrentHP, dr.MaxHP);
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
        string reason = nearAssembler ? "" : "需要将机甲移动到组装厂附近，才能改装或回收。";
        if (MechRefitButton) UIHoverHint.Set(MechRefitButton.gameObject, reason);
        if (MechRecycleButton) UIHoverHint.Set(MechRecycleButton.gameObject, reason);
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
        chassisImg.rectTransform.anchoredPosition = Vector2.zero;

        // 3. 循环挂载零件
        for (int i = 0; i < profile.SlotIndices.Count; i++)
        {
            int slotIdx = profile.SlotIndices[i];
            var comp = PlayerInventoryManager.Instance.GetComponentInstance(profile.EquippedComponentIDs[i]);
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
        Canvas.ForceUpdateCanvases();
        float availableWidth = Mathf.Max(1f, MechPreviewContainer.rect.width - 12f);
        float availableHeight = Mathf.Max(1f, MechPreviewContainer.rect.height - 12f);
        float spriteWidth = chassisImg.rectTransform.rect.width;
        float spriteHeight = chassisImg.rectTransform.rect.height;
        if (spriteWidth > 0f && spriteHeight > 0f)
        {
            float scale = Mathf.Min(availableWidth / spriteWidth, availableHeight / spriteHeight);
            chassisObj.transform.localScale = Vector3.one * scale;
        }
    }

    // ==========================================
    // 🖱️ 统一外壳按钮回调 (供 Inspector 绑定)
    // ==========================================
    public void OnClickResidentLog() => UIFeedback.Show("居民日志功能尚未实现。");
    public void OnClickBuildingUpgrade() => UIFeedback.Show("建筑升级功能尚未实现。");
    public void OnClickBuildingDismantle() => UIFeedback.Show("建筑拆除功能尚未实现。");
    public void OnClickMechDetail() { if (CurrentTargetMech) UnitDetailPanelUI.Instance.OpenDetail(CurrentTargetMech, true); }
    public void OnClickMechRefit() { if (CurrentTargetMech) AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(CurrentTargetMech); }
    public void OnClickMechRecycle() { if (CurrentTargetMech) { CurrentTargetMech.RecycleToWarehouse(); Refresh(null); } }
    public void OnClickExile() { if (CurrentTargetResident) { PopulationManager.Instance.ExileResident(CurrentTargetResident); Refresh(null); } }
}
