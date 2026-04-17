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
    private bool isUnequipSlot = false;

    public bool IsLootMode = false;

    [Header("=== 占用状态表现 ===")]
    public GameObject EquippedOverlay;
    public TMP_Text EquippedStatusText;

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
            switch (component.CurrentLevel)
            {
                case 1: rarityColor = Color.white; break;                               // 1级：普通白
                case 2: rarityColor = new Color(0.2f, 0.6f, 1f, 1f); break;             // 2级：稀有蓝
                case 3: rarityColor = new Color(0.7f, 0.2f, 0.9f, 1f); break;           // 3级：史诗紫
                case 4: rarityColor = new Color(1f, 0.6f, 0f, 1f); break;               // 4级：传说橙
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

            if (cachedComponent != null) ItemContextMenuUI.Instance.ShowMenu(cachedComponent, null, Input.mousePosition);
            else if (cachedChassis != null) ItemContextMenuUI.Instance.ShowMenu(null, cachedChassis, Input.mousePosition);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnequipSlot) return;

        if (cachedComponent != null && cachedComponent.BaseData != null)
        {
            ItemDetailPanelUI.Instance?.ShowComponentDetail(cachedComponent);
        }
        else if (cachedChassis != null && cachedChassis.BaseData != null)
        {
            ItemDetailPanelUI.Instance?.ShowChassisDetail(cachedChassis.BaseData);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemDetailPanelUI.Instance?.HidePanel();
    }
}