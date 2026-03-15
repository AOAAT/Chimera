using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryItemSlotUI : MonoBehaviour
{
    public Image ItemIcon;
    public TMP_Text ItemNameText;

    private Action onClickCallback; // 动态回调函数，点下后告诉大盘选了谁

    // ==========================================
    // 渲染底盘数据
    // ==========================================
    public void SetupChassis(InstancedChassis chassis, Action<InstancedChassis> onSelected)
    {
        ItemIcon.sprite = chassis.BaseData.ChassisSprite;
        ItemNameText.text = chassis.BaseData.ChassisName;

        // 当玩家点击这个按钮时，把这个底盘实体交还给大管家
        onClickCallback = () => onSelected?.Invoke(chassis);
    }

    // ==========================================
    // 渲染零件数据 (未来装配武器时用)
    // ==========================================
    public void SetupComponent(InstancedComponent component, Action<InstancedComponent> onSelected)
    {
        ItemIcon.sprite = component.BaseData.ComponentIcon;
        ItemNameText.text = component.BaseData.ComponentName;

        onClickCallback = () => onSelected?.Invoke(component);
    }

    // 绑定到 Button 的 OnClick 事件上
    public void OnSlotClicked()
    {
        onClickCallback?.Invoke();
    }
}