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

    // 👇【核心修复】：锁死底层换算率，统一暴露 PreviewScale
    [Range(0.1f, 5f)]
    public float PreviewScale = 1.5f;
    private const float WorldToUIMultiplier = 100f; // 锁死

    private SavedUnitProfile currentProfile;
    private int currentSlotIndex = -1;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    // --- 请替换 UnitDetailPanelUI.cs 中的 OpenDetail 方法 ---
    public void OpenDetail(int slotIndex, SavedUnitProfile profile)
    {
        currentSlotIndex = slotIndex;
        currentProfile = profile;
        gameObject.SetActive(true);

        NameText.text = profile.UnitName;
        APText.text = $"护甲: {profile.CurrentAP}";

        // 1. 抓取底盘基准值
        float maxHP = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.AddedHP);
        float totalBlock = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.AddedBlock);
        float totalMass = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.AddedMass);
        float totalEngine = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.EnginePower);

        // 2. 遍历组件叠加
        foreach (string compID in profile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                if (lvData != null)
                {
                    maxHP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedHP);
                    totalBlock += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedBlock);
                    totalMass += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedMass);
                    totalEngine += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.EnginePower);
                }
            }
        }

        // 3. 计算最终移速
        float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
        float finalSpeed = GameFormulas.CalcMoveSpeed(totalEngine, totalMass, speedMult);

        // 4. 灌入数据
        HPText.text = $"血量: {profile.CurrentHP} / {maxHP}";

        if (BlockText != null) BlockText.text = $"格挡: {totalBlock}";
        if (MassText != null) MassText.text = $"质量: {totalMass}t";
        if (SpeedText != null) SpeedText.text = $"移速: {finalSpeed:F1} m/s";

        BuildUnitVisual(profile);
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

        for (int i = 0; i < profile.SlotIndices.Count; i++)
        {
            int slotIdx = profile.SlotIndices[i];
            string compID = profile.EquippedComponentIDs[i];

            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;

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
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.gameObject.SetActive(false);
        AssemblyWorkshopUI.Instance.OpenWorkshopWithUnit(currentSlotIndex, currentProfile);
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

        // 3. 关闭当前面板并刷新机库
        CloseDetail();
        if (HangarMenuUI.Instance != null)
        {
            HangarMenuUI.Instance.RefreshHangar();
        }
    }
}