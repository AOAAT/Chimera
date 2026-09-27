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
    private MechUnit2D targetWorldUnit; // 🌟 如果是改装，记录目标

    [Header("=== 快照备份系统 ===")]
    private List<int> snapshot_SlotIndices = new List<int>();
    private List<string> snapshot_EquippedComponentIDs = new List<string>();
    private float snapshot_HP;
    private float snapshot_AP;
    private float previewMaxHP;
    private float previewMaxAP;
    private int selectedSocket = -1;
    [SerializeField] private TMP_Text socketHint;

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
    [Tooltip("用于显示无法保存的具体原因；未绑定时仍会在 Console 输出警告。")]
    public TMP_Text ValidationMessageText;

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
        UIBackHandler.Attach(gameObject, CancelAndExitWorkshop);
        gameObject.SetActive(false);
    }

    public void OpenEmptyWorkshop(AssemblerBuilding source)
    {
        selectedSocket = -1;
        currentCallSource = source;
        targetWorldUnit = null;
        isCreatingNew = true;

        MusicManager.Instance?.SetImmersionMode(true);
        gameObject.SetActive(true);

        currentEditingProfile = null;
        snapshot_SlotIndices.Clear();
        snapshot_EquippedComponentIDs.Clear();
        previewMaxHP = 0f;
        previewMaxAP = 0f;

        RefreshWorkshopState();
    }

    public void OpenWorkshopWithUnit(MechUnit2D worldUnit)
    {
        selectedSocket = -1;
        targetWorldUnit = worldUnit;
        currentEditingProfile = worldUnit.GetProfile();
        isCreatingNew = false;
        currentCallSource = null;

        snapshot_SlotIndices = new List<int>(currentEditingProfile.SlotIndices);
        snapshot_EquippedComponentIDs = new List<string>(currentEditingProfile.EquippedComponentIDs);
        snapshot_HP = currentEditingProfile.CurrentHP;
        snapshot_AP = currentEditingProfile.CurrentAP;

        foreach (string componentID in snapshot_EquippedComponentIDs)
        {
            InstancedComponent component = PlayerInventoryManager.Instance.GetComponentInstance(componentID);
            if (component != null)
            {
                LogisticsManager.Instance?.ReclaimEquippedComponent(component.InstanceID);
                component.EquippedUnitID = currentEditingProfile.UnitID;
            }
        }

        gameObject.SetActive(true);
        RefreshWorkshopState();
    }

    // --- AssemblyWorkshopUI.cs ---
    private void RefreshWorkshopState()
    {
        PrepareEditorFeedback();
        ClearValidationMessage();
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
            if (PowerText != null) PowerText.text = "动力: --";

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
                var comp = PlayerInventoryManager.Instance.GetComponentInstance(instanceID);

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
            previewMaxHP = maxHP;
            previewMaxAP = maxAP;
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
            // 改装旧机甲时这里只做满状态预览，不提前改写正式战损。
            // 维修会在合法配置成功保存时才正式生效；取消则保持入场前数值。

            // 3. 计算最终物理表现 (移速)
            float speedMult = CombatSandbox.GetSpeed(1f);
            float finalSpeed = GameFormulas.CalcMoveSpeed(totalEngine, totalMass, speedMult);

            // 4. 刷新 UI 文字显示
            float displayedHP = isCreatingNew ? currentEditingProfile.CurrentHP : maxHP;
            float displayedAP = isCreatingNew ? currentEditingProfile.CurrentAP : maxAP;
            HPText.text = $"血量: {displayedHP:F0} / {maxHP:F0}";
            APText.text = $"护甲: {displayedAP:F0} / {maxAP:F0}";

            if (BlockText != null) BlockText.text = $"格挡: {totalBlock:F0}";
            if (MassText != null) MassText.text = $"质量: {totalMass:F1}t";
            if (PowerText != null) PowerText.text = $"动力: {totalEngine:0.##}";
            if (SpeedText != null) SpeedText.text = $"移速: {finalSpeed:F1} m/s";

            // 5. 交互与视觉
            UnitNameInput.text = currentEditingProfile.UnitName;
            UnitNameInput.interactable = true;

            RenderMechAndSockets();
        }
        UpdateSocketFeedback();
    }
    private void RenderMechAndSockets()
    {
        // 1. 彻底清理环境
        foreach (Transform child in ChassisVisualRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
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
        WorldPixelMetrics.SizePreview(chassisImg, WorldToUIMultiplier);
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
            slotRect.anchorMin = slotRect.anchorMax = chassisImg.rectTransform.pivot;
            slotRect.anchoredPosition = slotDef.LocalPosition * WorldToUIMultiplier;

            // 👇【核心修复】：插槽本身的旋转必须先应用
            slotRect.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);
            slotRect.sizeDelta = new Vector2(SlotButtonSize, SlotButtonSize);

            // 按钮视觉
            Image slotVisual = slotObj.AddComponent<Image>();
            slotVisual.sprite = CircularSlotSprite;
            slotVisual.color = ChimeraUITheme.Accent;
            slotVisual.raycastTarget = true;

            slotRects.Add(slotIdx, slotRect);

            // 检查并渲染已装组件
            int equippedIdx = currentEditingProfile.SlotIndices.IndexOf(slotIdx);
            if (equippedIdx != -1)
            {
                string compID = currentEditingProfile.EquippedComponentIDs[equippedIdx];
                var comp = PlayerInventoryManager.Instance.GetComponentInstance(compID);
                if (comp != null)
                {
                    slotVisual.color = new Color(1f, 1f, 1f, 0f); // 隐藏圆点
                    if (comp.BaseData.Type == ComponentType.Core) coreTrans = slotRect;

                    // 👇【核心修复】：将 slotIdx 传入，实现点击贴图即改装
                    RenderComponentInSlot(slotRect, comp, slotDef, slotIdx);
                }
            }

            var badge=slotObj.AddComponent<AssemblySocketBadge>();
            badge.Initialize(slotIdx,equippedIdx!=-1,CircularSlotSprite,HPText!=null?HPText.font:null);
            badge.SetSelected(slotIdx==selectedSocket);
            Button btn = slotObj.AddComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked(slotIdx));
            ItemHoverTarget.Bind(slotObj, () => {
                if (currentEditingProfile == null) return null;
                int installed = currentEditingProfile.SlotIndices.IndexOf(slotIdx);
                var item = installed < 0 ? null : PlayerInventoryManager.Instance.GetComponentInstance(currentEditingProfile.EquippedComponentIDs[installed]);
                return ItemHoverContent.Socket(slotDef, item);
            }, HPText != null ? HPText.font : null);
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
        WorldPixelMetrics.FitPreview(ChassisVisualRoot as RectTransform, scalerObj.transform, PreviewScale);
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
        WorldPixelMetrics.SizePreview(compImg, WorldToUIMultiplier);

        // 👇【核心修复】：重心锚点偏置
        // 注意：图片是在 Hinge 之下，其 anchoredPosition 必须反向应用 AnchorOffset
        compImg.rectTransform.anchoredPosition = -comp.BaseData.AnchorOffset * WorldToUIMultiplier;

        // 3. 开启贴图点击反馈
        compImg.raycastTarget = true;
        Button compBtn = compVisObj.AddComponent<Button>();
        compBtn.onClick.AddListener(() => OnSlotClicked(slotIdx));
        ItemHoverTarget.Bind(compVisObj, () => ItemHoverContent.Socket(slotDef, comp), HPText != null ? HPText.font : null);

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
        RightInventoryPanelUI.Instance.OpenForChassisSelection(
            () => PlayerInventoryManager.Instance.GetChassisStacks(),
            (stack) => OnChassisSelectedFromInventory(stack.Instance)

        );
    }
    public void OnChassisSelectedFromInventory(InstancedChassis selectedChassis)
    {
        // 🌟 核心修复：尝试从仓库扣除实物底盘
        bool success = PlayerInventoryManager.Instance.TryTakeChassis(selectedChassis, "assembly-reservation");

        if (!success)
        {
            Debug.LogWarning("【车间】底盘库存不足，无法开始组装！");
            return;
        }
        string mechName = "奇美拉-" + UnityEngine.Random.Range(100, 999);
        currentEditingProfile = new SavedUnitProfile(selectedChassis, mechName);

        // 标记底盘已占用（此 ID 仅用于本次组装追踪）
        selectedChassis.EquippedUnitID = currentEditingProfile.UnitID;

        isCreatingNew = true;

        // 关闭右侧面板
        if (RightInventoryPanelUI.Instance != null)
            RightInventoryPanelUI.Instance.gameObject.SetActive(false);

        RefreshWorkshopState();
    }
    private void OnSlotClicked(int slotIndex)
    {
        ItemHoverTooltip.Hide();
        selectedSocket=slotIndex;
        UpdateSocketFeedback();
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
                OnComponentSelectedFromInventory(slotIndex,
                    selectedStack != null ? selectedStack.Representative : null);
            },
            candidate => BuildCandidateHover(slotIndex,candidate)
        );
    }

    public void PrepareEditorFeedback()
    {
        if(socketHint!=null || CenterPreviewArea==null)return;
        socketHint=new GameObject("SocketSelectionHint",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        socketHint.transform.SetParent(CenterPreviewArea.transform,false);socketHint.font=HPText!=null?HPText.font:TMP_Settings.defaultFontAsset;
        var rect=socketHint.rectTransform;rect.anchorMin=new Vector2(.04f,.79f);rect.anchorMax=new Vector2(.96f,.90f);
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        socketHint.fontSize=17;socketHint.enableWordWrapping=true;socketHint.raycastTarget=false;
        socketHint.alignment=TextAlignmentOptions.TopLeft;
        UIThemeBinding.Bind(socketHint,UIThemeRole.SecondaryText);
        UpdateSocketFeedback();
    }
    private void UpdateSocketFeedback()
    {
        if(socketHint==null)return;
        string legend="浅色：空闲   绿色：已安装   铜色：当前节点";
        if(currentEditingProfile==null || selectedSocket<0 || selectedSocket>=currentEditingProfile.ChassisData.Sockets.Count)
            socketHint.text="点击节点选择组件\n"+legend;
        else
        {
            var slot=currentEditingProfile.ChassisData.Sockets[selectedSocket];
            string state=currentEditingProfile.SlotIndices.Contains(selectedSocket)?"已安装，可替换或卸下":"空闲";
            socketHint.text=$"当前 #{selectedSocket+1} · {slot.SlotName} · {state}\n"+legend;
        }
        foreach(var badge in ChassisVisualRoot.GetComponentsInChildren<AssemblySocketBadge>())badge.SetSelected(badge.SlotIndex==selectedSocket);
    }
    public ItemHoverContent BuildCandidateHover(int slotIndex,InstancedComponent candidate)
    {
        var profile=currentEditingProfile;
        if(profile==null || slotIndex<0 || slotIndex>=profile.ChassisData.Sockets.Count)return null;
        var slot=profile.ChassisData.Sockets[slotIndex];
        if(candidate!=null && !slot.AllowedTypes.Contains(candidate.BaseData.Type))return null;
        var installed=new InstancedComponent[profile.ChassisData.Sockets.Count];
        for(int i=0;i<profile.SlotIndices.Count;i++)
        {
            int index=profile.SlotIndices[i];
            if(index>=0&&index<installed.Length)installed[index]=PlayerInventoryManager.Instance.GetComponentInstance(profile.EquippedComponentIDs[i]);
        }
        var comparison=AssemblyComparison.Evaluate(profile.ChassisData,installed,slotIndex,candidate);
        var content=candidate!=null?ItemHoverContent.Component(candidate):new ItemHoverContent {Title="卸下当前组件",Subtitle=slot.SlotName,Body="卸下后组件返回仓库。"};
        string replacement=installed[slotIndex]!=null ? "当前组件："+installed[slotIndex].DisplayName+"\n" : "当前节点为空。\n";
        content.Body=$"<b>{(candidate==null?"卸下":"安装")}后的整机变化 · #{slotIndex+1}</b>\n"+replacement+
            comparison.Summary+"\n点击后应用；特殊效果见下方说明。\n\n"+content.Body;
        return content;
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
            oldComp = PlayerInventoryManager.Instance.GetComponentInstance(oldID);
        }

        // 2. 逻辑分支：安装新零件 OR 纯卸载
        if (selectedComp != null)
        {
            if (!PlayerInventoryManager.Instance.TryEquipComponent(selectedComp, currentEditingProfile.UnitID))
            {
                Debug.LogWarning("【车间】该组件已被其他机甲占用，无法安装。");
                return;
            }

            if (existingIdx != -1)
            {
                if (oldComp != null) PlayerInventoryManager.Instance.ReleaseComponent(oldComp);
                currentEditingProfile.SlotIndices.RemoveAt(existingIdx);
                currentEditingProfile.EquippedComponentIDs.RemoveAt(existingIdx);
            }

            currentEditingProfile.SlotIndices.Add(slotIndex);
            currentEditingProfile.EquippedComponentIDs.Add(selectedComp.InstanceID);

            OnComponentEquipped(slotIndex);
        }
        else
        {
            // 玩家点击了“卸载”
            if (existingIdx != -1)
            {
                if (oldComp != null) PlayerInventoryManager.Instance.ReleaseComponent(oldComp);
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
            var comp = PlayerInventoryManager.Instance.GetComponentInstance(compID);
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

    private void ShowValidationMessage(string message)
    {
        if (ValidationMessageText != null)
        {
            ValidationMessageText.text = message;
            ValidationMessageText.gameObject.SetActive(true);
        }

        GlobalAudioManager.Instance?.PlayUISound(UISoundType.Generic_Warning);
        UIFeedback.Show(message);
        Debug.LogWarning($"【装配校验】{message}");
    }

    private void ClearValidationMessage()
    {
        if (ValidationMessageText == null) return;
        ValidationMessageText.text = string.Empty;
        ValidationMessageText.gameObject.SetActive(false);
    }

    public void SaveAndExitWorkshop()
    {
        if (currentEditingProfile == null) return;

        if (!ValidateUnitLegality(out string errorMessage))
        {
            ShowValidationMessage(errorMessage);
            return;
        }

        // 车间兼具维修功能：只有合法配置成功保存时，才恢复至新上限。
        currentEditingProfile.CurrentHP = previewMaxHP;
        currentEditingProfile.CurrentAP = previewMaxAP;
        currentEditingProfile.UnitName = UnitNameInput.text;

        if (isCreatingNew && currentCallSource != null)
        {
            currentCallSource.SpawnMech(currentEditingProfile);
        }
        else if (!isCreatingNew && targetWorldUnit != null)
        {
            targetWorldUnit.ReAssemble();
        }

        ExitWorkshop();
    }
    // ==========================================
    // 🔙 核心逻辑：取消装配并回滚仓库
    // ==========================================
    public void CancelAndExitWorkshop()
    {
        if (currentEditingProfile != null)
        {
            // 释放本次预览中的占用状态；实例本身始终留在永久仓库中。
            foreach (var compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.GetComponentInstance(compID);
                if (comp != null) PlayerInventoryManager.Instance.ReleaseComponent(comp);
            }

            if (isCreatingNew)
            {
                // 2. 【新建模式】：归还底盘
                if (currentEditingProfile.ChassisData != null)
                {
                    PlayerInventoryManager.Instance.ReleaseChassis(currentEditingProfile);
                }
            }
            else
            {
                currentEditingProfile.SlotIndices = new List<int>(snapshot_SlotIndices);
                currentEditingProfile.EquippedComponentIDs = new List<string>(snapshot_EquippedComponentIDs);
                currentEditingProfile.CurrentHP = snapshot_HP;
                currentEditingProfile.CurrentAP = snapshot_AP;

                foreach (var originalCompID in snapshot_EquippedComponentIDs)
                {
                    var comp = PlayerInventoryManager.Instance.GetComponentInstance(originalCompID);
                    if (comp != null)
                    {
                        LogisticsManager.Instance?.ReclaimEquippedComponent(comp.InstanceID);
                        comp.EquippedUnitID = currentEditingProfile.UnitID;
                    }
                }
                PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();
            }
        }

        // 执行退出视觉逻辑
        ExitWorkshopInternal();
    }

    private void ExitWorkshopInternal()
    {
        // 1. 关闭详情页提示
        ItemHoverTooltip.Hide();

        // 2. 关闭车间界面
        gameObject.SetActive(false);

        // 3. 恢复沉浸式音效
        MusicManager.Instance?.SetImmersionMode(false);

        // 4. 🌟 关键：通知底部 HUD 清空状态，返回战场
        if (SelectionContextHUD.Instance != null)
        {
            SelectionContextHUD.Instance.Refresh(null);
        }

        Debug.Log("<color=orange>【车间】</color> 装配已取消，数据已回滚。");
    }
    private void ExitWorkshop()
    {
        gameObject.SetActive(false);
        MusicManager.Instance?.SetImmersionMode(false);
        // 通知场景 HUD 刷新。
        if (SelectionContextHUD.Instance != null) SelectionContextHUD.Instance.Refresh(null);
    }
  
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
