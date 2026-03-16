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

    // 👇👇👇 【新增核心系统】：时光倒流快照机！
    [Header("=== 快照备份系统 ===")]
    private List<int> snapshot_SlotIndices = new List<int>();
    private List<string> snapshot_EquippedComponentIDs = new List<string>();

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
    public TMP_InputField UnitNameInput; // 【新增】变成可输入文本框！

    [Header("=== 视觉与排版控制 ===")]
    public float WorldToUIMultiplier = 100f;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    // ==========================================
    // 入口 1：从机库点击 "+" 号进来 (新建)
    // ==========================================
    public void OpenEmptyWorkshop()
    {
        gameObject.SetActive(true);
        currentEditingProfile = null;
        isCreatingNew = true;

        // 新建机甲，清空快照
        snapshot_SlotIndices.Clear();
        snapshot_EquippedComponentIDs.Clear();

        RefreshWorkshopState();
    }

    // ==========================================
    // 入口 2：从机库点击已有单位进来 (修改)
    // ==========================================
    public void OpenWorkshopWithUnit(SavedUnitProfile unitProfile)
    {
        gameObject.SetActive(true);
        currentEditingProfile = unitProfile;
        isCreatingNew = false;

        // 👇【核心】：进门瞬间，拍下快照备份！
        snapshot_SlotIndices = new List<int>(unitProfile.SlotIndices);
        snapshot_EquippedComponentIDs = new List<string>(unitProfile.EquippedComponentIDs);

        RefreshWorkshopState();
    }

    // ==========================================
    // 核心心流：刷新车间表现 (保持不变)
    // ==========================================
    private void RefreshWorkshopState()
    {
        RightInventoryPanel.SetActive(false);

        if (currentEditingProfile == null)
        {
            GhostChassisPrompt.SetActive(true);
            ChassisVisualRoot.gameObject.SetActive(false);

            HPText.text = "HP: --";
            APText.text = "AP: --";
            PowerText.text = "耗电: --";
            UnitNameInput.text = "等待底盘接入...";
            UnitNameInput.interactable = false; // 还没装底盘时不让改名
        }
        else
        {
            GhostChassisPrompt.SetActive(false);
            ChassisVisualRoot.gameObject.SetActive(true);

            float totalHP = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedHP);
            float totalAP = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.AddedAP);
            float totalPower = PlayerInventoryManager.GetStatValue(currentEditingProfile.ChassisData.BaseStats, StatType.PowerCost);

            foreach (string compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null && comp.BaseData != null)
                {
                    totalHP += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.AddedHP);
                    totalAP += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.AddedAP);
                    totalPower += PlayerInventoryManager.GetStatValue(comp.BaseData.BaseStats, StatType.PowerCost);
                }
            }

            currentEditingProfile.CurrentHP = totalHP;
            currentEditingProfile.CurrentAP = totalAP;

            HPText.text = $"HP: {totalHP}";
            APText.text = $"AP: {totalAP}";
            PowerText.text = $"耗电: {totalPower}";
            UnitNameInput.text = currentEditingProfile.UnitName;
            UnitNameInput.interactable = true; // 允许玩家自由敲键盘改名！

            RenderMechAndSockets();
        }
    }
    public void OnClickGhostChassis()
    {
        // 把原本的死列表替换成 ()=> 的匿名获取函数！
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

        // 同样，传入 ()=> 筛选逻辑，而不是提前算好的列表！
        RightInventoryPanelUI.Instance.OpenForComponentSelection(
            () => PlayerInventoryManager.Instance.ComponentInventory.FindAll(c => !c.IsEquipped && slotDef.AllowedTypes.Contains(c.BaseData.Type)),
            hasEquippedComp,
            (selectedComp) => OnComponentSelectedFromInventory(slotIndex, selectedComp)
        );
    }

    private void OnComponentSelectedFromInventory(int slotIndex, InstancedComponent selectedComp)
    {
        int existingIdx = currentEditingProfile.SlotIndices.IndexOf(slotIndex);
        if (existingIdx != -1)
        {
            string oldCompID = currentEditingProfile.EquippedComponentIDs[existingIdx];
            var oldComp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == oldCompID);
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

    // ==========================================
    // 出厂安检：校验机甲合法性
    // ==========================================
    private bool ValidateUnitLegality(out string errorMessage)
    {
        errorMessage = "";
        int coreCount = 0;
        int mobilityCount = 0;

        // 遍历当前插槽上所有已安装的零件
        foreach (string compID in currentEditingProfile.EquippedComponentIDs)
        {
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp != null && comp.BaseData != null)
            {
                // ⚠️注意：如果你的枚举名字不叫 Core 或 Mobility，请把下面换成你实际的枚举名！
                if (comp.BaseData.Type == ComponentType.Core) coreCount++;
                if (comp.BaseData.Type == ComponentType.Movement) mobilityCount++;
            }
        }

        // 规则 1：必须有且仅有一个核心
        if (coreCount != 1)
        {
            errorMessage = $"【安检失败】机甲必须有且仅有 1 个核心引擎！当前数量: {coreCount}";
            return false;
        }

        // 规则 2：必须至少有一个移动组件
        if (mobilityCount < 1)
        {
            errorMessage = "【安检失败】机甲缺乏移动模块 (Mobility)，无法出击！";
            return false;
        }

        return true; // 安检通过！
    }

    // ==========================================
    // 终极按钮 1：保存并退出 (确定)
    // ==========================================
    // ==========================================
    // 终极按钮 1：保存并退出 (确定)
    // ==========================================
    public void SaveAndExitWorkshop()
    {
        if (currentEditingProfile == null)
        {
            CancelAndExitWorkshop(); return;
        }

        // 👇👇👇 【新增核心拦截：出厂安检！】 👇👇👇
        if (!ValidateUnitLegality(out string errorMsg))
        {
            Debug.LogWarning(errorMsg);
            // TODO: 未来你可以在这里弹出一个红色的 UI 警告框，把 errorMsg 显示给玩家看！
            return; // 直接 return，绝不执行后续的保存和退出逻辑！
        }
        // 👆👆👆 ============================== 👆👆👆

        // 如果安检通过，才允许落盘和改名！
        currentEditingProfile.UnitName = UnitNameInput.text;

        if (isCreatingNew)
        {
            PlayerInventoryManager.Instance.HangarUnits.Add(currentEditingProfile);
            Debug.Log($"【保存成功】新机甲 [{currentEditingProfile.UnitName}] 正式入驻机库！");
        }
        else
        {
            Debug.Log($"【保存成功】老机甲 [{currentEditingProfile.UnitName}] 改装覆盖完毕！");
        }

        ExitToHangar();
    }
    // ==========================================
    // 终极按钮 2：撤销并退出 (取消)
    // ==========================================
    public void CancelAndExitWorkshop()
    {
        if (currentEditingProfile == null)
        {
            Debug.Log("【撤销】空车间直接退出。");
            ExitToHangar(); return;
        }

        if (isCreatingNew)
        {
            // 撤销新建：把消耗的底盘和零件全洗白还给仓库！
            var chassis = PlayerInventoryManager.Instance.ChassisInventory.Find(c => c.InstanceID == currentEditingProfile.ChassisInstanceID);
            if (chassis != null) chassis.EquippedUnitID = string.Empty;

            foreach (var compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null) comp.EquippedUnitID = string.Empty;
            }
            Debug.Log("【撤销成功】新机甲生产取消，所有物资已退回仓库！");
        }
        else
        {
            // 撤销修改：触发“时光倒流”机制！
            // 1. 把现在身上的零件全扒下来！
            foreach (var compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null) comp.EquippedUnitID = string.Empty;
            }

            // 2. 覆盖旧数据
            currentEditingProfile.SlotIndices = new List<int>(snapshot_SlotIndices);
            currentEditingProfile.EquippedComponentIDs = new List<string>(snapshot_EquippedComponentIDs);

            // 3. 照着快照，重新把老零件焊回去！
            foreach (var compID in currentEditingProfile.EquippedComponentIDs)
            {
                var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
                if (comp != null) comp.EquippedUnitID = currentEditingProfile.UnitID;
            }
            Debug.Log("【撤销成功】改装已取消，机甲已恢复到进车间前的状态！");
        }

        ExitToHangar();
    }

    // ==========================================
    // 通用退出逻辑
    // ==========================================
    private void ExitToHangar()
    {
        currentEditingProfile = null;
        gameObject.SetActive(false);
        HangarMenuUI.Instance.gameObject.SetActive(true);
        HangarMenuUI.Instance.RefreshHangar();
    }
}