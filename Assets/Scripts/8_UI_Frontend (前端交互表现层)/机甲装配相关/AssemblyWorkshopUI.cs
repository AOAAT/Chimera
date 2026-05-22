using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Unity.VisualScripting.Member;

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


    private AssemblerBuilding currentCallSource;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        gameObject.SetActive(false);
    }

    public void OpenEmptyWorkshop(int slotIndex, AssemblerBuilding source = null)
    {
        // 🌟 核心：将传入的建筑来源记录下来
        currentCallSource = source;

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

    // --- AssemblyWorkshopUI.cs ---
    private void RefreshWorkshopState()
    {
        if (RightInventoryPanel != null) RightInventoryPanel.SetActive(false);

        // 1. 如果还没选底盘，显示占位符并清空数值
        if (currentEditingProfile == null)
        {
            GhostChassisPrompt.SetActive(true);
            ChassisVisualRoot.gameObject.SetActive(false);
            HPText.text = "血量: -- / --";
            APText.text = "护甲: -- / --";
            if (BlockText != null) BlockText.text = "格挡: --";
            if (MassText != null) MassText.text = "质量: --";
            if (SpeedText != null) SpeedText.text = "移速: --";

            UnitNameInput.text = "等待选择底盘...";
            UnitNameInput.interactable = false;
        }
        else
        {
            GhostChassisPrompt.SetActive(false);
            ChassisVisualRoot.gameObject.SetActive(true);

            // ==========================================
            // 🚀 核心重构：调用积木引擎执行【装配模拟】
            // ==========================================

            // A. 准备当前插槽的零件快照 (必须严格对应插槽索引)
            int totalSockets = currentEditingProfile.ChassisData.Sockets.Count;
            InstancedComponent[] tempComps = new InstancedComponent[totalSockets];

            for (int i = 0; i < currentEditingProfile.SlotIndices.Count; i++)
            {
                int slotIdx = currentEditingProfile.SlotIndices[i];
                string instanceID = currentEditingProfile.EquippedComponentIDs[i];

                // 去库存里抓取这个实时的零件实例
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == instanceID);

                if (slotIdx < totalSockets)
                {
                    tempComps[slotIdx] = comp;
                }
            }

            // B. 呼叫后端解算器 (这会触发底盘的 OnAssembleActions 积木)
            RuntimeChimeraData calcData = new RuntimeChimeraData();
            calcData.Assemble(currentEditingProfile.ChassisData, tempComps);

            // C. 提取解算后的“真理数值”
            float maxHP = calcData.MaxHP;
            float maxAP = calcData.MaxAP;
            float totalBlock = calcData.GetGlobalStat(StatType.AddedBlock);
            float totalMass = calcData.TotalMass;
            float totalEngine = calcData.TotalEnginePower;

            // ==========================================

            // 2. 更新机甲档案的实时战损状态
            if (isCreatingNew)
            {
                currentEditingProfile.CurrentHP = maxHP;
                currentEditingProfile.CurrentAP = maxAP;
            }
            else
            {
                // 如果是改装旧机甲，保持之前的战损（通过 snapshot_DamageTaken 计算）
                // 如果加成后的上限变低了，强制收缩当前血量
                currentEditingProfile.CurrentHP = Mathf.Min(maxHP - snapshot_DamageTaken, maxHP);
                currentEditingProfile.CurrentAP = maxAP;
            }

            // 3. 计算最终物理表现 (移速)
            float speedMult = CombatSandbox.GetSpeed(1f);
            float finalSpeed = GameFormulas.CalcMoveSpeed(totalEngine, totalMass, speedMult);

            // 4. 刷新 UI 文字显示
            HPText.text = $"血量: {currentEditingProfile.CurrentHP:F0} / {maxHP:F0}";
            APText.text = $"护甲: {currentEditingProfile.CurrentAP:F0} / {maxAP:F0}";

            if (BlockText != null) BlockText.text = $"格挡: {totalBlock:F0}";
            if (MassText != null) MassText.text = $"质量: {totalMass:F1}t";
            if (SpeedText != null) SpeedText.text = $"移速: {finalSpeed:F1} m/s";

            // 5. 交互与视觉
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
        // 🌟 核心修复：直接从字典中获取底盘堆栈
        RightInventoryPanelUI.Instance.OpenForChassisSelection(
            () => PlayerInventoryManager.Instance.GetChassisStacks(),
            (selectedChassisStack) => {
                // 将 Stack 转换回逻辑需要的 Instanced 对象（这里需要手动解包一次）
                InstancedChassis tempChassis = new InstancedChassis(selectedChassisStack.BaseData);
                OnChassisSelectedFromInventory(tempChassis);
            }
        );
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

        // 🌟 核心修复：从堆栈中筛选出符合该插槽类型的零件
        RightInventoryPanelUI.Instance.OpenForComponentSelection(
            () => PlayerInventoryManager.Instance.GetAvailableStacks()
                  .Where(s => slotDef.AllowedTypes.Contains(s.BaseData.Type))
                  .ToList(),
            hasEquippedComp,
            (selectedStack) => {
                // 如果选了东西，转回旧逻辑需要的实例（OnComponentSelectedFromInventory 内部会处理消耗逻辑）
                if (selectedStack != null)
                {
                    InstancedComponent tempComp = new InstancedComponent(selectedStack.BaseData, selectedStack.Level);
                    OnComponentSelectedFromInventory(slotIndex, tempComp);
                }
                else
                {
                    OnComponentSelectedFromInventory(slotIndex, null); // 触发卸载
                }
            }
        );
    }

    private void OnComponentSelectedFromInventory(int slotIndex, InstancedComponent selectedComp)
    {
        // 1. 获取该插槽当前已装备的旧零件信息
        int existingIdx = currentEditingProfile.SlotIndices.IndexOf(slotIndex);
        InstancedComponent oldComp = null;
        if (existingIdx != -1)
        {
            string oldID = currentEditingProfile.EquippedComponentIDs[existingIdx];
            // 注意：这里需要根据旧 ID 找到之前的零件配置
            // 由于我们改了堆叠系统，这里我们通过 Snapshot 记录的数据来找
            oldComp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == oldID);
        }

        // 2. 逻辑分支：安装新零件 OR 纯卸载
        if (selectedComp != null)
        {
            // 尝试从仓库扣除实物
            bool success = PlayerInventoryManager.Instance.TryConsumeFromWarehouse(selectedComp.BaseData, selectedComp.CurrentLevel);

            if (!success)
            {
                Debug.LogWarning("【车间】库存不足，无法安装！");
                return;
            }

            // 扣除成功，如果原本有旧零件，将旧零件还给仓库
            if (oldComp != null)
            {
                PlayerInventoryManager.Instance.AddComponentToWarehouse(oldComp.BaseData, oldComp.CurrentLevel, 1);
                // 从当前机甲逻辑列表中移除旧数据
                currentEditingProfile.SlotIndices.RemoveAt(existingIdx);
                currentEditingProfile.EquippedComponentIDs.RemoveAt(existingIdx);
            }

            // 将新零件装上机甲 (这里我们生成一个临时的 InstanceID 作为标识)
            selectedComp.InstanceID = System.Guid.NewGuid().ToString();
            currentEditingProfile.SlotIndices.Add(slotIndex);
            currentEditingProfile.EquippedComponentIDs.Add(selectedComp.InstanceID);

            // 为了详情页能搜到，同步存入临时列表（仅限本次车间会话）
            PlayerInventoryManager.Instance.ComponentInventory.Add(selectedComp);

            OnComponentEquipped(slotIndex);
        }
        else
        {
            // 玩家点击了“卸载”
            if (oldComp != null)
            {
                PlayerInventoryManager.Instance.AddComponentToWarehouse(oldComp.BaseData, oldComp.CurrentLevel, 1);
                currentEditingProfile.SlotIndices.RemoveAt(existingIdx);
                currentEditingProfile.EquippedComponentIDs.RemoveAt(existingIdx);
            }
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

        // 验证合法性（必须有引擎和腿）
        if (!ValidateUnitLegality(out string errorMsg)) { Debug.LogWarning(errorMsg); return; }

        currentEditingProfile.UnitName = UnitNameInput.text;

        if (currentCallSource != null)
        {
            // --- 🌟 路径 A：建筑产出模式 ---
            // 告诉建筑：组装完成了，请在你的出口生出来
            currentCallSource.SpawnMech(currentEditingProfile);

            // 逻辑闭环：既然实体已经出来了，就不再进机库列表了
            ExitToHangarDirectly();
        }
        else
        {
            // --- 🌟 路径 B：调试/机库模式 ---
            if (targetHangarSlotIndex >= 0)
                PlayerInventoryManager.Instance.HangarUnits[targetHangarSlotIndex] = currentEditingProfile;

            ExitToHangar();
        }
    }
    private void ExitToHangarDirectly()
    {
        gameObject.SetActive(false);
        MusicManager.Instance?.SetImmersionMode(false);
    }
    public void CancelAndExitWorkshop()
    {
        if (currentEditingProfile != null)
        {
            // 1. 将当前机甲身上所有的零件实物还给仓库
            foreach (var compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null)
                {
                    PlayerInventoryManager.Instance.AddComponentToWarehouse(comp.BaseData, comp.CurrentLevel, 1);
                }
            }

            if (isCreatingNew)
            {
                // 如果是新建，直接名字还给池子，结束
                PlayerInventoryManager.Instance.ReturnNameToPool(currentEditingProfile.UnitName);
            }
            else
            {
                // 如果是改装，则需要根据 snapshot 还原原始状态，并从仓库重新扣除原始零件
                currentEditingProfile.SlotIndices = new List<int>(snapshot_SlotIndices);
                currentEditingProfile.EquippedComponentIDs = new List<string>(snapshot_EquippedComponentIDs);

                foreach (var originalCompID in snapshot_EquippedComponentIDs)
                {
                    var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == originalCompID);
                    if (comp != null)
                    {
                        PlayerInventoryManager.Instance.TryConsumeFromWarehouse(comp.BaseData, comp.CurrentLevel);
                    }
                }
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