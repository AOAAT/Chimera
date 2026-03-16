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

    // ==========================================
    // 渲染【卸载专属】按钮
    // ==========================================
    public void SetupUnequip(Action onSelected)
    {
        // 隐藏图标 (设为全透明)，或者如果你有红叉的图，可以赋值进去
        ItemIcon.color = new Color(1, 1, 1, 0);

        ItemNameText.text = "【 卸载当前组件 】";
        ItemNameText.color = Color.red; // 字体标红，警示玩家

        onClickCallback = () => onSelected?.Invoke();
    }

    // 绑定到 Button 的 OnClick 事件上
    public void OnSlotClicked()
    {
        onClickCallback?.Invoke();
    }
}