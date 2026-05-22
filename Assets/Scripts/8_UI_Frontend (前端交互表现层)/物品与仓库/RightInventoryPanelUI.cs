using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RightInventoryPanelUI : MonoBehaviour
{
    public static RightInventoryPanelUI Instance;

    public Transform ContentRoot;
    public InventoryItemSlotUI ItemSlotPrefab;

    [Tooltip("如果不为空，当仓库没东西时会自动显示这个提示字")]
    public GameObject EmptyWarningText;

    private enum PanelMode { None, Chassis, Component }
    private PanelMode currentMode = PanelMode.None;

    // 🌟 核心修复：更新 Func 签名以匹配新的堆叠结构
    private Func<List<ChassisStack>> getChassisFunc;
    private Action<ChassisStack> onChassisSelectedCallback;

    private Func<List<ComponentStack>> getComponentsFunc;
    private bool currentAllowUnequip;
    private Action<ComponentStack> onComponentSelectedCallback;

    private void Awake() { Instance = this; }

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
    // 🚀 打开面板入口：针对底盘堆栈
    // ==========================================
    public void OpenForChassisSelection(Func<List<ChassisStack>> getChassis, Action<ChassisStack> onChassisSelected)
    {
        currentMode = PanelMode.Chassis;
        getChassisFunc = getChassis;
        onChassisSelectedCallback = onChassisSelected;
        gameObject.SetActive(true);
        RefreshPanel();
    }

    // ==========================================
    // 🚀 打开面板入口：针对零件堆栈
    // ==========================================
    public void OpenForComponentSelection(Func<List<ComponentStack>> getComponents, bool allowUnequip, Action<ComponentStack> onComponentSelected)
    {
        currentMode = PanelMode.Component;
        getComponentsFunc = getComponents;
        currentAllowUnequip = allowUnequip;
        onComponentSelectedCallback = onComponentSelected;
        gameObject.SetActive(true);
        RefreshPanel();
    }

    public void RefreshPanel()
    {
        if (!gameObject.activeSelf) return;

        ClearShelf();

        if (currentMode == PanelMode.Chassis && getChassisFunc != null)
        {
            var list = getChassisFunc.Invoke();
            if (EmptyWarningText != null) EmptyWarningText.SetActive(list.Count == 0);

            foreach (var stack in list)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                // 🌟 使用专门的堆叠设置方法
                slotObj.SetupChassisStack(stack, (selected) => {
                    gameObject.SetActive(false);
                    onChassisSelectedCallback?.Invoke(selected);
                });
            }
        }
        else if (currentMode == PanelMode.Component && getComponentsFunc != null)
        {
            var list = getComponentsFunc.Invoke();
            if (EmptyWarningText != null) EmptyWarningText.SetActive(list.Count == 0 && !currentAllowUnequip);

            // 卸载槽位逻辑
            if (currentAllowUnequip)
            {
                var unequipSlotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                unequipSlotObj.SetupUnequip(() => {
                    gameObject.SetActive(false);
                    onComponentSelectedCallback?.Invoke(null);
                });
            }

            foreach (var stack in list)
            {
                var slotObj = Instantiate(ItemSlotPrefab, ContentRoot);
                // 🌟 使用专门的堆叠设置方法
                slotObj.SetupComponentStack(stack, (selected) => {
                    gameObject.SetActive(false);
                    onComponentSelectedCallback?.Invoke(selected);
                });
            }
        }
    }
}