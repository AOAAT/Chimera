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
    public TMP_Text PowerText; // 【新增】耗电量显示

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
        // 查字典获取底盘自身耗电
        float power = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.PowerCost);

        // 遍历所有已安装的组件，累加耗电量
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
    // 【核心渲染】UI层面的“科学怪人”实时组装
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

            // 去大管家那里查这个实体的具体数据
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;

            // 获取这个插槽在图纸上的定义
            var slotDef = profile.ChassisData.Sockets[slotIdx];

            // 生成零件UI实体
            GameObject compObj = new GameObject($"UI_Comp_{comp.BaseData.ComponentName}");
            compObj.transform.SetParent(chassisObj.transform, false); // 作为底盘的子节点，完美继承相对坐标
            Image compImg = compObj.AddComponent<Image>();
            compImg.sprite = comp.BaseData.ComponentIcon;
            compImg.SetNativeSize();

            RectTransform compRect = compObj.GetComponent<RectTransform>();

            // 【极其惊艳的坐标系转换】世界坐标偏移量 * 缩放器 + 零件自带的微调补偿
            compRect.anchoredPosition = (slotDef.LocalPosition + comp.BaseData.AnchorOffset) * WorldToUIMultiplier;

            // 完美还原旋转角度
            compRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle + comp.BaseData.BaseRotationOffset);

            // 完美还原缩放 (插槽默认缩放 * 零件视觉缩放修正)
            compRect.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);
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