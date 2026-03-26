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
    // 👇【给三选一高亮预留的神器】
    public GameObject HighlightFrame;

    private Action onClickCallback;
    private InstancedChassis cachedChassis;
    private InstancedComponent cachedComponent;
    private bool isUnequipSlot = false;

    // 👇【新增：高亮开关】
    public void SetHighlight(bool isOn)
    {
        if (HighlightFrame != null) HighlightFrame.SetActive(isOn);
    }

    // ==========================================
    // 渲染底盘数据
    // ==========================================
    public void SetupChassis(InstancedChassis chassis, Action<InstancedChassis> onSelected)
    {
        cachedChassis = chassis;
        cachedComponent = null;
        isUnequipSlot = false;

        if (chassis != null && chassis.BaseData != null)
        {
            ItemIcon.sprite = chassis.BaseData.ChassisSprite;
            ItemNameText.text = chassis.BaseData.ChassisName;
        }

        onClickCallback = () => onSelected?.Invoke(chassis);
    }

    // ==========================================
    // 渲染零件数据
    // ==========================================
    public void SetupComponent(InstancedComponent component, Action<InstancedComponent> onSelected)
    {
        cachedComponent = component;
        cachedChassis = null;
        isUnequipSlot = false;

        if (component != null && component.BaseData != null)
        {
            ItemIcon.sprite = component.BaseData.ComponentIcon;
            ItemNameText.text = component.BaseData.ComponentName;
        }

        onClickCallback = () => onSelected?.Invoke(component);
    }

    // ==========================================
    // 渲染卸载按钮
    // ==========================================
    public void SetupUnequip(Action onSelected)
    {
        isUnequipSlot = true;
        ItemIcon.color = new Color(1, 1, 1, 0); // 隐藏图标
        ItemNameText.text = "【 卸载当前组件 】";
        ItemNameText.color = Color.red;
        onClickCallback = () => onSelected?.Invoke();
    }

    // ==========================================
    // 物理射线事件拦截
    // ==========================================
    public void OnPointerClick(PointerEventData eventData)
    {
        // 👇【核心防抖】：如果 0.2 秒内连续触发了两次点击，直接拦截第二次！
        if (Time.time - lastClickTime < 0.2f) return;
        lastClickTime = Time.time;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 极其干净的单次回调
            onClickCallback?.Invoke();

            // 只有当不是卸载按钮时，才去关详情页（看你需求，如果觉得没必要可以删掉这行）
            if (!isUnequipSlot) ItemDetailPanelUI.Instance?.HidePanel();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log($"【系统提示】你右键点击了 [{ItemNameText.text}]！此处预留给未来的【分解/强化】功能！");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnequipSlot) return;

        if (cachedChassis != null)
            ItemDetailPanelUI.Instance?.ShowChassisDetail(cachedChassis.BaseData);
        else if (cachedComponent != null)
            ItemDetailPanelUI.Instance?.ShowComponentDetail(cachedComponent.BaseData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemDetailPanelUI.Instance?.HidePanel();
    }
}