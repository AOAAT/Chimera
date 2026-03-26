using System;
using System.Linq;
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

    [Header("=== 占用状态表现 ===")]
    public GameObject EquippedOverlay;   // 一个半透明黑底的遮罩面板
    public TMP_Text EquippedStatusText;  // 显示 "已搭载于: 狂怒号"

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

        // 状态可视化
        bool isEquipped = chassis != null && chassis.IsEquipped;
        if (EquippedOverlay != null) EquippedOverlay.SetActive(isEquipped);

        // 【核心修复 2】：将变量声明在外部，解决作用域找不到的问题
        string mechName = "未知机甲";

        if (isEquipped)
        {
            // 【核心修复 1】：HangarUnits 是数组，必须用 System.Array.Find 或 Linq
            var ownerMech = System.Array.Find(PlayerInventoryManager.Instance.HangarUnits, u => u != null && u.UnitID == chassis.EquippedUnitID);
            if (ownerMech != null) mechName = ownerMech.UnitName;

            if (EquippedStatusText != null)
            {
                EquippedStatusText.text = $"已搭载于\n<color=#FFD700>{mechName}</color>";
            }
        }

        // 绑定点击事件，加入防呆拦截
        onClickCallback = () => {
            if (isEquipped)
            {
                Debug.LogWarning($"【操作拒绝】该底盘已被 [{mechName}] 占用，请先去机库将其卸下！");
            }
            else
            {
                onSelected?.Invoke(chassis);
            }
        };
    }

    // ==========================================
    // 渲染组件数据
    // ==========================================
    public void SetupComponent(InstancedComponent component, Action<InstancedComponent> onSelected)
    {
        cachedChassis = null;
        cachedComponent = component;
        isUnequipSlot = false;

        if (component != null && component.BaseData != null)
        {
            ItemIcon.sprite = component.BaseData.ComponentIcon;
            ItemNameText.text = component.BaseData.ComponentName;
        }

        // 状态可视化
        bool isEquipped = component != null && component.IsEquipped;
        if (EquippedOverlay != null) EquippedOverlay.SetActive(isEquipped);

        // 【核心修复 2】：将变量声明在外部
        string mechName = "未知机甲";

        if (isEquipped)
        {
            // 【核心修复 1】：修复数组查询报错
            var ownerMech = System.Array.Find(PlayerInventoryManager.Instance.HangarUnits, u => u != null && u.UnitID == component.EquippedUnitID);
            if (ownerMech != null) mechName = ownerMech.UnitName;

            if (EquippedStatusText != null)
            {
                EquippedStatusText.text = $"已搭载于\n<color=#FFD700>{mechName}</color>";
            }
        }

        // 绑定点击事件，加入防呆拦截
        onClickCallback = () => {
            if (isEquipped)
            {
                Debug.LogWarning($"【操作拒绝】该组件已被 [{mechName}] 占用，请先去机库将其卸下！");
            }
            else
            {
                onSelected?.Invoke(component);
            }
        };
    }

    // ==========================================
    // 渲染卸载槽位 (特殊用)
    // ==========================================
    public void SetupUnequip(Action onSelected)
    {
        cachedChassis = null;
        cachedComponent = null;
        isUnequipSlot = true;

        if (EquippedOverlay != null) EquippedOverlay.SetActive(false); // 卸载槽不需要遮罩

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

            // 只有当不是卸载按钮时，才去关详情页
            if (!isUnequipSlot) ItemDetailPanelUI.Instance?.HidePanel();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log($"【系统提示】你右键点击了 [{ItemNameText.text}]！此处预留给未来的【分解/强化】功能！");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUnequipSlot) return; // 卸载槽不弹详情页

        if (cachedComponent != null && cachedComponent.BaseData != null)
        {
            ItemDetailPanelUI.Instance?.ShowComponentDetail(cachedComponent.BaseData);
        }
        else if (cachedChassis != null && cachedChassis.BaseData != null)
        {
            ItemDetailPanelUI.Instance?.ShowChassisDetail(cachedChassis.BaseData);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 建议保留此处为空，让玩家点击其他地方或点击自身时再关闭面板
    }
}