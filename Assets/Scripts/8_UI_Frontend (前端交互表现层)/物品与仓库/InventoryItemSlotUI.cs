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
            ItemNameText.color = Color.white;

            if (ItemLevelText != null)
            {
                ItemLevelText.text = "";
                ItemLevelText.gameObject.SetActive(false);
            }
        }

        bool isEquipped = chassis != null && chassis.IsEquipped;
        if (EquippedOverlay != null) EquippedOverlay.SetActive(isEquipped);

        onClickCallback = () => {
            if (!isEquipped) onSelected?.Invoke(chassis);
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

            Color rarityColor = GetRarityColor(component.CurrentLevel);
            ItemNameText.color = rarityColor;

            if (ItemLevelText != null)
            {
                ItemLevelText.gameObject.SetActive(true);
                ItemLevelText.text = $"Lv.{component.CurrentLevel}";
                ItemLevelText.color = rarityColor;
            }
        }

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

    public void SetupUnequip(Action onSelected)
    {
        cachedChassis = null;
        cachedComponent = null;
        isUnequipSlot = true;

        if (EquippedOverlay != null) EquippedOverlay.SetActive(false);
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

        // 🌟 只保留左键点击逻辑，右键逻辑已剔除
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