using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ItemDetailPanelUI : MonoBehaviour
{
    public static ItemDetailPanelUI Instance;
    private bool isTooltipMode = false;
    private float openTime = 0f;
    private RectTransform targetAnchorRect;

    private CanvasGroup canvasGroup;

    [Header("=== Alt 键透视系统 ===")]
    private List<StatSwitcher> activeSwitchers = new List<StatSwitcher>();
    private bool isAltModeActive = false;

    [Header("=== 模式切换 (新增) ===")]
    private RectTransform fixedAnchor = null; // 如果不为 null，则锁定在此位置
    // ==========================================
    // 1. 核心面板容器
    // ==========================================
    [Header("=== 面板容器 ===")]
    public GameObject Panel_Weapon;
    public GameObject Panel_Core;
    public GameObject Panel_Movement;
    public GameObject Panel_Support;
    public GameObject Panel_Chassis;
    public GameObject Panel_Accessory;

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
        public TMP_Text SpecialMechanicText;
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

    public CommonUIElements Accessory_Common;

    public Transform TagsContainer;
    public GameObject TagPrefab;
    private Coroutine activeTagRoutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        Instance = this;
        isTooltipMode = true;

        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        HidePanel();
    }

    // ==========================================
    // ⚔️ 外部显示接口 (已移除 diffData 参数)
    // ==========================================

    public void ShowComponentDetail(InstancedComponent instance)
    {
        if (instance == null || instance.BaseData == null) return;

        openTime = Time.time;
        targetAnchorRect = null;

        Panel_Weapon?.SetActive(false);
        Panel_Core?.SetActive(false);
        Panel_Movement?.SetActive(false);
        Panel_Support?.SetActive(false);
        Panel_Chassis?.SetActive(false);
        Panel_Accessory?.SetActive(false);

        gameObject.SetActive(true);
        if (canvasGroup) canvasGroup.alpha = 1f;

        switch (instance.BaseData.Type)
        {
            case ComponentType.Weapon: Panel_Weapon?.SetActive(true); FillWeaponData(instance); break;
            case ComponentType.Core: Panel_Core?.SetActive(true); FillCoreData(instance); break;
            case ComponentType.Movement: Panel_Movement?.SetActive(true); FillMovementData(instance); break;
            case ComponentType.Support:
            case ComponentType.Factory: Panel_Support?.SetActive(true); FillSupportData(instance, Support_Common, Bg_Support, Support_PowerCostText); break;
        }

        RenderTags(instance.BaseData.MacroCategory, instance.BaseData.Type, instance.BaseData.BaseSubTags, instance.CurrentMark);
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
        Panel_Chassis?.SetActive(true);
        Panel_Accessory?.SetActive(false);

        gameObject.SetActive(true);
        if (canvasGroup) canvasGroup.alpha = 1f;

        FillCommonData(Chassis_Common, data.ChassisName, "", data.ChassisSprite, data.Description, "载具底盘", data.SpecialMechanicDesc);

        if (Chassis_HPText) Chassis_HPText.text = $"+{GetStat(data.BaseStats, StatType.AddedHP)}";
        if (Chassis_APText) Chassis_APText.text = $"+{GetStat(data.BaseStats, StatType.AddedAP)}";
        if (Chassis_MassText) Chassis_MassText.text = $"{GetStat(data.BaseStats, StatType.AddedMass)}t";
        if (Chassis_BlockText) Chassis_BlockText.text = $"+{GetStat(data.BaseStats, StatType.AddedBlock)}";

        RenderTags(data.MacroCategory, null, data.SubTags, 0);
        RefreshSwitchers();
    }

    public void ShowAccessoryDetail(InstancedAccessory instance)
    {
        if (instance == null || instance.BaseData == null) return;
        transform.SetAsLastSibling();
        openTime = Time.time;
        targetAnchorRect = null;

        Panel_Weapon?.SetActive(false);
        Panel_Core?.SetActive(false);
        Panel_Movement?.SetActive(false);
        Panel_Support?.SetActive(false);
        Panel_Chassis?.SetActive(false);
        Panel_Accessory?.SetActive(true);

        gameObject.SetActive(true);
        if (canvasGroup) canvasGroup.alpha = 1f;

        FillCommonData(Accessory_Common, instance.BaseData.AccessoryName, "", instance.BaseData.AccessoryIcon, instance.BaseData.Description, "逻辑配件", instance.BaseData.SpecialMechanicDesc);

        RenderTags(MacroCategory.Magic, null, instance.BaseData.RequiredTags, 0);
        RefreshSwitchers();
    }

    public void HidePanel()
    {
        // 1. 隐藏主容器
        if (isTooltipMode && canvasGroup != null) canvasGroup.alpha = 0f;

        // 2. 停掉正在运行的标签生成协程，防止隐藏后还在“蹦”新标签
        if (activeTagRoutine != null)
        {
            StopCoroutine(activeTagRoutine);
            activeTagRoutine = null;
        }

        // 🌟 3. 核心修复：立即清空标签容器里的所有旧标签
        if (TagsContainer != null)
        {
            foreach (Transform child in TagsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 4. 隐藏各分类子面板
        Panel_Weapon?.SetActive(false);
        Panel_Core?.SetActive(false);
        Panel_Movement?.SetActive(false);
        Panel_Support?.SetActive(false);
        Panel_Chassis?.SetActive(false);
        Panel_Accessory?.SetActive(false);

        targetAnchorRect = null;
        isAltModeActive = false;
    }

    // ==========================================
    // 🔍 内部驱动
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

    public void SetFixedAnchor(RectTransform anchor)
    {
        fixedAnchor = anchor;
        if (anchor != null)
        {
            // 开启显示，并强制设置为不跟随鼠标
            isTooltipMode = false;
            canvasGroup.alpha = 1f;

            // 立即对齐坐标
            RectTransform myRect = GetComponent<RectTransform>();
            myRect.pivot = new Vector2(0.5f, 0.5f); // 固定模式通常采用中心对齐
            myRect.position = anchor.position;
            myRect.sizeDelta = anchor.sizeDelta; // 适配锚点大小
        }
        else
        {
            isTooltipMode = true;
        }
    }


    private void LateUpdate()
    {
        if (fixedAnchor != null)
        {
            GetComponent<RectTransform>().position = fixedAnchor.position;
            return;
        }

        if (!isTooltipMode || canvasGroup.alpha < 0.1f) return;

        if (targetAnchorRect == null)
        {
            var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = Input.mousePosition };
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
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

        if (Input.GetMouseButtonDown(0) && (Time.time - openTime) > 0.2f) HidePanel();
    }

    private void FillCommonData(CommonUIElements ui, string n, string lv, Sprite ic, string d, string role, string mech)
    {
        if (ui.NameText) ui.NameText.text = n;
        if (ui.DescriptionText) ui.DescriptionText.text = d;
        if (ui.TacticalRoleText) ui.TacticalRoleText.text = role;
        if (ui.SpecialMechanicText) ui.SpecialMechanicText.text = mech;
        if (ui.IconImage) { ui.IconImage.sprite = ic; ui.IconImage.SetNativeSize(); }
        if (ui.LevelText) ui.LevelText.text = string.IsNullOrEmpty(lv) ? "" : $"Lv.{lv}";
    }

    private string FormatStat(float value, StatType statType)
    {
        if (statType == StatType.CriticalChance) return (value * 100f).ToString("F0") + "%";
        if (statType == StatType.AddedMass) return value.ToString("F1");
        return value.ToString("F0");
    }

    private void FillWeaponData(InstancedComponent instance)
    {
        var lvData = instance.BaseData.GetModelData(instance.CurrentMark);
        FillCommonData(Weapon_Common, instance.BaseData.ComponentName, instance.CurrentMark.ToString(), instance.BaseData.ComponentIcon, instance.BaseData.Description, instance.BaseData.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Weapon_Common.BackgroundImage, Bg_Weapon, instance.CurrentMark);

        if (Weapon_DamageText) Weapon_DamageText.text = $"{FormatStat(GetStat(lvData.Stats, StatType.MinDamage), StatType.MinDamage)} ~ {FormatStat(GetStat(lvData.Stats, StatType.MaxDamage), StatType.MaxDamage)}";
        if (Weapon_RangeText) Weapon_RangeText.text = $"{FormatStat(GetStat(lvData.Stats, StatType.MinRange), StatType.MinRange)} ~ {FormatStat(GetStat(lvData.Stats, StatType.MaxRange), StatType.MaxRange)}";
        if (Weapon_AttackSpeedText) Weapon_AttackSpeedText.text = FormatStat(GetStat(lvData.Stats, StatType.AttackSpeed), StatType.AttackSpeed);
        if (Weapon_CritText) Weapon_CritText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.CriticalChance), StatType.CriticalChance)}";
        
    }

    private void FillCoreData(InstancedComponent instance)
    {
        var lvData = instance.BaseData.GetModelData(instance.CurrentMark);
        FillCommonData(Core_Common, instance.BaseData.ComponentName, instance.CurrentMark.ToString(), instance.BaseData.ComponentIcon, instance.BaseData.Description, instance.BaseData.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Core_Common.BackgroundImage, Bg_Core, instance.CurrentMark);

        if (Core_HPText) Core_HPText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedHP), StatType.AddedHP)}";
        if (Core_APText) Core_APText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedAP), StatType.AddedAP)}";
        
    }

    private void FillMovementData(InstancedComponent instance)
    {
        var lvData = instance.BaseData.GetModelData(instance.CurrentMark);
        FillCommonData(Movement_Common, instance.BaseData.ComponentName, instance.CurrentMark.ToString(), instance.BaseData.ComponentIcon, instance.BaseData.Description, instance.BaseData.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Movement_Common.BackgroundImage, Bg_Movement, instance.CurrentMark);

        if (Movement_SpeedText) Movement_SpeedText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.EnginePower), StatType.EnginePower)}";
        if (Movement_HPText) Movement_HPText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedHP), StatType.AddedHP)}";
        if (Movement_APText) Movement_APText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedAP), StatType.AddedAP)}";
        if (Movement_MassText) Movement_MassText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedMass), StatType.AddedMass)}t";
        
    }

    private void FillSupportData(InstancedComponent instance, CommonUIElements common, Sprite[] bgArray, TMP_Text powerText)
    {
        var lvData = instance.BaseData.GetModelData(instance.CurrentMark);
        FillCommonData(common, instance.BaseData.ComponentName, instance.CurrentMark.ToString(), instance.BaseData.ComponentIcon, instance.BaseData.Description, instance.BaseData.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(common.BackgroundImage, bgArray, instance.CurrentMark);

        if (Support_HPText) Support_HPText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedHP), StatType.AddedHP)}";
        if (Support_APText) Support_APText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedAP), StatType.AddedAP)}";
        if (Support_BlockText) Support_BlockText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedBlock), StatType.AddedBlock)}";
        if (Support_MassText) Support_MassText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedMass), StatType.AddedMass)}t";
        
    }

    private float GetStat(List<StatEntry> stats, StatType type) { var e = stats.Find(x => x.StatID == type); return e != null ? e.Value : 0f; }
    private void SetLevelBackground(Image img, Sprite[] bgs, int lv) { if (img && bgs.Length >= 4) img.sprite = bgs[Mathf.Clamp(lv - 1, 0, 3)]; }

    private void RenderTags(MacroCategory macro, ComponentType? type, List<SubTag> subs, int lv)
    {
        if (!TagsContainer || !TagPrefab) return;
        if (activeTagRoutine != null) StopCoroutine(activeTagRoutine);
        foreach (Transform child in TagsContainer) Destroy(child.gameObject);

        List<(string text, Color bgColor)> tagsToCreate = new List<(string, Color)>();
        Color macroColor = macro == MacroCategory.Tech ? new Color(0.2f, 0.6f, 1f) : (macro == MacroCategory.Flesh ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.7f, 0.3f, 1f));
        tagsToCreate.Add((macro == MacroCategory.Tech ? "科技" : (macro == MacroCategory.Flesh ? "血肉" : "魔法"), macroColor));

        if (type.HasValue) tagsToCreate.Add((TranslateComponentType(type.Value), new Color(0.9f, 0.9f, 0.9f)));
        if (lv > 0) tagsToCreate.Add((lv == 4 ? "传说" : (lv == 3 ? "史诗" : (lv == 2 ? "稀有" : "普通")), lv == 4 ? new Color(1f, 0.8f, 0.4f) : new Color(0.85f, 0.85f, 0.85f)));
        if (subs != null) foreach (var sub in subs) tagsToCreate.Add((TranslateSubTag(sub), new Color(0.8f, 0.8f, 0.8f)));

        activeTagRoutine = StartCoroutine(StaggeredTagRoutine(tagsToCreate));
    }

    private IEnumerator StaggeredTagRoutine(List<(string text, Color bgColor)> dataList)
    {
        yield return null;
        foreach (var tagData in dataList)
        {
            GameObject tagObj = Instantiate(TagPrefab, TagsContainer);
            tagObj.GetComponentInChildren<TMP_Text>().text = tagData.text;
            tagObj.GetComponentInChildren<TMP_Text>().color = Color.black;
            tagObj.GetComponent<Image>().color = tagData.bgColor;
            yield return new WaitForSecondsRealtime(0.05f);
        }
        activeTagRoutine = null;
    }

    private string TranslateSubTag(SubTag tag)
    {
        switch (tag)
        {
            case SubTag.StrongAcid: return "强酸";
            case SubTag.Melee: return "近战";
            case SubTag.Ranged: return "远程";
            case SubTag.Charge: return "冲撞";
            case SubTag.Heavy: return "重型";
            case SubTag.Armor: return "装甲";
            case SubTag.Devotion: return "奉献";
            case SubTag.Smash: return "强击";
            case SubTag.Knockback: return "冲力";
            case SubTag.Wasteland: return "废土";
            case SubTag.Industry: return "工业";
            case SubTag.Firearms: return "枪械";
            case SubTag.Laboratory: return "实验室";
            case SubTag.Reload: return "装填";
            case SubTag.Kinetic: return "动能";
            case SubTag.Plasma: return "等离子";
            case SubTag.Head: return "头颅";
            case SubTag.Organs: return "内脏";
            case SubTag.Limbs: return "四肢";
            case SubTag.Parasite: return "寄生";
            case SubTag.Pain: return "痛苦";
            case SubTag.Artifact: return "遗物";
            case SubTag.Otherworld: return "异界";
            case SubTag.Mana: return "魔力";
            case SubTag.Chaos: return "混沌";
            case SubTag.Order: return "秩序";
            default: return tag.ToString();
        }
    }

    private string TranslateComponentType(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Core: return "核心";
            case ComponentType.Weapon: return "武器";
            case ComponentType.Movement: return "移动";
            case ComponentType.Support: return "辅助";
            default: return "插件";
        }
    }
}