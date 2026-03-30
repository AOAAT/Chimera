using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AssemblyWorkshopUI : MonoBehaviour
{
    public static AssemblyWorkshopUI Instance;

    [Header("=== 核心状态机数据 ===")]
    private SavedUnitProfile currentEditingProfile;
    private bool isCreatingNew = false;
    private int targetHangarSlotIndex = -1;

    [Header("=== 快照备份系统 ===")]
    private List<int> snapshot_SlotIndices = new List<int>();
    private List<string> snapshot_EquippedComponentIDs = new List<string>();
    private float snapshot_HP;
    private float snapshot_AP;
    private float snapshot_DamageTaken = 0f;

    [Header("=== 左右分层 UI 面板 ===")]
    public GameObject LeftStatsPanel;
    public GameObject CenterPreviewArea;
    public GameObject RightInventoryPanel;

    [Header("=== 中央预览区 UI 绑定 ===")]
    public GameObject GhostChassisPrompt;
    public Transform ChassisVisualRoot;

    [Header("=== 左侧属性区 UI 绑定 ===")]
    public TMP_Text HPText;
    public TMP_Text APText;
    public TMP_Text PowerText;
    public TMP_InputField UnitNameInput;

    [Header("=== 视觉与排版控制 ===")]
    public float WorldToUIMultiplier = 100f;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void OpenEmptyWorkshop(int slotIndex)
    {
        gameObject.SetActive(true);
        currentEditingProfile = null;
        isCreatingNew = true;
        targetHangarSlotIndex = slotIndex;

        snapshot_SlotIndices.Clear();
        snapshot_EquippedComponentIDs.Clear();
        snapshot_DamageTaken = 0f;

        RefreshWorkshopState();
    }

    public void OpenWorkshopWithUnit(int slotIndex, SavedUnitProfile unitProfile)
    {
        gameObject.SetActive(true);
        currentEditingProfile = unitProfile;
        isCreatingNew = false;
        targetHangarSlotIndex = slotIndex;

        snapshot_SlotIndices = new List<int>(unitProfile.SlotIndices);
        snapshot_EquippedComponentIDs = new List<string>(unitProfile.EquippedComponentIDs);
        snapshot_HP = unitProfile.CurrentHP;
        snapshot_AP = unitProfile.CurrentAP;

        float initialMaxHP = PlayerInventoryManager.GetStatValue(unitProfile.ChassisData.BaseStats, StatType.AddedHP);
        foreach (string compID in unitProfile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                // 👇【核心修复 1】：从当前等级的数据块中读取血量
                var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                if (lvData != null) initialMaxHP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedHP);
            }
        }
        snapshot_DamageTaken = initialMaxHP - unitProfile.CurrentHP;

        RefreshWorkshopState();
    }

    private void RefreshWorkshopState()
    {
        RightInventoryPanel.SetActive(false);

        if (currentEditingProfile == null)
        {
            GhostChassisPrompt.SetActive(true);
            ChassisVisualRoot.gameObject.SetActive(false);
            HPText.text = "HP: -- / --";
            APText.text = "AP: -- / --";
            PowerText.text = "耗电: --";
            UnitNameInput.text = "等待底盘接入...";
            UnitNameInput.interactable = false;
        }
        else
        {
            GhostChassisPrompt.SetActive(false);
            ChassisVisualRoot.gameObject.SetActive(true);

            float maxHP = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedHP);
            float maxAP = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedAP);
            float totalPower = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.PowerCost);

            foreach (string compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null && comp.BaseData != null)
                {
                    // 👇【核心修复 2】：从当前等级的数据块中读取面板属性
                    var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                    if (lvData != null)
                    {
                        maxHP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedHP);
                        maxAP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedAP);
                        totalPower += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.PowerCost);
                    }
                }
            }

            if (isCreatingNew)
            {
                currentEditingProfile.CurrentHP = maxHP;
                currentEditingProfile.CurrentAP = maxAP;
            }
            else
            {
                currentEditingProfile.CurrentHP = Mathf.Max(1f, maxHP - snapshot_DamageTaken);
                currentEditingProfile.CurrentAP = maxAP;
            }

            HPText.text = $"HP: {currentEditingProfile.CurrentHP} / {maxHP}";
            APText.text = $"AP: {currentEditingProfile.CurrentAP} / {maxAP}";
            PowerText.text = $"耗电: {totalPower}";
            UnitNameInput.text = currentEditingProfile.UnitName;
            UnitNameInput.interactable = true;

            RenderMechAndSockets();
        }
    }

    public void OnClickGhostChassis()
    {
        RightInventoryPanelUI.Instance.OpenForChassisSelection(
            () => PlayerInventoryManager.Instance.ChassisInventory.FindAll(c => !c.IsEquipped),
            OnChassisSelectedFromInventory
        );
    }

    public void OnChassisSelectedFromInventory(InstancedChassis selectedChassis)
    {
        currentEditingProfile = new SavedUnitProfile(selectedChassis, "特制原型机");
        selectedChassis.EquippedUnitID = currentEditingProfile.UnitID;
        isCreatingNew = true;
        RightInventoryPanel.SetActive(false);
        RefreshWorkshopState();
    }

    private void RenderMechAndSockets()
    {
        foreach (Transform child in ChassisVisualRoot) Destroy(child.gameObject);

        GameObject chassisObj = new GameObject("UI_ChassisBase");
        chassisObj.transform.SetParent(ChassisVisualRoot, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = currentEditingProfile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();

        for (int i = 0; i < currentEditingProfile.ChassisData.Sockets.Count; i++)
        {
            var slotDef = currentEditingProfile.ChassisData.Sockets[i];
            int slotIndex = i;

            GameObject slotObj = new GameObject($"UI_Socket_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);

            Image slotImg = slotObj.AddComponent<Image>();
            slotImg.color = new Color(1f, 1f, 1f, 0.3f);
            slotImg.rectTransform.sizeDelta = new Vector2(60, 60);
            slotImg.rectTransform.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;
            slotImg.rectTransform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            Button slotBtn = slotObj.AddComponent<Button>();
            slotBtn.onClick.AddListener(() => OnSlotClicked(slotIndex));

            int equippedIdx = currentEditingProfile.SlotIndices.IndexOf(slotIndex);
            if (equippedIdx != -1)
            {
                string compID = currentEditingProfile.EquippedComponentIDs[equippedIdx];
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);

                if (comp != null && comp.BaseData != null)
                {
                    slotImg.color = new Color(1f, 1f, 1f, 0f);

                    GameObject compHingeObj = new GameObject($"UI_Hinge_{comp.BaseData.ComponentName}");
                    compHingeObj.transform.SetParent(slotObj.transform, false);
                    compHingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
                    compHingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

                    GameObject compVisObj = new GameObject("Sprite_Visual");
                    compVisObj.transform.SetParent(compHingeObj.transform, false);
                    Image compImg = compVisObj.AddComponent<Image>();
                    compImg.sprite = comp.BaseData.ComponentIcon;
                    compImg.SetNativeSize();
                    compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * WorldToUIMultiplier;
                }
            }
        }
    }

    private void OnSlotClicked(int slotIndex)
    {
        var slotDef = currentEditingProfile.ChassisData.Sockets[slotIndex];
        int existingIdx = currentEditingProfile.SlotIndices.IndexOf(slotIndex);
        bool hasEquippedComp = (existingIdx != -1);

        RightInventoryPanelUI.Instance.OpenForComponentSelection(
            () => PlayerInventoryManager.Instance.ComponentInventory.FindAll(c => !c.IsEquipped && slotDef.AllowedTypes.Contains(c.BaseData.Type)),
            hasEquippedComp,
            (selectedComp) => OnComponentSelectedFromInventory(slotIndex, selectedComp)
        );
    }

    private void OnComponentSelectedFromInventory(int slotIndex, InstancedComponent selectedComp)
    {
        int existingIdx = currentEditingProfile.SlotIndices.IndexOf(slotIndex);
        InstancedComponent oldComp = null;

        if (existingIdx != -1)
        {
            string oldCompID = currentEditingProfile.EquippedComponentIDs[existingIdx];
            oldComp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == oldCompID);
        }

        if (!isCreatingNew)
        {
            if (!PlayerInventoryManager.Instance.ValidateHPBeforeUnequip(currentEditingProfile, oldComp, selectedComp))
            {
                Debug.LogWarning("【车间警报】拆卸该装甲会导致机体直接解体，操作已强制撤销！");
                return;
            }
        }

        if (existingIdx != -1)
        {
            if (oldComp != null) oldComp.EquippedUnitID = string.Empty;
            currentEditingProfile.SlotIndices.RemoveAt(existingIdx);
            currentEditingProfile.EquippedComponentIDs.RemoveAt(existingIdx);
        }

        if (selectedComp != null)
        {
            selectedComp.EquippedUnitID = currentEditingProfile.UnitID;
            currentEditingProfile.SlotIndices.Add(slotIndex);
            currentEditingProfile.EquippedComponentIDs.Add(selectedComp.InstanceID);
        }
        RefreshWorkshopState();
    }

    private bool ValidateUnitLegality(out string errorMessage)
    {
        errorMessage = "";
        int coreCount = 0;
        int mobilityCount = 0;

        foreach (string compID in currentEditingProfile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                if (comp.BaseData.Type == ComponentType.Core) coreCount++;
                if (comp.BaseData.Type == ComponentType.Movement) mobilityCount++;
            }
        }

        if (coreCount != 1)
        {
            errorMessage = $"【安检失败】机甲必须有且仅有 1 个核心引擎！当前数量: {coreCount}";
            return false;
        }

        if (mobilityCount < 1)
        {
            errorMessage = "【安检失败】机甲缺乏移动模块 (Mobility)，无法出击！";
            return false;
        }

        return true;
    }

    public void SaveAndExitWorkshop()
    {
        if (currentEditingProfile == null)
        {
            CancelAndExitWorkshop(); return;
        }

        if (!ValidateUnitLegality(out string errorMsg))
        {
            Debug.LogWarning(errorMsg);
            return;
        }

        currentEditingProfile.UnitName = UnitNameInput.text;

        if (isCreatingNew)
        {
            PlayerInventoryManager.Instance.HangarUnits[targetHangarSlotIndex] = currentEditingProfile;
        }
        else
        {
            PlayerInventoryManager.Instance.HangarUnits[targetHangarSlotIndex] = currentEditingProfile;
        }

        ExitToHangar();
    }

    public void CancelAndExitWorkshop()
    {
        if (currentEditingProfile == null)
        {
            ExitToHangar(); return;
        }

        if (isCreatingNew)
        {
            var chassis = PlayerInventoryManager.Instance.ChassisInventory.Find(c => c.InstanceID == currentEditingProfile.ChassisInstanceID);
            if (chassis != null) chassis.EquippedUnitID = string.Empty;

            foreach (var compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null) comp.EquippedUnitID = string.Empty;
            }
        }
        else
        {
            foreach (var compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null) comp.EquippedUnitID = string.Empty;
            }

            currentEditingProfile.SlotIndices = new List<int>(snapshot_SlotIndices);
            currentEditingProfile.EquippedComponentIDs = new List<string>(snapshot_EquippedComponentIDs);
            currentEditingProfile.CurrentHP = snapshot_HP;
            currentEditingProfile.CurrentAP = snapshot_AP;

            foreach (var compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null) comp.EquippedUnitID = currentEditingProfile.UnitID;
            }
        }

        ExitToHangar();
    }

    private void ExitToHangar()
    {
        currentEditingProfile = null;
        gameObject.SetActive(false);
        HangarMenuUI.Instance.gameObject.SetActive(true);
        HangarMenuUI.Instance.RefreshHangar();
    }
}