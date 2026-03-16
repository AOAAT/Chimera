using System;
using System.Collections.Generic;
using UnityEngine;

public class RightInventoryPanelUI : MonoBehaviour
{
    public static RightInventoryPanelUI Instance;

    public Transform ContentRoot; // 挂载了 GridLayoutGroup 的那个 Content 节点
    public InventoryItemSlotUI ItemSlotPrefab; // 你的商品格子预制体

    private void Awake()
    {
        Instance = this;
    }

    // 每次打开前，清理旧货架
    private void ClearShelf()
    {
        foreach (Transform child in ContentRoot)
        {
            Destroy(child.gameObject);
        }
    }

    // ==========================================
    // 专属通道：只展示闲置的底盘供玩家挑选
    // ==========================================
    public void OpenForChassisSelection(List<InstancedChassis> availableChassis, Action<InstancedChassis> onChassisSelected)
    {
        gameObject.SetActive(true);
        ClearShelf();

        foreach (var chassis in availableChassis)
        {
            var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
            slotObj.SetupChassis(chassis, (selected) =>
            {
                // 玩家点击后，关闭面板，并执行选择回调
                gameObject.SetActive(false);
                onChassisSelected?.Invoke(selected);
            });
        }
    }
    // ==========================================
    // 专属通道：根据插槽要求，严格筛选零件供玩家挑选
    // ==========================================
    public void OpenForComponentSelection(List<InstancedComponent> availableComponents, bool allowUnequip, Action<InstancedComponent> onComponentSelected)
    {
        gameObject.SetActive(true);
        ClearShelf();

        // 【极其核心】：如果允许卸载（插槽上原本有东西），就把卸载按钮放在第一位！
        if (allowUnequip)
        {
            var unequipSlotObj = Instantiate(ItemSlotPrefab, ContentRoot);
            unequipSlotObj.SetupUnequip(() =>
            {
                gameObject.SetActive(false); // 选完关门
                onComponentSelected?.Invoke(null); // 【魔法信号】：传 null 代表玩家选择了卸载！
            });
        }

        // 正常遍历生成仓库里其他可换装的组件
        foreach (var comp in availableComponents)
        {
            var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);

            // 确保生成正常组件时图标是可见的 (防止被预制体污染)
            slotObj.ItemIcon.color = new Color(1, 1, 1, 1);
            slotObj.ItemNameText.color = Color.white;

            slotObj.SetupComponent(comp, (selected) =>
            {
                gameObject.SetActive(false);
                onComponentSelected?.Invoke(selected);
            });
        }
    }
    // TODO: 未来还会有一个 OpenForComponentSelection 方法，用来选武器！
}