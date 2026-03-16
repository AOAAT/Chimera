using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HangarSlotUI : MonoBehaviour
{
    [Header("=== 状态切换节点 ===")]
    public GameObject EmptyStateObj;
    public GameObject OccupiedStateObj;

    [Header("=== 占用态 UI 绑定 ===")]
    [Tooltip("现在这里是一个空的RectTransform容器，用来挂载动态生成的拼装图")]
    public RectTransform UnitVisualContainer;
    public TMP_Text UnitNameText;
    public TMP_Text HPText;
    public TMP_Text APText;
    public TMP_Text PowerText; // 耗电量显示

    [Header("=== 视觉缩放与排版控制 ===")]
    [Range(0.1f, 3f)]
    [Tooltip("整个拼装机甲在格子里的缩放比例")]
    public float PreviewScale = 1.0f;

    [Tooltip("世界坐标系(Unit)转换到UI像素坐标系的乘数。通常如果PPU是100，这里填100")]
    public float WorldToUIMultiplier = 100f;

    private SavedUnitProfile bindedProfile;

    // ==========================================
    // 刷新格子显示
    // ==========================================
    public void RefreshSlot(SavedUnitProfile profile)
    {
        bindedProfile = profile;

        if (profile == null)
        {
            EmptyStateObj.SetActive(true);
            OccupiedStateObj.SetActive(false);
        }
        else
        {
            EmptyStateObj.SetActive(false);
            OccupiedStateObj.SetActive(true);

            UnitNameText.text = profile.UnitName;
            HPText.text = $"HP: {profile.CurrentHP}";
            APText.text = $"AP: {profile.CurrentAP}";

            // 1. 计算总耗电量 (底盘耗电 + 所有零件耗电)
            float totalPower = CalculateTotalPowerCost(profile);
            PowerText.text = $"耗电: {totalPower}";

            // 2. 动态拼装完整机甲图样
            BuildUnitVisual(profile);
        }
    }

    // ==========================================
    // 【核心算力】动态计算耗电量
    // ==========================================
    private float CalculateTotalPowerCost(SavedUnitProfile profile)
    {
        float power = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.PowerCost);

        foreach (string compID in profile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                power += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.PowerCost);
            }
        }
        return power;
    }

    // ==========================================
    // 【核心渲染】UI层面的“科学怪人”实时组装 (已接入正骨魔法)
    // ==========================================
    private void BuildUnitVisual(SavedUnitProfile profile)
    {
        // 1. 清理旧的拼装图 (防止刷新时重叠)
        foreach (Transform child in UnitVisualContainer)
        {
            Destroy(child.gameObject);
        }

        // 应用用户自定义的缩放比例
        UnitVisualContainer.localScale = Vector3.one * PreviewScale;

        // 2. 生成底盘基座
        GameObject chassisObj = new GameObject("UI_ChassisBase");
        chassisObj.transform.SetParent(UnitVisualContainer, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = profile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize(); // 恢复原始像素大小

        // 3. 按照插槽数据，把零件一个个“焊”上去
        for (int i = 0; i < profile.SlotIndices.Count; i++)
        {
            int slotIdx = profile.SlotIndices[i];
            string compID = profile.EquippedComponentIDs[i];

            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;

            var slotDef = profile.ChassisData.Sockets[slotIdx];

            // A. 插槽基座 (负责基础坐标和插槽原本的旋转)
            GameObject slotObj = new GameObject($"UI_Slot_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;
            slotRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            // B. Hinge 转轴 (负责组件自带的旋转和缩放)
            GameObject hingeObj = new GameObject("UI_Hinge");
            hingeObj.transform.SetParent(slotRect, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            // C. Visual 图片 (负责核心的【负号偏移】)
            GameObject visObj = new GameObject("Sprite_Visual");
            visObj.transform.SetParent(hingeObj.transform, false);
            Image compImg = visObj.AddComponent<Image>();
            compImg.sprite = comp.BaseData.ComponentIcon;
            compImg.SetNativeSize();

            // 【极其惊艳的修复】：用上负号，彻底治愈机库里的脱臼问题！
            compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * WorldToUIMultiplier;
        }
    }

    // ==========================================
    // 玩家点击事件
    // ==========================================
    public void OnSlotClicked()
    {
        if (bindedProfile == null)
        {
            HangarMenuUI.Instance.TriggerCreateNewUnit();
        }
        else
        {
            HangarMenuUI.Instance.TriggerOpenUnitDetail(bindedProfile);
        }
    }
}