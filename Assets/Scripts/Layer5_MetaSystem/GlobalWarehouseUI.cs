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
    public TMP_Dropdown MainCategoryDropdown; // 0:底盘, 1:组件
    public TMP_Dropdown TypeDropdown;         // 武器、核心... (底盘模式下会被禁用)
    public TMP_Dropdown TagDropdown;          // 血肉、科技...

    [Header("=== 展现区 ===")]
    public Transform ContentRoot;
    public InventoryItemSlotUI ItemSlotPrefab;
    public GameObject EmptyWarningText;

    // 缓存下拉菜单对应的真实枚举值，防止索引错乱
    private List<ComponentType> availableTypes = new List<ComponentType>();
    private List<ComponentTag> availableTags = new List<ComponentTag>();

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    private void Start()
    {
        // 1. 初始化下拉菜单 (让代码自动生成选项！)
        InitDropdowns();

        // 2. 绑定下拉菜单被玩家修改时的事件
        MainCategoryDropdown.onValueChanged.AddListener(delegate { OnFilterChanged(); });
        TypeDropdown.onValueChanged.AddListener(delegate { OnFilterChanged(); });
        TagDropdown.onValueChanged.AddListener(delegate { OnFilterChanged(); });

        // 3. 监听全局进货喇叭，保证大仓库也能“热更新”！
        PlayerInventoryManager.Instance.OnInventoryChanged += RefreshWarehouse;
    }

    private void OnDestroy()
    {
        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged -= RefreshWarehouse;
    }

    // ==========================================
    // 外部调用：打开/关闭全局仓库
    // ==========================================
    public void OpenWarehouse()
    {
        gameObject.SetActive(true);
        RefreshWarehouse();
    }

    public void CloseWarehouse()
    {
        gameObject.SetActive(false);
        ItemDetailPanelUI.Instance?.HidePanel(); // 关门时顺手把详情页也关了
    }

    // ==========================================
    // 自动装填下拉菜单的选项 (硬核技巧)
    // ==========================================
    private void InitDropdowns()
    {
        // 1. 一级分类 (写死两个选项)
        MainCategoryDropdown.ClearOptions();
        MainCategoryDropdown.AddOptions(new List<string> { "装甲底盘", "机甲组件" });

        // 2. 二级分类 (自动读取 ComponentType 枚举)
        TypeDropdown.ClearOptions();
        List<string> typeNames = new List<string> { "全部分类" }; // 第0项永远是全部
        foreach (ComponentType t in Enum.GetValues(typeof(ComponentType)))
        {
            typeNames.Add(TranslateComponentType(t));
            availableTypes.Add(t);
        }
        TypeDropdown.AddOptions(typeNames);

        // 3. 三级分类 (自动读取 ComponentTag 枚举)
        TagDropdown.ClearOptions();
        List<string> tagNames = new List<string> { "所有流派" }; // 第0项永远是全部
        foreach (ComponentTag t in Enum.GetValues(typeof(ComponentTag)))
        {
            if (t == ComponentTag.None) continue; // 略过无标签
            tagNames.Add(TranslateFactionTag(t));
            availableTags.Add(t);
        }
        TagDropdown.AddOptions(tagNames);
    }

    // ==========================================
    // 联动逻辑：切到底盘时，禁用二级菜单
    // ==========================================
    private void OnFilterChanged()
    {
        bool isChassisMode = (MainCategoryDropdown.value == 0);

        // 【完全还原你的草图】：只有选择组件才能显示/使用二级分类！
        TypeDropdown.interactable = !isChassisMode;

        RefreshWarehouse();
    }

    // ==========================================
    // 核心引擎：三重过滤与渲染
    // ==========================================
    private void RefreshWarehouse()
    {
        if (!gameObject.activeSelf) return;

        // 1. 清理货架
        foreach (Transform child in ContentRoot) Destroy(child.gameObject);

        // 2. 提取玩家当前的筛选条件
        bool isChassisMode = (MainCategoryDropdown.value == 0);
        int selectedTypeIdx = TypeDropdown.value - 1; // 减1是因为第0个是"全部"
        int selectedTagIdx = TagDropdown.value - 1;   // 同上

        int displayCount = 0;

        // 3A. 渲染【底盘】
        if (isChassisMode)
        {
            var list = PlayerInventoryManager.Instance.ChassisInventory.AsEnumerable();

            // 执行三级标签过滤
            if (selectedTagIdx >= 0)
            {
                ComponentTag requiredTag = availableTags[selectedTagIdx];
                list = list.Where(c => c.BaseData.Tags.Contains(requiredTag));
            }

            foreach (var chassis in list)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                // 仓库里不需要点击安装，所以回调传 null 即可（左键交由底层的悬停展示，右键交由底层拦截）
                slotObj.SetupChassis(chassis, null);
                displayCount++;
            }
        }
        // 3B. 渲染【组件】
        else
        {
            var list = PlayerInventoryManager.Instance.ComponentInventory.AsEnumerable();

            // 执行二级分类过滤
            if (selectedTypeIdx >= 0)
            {
                ComponentType requiredType = availableTypes[selectedTypeIdx];
                list = list.Where(c => c.BaseData.Type == requiredType);
            }

            // 执行三级标签过滤
            if (selectedTagIdx >= 0)
            {
                ComponentTag requiredTag = availableTags[selectedTagIdx];
                list = list.Where(c => c.BaseData.Tags.Contains(requiredTag));
            }

            // 【排序优化】：没被装配的在前面，被装配的在后面
            list = list.OrderBy(c => c.IsEquipped).ThenBy(c => c.BaseData.ComponentName);

            foreach (var comp in list)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                slotObj.ItemIcon.color = Color.white;
                slotObj.ItemNameText.color = Color.white;

                // 如果零件被装备了，可以给它贴个半透明或者加个红字，这里我先简单处理

                slotObj.SetupComponent(comp, null);
                displayCount++;
            }
        }

        // 4. 空空如也提示
        if (EmptyWarningText != null) EmptyWarningText.SetActive(displayCount == 0);
    }

    // ==========================================
    // 翻译官 (保持和详情页一致)
    // ==========================================
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

    private string TranslateFactionTag(ComponentTag tag)
    {
        switch (tag)
        {
            case ComponentTag.Factory: return "工厂重工";
            case ComponentTag.Tech: return "赛博科技";
            case ComponentTag.Flesh: return "血肉畸变";
            default: return "无";
        }
    }
}