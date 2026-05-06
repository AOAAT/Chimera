using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 🌟【强制加固】：这行代码会让 Unity 自动在物体上挂载 CanvasGroup，解决报错
[RequireComponent(typeof(CanvasGroup))]
public class ItemDetailPanelUI : MonoBehaviour
{
    public static ItemDetailPanelUI Instance;
    private bool isTooltipMode = false;
    private float openTime = 0f;
    private RectTransform targetAnchorRect;

    // 🌟 内部引用
    private CanvasGroup canvasGroup;

    [Header("=== Alt 键透视系统 ===")]
    private List<StatSwitcher> activeSwitchers = new List<StatSwitcher>();
    private bool isAltModeActive = false;

    // ==========================================
    // 1. 核心面板容器
    // ==========================================
    [Header("=== 面板容器 ===")]
    public GameObject Panel_Weapon;
    public GameObject Panel_Core;
    public GameObject Panel_Movement;
    public GameObject Panel_Support;
    public GameObject Panel_Chassis;

    [Header("=== 动态背景库 ===")]
    public Sprite[] Bg_Weapon = new Sprite[4];
    public Sprite[] Bg_Core = new Sprite[4];
    public Sprite[] Bg_Movement = new Sprite[4];
    public Sprite[] Bg_Support = new Sprite[4];

    [System.Serializable]
    public struct CommonUIElements
    {
        public Image BackgroundImage;
        public Image IconImage;
        public TMP_Text NameText;
        public TMP_Text LevelText;
        public TMP_Text DescriptionText;
        public TMP_Text TacticalRoleText;
        public TMP_Text SpecialMechanicText; // 👈 机制文本引用
        public TMP_Text ScrapValueText;
    }

    [Header("=== 各属性文本绑定 ===")]
    public CommonUIElements Weapon_Common;
    public TMP_Text Weapon_DamageText;
    public TMP_Text Weapon_AttackSpeedText;
    public TMP_Text Weapon_RangeText;
    public TMP_Text Weapon_CritText;
    public TMP_Text Weapon_PowerCostText;

    public CommonUIElements Core_Common;
    public TMP_Text Core_HPText;
    public TMP_Text Core_APText;
    public TMP_Text Core_PowerCostText;

    public CommonUIElements Movement_Common;
    public TMP_Text Movement_SpeedText;
    public TMP_Text Movement_HPText;
    public TMP_Text Movement_APText;
    public TMP_Text Movement_MassText;
    public TMP_Text Movement_PowerCostText;

    public CommonUIElements Support_Common;
    public TMP_Text Support_PowerCostText;
    public TMP_Text Support_HPText;
    public TMP_Text Support_APText;
    public TMP_Text Support_BlockText;
    public TMP_Text Support_MassText;

    public CommonUIElements Chassis_Common;
    public TMP_Text Chassis_HPText;
    public TMP_Text Chassis_APText;
    public TMP_Text Chassis_PowerCostText;
    public TMP_Text Chassis_MassText;
    public TMP_Text Chassis_BlockText;

    public Transform TagsContainer;
    public GameObject TagPrefab;
    public Image[] Chassis_SocketIcons;

    private void Awake()
    {
        // 🌟 优先初始化 CanvasGroup
        canvasGroup = GetComponent<CanvasGroup>();

        if (GetComponentInParent<UpgradePreviewPanelUI>() != null)
        {
            isTooltipMode = false;
            if (canvasGroup) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; }
            return;
        }

        Instance = this;
        isTooltipMode = true;

