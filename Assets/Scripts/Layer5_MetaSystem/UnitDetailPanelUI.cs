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
    public TMP_Text PowerText;

    [Header("=== 机甲预览图 UI 绑定 ===")]
    public RectTransform UnitVisualContainer;

    [Range(0.1f, 5f)]
    public float PreviewScale = 1.5f;
    public float WorldToUIMultiplier = 100f;

    private SavedUnitProfile currentProfile;
    private int currentSlotIndex = -1;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void OpenDetail(int slotIndex, SavedUnitProfile profile)
    {
        currentSlotIndex = slotIndex;
        currentProfile = profile;
        gameObject.SetActive(true);

        NameText.text = profile.UnitName;
        APText.text = $"AP: {profile.CurrentAP}";

        float maxHP = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.AddedHP);
        foreach (string compID in profile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                // 👇【核心修复 3】：从等级数据中读血量
                var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                if (lvData != null) maxHP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedHP);
            }
        }
        HPText.text = $"HP: {profile.CurrentHP} / {maxHP}";

        float totalPower = PlayerInventoryManager.GetStatValue(profile.ChassisData.BaseStats, StatType.PowerCost);
        foreach (string compID in profile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                // 👇【核心修复 4】：从等级数据中读耗电
                var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                if (lvData != null) totalPower += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.PowerCost);
            }
        }
        PowerText.text = $"耗电量: {totalPower}";

        BuildUnitVisual(profile);
    }

    private void BuildUnitVisual(SavedUnitProfile profile)
    {
        foreach (Transform child in UnitVisualContainer) Destroy(child.gameObject);

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
            ComponentDataSO targetData = comp.BaseData;
            compBtn.onClick.AddListener(() =>
            {
                ItemDetailPanelUI.Instance.ShowComponentDetail(targetData);
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
}