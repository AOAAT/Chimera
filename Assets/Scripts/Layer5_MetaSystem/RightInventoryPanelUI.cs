using System;
using System.Collections.Generic;
using UnityEngine;

public class RightInventoryPanelUI : MonoBehaviour
{
    public static RightInventoryPanelUI Instance;

    public Transform ContentRoot;
    public InventoryItemSlotUI ItemSlotPrefab;

    [Tooltip("如果不为空，当仓库没东西时会自动显示这个提示字")]
    public GameObject EmptyWarningText; // 【新增】防空堡垒

    // ==========================================
    // 缓存“筛选逻辑”，用于热更新时重新去后台提货！
    // ==========================================
    private enum PanelMode { None, Chassis, Component }
    private PanelMode currentMode = PanelMode.None;

    private Func<List<InstancedChassis>> getChassisFunc;
    private Action<InstancedChassis> onChassisSelectedCallback;

    private Func<List<InstancedComponent>> getComponentsFunc;
    private bool currentAllowUnequip;
    private Action<InstancedComponent> onComponentSelectedCallback;

    private void Awake() { Instance = this; }

    // 👇 【核心接线】：开机戴耳机，关机摘耳机
    private void Start()
    {
        PlayerInventoryManager.Instance.OnInventoryChanged += RefreshPanel;
    }
    private void OnDestroy()
    {
        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged -= RefreshPanel;
    }

    private void ClearShelf()
    {
        foreach (Transform child in ContentRoot) Destroy(child.gameObject);
    }

    // ==========================================
    // 打开面板入口 (参数变成了 Func 获取器！)
    // ==========================================
    public void OpenForChassisSelection(Func<List<InstancedChassis>> getChassis, Action<InstancedChassis> onChassisSelected)
    {
        currentMode = PanelMode.Chassis;
        getChassisFunc = getChassis;
        onChassisSelectedCallback = onChassisSelected;
        gameObject.SetActive(true);
        RefreshPanel(); // 打开时立刻刷一次
    }

    public void OpenForComponentSelection(Func<List<InstancedComponent>> getComponents, bool allowUnequip, Action<InstancedComponent> onComponentSelected)
    {
        currentMode = PanelMode.Component;
        getComponentsFunc = getComponents;
        currentAllowUnequip = allowUnequip;
        onComponentSelectedCallback = onComponentSelected;
        gameObject.SetActive(true);
        RefreshPanel();
    }

    // ==========================================
    // 【神级功能】：热更新引擎
    // ==========================================
    private void RefreshPanel()
    {
        if (!gameObject.activeSelf) return; // 如果面板没开着，就不浪费性能去刷

        ClearShelf();

        if (currentMode == PanelMode.Chassis && getChassisFunc != null)
        {
            var list = getChassisFunc.Invoke(); // 实时去后台现抓最新数据！
            if (EmptyWarningText != null) EmptyWarningText.SetActive(list.Count == 0);

            foreach (var chassis in list)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                slotObj.SetupChassis(chassis, (selected) => {
                    gameObject.SetActive(false);
                    onChassisSelectedCallback?.Invoke(selected);
                });
            }
        }
        else if (currentMode == PanelMode.Component && getComponentsFunc != null)
        {
            var list = getComponentsFunc.Invoke();
            if (EmptyWarningText != null) EmptyWarningText.SetActive(list.Count == 0 && !currentAllowUnequip);

            if (currentAllowUnequip)
            {
                var unequipSlotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                unequipSlotObj.SetupUnequip(() => {
                    gameObject.SetActive(false);
                    onComponentSelectedCallback?.Invoke(null);
                });
            }

            foreach (var comp in list)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                slotObj.ItemIcon.color = Color.white;
                slotObj.ItemNameText.color = Color.white;
                slotObj.SetupComponent(comp, (selected) => {
                    gameObject.SetActive(false);
                    onComponentSelectedCallback?.Invoke(selected);
                });
            }
        }
    }
}