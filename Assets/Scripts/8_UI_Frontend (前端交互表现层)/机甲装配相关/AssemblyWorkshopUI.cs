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
    public TMP_Text BlockText;
    public TMP_Text MassText;
    public TMP_Text SpeedText;

    public TMP_InputField UnitNameInput;

    [Header("=== 视觉与排版控制 ===")]
    [Range(0.1f, 5f)]
    public float PreviewScale = 1.0f;
    private const float WorldToUIMultiplier = 100f;

    [Header("=== 插槽表现控制 ===")]
    public float SlotButtonSize = 35f;
    public Sprite CircularSlotSprite;

    [Header("=== 能量导线系统 ===")]
    public GameObject UIConduitPrefab;
    private Dictionary<int, MechEnergyConduit> activeConduitMap = new Dictionary<int, MechEnergyConduit>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        gameObject.SetActive(false);
    }

    public void OpenEmptyWorkshop(int slotIndex)
    {
        MusicManager.Instance?.SetImmersionMode(true);
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
        MusicManager.Instance?.SetImmersionMode(true);
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
                var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                if (lvData != null) initialMaxHP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedHP);
            }
        }
        snapshot_DamageTaken = initialMaxHP - unitProfile.CurrentHP;
        RefreshWorkshopState();
    }

    private void RefreshWorkshopState()
    {
        if (RightInventoryPanel != null) RightInventoryPanel.SetActive(false);

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
            float totalBlock = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedBlock);
            float totalMass = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedMass);
            float totalEngine = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.EnginePower);

            foreach (string compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null)
                {
                    var lvData = comp.BaseData.GetLevelData(comp.CurrentLevel);
                    maxHP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedHP);
                    maxAP += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedAP);
                    totalPower += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.PowerCost);
                    totalBlock += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedBlock);
                    totalMass += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.AddedMass);
                    totalEngine += PlayerInventoryManager.GetStatValue(lvData.Stats, StatType.EnginePower);
                }
            }

            if (isCreatingNew) { currentEditingProfile.CurrentHP = maxHP; currentEditingProfile.CurrentAP = maxAP; }
            else { currentEditingProfile.CurrentHP = Mathf.Max(1f, maxHP - snapshot_DamageTaken); currentEditingProfile.CurrentAP = maxAP; }

            float speedMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.SpeedMultiplier : 1f;
            float finalSpeed = GameFormulas.CalcMoveSpeed(totalEngine, totalMass, speedMult);

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
        // 1. 彻底清理环境
        foreach (Transform child in ChassisVisualRoot) Destroy(child.gameObject);
        activeConduitMap.Clear();

        if (currentEditingProfile == null) return;

        // 2. 生成缩放根节点
        GameObject scalerObj = new GameObject("UI_ScalerRoot");
        scalerObj.transform.SetParent(ChassisVisualRoot, false);
        scalerObj.transform.localScale = Vector3.one * PreviewScale;

        // 3. 生成底盘
        GameObject chassisObj = new GameObject("UI_ChassisBase");
        chassisObj.transform.SetParent(scalerObj.transform, false);
        Image chassisImg = chassisObj.AddComponent<Image>();
        chassisImg.sprite = currentEditingProfile.ChassisData.ChassisSprite;
        chassisImg.SetNativeSize();
        chassisImg.raycastTarget = false;

        RectTransform coreTrans = null;
        Dictionary<int, RectTransform> slotRects = new Dictionary<int, RectTransform>();

        // 4. 第一遍循环：部署插槽与零件
        for (int i = 0; i < currentEditingProfile.ChassisData.Sockets.Count; i++)
        {
            var slotDef = currentEditingProfile.ChassisData.Sockets[i];
            int slotIdx = i;

            // 创建 Socket 容器
            GameObject slotObj = new GameObject($"UI_Socket_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            RectTransform slotRect = slotObj.AddComponent<RectTransform>();

            // 应用底盘定义的坐标
            slotRect.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;

            // 👇【核心修复】：插槽本身的旋转必须先应用
            slotRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);
            slotRect.sizeDelta = new Vector2(SlotButtonSize, SlotButtonSize);

            // 按钮视觉
            Image slotVisual = slotObj.AddComponent<Image>();
            slotVisual.sprite = CircularSlotSprite;
            slotVisual.color = new Color(1f, 1f, 1f, 0.3f);
            slotVisual.raycastTarget = true;

            slotRects.Add(slotIdx, slotRect);

            // 检查并渲染已装组件
            int equippedIdx = currentEditingProfile.SlotIndices.IndexOf(slotIdx);
            if (equippedIdx != -1)
            {
                string compID = currentEditingProfile.EquippedComponentIDs[equippedIdx];
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null)
                {
                    slotVisual.color = new Color(1f, 1f, 1f, 0f); // 隐藏圆点
                    if (comp.BaseData.Type == ComponentType.Core) coreTrans = slotRect;

                    // 👇【核心修复】：将 slotIdx 传入，实现点击贴图即改装
                    RenderComponentInSlot(slotRect, comp, slotDef, slotIdx);
                }
            }

            Button btn = slotObj.AddComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked(slotIdx));
        }

        // 5. 第二遍循环：生成能量导线（确保在底盘上层，插槽下层）
        if (coreTrans != null && UIConduitPrefab != null)
        {
            foreach (var kvp in slotRects)
            {
                if (kvp.Value == coreTrans) continue;
                GameObject lineObj = Instantiate(UIConduitPrefab, chassisObj.transform);
                lineObj.transform.SetAsFirstSibling();
                var conduit = lineObj.GetComponent<MechEnergyConduit>();
                if (conduit != null) { conduit.Initialize(coreTrans, kvp.Value); activeConduitMap.Add(kvp.Key, conduit); }
            }
        }
    }

    private void RenderComponentInSlot(RectTransform slotRect, InstancedComponent comp, SlotDefinition slotDef, int slotIdx)
    {
        // 1. 创建转轴节点 (Hinge)
        GameObject compHingeObj = new GameObject($"UI_Hinge_{comp.BaseData.ComponentName}");
        compHingeObj.transform.SetParent(slotRect, false);

        // 👇【核心修复】：复合旋转逻辑
        // Hinge 是 slotRect 的子物体。slotRect 已经带了 MountAngle，
        // 这里的 localRotation 只需要应用组件自带的偏移 BaseRotationOffset 即可。
        compHingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);

        // 应用缩放
        compHingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

        // 2. 创建视觉展示节点
        GameObject compVisObj = new GameObject("Sprite_Visual");
        compVisObj.transform.SetParent(compHingeObj.transform, false);

        Image compImg = compVisObj.AddComponent<Image>();
        compImg.sprite = comp.BaseData.ComponentIcon;
        compImg.SetNativeSize();

        // 👇【核心修复】：重心锚点偏置
        // 注意：图片是在 Hinge 之下，其 anchoredPosition 必须反向应用 AnchorOffset
        compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * WorldToUIMultiplier;

        // 3. 开启贴图点击反馈
        compImg.raycastTarget = true;
        Button compBtn = compVisObj.AddComponent<Button>();
        compBtn.onClick.AddListener(() => OnSlotClicked(slotIdx));

        // DebugLog：验证旋转角度
        // Debug.Log($"<color=cyan>【装配视觉】</color> 插槽:[{slotDef.SlotName}] 底角:{slotDef.MountAngle} + 组件偏角:{comp.BaseData.BaseRotationOffset} = 总角度:{slotDef.MountAngle + comp.BaseData.BaseRotationOffset}");
    }

    public void OnComponentEquipped(int slotIndex)
    {
        if (activeConduitMap.ContainsKey(slotIndex)) activeConduitMap[slotIndex].TriggerPulse();
        if (GameFeelManager.Instance != null) GameFeelManager.Instance.RequestHitStop(0.05f);
        if (ScreenEffectManager.Instance != null) ScreenEffectManager.Instance.TriggerShake(0.1f, 0.1f);
        GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_Attach);
    }

    public void OnClickGhostChassis()
    {
        RightInventoryPanelUI.Instance.OpenForChassisSelection(() => PlayerInventoryManager.Instance.ChassisInventory, OnChassisSelectedFromInventory);
    }

    public void OnChassisSelectedFromInventory(InstancedChassis selectedChassis)
    {
        // 👇【核心修改】：从池子中抓取一个不重复的名字
        string mechName = PlayerInventoryManager.Instance.GetNextAvailableName();

        currentEditingProfile = new SavedUnitProfile(selectedChassis, mechName);
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
        if (existingIdx != -1) oldComp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == currentEditingProfile.EquippedComponentIDs[existingIdx]);

        if (!isCreatingNew && !PlayerInventoryManager.Instance.ValidateHPBeforeUnequip(currentEditingProfile, oldComp, selectedComp))
        {
            Debug.LogWarning("【车间警报】血量过低，禁止拆除生存组件！");
            return;
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
            OnComponentEquipped(slotIndex);
        }
        RefreshWorkshopState();
    }

    private bool ValidateUnitLegality(out string errorMessage)
    {
        errorMessage = ""; int coreCount = 0, mobilityCount = 0;
        foreach (string compID in currentEditingProfile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null)
            {
                if (comp.BaseData.Type == ComponentType.Core) coreCount++;
                if (comp.BaseData.Type == ComponentType.Movement) mobilityCount++;
            }
        }
        if (coreCount != 1) { errorMessage = "必须有且仅有 1 个核心引擎！"; return false; }
        if (mobilityCount < 1) { errorMessage = "缺乏移动模块，无法出击！"; return false; }
        return true;
    }

    public void SaveAndExitWorkshop()
    {
        if (currentEditingProfile == null) { CancelAndExitWorkshop(); return; }
        if (!ValidateUnitLegality(out string errorMsg)) { Debug.LogWarning(errorMsg); return; }
        currentEditingProfile.UnitName = UnitNameInput.text;
        PlayerInventoryManager.Instance.HangarUnits[targetHangarSlotIndex] = currentEditingProfile;
        ExitToHangar();
    }

    public void CancelAndExitWorkshop()
    {
        if (currentEditingProfile != null)
        {
            if (isCreatingNew)
            {
                PlayerInventoryManager.Instance.ReturnNameToPool(currentEditingProfile.UnitName);
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
                currentEditingProfile.SlotIndices = new List<int>(snapshot_SlotIndices);
                currentEditingProfile.EquippedComponentIDs = new List<string>(snapshot_EquippedComponentIDs);
                currentEditingProfile.CurrentHP = snapshot_HP;
                currentEditingProfile.CurrentAP = snapshot_AP;
            }
        }
        ExitToHangar();
    }

    private void ExitToHangar()
    {
        if (ItemDetailPanelUI.Instance != null) ItemDetailPanelUI.Instance.HidePanel();
        gameObject.SetActive(false);
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.gameObject.SetActive(true);
        HangarMenuUI.Instance.RefreshHangar();
        MusicManager.Instance?.SetImmersionMode(false);
    }
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}