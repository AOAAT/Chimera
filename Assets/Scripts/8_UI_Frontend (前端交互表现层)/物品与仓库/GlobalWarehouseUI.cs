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

        // 👇【只恢复音质】
        MusicManager.Instance?.SetImmersionMode(false);

        // --- ❌ 删除这一行 ---
        // MusicManager.Instance?.SwitchState(MusicState.Map); 

        if (CombatDirector.Instance != null)
            CombatDirector.Instance.SetNavigationVisibility(true);

        ItemDetailPanelUI.Instance?.HidePanel();
    }
    private void InitDropdowns()
    {
        // 1. 第一级：加入“全部资产”选项
        MainCategoryDropdown.ClearOptions();
        // 👇【核心新增】：配件选项
        MainCategoryDropdown.AddOptions(new List<string> { "全部资产", "装甲底盘", "机甲组件", "逻辑配件" });
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

        // 1. 彻底清理旧格子
        foreach (Transform child in ContentRoot) Destroy(child.gameObject);
        int displayCount = 0;

        int mainCategory = MainCategoryDropdown.value; // 0:全部, 1:底盘, 2:组件, 3:配件

        // --- 分支 A：底盘 ---
        // --- 分支 A：底盘 ---
        if (mainCategory == 0 || mainCategory == 1)
        {
            foreach (var stack in PlayerInventoryManager.Instance.GetChassisStacks())
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                // 注意：我们需要在 InventoryItemSlotUI 里补一个 SetupChassisStack
                slotObj.GetComponent<InventoryItemSlotUI>().SetupChassisStack(stack, null);
                displayCount++;
            }
        }

        // --- 分支 B：组件 ---
        if (mainCategory == 0 || mainCategory == 2)
        {
            foreach (var stack in PlayerInventoryManager.Instance.GetAvailableStacks())
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                slotObj.GetComponent<InventoryItemSlotUI>().SetupComponentStack(stack, null);
                displayCount++;
            }
        }

        // --- 分支 C：配件芯片 ---
        if (mainCategory == 0 || mainCategory == 3)
        {
            foreach (var acc in PlayerInventoryManager.Instance.AccessoryInventory)
            {
                // 👇【核心加固点 C】：过滤空芯片
                if (acc != null && acc.BaseData != null)
                {
                    var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                    slotObj.GetComponent<InventoryItemSlotUI>().SetupAccessory(acc, (selected) => {
                        Debug.Log($"选中了芯片: {selected.BaseData.AccessoryName}");
                    });
                    displayCount++;
                }
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
            //通用
            case SubTag.StrongAcid: return "强酸";
            case SubTag.Melee: return "近战";
            case SubTag.Ranged: return "远程";
            case SubTag.Charge: return "冲撞";
            case SubTag.Heavy: return "重型";
            case SubTag.Armor: return "装甲";
            case SubTag.Devotion: return "奉献";
            case SubTag.Smash: return "强击";
            case SubTag.Knockback: return "冲力";

            //科技
            case SubTag.Wasteland: return "废土";
            case SubTag.Industry: return "工业";
            case SubTag.Firearms: return "枪械";
            case SubTag.Laboratory: return "实验室";
            case SubTag.Reload: return "装填";
            case SubTag.Kinetic: return "动能";
            case SubTag.Plasma: return "等离子";
   
            //血肉
            case SubTag.Head: return "头颅";
            case SubTag.Organs: return "内脏";
            case SubTag.Limbs: return "四肢";
            case SubTag.Parasite: return "寄生";
            case SubTag.Pain: return "痛苦";

            //魔法
            case SubTag.Artifact: return "遗物";
            case SubTag.Otherworld: return "异界";
            case SubTag.Mana: return "魔力";
            case SubTag.Chaos: return "混沌";
            case SubTag.Order: return "秩序";

            default: return tag.ToString();
        }
    }

    // 在 UI 脚本中加入这个，确保场景卸载后，单例引用被正确清理

}