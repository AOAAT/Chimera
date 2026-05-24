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
    private InstancedAccessory cachedAccessory;
    private bool isUnequipSlot = false;

    [Header("=== 占用状态表现 ===")]
    public GameObject EquippedOverlay;
    public TMP_Text EquippedStatusText;

    [Header("=== 配件插槽提示 ===")]
    public RectTransform SocketIndicatorRoot;
    public GameObject SocketDotPrefab;

    [Header("=== 数量显示控件 (堆叠模式专用) ===")]
    public GameObject QuantityBadge; // UI上的数字底框
    public TMP_Text QuantityText;    // 数量文字，如 x5

    public void SetHighlight(bool isOn)
    {
        if (HighlightFrame != null) HighlightFrame.SetActive(isOn);
    }

    // --- 1. 底盘单体初始化 (用于机库/详情) ---
    public void SetupChassis(InstancedChassis chassis, Action<InstancedChassis> onSelected)
    {
        cachedChassis = chassis;
        cachedComponent = null;
        isUnequipSlot = false;

        if (chassis != null && chassis.BaseData != null)
        {
            ItemIcon.sprite = chassis.BaseData.ChassisSprite;
            ItemNameText.text = chassis.BaseData.ChassisName;
            ItemNameText.color = Color.white;

            if (ItemLevelText != null)
            {
                ItemLevelText.text = "";
                ItemLevelText.gameObject.SetActive(false);
            }
        }

        if (QuantityBadge != null) QuantityBadge.SetActive(false);

        bool isEquipped = chassis != null && chassis.IsEquipped;
        if (EquippedOverlay != null) EquippedOverlay.SetActive(isEquipped);

        onClickCallback = () => {
            if (!isEquipped) onSelected?.Invoke(chassis);
        };
    }

    // --- 2. 零件单体初始化 (兼容层) ---
    public void SetupComponent(InstancedComponent component, Action<InstancedComponent> onSelected)
    {
        cachedChassis = null;
        cachedComponent = component;
        isUnequipSlot = false;

        if (component != null && component.BaseData != null)
        {
            ItemIcon.sprite = component.BaseData.ComponentIcon;
            ItemNameText.text = component.BaseData.ComponentName;

            Color rarityColor = GetRarityColor(component.CurrentMark);
            ItemNameText.color = rarityColor;

            if (ItemLevelText != null)
            {
                ItemLevelText.gameObject.SetActive(true);
                ItemLevelText.text = $"Lv.{component.CurrentMark}";
                ItemLevelText.color = rarityColor;
            }
        }

        if (QuantityBadge != null) QuantityBadge.SetActive(false);

        // 刷新配件插槽点
        if (SocketIndicatorRoot != null && SocketDotPrefab != null)
        {
            foreach (Transform child in SocketIndicatorRoot) Destroy(child.gameObject);
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

        onClickCallback = () => {
            if (!isEquipped) onSelected?.Invoke(component);
        };
    }

    // --- 3. 核心：零件堆叠显示 (用于仓库/选配面板) ---
    public void SetupComponentStack(ComponentStack stack, Action<ComponentStack> onSelected)
    {
        // 为了兼容详情页，创建一个临时实例
        cachedComponent = new InstancedComponent(stack.BaseData, stack.Level);
        cachedChassis = null;
        isUnequipSlot = false;

        ItemIcon.sprite = stack.BaseData.ComponentIcon;
        ItemNameText.text = stack.BaseData.ComponentName;

        Color rarityColor = GetRarityColor(stack.Level);
        ItemNameText.color = rarityColor;

        if (ItemLevelText != null)
        {
            ItemLevelText.gameObject.SetActive(true);
            ItemLevelText.text = $"Lv.{stack.Level}";
            ItemLevelText.color = rarityColor;
        }

        // 数量堆叠逻辑
        if (QuantityBadge != null && QuantityText != null)
        {
            QuantityBadge.SetActive(stack.Quantity > 1);
            QuantityText.text = $"x{stack.Quantity}";
        }

        if (EquippedOverlay != null) EquippedOverlay.SetActive(false);
        if (SocketIndicatorRoot != null) SocketIndicatorRoot.gameObject.SetActive(false);

        onClickCallback = () => onSelected?.Invoke(stack);
    }

    // --- 4. 核心：底盘堆叠显示 (用于仓库) ---
    public void SetupChassisStack(ChassisStack stack, Action<ChassisStack> onSelected)
    {
        cachedChassis = new InstancedChassis(stack.BaseData);
        cachedComponent = null;
        isUnequipSlot = false;

        ItemIcon.sprite = stack.BaseData.ChassisSprite;
        ItemNameText.text = stack.BaseData.ChassisName;
        ItemNameText.color = Color.white;

        if (QuantityBadge != null && QuantityText != null)
        {
            QuantityBadge.SetActive(stack.Quantity > 1);
            QuantityText.text = $"x{stack.Quantity}";
        }

        if (ItemLevelText != null) ItemLevelText.gameObject.SetActive(false);
        if (EquippedOverlay != null) EquippedOverlay.SetActive(false);

        onClickCallback = () => onSelected?.Invoke(stack);
    }

    public void SetupUnequip(Action onSelected)
    {
        cachedChassis = null;
        cachedComponent = null;
        isUnequipSlot = true;

        if (EquippedOverlay != null) EquippedOverlay.SetActive(false);
        if (QuantityBadge != null) QuantityBadge.SetActive(false);
        ItemIcon.color = new Color(1, 1, 1, 0);
        ItemNameText.text = "【 卸载当前组件 】";
        ItemNameText.color = Color.red;

        if (ItemLevelText != null) ItemLevelText.gameObject.SetActive(false);
        onClickCallback = () => onSelected?.Invoke();
    }

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
            ItemNameText.color = GetRarityColor(accessory.BaseData.Rarity);

            if (ItemLevelText != null) ItemLevelText.gameObject.SetActive(false);
            if (SocketIndicatorRoot != null) SocketIndicatorRoot.gameObject.SetActive(false);
        }
        else
        {
            this.gameObject.SetActive(false);
            return;
        }

        if (QuantityBadge != null) QuantityBadge.SetActive(false);

        bool isEquipped = accessory.IsEquipped;
        if (EquippedOverlay != null) EquippedOverlay.SetActive(isEquipped);

        onClickCallback = () => {
            if (!isEquipped) onSelected?.Invoke(accessory);
        };
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
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnequipSlot) return;
        if (cachedComponent != null) ItemDetailPanelUI.Instance?.ShowComponentDetail(cachedComponent);
        else if (cachedChassis != null) ItemDetailPanelUI.Instance?.ShowChassisDetail(cachedChassis.BaseData);
        else if (cachedAccessory != null) ItemDetailPanelUI.Instance?.ShowAccessoryDetail(cachedAccessory);
    }

    public void OnPointerExit(PointerEventData eventData) => ItemDetailPanelUI.Instance?.HidePanel();

    private Color GetRarityColor(int rarity)
    {
        switch (rarity)
        {
            case 1: return Color.white;
            case 2: return new Color(0.2f, 0.6f, 1f, 1f);
            case 3: return new Color(0.7f, 0.2f, 0.9f, 1f);
            case 4: return new Color(1f, 0.6f, 0f, 1f);
            default: return Color.white;
        }
    }
}