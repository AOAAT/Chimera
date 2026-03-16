using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 【新增】引入事件系统
using TMPro;

// 【修改】继承三个物理射线的事件接口
public class InventoryItemSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image ItemIcon;
    public TMP_Text ItemNameText;

    private Action onClickCallback;

    // 缓存数据，用于悬停时传给详情页
    private InstancedChassis cachedChassis;
    private InstancedComponent cachedComponent;
    private bool isUnequipSlot = false;

    // ==========================================
    // 渲染底盘数据
    // ==========================================
    public void SetupChassis(InstancedChassis chassis, Action<InstancedChassis> onSelected)
    {
        cachedChassis = chassis;
        cachedComponent = null;
        isUnequipSlot = false;

        ItemIcon.sprite = chassis.BaseData.ChassisSprite;
        ItemNameText.text = chassis.BaseData.ChassisName;
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

        ItemIcon.sprite = component.BaseData.ComponentIcon;
        ItemNameText.text = component.BaseData.ComponentName;
        onClickCallback = () => onSelected?.Invoke(component);
    }

    public void SetupUnequip(Action onSelected)
    {
        isUnequipSlot = true;
        ItemIcon.color = new Color(1, 1, 1, 0);
        ItemNameText.text = "【 卸载当前组件 】";
        ItemNameText.color = Color.red;
        onClickCallback = () => onSelected?.Invoke();
    }

    // ==========================================
    // 【全新机制】：物理射线事件拦截
    // ==========================================
    public void OnPointerClick(PointerEventData eventData)
    {
        onClickCallback?.Invoke();
        // 点击后如果要隐藏悬浮窗，可以解除下面这行的注释
        // ItemDetailPanelUI.Instance?.HidePanel(); 
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnequipSlot) return; // 卸载按钮不弹详情页

        // 鼠标悬停时，召唤全局详情页！
        if (cachedChassis != null)
            ItemDetailPanelUI.Instance?.ShowChassisDetail(cachedChassis.BaseData);
        else if (cachedComponent != null)
            ItemDetailPanelUI.Instance?.ShowComponentDetail(cachedComponent.BaseData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 鼠标移开时，隐藏详情页
        ItemDetailPanelUI.Instance?.HidePanel();
    }
}