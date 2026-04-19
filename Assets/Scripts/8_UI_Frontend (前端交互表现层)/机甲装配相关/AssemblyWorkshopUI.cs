// --- START OF FILE AssemblyWorkshopUI.cs ---
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
    public TMP_Text BlockText;      // 总格挡
    public TMP_Text MassText;       // 总质量
    public TMP_Text SpeedText;      // 实际计算后的移速

    public TMP_InputField UnitNameInput;

    [Header("=== 视觉与排版控制 ===")]
    [Range(0.1f, 5f)]
    public float PreviewScale = 1.0f;
    private const float WorldToUIMultiplier = 100f; // 1米 = 100像素

    [Header("=== 插槽表现控制 ===")]
    [Tooltip("插槽可点击区域的大小 (推荐 35)")]
    public float SlotButtonSize = 35f;
    [Tooltip("插槽的圆形贴图")]
    public Sprite CircularSlotSprite;

    [Header("=== 能量导线系统 ===")]
    public GameObject UIConduitPrefab; // 拖入带 LineRenderer 和 MechEnergyConduit 脚本的预制体
    // 使用字典精准映射：插槽索引 -> 对应的导线脚本
    private Dictionary<int, MechEnergyConduit> activeConduitMap = new Dictionary<int, MechEnergyConduit>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
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

        // 计算当前战损
        float initialMaxHP = PlayerInventoryManager.GetStatValue(unitProfile.ChassisData.BaseStats, StatType.AddedHP);
        foreach (string compID in unitProfile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
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
            if (BlockText != null) BlockText.text = "格挡: --";
            if (MassText != null) MassText.text = "质量: --";
            if (SpeedText != null) SpeedText.text = "移速: --";
            UnitNameInput.text = "等待底盘接入...";
            UnitNameInput.interactable = false;
        }
        else
        {
            GhostChassisPrompt.SetActive(false);
            ChassisVisualRoot.gameObject.SetActive(true);

            // 1. 基础数值计算
            float maxHP = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedHP);
            float maxAP = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedAP);
            float totalPower = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.PowerCost);
            float totalBlock = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedBlock);
            float totalMass = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedMass);
            float totalEngine = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.EnginePower);

            foreach (string compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null && comp.BaseData != null)
                {
                    var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                    if (lvData != null)
                    {
                        maxHP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedHP);
                        maxAP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedAP);
                        totalPower += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.PowerCost);
                        totalBlock += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedBlock);
                        totalMass += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedMass);
                        totalEngine += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.EnginePower);
                    }
                }
            }

            // 2. 更新血量状态
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

            // 3. 物理公式应用
            float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
            float finalSpeed = GameFormulas.CalcMoveSpeed(totalEngine, totalMass, speedMult);

            // 4. UI 文字刷新
            HPText.text = $"HP: {currentEditingProfile.CurrentHP:F0} / {maxHP:F0}";
            APText.text = $"AP: {currentEditingProfile.CurrentAP:F0} / {maxAP:F0}";
            PowerText.text = $"耗电: {totalPower:F0}";
            if (BlockText != null) BlockText.text = $"格挡: {totalBlock:F0}";
            if (MassText != null) MassText.text = $"质量: {totalMass:F1}t";
            if (SpeedText != null) SpeedText.text = $"移速: {finalSpeed:F1} m/s";

            UnitNameInput.text = currentEditingProfile.UnitName;
            UnitNameInput.interactable = true;

            RenderMechAndSockets();
        }
    }

    private void RenderMechAndSockets()
    {
        // 清理旧物件和导线映射
        foreach (Transform child in ChassisVisualRoot) Destroy(child.gameObject);
        activeConduitMap.Clear();

        if (currentEditingProfile == null) return;

        // 生成缩放根节点
        GameObject scalerObj = new GameObject("UI_ScalerRoot");
        scalerObj.transform.SetParent(ChassisVisualRoot, false);
        scalerObj.transform.localScale = Vector3.one * PreviewScale;

        // 生成底盘图片
        GameObject chassisObj = new GameObject("UI_ChassisBase");
        chassisObj.transform.SetParent(scalerObj.transform, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = currentEditingProfile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();
        chassisImg.raycastTarget = false; // 底盘不挡点击

        RectTransform coreTrans = null;
        Dictionary<int, RectTransform> slotRects = new Dictionary<int, RectTransform>();

        // 第一遍循环：生成插槽和已装组件
        for (int i = 0; i < currentEditingProfile.ChassisData.Sockets.Count; i++)
        {
            var slotDef = currentEditingProfile.ChassisData.Sockets[i];
            int slotIdx = i;

            GameObject slotObj = new GameObject($"UI_Socket_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;
            slotRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);
            slotRect.sizeDelta = new Vector2(SlotButtonSize, SlotButtonSize);

            // 必须添加 Image 才能被 Button 识别点击
            Image slotVisual = slotObj.AddComponent<Image>();
            slotVisual.sprite = CircularSlotSprite;
            slotVisual.color = new Color(1f, 1f, 1f, 0.3f);
            slotVisual.raycastTarget = true;

            slotRects.Add(slotIdx, slotRect);

            // 检查已装组件
            int equippedIdx = currentEditingProfile.SlotIndices.IndexOf(slotIdx);
            if (equippedIdx != -1)
            {
                string compID = currentEditingProfile.EquippedComponentIDs[equippedIdx];
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null)
                {
                    slotVisual.color = new Color(1f, 1f, 1f, 0f); // 装了东西就隐形
                    if (comp.BaseData.Type == ComponentType.Core) coreTrans = slotRect;
                    RenderComponentInSlot(slotRect, comp, slotDef);
                }
            }

            Button btn = slotObj.AddComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked(slotIdx));
        }

        // 第二遍循环：拉取导线
        if (coreTrans != null && UIConduitPrefab != null)
        {
            foreach (var kvp in slotRects)
            {
                if (kvp.Value == coreTrans) continue;

                GameObject lineObj = Instantiate(UIConduitPrefab, chassisObj.transform);
                lineObj.transform.SetAsFirstSibling(); // 垫在最底层

                var conduit = lineObj.GetComponent<MechEnergyConduit>();
                if (conduit != null)
                {
                    conduit.Initialize(coreTrans, kvp.Value);
                    activeConduitMap.Add(kvp.Key, conduit);
                }
            }
        }
    }

    private void RenderComponentInSlot(Transform slotRect, InstancedComponent comp, SlotDefinition slotDef)
    {
        GameObject compHingeObj = new GameObject($"UI_Hinge_{comp.BaseData.ComponentName}");
        compHingeObj.transform.SetParent(slotRect, false);
        compHingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
        compHingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

        GameObject compVisObj = new GameObject("Sprite_Visual");
        compVisObj.transform.SetParent(compHingeObj.transform, false);
        Image compImg = compVisObj.AddComponent<Image>();
        compImg.sprite = comp.BaseData.ComponentIcon;
        compImg.SetNativeSize();
        compImg.raycastTarget = false;
        compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * WorldToUIMultiplier;
    }

    // 当组件成功安装时触发
    public void OnComponentEquipped(int slotIndex)
    {
        if (activeConduitMap.ContainsKey(slotIndex))
        {
            activeConduitMap[slotIndex].TriggerPulse();
        }

        if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(0.05f);
        if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(0.1f, 0.1f);
    }

    public void OnClickGhostChassis()
    {
        RightInventoryPanelUI.Instance.OpenForChassisSelection(
            () => PlayerInventoryManager.Instance.ChassisInventory,
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

    private void OnSlotClicked(int slotIndex)
    {
        var slotDef = currentEditingProfile.ChassisData.Sockets[slotIndex];
        int existingIdx = currentEditingProfile.SlotIndices.IndexOf(slotIndex);
        bool hasEquippedComp = (existingIdx != -1);

        RightInventoryPanelUI.Instance.OpenForComponentSelection(
            () => PlayerInventoryManager.Instance.ComponentInventory.FindAll(c => slotDef.AllowedTypes.Contains(c.BaseData.Type)),
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
                Debug.LogWarning("【车间警报】血量过低，禁止拆除生存组件！");
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

            // 👇 安装成功，触发导线脉冲和震动
            OnComponentEquipped(slotIndex);
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
            errorMessage = $"【安检失败】机甲必须有且仅有 1 个核心引擎！";
            return false;
        }

        if (mobilityCount < 1)
        {
            errorMessage = "【安检失败】机甲缺乏移动模块，无法出击！";
            return false;
        }

        return true;
    }

    public void SaveAndExitWorkshop()
    {
        if (currentEditingProfile == null) { CancelAndExitWorkshop(); return; }

        if (!ValidateUnitLegality(out string errorMsg))
        {
            Debug.LogWarning(errorMsg);
            return;
        }

        currentEditingProfile.UnitName = UnitNameInput.text;
        PlayerInventoryManager.Instance.HangarUnits[targetHangarSlotIndex] = currentEditingProfile;
        ExitToHangar();
    }

    public void CancelAndExitWorkshop()
    {
        if (currentEditingProfile == null) { ExitToHangar(); return; }

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
            // 回滚快照
            currentEditingProfile.SlotIndices = new List<int>(snapshot_SlotIndices);
            currentEditingProfile.EquippedComponentIDs = new List<string>(snapshot_EquippedComponentIDs);
            currentEditingProfile.CurrentHP = snapshot_HP;
            currentEditingProfile.CurrentAP = snapshot_AP;
        }

        ExitToHangar();
    }

    private void ExitToHangar()
    {
        if (ItemDetailPanelUI.Instance != null) ItemDetailPanelUI.Instance.HidePanel();
        gameObject.SetActive(false);
        HangarMenuUI.Instance.gameObject.SetActive(true);
        HangarMenuUI.Instance.RefreshHangar();
    }
}