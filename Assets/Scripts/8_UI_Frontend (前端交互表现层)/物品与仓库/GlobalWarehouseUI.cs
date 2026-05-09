using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GlobalWarehouseUI : MonoBehaviour
{
    public static GlobalWarehouseUI Instance;

    [Header("=== 核心筛选器 (下拉菜单) ===")]
    public TMP_Dropdown MainCategoryDropdown;
    public TMP_Dropdown TypeDropdown;
    public TMP_Dropdown TagDropdown;

    [Header("=== 展现区 ===")]
    public Transform ContentRoot;
    public InventoryItemSlotUI ItemSlotPrefab;
    public GameObject EmptyWarningText;

    private List<ComponentType> availableTypes = new List<ComponentType>();
    private List<SubTag> availableTags = new List<SubTag>();

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    private void Start()
    {
        InitDropdowns();
        MainCategoryDropdown.onValueChanged.AddListener(delegate { OnFilterChanged(); });
        TypeDropdown.onValueChanged.AddListener(delegate { OnFilterChanged(); });
        TagDropdown.onValueChanged.AddListener(delegate { OnFilterChanged(); });

        PlayerInventoryManager.Instance.OnInventoryChanged += RefreshWarehouse;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged -= RefreshWarehouse;
    }
    public void OpenWarehouse()
    {
        gameObject.SetActive(true);
        MusicManager.Instance?.SetImmersionMode(true);

        // --- 👇【核心新增】：隐藏主界面进入按钮 ---
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.SetNavigationVisibility(false);

        RefreshWarehouse();
    }

    public void CloseWarehouse()
    {
        gameObject.SetActive(false);
        MusicManager.Instance?.SetImmersionMode(false);

        // --- 👇【核心新增】：恢复主界面进入按钮 ---
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.SetNavigationVisibility(true);

        ItemDetailPanelUI.Instance?.HidePanel();
    }
    private void InitDropdowns()
    {
        // 1. 第一级：加入“全部资产”选项
        MainCategoryDropdown.ClearOptions();
        MainCategoryDropdown.AddOptions(new List<string> { "全部资产", "装甲底盘", "机甲组件" });
        MainCategoryDropdown.value = 0; // 👈 默认为“全部”

        // 2. 第二级：类型检索 (保持不变)
        TypeDropdown.ClearOptions();
        List<string> typeNames = new List<string> { "全部分类" };
        availableTypes.Clear();
        foreach (ComponentType t in Enum.GetValues(typeof(ComponentType)))
        {
            typeNames.Add(TranslateComponentType(t));
            availableTypes.Add(t);
        }
        TypeDropdown.AddOptions(typeNames);

        // 3. 第三级：流派检索 (保持不变)
        TagDropdown.ClearOptions();
        List<string> tagNames = new List<string> { "所有流派" };
        availableTags.Clear();
        foreach (SubTag t in Enum.GetValues(typeof(SubTag)))
        {
            tagNames.Add(TranslateFactionTag(t));
            availableTags.Add(t);
        }
        TagDropdown.AddOptions(tagNames);
        SetDropdownColor(MainCategoryDropdown, Color.black);
        SetDropdownColor(TypeDropdown, Color.black);
        SetDropdownColor(TagDropdown, Color.black);
        // 初始化时手动触发一次状态刷新
        OnFilterChanged();
    }
    // 辅助方法：同时修改标签和下拉列表的文字颜色
    private void SetDropdownColor(TMP_Dropdown dropdown, Color targetColor)
    {
        if (dropdown == null) return;

        // 修改平时显示的文字颜色
        if (dropdown.captionText != null)
            dropdown.captionText.color = targetColor;

        // 修改展开后列表里的文字颜色
        if (dropdown.itemText != null)
            dropdown.itemText.color = targetColor;
    }
    private void OnFilterChanged()
    {
        int mainCategory = MainCategoryDropdown.value;

        if (TypeDropdown != null)
        {
            // 只有组件模式下可点
            TypeDropdown.interactable = (mainCategory == 2);

            if (TypeDropdown.captionText != null)
            {
                // --- 👇【颜色修正】---
                // 可用时：纯黑色 (Alpha 1.0)
                // 禁用时：半透明黑 (Alpha 0.3) 
                TypeDropdown.captionText.color = TypeDropdown.interactable ?
                    Color.black : new Color(0, 0, 0, 0.3f);
            }
        }

        RefreshWarehouse();
    }
    private void RefreshWarehouse()
    {
        if (!gameObject.activeSelf) return;

        // 彻底物理清理
        foreach (Transform child in ContentRoot) Destroy(child.gameObject);

        int mainCategory = MainCategoryDropdown.value; // 0:All, 1:Chassis, 2:Component
        int selectedTypeIdx = TypeDropdown.value - 1;
        int selectedTagIdx = TagDropdown.value - 1;

        int displayCount = 0;

        // --- 分支 A：处理底盘 (只有在 0:全部 或 1:底盘 模式下显示) ---
        if (mainCategory == 0 || mainCategory == 1)
        {
            var chassisList = PlayerInventoryManager.Instance.ChassisInventory.AsEnumerable();

            // 应用标签筛选
            if (selectedTagIdx >= 0)
            {
                SubTag requiredTag = availableTags[selectedTagIdx];
                chassisList = chassisList.Where(c => c.BaseData.SubTags.Contains(requiredTag));
            }

            foreach (var chassis in chassisList)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                slotObj.SetupChassis(chassis, null);
                displayCount++;
            }
        }

        // --- 分支 B：处理组件 (只有在 0:全部 或 2:组件 模式下显示) ---
        if (mainCategory == 0 || mainCategory == 2)
        {
            var compList = PlayerInventoryManager.Instance.ComponentInventory.AsEnumerable();

            // 1. 应用类型筛选 (仅在组件模式下生效，全部模式下不看类型)
            if (mainCategory == 2 && selectedTypeIdx >= 0)
            {
                ComponentType requiredType = availableTypes[selectedTypeIdx];
                compList = compList.Where(c => c.BaseData.Type == requiredType);
            }

            // 2. 应用标签筛选
            if (selectedTagIdx >= 0)
            {
                SubTag requiredTag = availableTags[selectedTagIdx];
                compList = compList.Where(c => c.BaseData.BaseSubTags.Contains(requiredTag));
            }

            // 3. 排序规则：未搭载优先
            compList = compList.OrderBy(c => c.IsEquipped).ThenBy(c => c.BaseData.ComponentName);

            foreach (var comp in compList)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                slotObj.SetupComponent(comp, null);
                displayCount++;
            }
        }

        if (EmptyWarningText != null) EmptyWarningText.SetActive(displayCount == 0);
    }

    private string TranslateComponentType(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Core: return "核心组件";
            case ComponentType.Weapon: return "武器组件";
            case ComponentType.Support: return "辅助组件";
            case ComponentType.Movement: return "移动组件";
            default: return type.ToString();
        }
    }

    private string TranslateFactionTag(SubTag tag)
    {
        switch (tag)
        {
            case SubTag.Ballistic: return "实弹武装";
            case SubTag.Energy: return "能量科技";
            case SubTag.Mutation: return "血肉突变";
            case SubTag.Parasite: return "异星寄生";
            case SubTag.Curse: return "远古诅咒";
            case SubTag.Economy: return "经济扩容";
            default: return tag.ToString();
        }
    }

    // 在 UI 脚本中加入这个，确保场景卸载后，单例引用被正确清理

}