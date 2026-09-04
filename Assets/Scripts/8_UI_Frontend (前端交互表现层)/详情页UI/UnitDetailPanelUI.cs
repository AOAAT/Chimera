// --- START OF FILE UnitDetailPanelUI.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitDetailPanelUI : MonoBehaviour
{
    public static UnitDetailPanelUI Instance;

    [Header("=== 纯文本数据 UI 绑定 ===")]
    public TMP_Text NameText;
    public TMP_Text HPText;
    public TMP_Text APText;
    public TMP_Text BlockText;
    public TMP_Text MassText;
    public TMP_Text SpeedText;

    [Header("=== 机甲预览图 UI 绑定 ===")]
    public RectTransform UnitVisualContainer;

    [Header("=== 操作权限控制 ===")]
    public GameObject ModificationButtonsGroup; // 包含“改装”和“拆解”的父物体

    // 👇【核心修复】：锁死底层换算率，统一暴露 PreviewScale
    [Range(0.1f, 5f)]
    public float PreviewScale = 1.5f;
    private const float WorldToUIMultiplier = 100f; // 锁死

    private SavedUnitProfile currentProfile;
    private int currentSlotIndex = -1;
    private MechUnit2D bindedUnit; // 🌟 增加对物理机甲的引用
    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    // --- 请替换 UnitDetailPanelUI.cs 中的 OpenDetail 方法 ---
    public void OpenDetail(MechUnit2D unit, bool isReadOnly = true)
    {
        if (unit == null) return;

        // 1. 存入物理引用，供后续“改装”按钮跳转使用
        bindedUnit = unit;

        // 2. 从物理对象中提取出它的配置档案
        currentProfile = unit.GetProfile();

        gameObject.SetActive(true);

        // --- 以下显示逻辑全部改为使用提取出来的 currentProfile ---
        NameText.text = currentProfile.UnitName;
        APText.text = $"护甲: {currentProfile.CurrentAP}";

        // 3. 抓取底盘基准值
        float maxHP = PlayerInventoryManager.GetStatValue(currentProfile.ChassisData.BaseStats, StatType.AddedHP);
        float totalBlock = PlayerInventoryManager.GetStatValue(currentProfile.ChassisData.BaseStats, StatType.AddedBlock);
        float totalMass = PlayerInventoryManager.GetStatValue(currentProfile.ChassisData.BaseStats, StatType.AddedMass);
        float totalEngine = PlayerInventoryManager.GetStatValue(currentProfile.ChassisData.BaseStats, StatType.EnginePower);

        // 4. 遍历组件叠加
        foreach (EquippedSlotRecord equippedSlot in currentProfile.EquippedSlots)
        {
            if (equippedSlot == null) continue;

            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == equippedSlot.ComponentInstanceID);
            if (comp != null && comp.BaseData != null)
            {
                // 注意：这里已经对齐了新语义 GetModelData
                var modelData = comp.BaseData.GetModelData(comp.CurrentMark);
                if (modelData != null)
                {
                    maxHP += PlayerInventoryManager.GetStatValue(modelData.Stats, StatType.AddedHP);
                    totalBlock += PlayerInventoryManager.GetStatValue(modelData.Stats, StatType.AddedBlock);
                    totalMass += PlayerInventoryManager.GetStatValue(modelData.Stats, StatType.AddedMass);
                    totalEngine += PlayerInventoryManager.GetStatValue(modelData.Stats, StatType.EnginePower);
                }
            }
        }

        // 5. 计算最终移速
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float finalSpeed = GameFormulas.CalcMoveSpeed(totalEngine, totalMass, speedMult);

        // 6. 灌入数据
        HPText.text = $"血量: {currentProfile.CurrentHP:F0} / {maxHP:F0}";

        if (BlockText != null) BlockText.text = $"格挡: {totalBlock:F0}";
        if (MassText != null) MassText.text = $"质量: {totalMass:F1}t";
        if (SpeedText != null) SpeedText.text = $"移速: {finalSpeed:F1} m/s";
        if (ModificationButtonsGroup != null)
        {
            ModificationButtonsGroup.SetActive(!isReadOnly);
        }
        // 7. 渲染视觉预览
        BuildUnitVisual(currentProfile);
    }
    private void BuildUnitVisual(SavedUnitProfile profile)
    {
        foreach (Transform child in UnitVisualContainer) Destroy(child.gameObject);

        // PreviewScale 的缩放魔法！
        UnitVisualContainer.localScale = Vector3.one * PreviewScale;

        GameObject chassisObj = new GameObject("UI_ChassisBase");
        chassisObj.transform.SetParent(UnitVisualContainer, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = profile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();

        foreach (EquippedSlotRecord equippedSlot in profile.EquippedSlots)
        {
            if (equippedSlot == null) continue;

            int slotIdx = equippedSlot.SlotIndex;
            string compID = equippedSlot.ComponentInstanceID;

            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;

            if (slotIdx < 0 || slotIdx >= profile.ChassisData.Sockets.Count) continue;

            var slotDef = profile.ChassisData.Sockets[slotIdx];

            GameObject slotObj = new GameObject($"UI_Slot_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;
            slotRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            GameObject hingeObj = new GameObject("UI_Hinge");
            hingeObj.transform.SetParent(slotRect, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            GameObject visObj = new GameObject("Sprite_Visual");
            visObj.transform.SetParent(hingeObj.transform, false);
            Image compImg = visObj.AddComponent<Image>();
            compImg.sprite = comp.BaseData.ComponentIcon;
            compImg.SetNativeSize();
            compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * WorldToUIMultiplier;

            Button compBtn = visObj.AddComponent<Button>();
            InstancedComponent targetInstance = comp;

            compBtn.onClick.AddListener(() =>
            {
                ItemDetailPanelUI.Instance.ShowComponentDetail(targetInstance);
            });
        }
    }

    public void OnClickRefit()
    {
        if (ItemDetailPanelUI.Instance != null) ItemDetailPanelUI.Instance.HidePanel();
        gameObject.SetActive(false);

        // 🌟 修复：直接传入 bindedUnit 引用
        AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(bindedUnit);
    }

    public void CloseDetail()
    {
        if (ItemDetailPanelUI.Instance != null) ItemDetailPanelUI.Instance.HidePanel();
        gameObject.SetActive(false);
    }

    public void OnClickDismantle()
    {
        if (currentSlotIndex < 0 || currentProfile == null) return;

        // 1. 调用底层的原子化拆解
      

        // 2. 视觉反馈：震一下并关闭详情页
        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.15f, 0.15f);

        GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_Detach);


    }
}
