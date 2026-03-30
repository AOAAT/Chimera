using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryItemSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image ItemIcon;
    public TMP_Text ItemNameText;
    private float lastClickTime = 0f;
    public GameObject HighlightFrame;

    private Action onClickCallback;
    private InstancedChassis cachedChassis;
    private InstancedComponent cachedComponent;
    private bool isUnequipSlot = false;

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
            // 底盘无等级
            ItemNameText.text = chassis.BaseData.ChassisName;
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

            // 👇【史诗级体验提升】：名字前面挂上大巴扎星级标识！
            ItemNameText.text = $"<color=#00FFFF>Lv.{component.CurrentLevel}</color> {component.BaseData.ComponentName}";
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
        ItemNameText.color = Color.red;
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
            Debug.Log($"【系统提示】你右键点击了 [{ItemNameText.text}]！预留给未来的【分解/强化】功能！");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnequipSlot) return;

        if (cachedComponent != null && cachedComponent.BaseData != null)
        {
            ItemDetailPanelUI.Instance?.ShowComponentDetail(cachedComponent.BaseData);
        }
        else if (cachedChassis != null && cachedChassis.BaseData != null)
        {
            ItemDetailPanelUI.Instance?.ShowChassisDetail(cachedChassis.BaseData);
        }
    }

    public void OnPointerExit(PointerEventData eventData) { }
}