        // 🌟 初始彻底隐藏且不挡鼠标
        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        HidePanel();
    }

    // ==========================================
    // ⚔️ 外部显示接口
    // ==========================================

    public void ShowComponentDetail(InstancedComponent instance, UpgradePreviewData diffData = null)
    {
        if (instance == null || instance.BaseData == null) return;

        openTime = Time.time;
        targetAnchorRect = null; // 重置锚点，触发 LateUpdate 重新搜索

        // 这种切换方式比 HidePanel 更快，不会闪烁
        Panel_Weapon?.SetActive(false);
        Panel_Core?.SetActive(false);
        Panel_Movement?.SetActive(false);
        Panel_Support?.SetActive(false);
        Panel_Chassis?.SetActive(false);

        gameObject.SetActive(true);
        if (canvasGroup) canvasGroup.alpha = 1f;

        switch (instance.BaseData.Type)
        {
            case ComponentType.Weapon: Panel_Weapon?.SetActive(true); FillWeaponData(instance, diffData); break;
            case ComponentType.Core: Panel_Core?.SetActive(true); FillCoreData(instance, diffData); break;
            case ComponentType.Movement: Panel_Movement?.SetActive(true); FillMovementData(instance, diffData); break;
            case ComponentType.Support:
            case ComponentType.Factory: Panel_Support?.SetActive(true); FillSupportData(instance, Support_Common, Bg_Support, Support_PowerCostText, diffData); break;
        }

        RenderTags(instance.BaseData.MacroCategory, instance.BaseData.Type, instance.BaseData.BaseSubTags, instance.CurrentLevel);
        RefreshSwitchers();
    }

    public void ShowChassisDetail(ChassisDataSO data)
    {
        if (data == null) return;
        openTime = Time.time;
        targetAnchorRect = null;

        Panel_Weapon?.SetActive(false);
        Panel_Core?.SetActive(false);
        Panel_Movement?.SetActive(false);
        Panel_Support?.SetActive(false);

        gameObject.SetActive(true);
        if (canvasGroup) canvasGroup.alpha = 1f;

        if (Panel_Chassis != null) Panel_Chassis.SetActive(true);
        FillCommonData(Chassis_Common, data.ChassisName, "", data.ChassisSprite, data.Description, data.ScrapValue, "载具底盘", data.SpecialMechanicDesc);

        // 数值填充
        if (Chassis_HPText) Chassis_HPText.text = $"+{GetStat(data.BaseStats, StatType.AddedHP)}";
        if (Chassis_APText) Chassis_APText.text = $"+{GetStat(data.BaseStats, StatType.AddedAP)}";
        if (Chassis_PowerCostText) Chassis_PowerCostText.text = $"{GetStat(data.BaseStats, StatType.PowerCost)}";
        if (Chassis_MassText) Chassis_MassText.text = $"{GetStat(data.BaseStats, StatType.AddedMass)}t";
        if (Chassis_BlockText) Chassis_BlockText.text = $"+{GetStat(data.BaseStats, StatType.AddedBlock)}";

        RenderTags(data.MacroCategory, null, data.SubTags, 0);
        RefreshSwitchers();
    }

    public void HidePanel()
    {
        if (isTooltipMode && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        Panel_Weapon?.SetActive(false);
        Panel_Core?.SetActive(false);
        Panel_Movement?.SetActive(false);
        Panel_Support?.SetActive(false);
        Panel_Chassis?.SetActive(false);

        targetAnchorRect = null;
        isAltModeActive = false;
    }

    // ==========================================
    // 🔍 内部驱动 (Alt 键与位置)
    // ==========================================

    private void RefreshSwitchers()
    {
        activeSwitchers.Clear();
        StatSwitcher[] found = GetComponentsInChildren<StatSwitcher>(true);
        foreach (var s in found)
        {
            s.Initialize();
            activeSwitchers.Add(s);
            s.SetMode(isAltModeActive);
        }
    }

    private void Update()
    {
        if (!gameObject.activeSelf || (isTooltipMode && canvasGroup.alpha < 0.1f)) return;

        bool currentAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (currentAlt != isAltModeActive)
        {
            isAltModeActive = currentAlt;
            foreach (var s in activeSwitchers) if (s != null) s.SetMode(isAltModeActive);
        }
    }

    private void LateUpdate()
    {
        if (!isTooltipMode || canvasGroup.alpha < 0.1f) return;

        // 🌟【位置追随加固】：只要没抓到锚点，就一直抓
        if (targetAnchorRect == null)
        {
            var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = Input.mousePosition };
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                // 排除自己，寻找底下的格子
                if (result.gameObject.transform.IsChildOf(this.transform)) continue;
                var slot = result.gameObject.GetComponentInParent<InventoryItemSlotUI>();
                if (slot != null) { targetAnchorRect = slot.GetComponent<RectTransform>(); break; }
            }
        }

        if (targetAnchorRect != null)
        {
            RectTransform myRect = GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            targetAnchorRect.GetWorldCorners(corners);
            Vector3 slotCenter = (corners[0] + corners[2]) / 2f;
            float slotHeight = corners[1].y - corners[0].y;

            float pivotX = slotCenter.x / Screen.width > 0.6f ? 1f : 0f;
            float pivotY = slotCenter.y / Screen.height > 0.6f ? 1f : 0f;
            myRect.pivot = new Vector2(pivotX, pivotY);

            float offsetY = pivotY == 0 ? (slotHeight / 2f + 15f) : -(slotHeight / 2f + 15f);
            myRect.position = new Vector3(slotCenter.x, slotCenter.y + offsetY, 0);
        }

        // 点击空白处关闭
        if (Input.GetMouseButtonDown(0) && (Time.time - openTime) > 0.2f) HidePanel();
    }

    // ==========================================
    // 🎨 样式与颜色设置
    // ==========================================

    private void FillCommonData(CommonUIElements ui, string n, string lv, Sprite ic, string d, int s, string role, string mech)
    {
        // ---------------------------------------------------------
        // 🎨【主程提示】：这里就是设置默认文本颜色的地方
        // ---------------------------------------------------------
        Color defaultTextColor = Color.black;

        if (ui.NameText) ui.NameText.text = n;
        if (ui.DescriptionText) ui.DescriptionText.text = d;
        if (ui.TacticalRoleText) ui.TacticalRoleText.text = role;

        // --- 👇 机制文本颜色设置 ---
        if (ui.SpecialMechanicText)
        {
            ui.SpecialMechanicText.text = mech;
            ui.SpecialMechanicText.color = defaultTextColor; // 👈 改这里！
        }

        if (ui.ScrapValueText) ui.ScrapValueText.text = s.ToString();
        if (ui.IconImage) { ui.IconImage.sprite = ic; ui.IconImage.SetNativeSize(); }
        if (ui.LevelText) ui.LevelText.text = string.IsNullOrEmpty(lv) ? "" : $"Lv.{lv}";
    }

    // (以下方法 FormatStat, FillWeaponData 等保持逻辑加固，请保留)
    private string FormatStat(float value, StatType statType, UpgradePreviewData diffData)
    {
        string baseStr = value.ToString("F0");
        if (statType == StatType.CriticalChance) baseStr = (value * 100f).ToString("F0") + "%";
        if (statType == StatType.AddedMass) baseStr = value.ToString("F1");
        if (diffData == null) return baseStr;
        int diffIdx = diffData.StatDiffs.FindIndex(d => d.StatID == statType);
        if (diffIdx != -1)
        {
            var diff = diffData.StatDiffs[diffIdx];
            if (diff.HasChanged)
            {
                string colorHex = diff.IsBuff ? "#00FF00" : "#FF4500";
                string sign = diff.Delta > 0 ? "+" : "";
                string deltaStr = (statType == StatType.CriticalChance) ? (diff.Delta * 100f).ToString("F0") + "%" : diff.Delta.ToString("F0");
                return $"{baseStr} <color={colorHex}>({sign}{deltaStr})</color>";
            }
        }
        return baseStr;
    }

    private void FillWeaponData(InstancedComponent instance, UpgradePreviewData diffData)
    {
        var lvData = instance.BaseData.GetLevelData(instance.CurrentLevel);
        FillCommonData(Weapon_Common, instance.BaseData.ComponentName, instance.CurrentLevel.ToString(), instance.BaseData.ComponentIcon, instance.BaseData.Description, lvData.ScrapValue, instance.BaseData.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Weapon_Common.BackgroundImage, Bg_Weapon, instance.CurrentLevel);
        if (Weapon_DamageText) Weapon_DamageText.text = $"{FormatStat(GetStat(lvData.Stats, StatType.MinDamage), StatType.MinDamage, diffData)} ~ {FormatStat(GetStat(lvData.Stats, StatType.MaxDamage), StatType.MaxDamage, diffData)}";
        if (Weapon_RangeText) Weapon_RangeText.text = $"{FormatStat(GetStat(lvData.Stats, StatType.MinRange), StatType.MinRange, diffData)} ~ {FormatStat(GetStat(lvData.Stats, StatType.MaxRange), StatType.MaxRange, diffData)}";
        if (Weapon_AttackSpeedText) Weapon_AttackSpeedText.text = FormatStat(GetStat(lvData.Stats, StatType.AttackSpeed), StatType.AttackSpeed, diffData);
        if (Weapon_CritText) Weapon_CritText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.CriticalChance), StatType.CriticalChance, diffData)}";
        if (Weapon_PowerCostText) Weapon_PowerCostText.text = FormatStat(GetStat(lvData.Stats, StatType.PowerCost), StatType.PowerCost, diffData);
    }

    private void FillCoreData(InstancedComponent instance, UpgradePreviewData diffData)
    {
        var lvData = instance.BaseData.GetLevelData(instance.CurrentLevel);
        FillCommonData(Core_Common, instance.BaseData.ComponentName, instance.CurrentLevel.ToString(), instance.BaseData.ComponentIcon, instance.BaseData.Description, lvData.ScrapValue, instance.BaseData.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Core_Common.BackgroundImage, Bg_Core, instance.CurrentLevel);
        if (Core_HPText) Core_HPText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedHP), StatType.AddedHP, diffData)}";
        if (Core_APText) Core_APText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedAP), StatType.AddedAP, diffData)}";
        if (Core_PowerCostText) Core_PowerCostText.text = FormatStat(GetStat(lvData.Stats, StatType.PowerCost), StatType.PowerCost, diffData);
    }

    private void FillMovementData(InstancedComponent instance, UpgradePreviewData diffData)
    {
        var lvData = instance.BaseData.GetLevelData(instance.CurrentLevel);
        FillCommonData(Movement_Common, instance.BaseData.ComponentName, instance.CurrentLevel.ToString(), instance.BaseData.ComponentIcon, instance.BaseData.Description, lvData.ScrapValue, instance.BaseData.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Movement_Common.BackgroundImage, Bg_Movement, instance.CurrentLevel);
        if (Movement_SpeedText) Movement_SpeedText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.EnginePower), StatType.EnginePower, diffData)}";
        if (Movement_HPText) Movement_HPText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedHP), StatType.AddedHP, diffData)}";
        if (Movement_APText) Movement_APText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedAP), StatType.AddedAP, diffData)}";
        if (Movement_MassText) Movement_MassText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedMass), StatType.AddedMass, diffData)}t";
        if (Movement_PowerCostText) Movement_PowerCostText.text = FormatStat(GetStat(lvData.Stats, StatType.PowerCost), StatType.PowerCost, diffData);
    }

    private void FillSupportData(InstancedComponent instance, CommonUIElements common, Sprite[] bgArray, TMP_Text powerText, UpgradePreviewData diffData)
    {
        var lvData = instance.BaseData.GetLevelData(instance.CurrentLevel);
        FillCommonData(common, instance.BaseData.ComponentName, instance.CurrentLevel.ToString(), instance.BaseData.ComponentIcon, instance.BaseData.Description, lvData.ScrapValue, instance.BaseData.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(common.BackgroundImage, bgArray, instance.CurrentLevel);
        if (Support_HPText) Support_HPText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedHP), StatType.AddedHP, diffData)}";
        if (Support_APText) Support_APText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedAP), StatType.AddedAP, diffData)}";
        if (Support_BlockText) Support_BlockText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedBlock), StatType.AddedBlock, diffData)}";
        if (Support_MassText) Support_MassText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedMass), StatType.AddedMass, diffData)}t";
        if (powerText) powerText.text = FormatStat(GetStat(lvData.Stats, StatType.PowerCost), StatType.PowerCost, diffData);
    }

    private float GetStat(List<StatEntry> stats, StatType type) { var e = stats.Find(x => x.StatID == type); return e != null ? e.Value : 0f; }
    private void SetLevelBackground(Image img, Sprite[] bgs, int lv) { if (img && bgs.Length >= 4) img.sprite = bgs[Mathf.Clamp(lv - 1, 0, 3)]; }
    private void RenderTags(MacroCategory macro, ComponentType? type, List<SubTag> subs, int lv) { if (!TagsContainer) return; foreach (Transform child in TagsContainer) Destroy(child.gameObject); }
}