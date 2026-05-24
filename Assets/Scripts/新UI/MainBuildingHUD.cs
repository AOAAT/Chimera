using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MainBuildingHUD : MonoBehaviour
{
    // 🌟 [关键修复 1]：确保单例定义在最上方
    public static MainBuildingHUD Instance;

    public BuildingBase CurrentTargetBuilding { get; private set; }
    public MechUnit2D CurrentTargetMech { get; private set; }

    [Header("=== 容器切换 ===")]
    public GameObject BuildingRoot;
    public GameObject MechRoot;

    [Header("=== 机甲态 UI (你的草图实现) ===")]
    public TMP_Text MechNameText;
    public RectTransform PreviewContainer;
    public Slider HPBar;
    public Slider APBar;
    public TMP_Text HPText;
    public Button DetailButton;
    public Button RefitButton;
    public Button RecycleButton;

    [Header("=== 建筑态 UI ===")]
    public Image BuildingIcon;
    public TMP_Text BuildingName;
    public RectTransform FunctionStage;

    [Header("=== 逻辑参数 ===")]
    public float InteractionRadius = 5.0f;

    private void Awake()
    {
        // 🌟 [关键修复 2]：单例初始化
        Instance = this;

        // 初始状态隐藏所有
        if (BuildingRoot) BuildingRoot.SetActive(false);
        if (MechRoot) MechRoot.SetActive(false);
    }

    public void Refresh(object target)
    {
        // --- 1. 彻底初始化状态，防止界面残留与内存溢出 ---
        if (BuildingRoot) BuildingRoot.SetActive(false);
        if (MechRoot) MechRoot.SetActive(false);

        CurrentTargetBuilding = null;
        CurrentTargetMech = null;

        // 隐藏详情页，防止切换目标时详情页残留旧数据
        if (ItemDetailPanelUI.Instance != null)
        {
            ItemDetailPanelUI.Instance.HidePanel();
            ItemDetailPanelUI.Instance.SetFixedAnchor(null);
        }

        // 如果点的是空地，直接返回
        if (target == null) return;

        // ==========================================
        // 🏗️ 分支 A：选中了建筑实体
        // ==========================================
        if (target is BuildingBase building)
        {
            CurrentTargetBuilding = building;
            if (BuildingRoot) BuildingRoot.SetActive(true);

            // 填充建筑基础信息
            if (BuildingName) BuildingName.text = building.BuildingName;
            if (BuildingIcon) BuildingIcon.sprite = building.BuildingIcon;

            // 🌟 物理清空舞台模块：删除 FunctionStage 下的所有旧 UI 预制体
            foreach (Transform child in FunctionStage)
            {
                Destroy(child.gameObject);
            }

            // 如果该建筑配有功能模块（如：工厂货架预制体 或 组装厂进入按钮预制体）
            if (building.FunctionUIPrefab != null)
            {
                // 动态实例化模块
                GameObject moduleObj = Instantiate(building.FunctionUIPrefab, FunctionStage);

                // --- 🌟 [逻辑连边]：为新生成的模块寻找并绑定“大脑” ---

                // 1. 如果是组装厂模块：必须绑定当前的组装厂实例，否则“进入组装”按钮会失效
                var assemblerUI = moduleObj.GetComponent<AssemblerUIModule>();
                if (assemblerUI != null && building is AssemblerBuilding ab)
                {
                    assemblerUI.Initialize(ab);
                }

                // 2. 如果是工厂生产模块：初始化其货架显示（底盘/组件切换）
                var factoryUI = moduleObj.GetComponent<FactoryUIModule>();
                if (factoryUI != null)
                {
                    factoryUI.Initialize();
                }
            }
        }
        // ==========================================
        // 🤖 分支 B：选中了机甲单位 (单选模式)
        // ==========================================
        else if (target is MechUnit2D mech)
        {
            CurrentTargetMech = mech;
            if (MechRoot) MechRoot.SetActive(true);

            // 初始化机甲看板：加载名称、渲染预览图、初刷血条
            InitMechPanel(mech);
        }
    }

    private void InitMechPanel(MechUnit2D mech)
    {
        var profile = mech.GetProfile();
        MechNameText.text = profile.UnitName;

        // 🌟 [关键修复 3]：实现之前略过的渲染方法
        BuildMechPreview(profile);
        UpdateBars();
    }

    private void Update()
    {
        if (CurrentTargetMech != null && MechRoot.activeSelf)
        {
            UpdateBars();
            CheckProximity();
        }
    }

    private void CheckProximity()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(CurrentTargetMech.transform.position, InteractionRadius, LayerMask.GetMask("Building"));
        bool nearAssembler = false;
        foreach (var hit in hits)
        {
            if (hit.GetComponentInParent<AssemblerBuilding>() != null)
            {
                nearAssembler = true;
                break;
            }
        }
        RefitButton.interactable = nearAssembler;
        RecycleButton.interactable = nearAssembler;
    }

    private void UpdateBars()
    {
        var receiver = CurrentTargetMech.GetComponent<DamageReceiver>();
        if (receiver != null)
        {
            HPBar.maxValue = receiver.MaxHP;
            HPBar.value = receiver.CurrentHP;
            APBar.maxValue = receiver.MaxAP;
            APBar.value = receiver.CurrentAP;
            HPText.text = $"{receiver.CurrentHP:F0} / {receiver.MaxHP:F0}";
        }
    }

    // 🌟 [关键修复 4]：补全缺失的预览图生成逻辑
    private void BuildMechPreview(SavedUnitProfile profile)
    {
        foreach (Transform child in PreviewContainer) Destroy(child.gameObject);

        // 生成底盘预览
        GameObject chassisObj = new GameObject("UI_Chassis_Preview");
        chassisObj.transform.SetParent(PreviewContainer, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = profile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();

        // 生成零件预览
        for (int i = 0; i < profile.SlotIndices.Count; i++)
        {
            int slotIdx = profile.SlotIndices[i];
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == profile.EquippedComponentIDs[i]);
            if (comp == null) continue;

            var slotDef = profile.ChassisData.Sockets[slotIdx];
            GameObject slotObj = new GameObject("Slot_Preview");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * 100f; // 这里的 100f 是为了适配 UI 像素

            GameObject visObj = new GameObject("Comp_Icon");
            visObj.transform.SetParent(slotObj.transform, false);
            Image compImg = visObj.AddComponent<Image>();
            compImg.sprite = comp.BaseData.ComponentIcon;
            compImg.SetNativeSize();
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset * 100f;
            visObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
        }
    }

    // 按钮事件
    public void OnClickDetail()
    {
        if (CurrentTargetMech)
        {
            // 🌟 传入第二个参数 true，表示从战场打开是“只读”的
            UnitDetailPanelUI.Instance.OpenDetail(CurrentTargetMech, true);
        }
    }
    public void OnClickRefit() { if (CurrentTargetMech) AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(CurrentTargetMech); }
    public void OnClickRecycle() { if (CurrentTargetMech) { CurrentTargetMech.RecycleToWarehouse(); Refresh(null); } }
}