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
        MusicManager.Instance?.SetImmersionMode(true); // 👈 开启闷声
        RefreshWarehouse();
    }

    public void CloseWarehouse()
    {
        gameObject.SetActive(false);
        MusicManager.Instance?.SetImmersionMode(false); // 👈 恢复清亮
        ItemDetailPanelUI.Instance?.HidePanel();
    }

    private void InitDropdowns()
    {
        MainCategoryDropdown.ClearOptions();
        MainCategoryDropdown.AddOptions(new List<string> { "装甲底盘", "机甲组件" });

        TypeDropdown.ClearOptions();
        List<string> typeNames = new List<string> { "全部分类" };
        foreach (ComponentType t in Enum.GetValues(typeof(ComponentType)))
        {
            typeNames.Add(TranslateComponentType(t));
            availableTypes.Add(t);
        }
        TypeDropdown.AddOptions(typeNames);

        // 👇【核心修复 1】：将 ComponentTag 彻底换为 SubTag
        TagDropdown.ClearOptions();
        List<string> tagNames = new List<string> { "所有流派" };
        foreach (SubTag t in Enum.GetValues(typeof(SubTag)))
        {
            tagNames.Add(TranslateFactionTag(t));
            availableTags.Add(t);
        }
        TagDropdown.AddOptions(tagNames);
    }

    private void OnFilterChanged()
    {
        bool isChassisMode = (MainCategoryDropdown.value == 0);
        TypeDropdown.interactable = !isChassisMode;
        RefreshWarehouse();
    }

    private void RefreshWarehouse()
    {
        if (!gameObject.activeSelf) return;

        foreach (Transform child in ContentRoot) Destroy(child.gameObject);

        bool isChassisMode = (MainCategoryDropdown.value == 0);
        int selectedTypeIdx = TypeDropdown.value - 1;
        int selectedTagIdx = TagDropdown.value - 1;

        int displayCount = 0;

        if (isChassisMode)
        {
            var list = PlayerInventoryManager.Instance.ChassisInventory.AsEnumerable();

            if (selectedTagIdx >= 0)
            {
                SubTag requiredTag = availableTags[selectedTagIdx];
                // 👇【核心修复 2】：底盘现在的标签数组叫 SubTags
                list = list.Where(c => c.BaseData.SubTags.Contains(requiredTag));
            }

            foreach (var chassis in list)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                slotObj.SetupChassis(chassis, null);
                displayCount++;
            }
        }
        else
        {
            var list = PlayerInventoryManager.Instance.ComponentInventory.AsEnumerable();

            if (selectedTypeIdx >= 0)
            {
                ComponentType requiredType = availableTypes[selectedTypeIdx];
                list = list.Where(c => c.BaseData.Type == requiredType);
            }

            if (selectedTagIdx >= 0)
            {
                SubTag requiredTag = availableTags[selectedTagIdx];
                // 👇【核心修复 3】：组件现在的标签数组叫 BaseSubTags
                list = list.Where(c => c.BaseData.BaseSubTags.Contains(requiredTag));
            }

            list = list.OrderBy(c => c.IsEquipped).ThenBy(c => c.BaseData.ComponentName);

            foreach (var comp in list)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                slotObj.ItemIcon.color = Color.white;
                slotObj.ItemNameText.color = Color.white;
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
            case ComponentType.Core: return "核心模块";
            case ComponentType.Weapon: return "武器系统";
            case ComponentType.Support: return "辅助插件";
            case ComponentType.Factory: return "工厂设备";
            case ComponentType.Movement: return "移动装置";
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