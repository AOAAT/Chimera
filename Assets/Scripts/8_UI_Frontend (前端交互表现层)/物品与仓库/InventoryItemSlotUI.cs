// --- START OF FILE InventoryItemSlotUI.cs ---
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryItemSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image ItemIcon;
    public TMP_Text ItemNameText;

    [Tooltip("用来单独显示 Lv.1 ~ Lv.4 的文本框")]
    public TMP_Text ItemLevelText;

    private float lastClickTime = 0f;
    public GameObject HighlightFrame;

    private Action onClickCallback;
    private InstancedChassis cachedChassis;
    private InstancedComponent cachedComponent;
    private InstancedAccessory cachedAccessory; // 👈 新增缓存变量
    private bool isUnequipSlot = false;

    public bool IsLootMode = false;

    [Header("=== 占用状态表现 ===")]
    public GameObject EquippedOverlay;
    public TMP_Text EquippedStatusText;

    [Header("=== 配件插槽提示 ===")]
    // 将 Transform 改为 RectTransform
    public RectTransform SocketIndicatorRoot;
    public GameObject SocketDotPrefab;

    public void SetHighlight(bool isOn)
    {
        if (HighlightFrame != null) HighlightFrame.SetActive(isOn);
    }

    public void SetupChassis(InstancedChassis chassis, Action<InstancedChassis> onSelected)
    {
        cachedChassis = chassis;
        cachedComponent = null;
        isUnequipSlot = false;

        if (chassis != null && chassis.BaseData != null)
        {
            ItemIcon.sprite = chassis.BaseData.ChassisSprite;
            ItemNameText.text = chassis.BaseData.ChassisName;

            // 👇 底盘没有等级，名字默认显示为纯净的白色
            ItemNameText.color = Color.white;

            if (ItemLevelText != null)
            {
                ItemLevelText.text = "";
                ItemLevelText.gameObject.SetActive(false);
            }
        }

        bool isEquipped = chassis != null && chassis.IsEquipped;
        if (EquippedOverlay != null) EquippedOverlay.SetActive(isEquipped);

        string mechName = "未知机甲";
        if (isEquipped)
        {
            var ownerMech = System.Array.Find(PlayerInventoryManager.Instance.HangarUnits, u => u != null && u.UnitID == chassis.EquippedUnitID);
            if (ownerMech != null) mechName = ownerMech.UnitName;
            if (EquippedStatusText != null) EquippedStatusText.text = $"已搭载于\n<color=#FFD700>{mechName}</color>";
        }

        onClickCallback = () => {
            if (isEquipped) Debug.LogWarning($"【操作拒绝】该底盘已被 [{mechName}] 占用，请先去机库将其卸下！");
            else onSelected?.Invoke(chassis);
        };
    }

    public void SetupComponent(InstancedComponent component, Action<InstancedComponent> onSelected)
    {
        cachedChassis = null;
        cachedComponent = component;
        isUnequipSlot = false;

        if (component != null && component.BaseData != null)
        {
            ItemIcon.sprite = component.BaseData.ComponentIcon;
            ItemNameText.text = component.BaseData.ComponentName;

            // 👇【核心新增】：提取稀有度颜色，同时赋给等级和名字！
            Color rarityColor = Color.white;
            ItemNameText.color = GetRarityColor(component.CurrentLevel);
            if (ItemLevelText != null)
            {
                ItemLevelText.gameObject.SetActive(true);
                ItemLevelText.text = $"Lv.{component.CurrentLevel}";
                ItemLevelText.color = GetRarityColor(component.CurrentLevel);
            }
            // 名字变色！
            ItemNameText.color = rarityColor;

            if (ItemLevelText != null)
            {
                ItemLevelText.gameObject.SetActive(true);
                ItemLevelText.text = $"Lv.{component.CurrentLevel}";
                // 等级角标变色！
                ItemLevelText.color = rarityColor;
            }
        }
        if (SocketIndicatorRoot != null && SocketDotPrefab != null)
        {
            // 只有当引用存在时，才执行清理和生成逻辑
            foreach (Transform child in SocketIndicatorRoot)
            {
                Destroy(child.gameObject);
            }

            int actualMax = component.GetMaxSockets();
            for (int i = 0; i < actualMax; i++)
            {
                GameObject dot = Instantiate(SocketDotPrefab, SocketIndicatorRoot);
                Image dotImg = dot.GetComponent<Image>();

                bool isFilled = i < component.SocketedAccessoryIDs.Count;
                dotImg.color = isFilled ? Color.cyan : new Color(1, 1, 1, 0.2f);
            }
        }
        bool isEquipped = component != null && component.IsEquipped;
        if (EquippedOverlay != null) EquippedOverlay.SetActive(isEquipped);

        string mechName = "未知机甲";
        if (isEquipped)
        {
            var ownerMech = System.Array.Find(PlayerInventoryManager.Instance.HangarUnits, u => u != null && u.UnitID == component.EquippedUnitID);
            if (ownerMech != null) mechName = ownerMech.UnitName;
            if (EquippedStatusText != null) EquippedStatusText.text = $"已搭载于\n<color=#FFD700>{mechName}</color>";
        }

        onClickCallback = () => {
            if (isEquipped) Debug.LogWarning($"【操作拒绝】该组件已被 [{mechName}] 占用，请先去机库将其卸下！");
            else onSelected?.Invoke(component);
        };

        if (SocketIndicatorRoot != null && SocketDotPrefab != null)
        {
            // 清理旧点
            foreach (Transform child in SocketIndicatorRoot.transform) Destroy(child.gameObject);

            // 根据 MaxSocketCount 生成
            int actualMax = component.GetMaxSockets();
            for (int i = 0; i < actualMax; i++)
            {
                GameObject dot = Instantiate(SocketDotPrefab, SocketIndicatorRoot.transform);
                Image dotImg = dot.GetComponent<Image>();

                // 判定：如果第 i 个坑位有东西，点亮它，否则显示空心/半透明
                bool isFilled = i < component.SocketedAccessoryIDs.Count;
                dotImg.color = isFilled ? Color.cyan : new Color(1, 1, 1, 0.2f);
            }
        }
    }

    public void SetupUnequip(Action onSelected)
    {
        cachedChassis = null;
        cachedComponent = null;
        isUnequipSlot = true;

        if (EquippedOverlay != null) EquippedOverlay.SetActive(false);

        ItemIcon.color = new Color(1, 1, 1, 0);
        ItemNameText.text = "【 卸载当前组件 】";
        ItemNameText.color = Color.red; // 卸载槽位保持红色警示

        if (ItemLevelText != null) ItemLevelText.gameObject.SetActive(false);

        onClickCallback = () => onSelected?.Invoke();
    }
    // --- 请找到 SetupAccessory 方法，全量替换为以下逻辑 ---

    public void SetupAccessory(InstancedAccessory accessory, Action<InstancedAccessory> onSelected)
    {

        cachedAccessory = accessory;
        cachedComponent = null;
        cachedChassis = null;
        isUnequipSlot = false;
 
        if (accessory != null && accessory.BaseData != null)
        {
            ItemIcon.sprite = accessory.BaseData.AccessoryIcon;
            ItemNameText.text = accessory.BaseData.AccessoryName;

            // 1. 设置稀有度颜色
            Color rarityColor = GetRarityColor(accessory.BaseData.Rarity);
            ItemNameText.color = rarityColor;

            // 2. 芯片没有“等级”和“插槽小点”，强行关闭对应的 UI 元素防止重叠
            if (ItemLevelText != null) ItemLevelText.gameObject.SetActive(false);
            if (SocketIndicatorRoot != null) SocketIndicatorRoot.gameObject.SetActive(false);
            if (EquippedOverlay != null) EquippedOverlay.SetActive(accessory.IsEquipped);
        }
        else
        {
            // 👇【核心加固】：如果数据是空的，直接把自己关了，别在这吓人
            this.gameObject.SetActive(false);
            return;
        }
        // 3. 处理占用状态：芯片是否已经插在某个零件里了？
        bool isEquipped = accessory != null && accessory.IsEquipped;
        if (EquippedOverlay != null) EquippedOverlay.SetActive(isEquipped);

        if (isEquipped && EquippedStatusText != null)
        {
            // 去零件库里查一下，到底是谁占用了我
            var ownerComp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == accessory.ParentComponentID);
            string ownerName = ownerComp != null ? ownerComp.BaseData.ComponentName : "未知零件";
            EquippedStatusText.text = $"已装配于\n<color=#FFD700>{ownerName}</color>";
        }

        onClickCallback = () => {
            if (isEquipped) Debug.LogWarning("【操作拒绝】该芯片正在工作中，请先去改装界面拆卸！");
            else onSelected?.Invoke(accessory);
        };
    }
    public void SetupAccessoryRefitMode(InstancedAccessory accessory, bool canFit, string failReason, string occupantName, Action<InstancedAccessory> onSelected)
    {
        SetupAccessory(accessory, onSelected); // 先跑基础显示

        // 1. 如果已被占用 (图3需求)
        if (!string.IsNullOrEmpty(occupantName))
        {
            if (EquippedStatusText != null)
            {
                EquippedStatusText.text = $"已装配于\n<color=#FF8800>{occupantName}</color>";
            }
            // 已被占用的芯片不能再点（除非玩家先去那边拆下来）
            onClickCallback = () => Debug.LogWarning($"芯片已被 [{occupantName}] 占用！");

            // 视觉变暗
            if (EquippedOverlay != null) EquippedOverlay.SetActive(true);
        }

        // 2. 如果不符合逻辑契约 (审计置灰)
        if (!canFit && string.IsNullOrEmpty(occupantName))
        {
            ItemNameText.text = $"<color=red>[不匹配]</color> {accessory.BaseData.AccessoryName}";
            if (EquippedStatusText != null) EquippedStatusText.text = $"<size=80%>{failReason}</size>";

            onClickCallback = () => Debug.LogWarning($"不满足注入契约：{failReason}");

            // --- 👇【加固补丁】：先查后用，没有就现装一个 ---
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = gameObject.AddComponent<CanvasGroup>();
            }
            cg.alpha = 0.4f;
            // ---------------------------------------------
        }
        else
        {
            // 如果是匹配的，记得把透明度设回 1，否则复用时会半透明
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1.0f;
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Time.time - lastClickTime < 0.2f) return;
        lastClickTime = Time.time;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            onClickCallback?.Invoke();
            if (!isUnequipSlot) ItemDetailPanelUI.Instance?.HidePanel();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (isUnequipSlot || IsLootMode) return;

            // --- 👇【加固 D】：通过这种三元运算符，确保只有一个参数是不为 null 的 ---
            if (cachedComponent != null)
                ItemContextMenuUI.Instance.ShowMenu(cachedComponent, null, null, Input.mousePosition);
            else if (cachedChassis != null)
                ItemContextMenuUI.Instance.ShowMenu(null, cachedChassis, null, Input.mousePosition);
            else if (cachedAccessory != null)
                ItemContextMenuUI.Instance.ShowMenu(null, null, cachedAccessory, Input.mousePosition);
        }
    }

    // --- 找到 OnPointerEnter 方法，增加配件分支 ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnequipSlot) return;

        if (cachedComponent != null)
        {
            ItemDetailPanelUI.Instance?.ShowComponentDetail(cachedComponent);
        }
        else if (cachedChassis != null)
        {
            ItemDetailPanelUI.Instance?.ShowChassisDetail(cachedChassis.BaseData);
        }
        // --- 👇【核心新增】：配件悬停判定 ---
        else if (cachedAccessory != null)
        {
            ItemDetailPanelUI.Instance?.ShowAccessoryDetail(cachedAccessory);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemDetailPanelUI.Instance?.HidePanel();
    }

    private Color GetRarityColor(int rarity)
    {
        switch (rarity)
        {
            case 1: return Color.white;                               // 1级：普通白
            case 2: return new Color(0.2f, 0.6f, 1f, 1f);             // 2级：稀有蓝
            case 3: return new Color(0.7f, 0.2f, 0.9f, 1f);           // 3级：史诗紫
            case 4: return new Color(1f, 0.6f, 0f, 1f);               // 4级：传说橙
            default: return Color.white;
        }
    }
}