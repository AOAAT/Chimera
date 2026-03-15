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

    // TODO: 未来还会有一个 OpenForComponentSelection 方法，用来选武器！
}