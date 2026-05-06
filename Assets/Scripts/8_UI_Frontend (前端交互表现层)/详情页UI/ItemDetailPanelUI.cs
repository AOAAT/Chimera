// --- START OF FILE ItemDetailPanelUI.cs ---
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailPanelUI : MonoBehaviour
{
    public static ItemDetailPanelUI Instance;
    private bool isTooltipMode = false;
    private float openTime = 0f;
    private RectTransform targetAnchorRect;

    // ==========================================
    // 1. 核心控制器：面板显隐切换
    // ==========================================
    [Header("=== 面板开关控制 (Panel Toggles) ===")]
    public GameObject Panel_Weapon;
    public GameObject Panel_Core;
    public GameObject Panel_Movement;
    public GameObject Panel_Support;
    public GameObject Panel_Chassis;

    // ==========================================
    // 2. 动态背景库 (根据组件类型和 1~4 级切换)
    // ==========================================
    [Header("=== 动态背景库 (Backgrounds) ===")]
    public Sprite[] Bg_Weapon = new Sprite[4];
    public Sprite[] Bg_Core = new Sprite[4];
    public Sprite[] Bg_Movement = new Sprite[4];
    public Sprite[] Bg_Support = new Sprite[4];

    // ==========================================
    // 3. 通用图文信息 (所有面板都有的公共部分)
    // ==========================================
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
        public TMP_Text ScrapValueText;
    }

    [Header("=== 武器面板专属绑定 ===")]
    public CommonUIElements Weapon_Common;
    public TMP_Text Weapon_DamageText;
    public TMP_Text Weapon_AttackSpeedText;
    public TMP_Text Weapon_RangeText;
    public TMP_Text Weapon_CritText;
    public TMP_Text Weapon_PowerCostText;

    [Header("=== 核心面板专属绑定 ===")]
    public CommonUIElements Core_Common;
    public TMP_Text Core_HPText;
    public TMP_Text Core_APText;
    public TMP_Text Core_PowerCostText;

    [Header("=== 移动面板专属绑定 ===")]
    public CommonUIElements Movement_Common;
    public TMP_Text Movement_SpeedText;
    public TMP_Text Movement_HPText;
    public TMP_Text Movement_PowerCostText;
    public TMP_Text Movement_MassText;
    // 👇【新增】：移动组件的 AP 显示槽位
    public TMP_Text Movement_APText;

    [Header("=== 辅助面板专属绑定 ===")]
    public CommonUIElements Support_Common;
    public TMP_Text Support_PowerCostText;
    public TMP_Text Support_HPText;
    public TMP_Text Support_APText;
    public TMP_Text Support_BlockText;
    // 👇【新增】：辅助组件的质量槽位
    public TMP_Text Support_MassText;

    [Header("=== 底盘面板专属绑定 ===")]
    public CommonUIElements Chassis_Common;
    public TMP_Text Chassis_HPText;
    public TMP_Text Chassis_APText;
    public TMP_Text Chassis_PowerCostText;
    // 👇【新增】：底盘的重量和格挡槽位
    public TMP_Text Chassis_MassText;
    public TMP_Text Chassis_BlockText;

    [Tooltip("如果你画了固定的接口图标槽位，请依次把它们拖进来")]
    public Image[] Chassis_SocketIcons;

    [System.Serializable]
    public struct SocketIconMapping
    {
        public ComponentType Type;
        public Sprite Icon;
    }
    [Header("=== 字典：底盘接口小图标 ===")]
    public List<SocketIconMapping> SocketIconDict;
    public Sprite GenericSocketIcon;
    public Sprite EmptySocketIcon;

    private void Awake()
    {
        if (GetComponentInParent<UpgradePreviewPanelUI>() != null)
        {
            isTooltipMode = false;
            return;
        }

        Instance = this;
        isTooltipMode = true;

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        group.blocksRaycasts = false;
        group.interactable = false;

        HidePanel();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
        if (Panel_Weapon != null) Panel_Weapon.SetActive(false);
        if (Panel_Core != null) Panel_Core.SetActive(false);
        if (Panel_Movement != null) Panel_Movement.SetActive(false);
        if (Panel_Support != null) Panel_Support.SetActive(false);
        if (Panel_Chassis != null) Panel_Chassis.SetActive(false);
    }

    [Header("=== 顶部标签流式布局 ===")]
    public Transform TagsContainer;
    public GameObject TagPrefab;

    public Color TagColor_Tech = new Color(0.2f, 0.5f, 0.8f, 1f);
    public Color TagColor_Flesh = new Color(0.8f, 0.2f, 0.2f, 1f);
    public Color TagColor_Magic = new Color(0.6f, 0.2f, 0.8f, 1f);
    public Color TagColor_Default = new Color(0.3f, 0.3f, 0.3f, 1f);

    public void ShowComponentDetail(InstancedComponent instance, UpgradePreviewData diffData = null)
    {
        if (instance == null || instance.BaseData == null) return;
        openTime = Time.time;
        var data = instance.BaseData;

        HidePanel();
        gameObject.SetActive(true);

        switch (data.Type)
        {
            case ComponentType.Weapon:
                Panel_Weapon.SetActive(true);
                FillWeaponData(instance, diffData);
                break;
            case ComponentType.Core:
                Panel_Core.SetActive(true);
                FillCoreData(instance, diffData);
                break;
            case ComponentType.Movement:
                Panel_Movement.SetActive(true);
                FillMovementData(instance, diffData);
                break;
            case ComponentType.Support:
            case ComponentType.Factory:
                Panel_Support.SetActive(true);
                FillSupportData(instance, Support_Common, Bg_Support, Support_PowerCostText, diffData);
                break;
        }

        RenderTags(data.MacroCategory, data.Type, data.BaseSubTags, instance.CurrentLevel);
    }

    public void ShowChassisDetail(ChassisDataSO data)
    {
        if (data == null) return;
        openTime = Time.time;
        HidePanel();
        gameObject.SetActive(true);

        if (Panel_Chassis != null) Panel_Chassis.SetActive(true);

        FillCommonData(Chassis_Common, data.ChassisName, "", data.ChassisSprite, data.Description, data.ScrapValue, "载具底盘", data.SpecialMechanicDesc);

        if (Chassis_Common.BackgroundImage != null && data.DetailBackgroundSprite != null)
            Chassis_Common.BackgroundImage.sprite = data.DetailBackgroundSprite;

        // 👇【核心扩展】：底盘新增 Mass 和 Block
        float hp = GetStat(data.BaseStats, StatType.AddedHP);
        float ap = GetStat(data.BaseStats, StatType.AddedAP);
        float block = GetStat(data.BaseStats, StatType.AddedBlock);
        float mass = GetStat(data.BaseStats, StatType.AddedMass);
        float power = GetStat(data.BaseStats, StatType.PowerCost);

        if (Chassis_HPText != null) Chassis_HPText.text = $"+{hp}";
        if (Chassis_APText != null) Chassis_APText.text = $"+{ap}";
        if (Chassis_BlockText != null) Chassis_BlockText.text = $"+{block}";
        if (Chassis_MassText != null) Chassis_MassText.text = $"{mass}t"; // 加上吨的单位，更硬核
        if (Chassis_PowerCostText != null) Chassis_PowerCostText.text = $"{power}";

        if (Chassis_SocketIcons != null)
        {
            for (int i = 0; i < Chassis_SocketIcons.Length; i++)
            {
                if (i < data.Sockets.Count)
                {
                    var slot = data.Sockets[i];
                    int allowedCount = slot.AllowedTypes.Count;

                    if (allowedCount == 1)
                    {
                        var mapping = SocketIconDict.Find(x => x.Type == slot.AllowedTypes[0]);
                        Chassis_SocketIcons[i].sprite = mapping.Icon != null ? mapping.Icon : GenericSocketIcon;
                    }
                    else if (allowedCount > 1) Chassis_SocketIcons[i].sprite = GenericSocketIcon;
                    else Chassis_SocketIcons[i].sprite = EmptySocketIcon;

                    Chassis_SocketIcons[i].gameObject.SetActive(true);
                }
                else
                {
                    Chassis_SocketIcons[i].sprite = EmptySocketIcon;
                }
            }
        }
        RenderTags(data.MacroCategory, null, data.SubTags, 0);
    }

    private string FormatStat(float value, StatType statType, UpgradePreviewData diffData)
    {
        string baseStr = value.ToString();
        if (diffData == null) return baseStr;

        var diff = diffData.StatDiffs.Find(d => d.StatID == statType);
        if (diff.HasChanged)
        {
            string colorHex = diff.IsBuff ? "#00FF00" : "#FF4500";
            string sign = diff.Delta > 0 ? "+" : "";
            return $"{baseStr} <color={colorHex}>({sign}{diff.Delta})</color>";
        }
        return baseStr;
    }

    private void FillWeaponData(InstancedComponent instance, UpgradePreviewData diffData)
    {
        var data = instance.BaseData;
        var lvData = data.GetLevelData(instance.CurrentLevel);

        FillCommonData(Weapon_Common, data.ComponentName, instance.CurrentLevel.ToString(), data.ComponentIcon, data.Description, lvData.ScrapValue, data.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Weapon_Common.BackgroundImage, Bg_Weapon, instance.CurrentLevel);

        float minDmg = GetStat(lvData.Stats, StatType.MinDamage);
        float maxDmg = GetStat(lvData.Stats, StatType.MaxDamage);
        float minRng = GetStat(lvData.Stats, StatType.MinRange);
        float maxRng = GetStat(lvData.Stats, StatType.MaxRange);
        float atkSpeed = GetStat(lvData.Stats, StatType.AttackSpeed);
        float crit = GetStat(lvData.Stats, StatType.CriticalChance);
        float pwr = GetStat(lvData.Stats, StatType.PowerCost);

        if (Weapon_DamageText != null)
        {
            string minStr = FormatStat(minDmg, StatType.MinDamage, diffData);
            string maxStr = FormatStat(maxDmg, StatType.MaxDamage, diffData);
            Weapon_DamageText.text = (minDmg == maxDmg) ? $"{maxStr}" : $"{minStr} ~ {maxStr}";
        }
        if (Weapon_RangeText != null)
        {
            string minRngStr = FormatStat(minRng, StatType.MinRange, diffData);
            string maxRngStr = FormatStat(maxRng, StatType.MaxRange, diffData);
            Weapon_RangeText.text = (minRng == 0) ? $"{maxRngStr}" : $"{minRngStr} ~ {maxRngStr}";
        }
        if (Weapon_AttackSpeedText != null) Weapon_AttackSpeedText.text = FormatStat(atkSpeed, StatType.AttackSpeed, diffData);
        if (Weapon_PowerCostText != null) Weapon_PowerCostText.text = FormatStat(pwr, StatType.PowerCost, diffData);

        if (Weapon_CritText != null)
        {
            string critColorStr = "";
            if (diffData != null)
            {
                var diff = diffData.StatDiffs.Find(d => d.StatID == StatType.CriticalChance);
                if (diff.HasChanged)
                {
                    string colorHex = diff.IsBuff ? "#00FF00" : "#FF4500";
                    string sign = diff.Delta > 0 ? "+" : "";
                    critColorStr = $" <color={colorHex}>({sign}{diff.Delta:P0})</color>";
                }
            }
            Weapon_CritText.text = $"+{crit:P0}{critColorStr}";
        }
    }

    private void FillCoreData(InstancedComponent instance, UpgradePreviewData diffData)
    {
        var data = instance.BaseData;
        var lvData = data.GetLevelData(instance.CurrentLevel);

        FillCommonData(Core_Common, data.ComponentName, instance.CurrentLevel.ToString(), data.ComponentIcon, data.Description, lvData.ScrapValue, data.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Core_Common.BackgroundImage, Bg_Core, instance.CurrentLevel);

        float hp = GetStat(lvData.Stats, StatType.AddedHP);
        float ap = GetStat(lvData.Stats, StatType.AddedAP);
        // 为了保持核心面板整洁，格挡依然拼在 AP 后面
        float block = GetStat(lvData.Stats, StatType.AddedBlock);
        float power = GetStat(lvData.Stats, StatType.PowerCost);

        if (Core_HPText != null) Core_HPText.text = $"+{FormatStat(hp, StatType.AddedHP, diffData)}";
        if (Core_APText != null)
        {
            string apStr = $"+{FormatStat(ap, StatType.AddedAP, diffData)}";
            if (block > 0 || (diffData != null && diffData.StatDiffs.Exists(d => d.StatID == StatType.AddedBlock && d.HasChanged)))
            {
                string blockStr = FormatStat(block, StatType.AddedBlock, diffData);
                Core_APText.text = $"{apStr} <color=#FFD700>(格挡 {blockStr})</color>";
            }
            else
            {
                Core_APText.text = apStr;
            }
        }
        if (Core_PowerCostText != null) Core_PowerCostText.text = $"{FormatStat(power, StatType.PowerCost, diffData)}";
    }

    private void FillMovementData(InstancedComponent instance, UpgradePreviewData diffData)
    {
        var data = instance.BaseData;
        var lvData = data.GetLevelData(instance.CurrentLevel);

        FillCommonData(Movement_Common, data.ComponentName, instance.CurrentLevel.ToString(), data.ComponentIcon, data.Description, lvData.ScrapValue, data.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Movement_Common.BackgroundImage, Bg_Movement, instance.CurrentLevel);

        // 👇【核心扩展】：移动组件新增 AP 独立显示
        if (Movement_SpeedText != null) Movement_SpeedText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.EnginePower), StatType.EnginePower, diffData)}";
        if (Movement_HPText != null) Movement_HPText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedHP), StatType.AddedHP, diffData)}";
        if (Movement_APText != null) Movement_APText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedAP), StatType.AddedAP, diffData)}";
        if (Movement_MassText != null) Movement_MassText.text = $"+{FormatStat(GetStat(lvData.Stats, StatType.AddedMass), StatType.AddedMass, diffData)}t";
        if (Movement_PowerCostText != null) Movement_PowerCostText.text = $"{FormatStat(GetStat(lvData.Stats, StatType.PowerCost), StatType.PowerCost, diffData)}";
    }

    private void FillSupportData(InstancedComponent instance, CommonUIElements common, Sprite[] bgArray, TMP_Text powerText, UpgradePreviewData diffData)
    {
        var data = instance.BaseData;
        var lvData = data.GetLevelData(instance.CurrentLevel);

        // 获取所有防具和负面属性
        float hp = GetStat(lvData.Stats, StatType.AddedHP);
        float ap = GetStat(lvData.Stats, StatType.AddedAP);
        float block = GetStat(lvData.Stats, StatType.AddedBlock);
        float mass = GetStat(lvData.Stats, StatType.AddedMass); // 👇 获取质量

        // 格式化并灌入对应的 Text 组件
        if (Support_HPText != null) Support_HPText.text = $"+{FormatStat(hp, StatType.AddedHP, diffData)}";
        if (Support_APText != null) Support_APText.text = $"+{FormatStat(ap, StatType.AddedAP, diffData)}";
        if (Support_BlockText != null) Support_BlockText.text = $"+{FormatStat(block, StatType.AddedBlock, diffData)}";

        // 👇【核心新增】：显示质量加成，带上硬核的 't' (吨) 单位！
        if (Support_MassText != null) Support_MassText.text = $"+{FormatStat(mass, StatType.AddedMass, diffData)}t";

        string finalDesc = data.Description;

        if (block > 0 || (diffData != null && diffData.StatDiffs.Exists(d => d.StatID == StatType.AddedBlock && d.HasChanged)))
        {
            string blockStr = FormatStat(block, StatType.AddedBlock, diffData);
            finalDesc += $"\n\n<b><color=#FFD700>被动防弹：每次受击减免 {blockStr} 点伤害</color></b>";
        }

        FillCommonData(common, data.ComponentName, instance.CurrentLevel.ToString(), data.ComponentIcon, finalDesc, lvData.ScrapValue, data.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(common.BackgroundImage, bgArray, instance.CurrentLevel);

        if (powerText != null) powerText.text = $"{FormatStat(GetStat(lvData.Stats, StatType.PowerCost), StatType.PowerCost, diffData)}";
    }

    private void FillCommonData(CommonUIElements ui, string name, string lv, Sprite icon, string desc, int scrap, string role, string mechanic)
    {
        Color defaultTextColor = Color.black;

        if (ui.NameText != null) { ui.NameText.text = name; ui.NameText.color = defaultTextColor; }
        if (ui.DescriptionText != null) { ui.DescriptionText.text = desc; ui.DescriptionText.color = defaultTextColor; }
        if (ui.TacticalRoleText != null) { ui.TacticalRoleText.text = role; ui.TacticalRoleText.color = defaultTextColor; }
        if (ui.SpecialMechanicText != null) { ui.SpecialMechanicText.text = mechanic; ui.SpecialMechanicText.color = defaultTextColor; }
        if (ui.ScrapValueText != null) ui.ScrapValueText.text = $"{scrap}";

        if (ui.IconImage != null)
        {
            ui.IconImage.sprite = icon;
            ui.IconImage.SetNativeSize();
        }

        if (ui.LevelText != null)
        {
            if (string.IsNullOrEmpty(lv))
            {
                ui.LevelText.text = "";
            }
            else
            {
                ui.LevelText.text = $"Lv.{lv}";
                int levelNum = 1;
                int.TryParse(lv, out levelNum);

                switch (levelNum)
                {
                    case 1: ui.LevelText.color = Color.white; break;
                    case 2: ui.LevelText.color = new Color(0.2f, 0.6f, 1f, 1f); break;
                    case 3: ui.LevelText.color = new Color(0.7f, 0.2f, 0.9f, 1f); break;
                    case 4: ui.LevelText.color = new Color(1f, 0.6f, 0f, 1f); break;
                    default: ui.LevelText.color = Color.white; break;
                }
            }
        }
    }

    private void SetLevelBackground(Image img, Sprite[] bgArray, int level)
    {
        if (img == null || bgArray == null || bgArray.Length < 4) return;
        int bgIndex = Mathf.Clamp(level - 1, 0, 3);
        img.sprite = bgArray[bgIndex];
    }

    private float GetStat(List<StatEntry> stats, StatType targetStat)
    {
        if (stats == null) return 0f;
        var stat = stats.Find(s => s.StatID == targetStat);
        return stat != null ? stat.Value : 0f;
    }

    private void RenderTags(MacroCategory macro, ComponentType? compType, List<SubTag> subTags, int level)
    {
        if (TagsContainer == null || TagPrefab == null) return;
        foreach (Transform child in TagsContainer) Destroy(child.gameObject);

        string baseTag = compType.HasValue ? "机甲组件" : "载具底盘";
        CreateTag(baseTag, TagColor_Default);

        if (compType.HasValue) CreateTag(TranslateComponentType(compType.Value), TagColor_Default);

        Color macroColor = TagColor_Default;
        string macroName = "";
        switch (macro)
        {
            case MacroCategory.Tech: macroName = "科技"; macroColor = TagColor_Tech; break;
            case MacroCategory.Flesh: macroName = "血肉"; macroColor = TagColor_Flesh; break;
            case MacroCategory.Magic: macroName = "魔法"; macroColor = TagColor_Magic; break;
        }
        CreateTag(macroName, macroColor);

        if (subTags != null)
        {
            foreach (var tag in subTags) CreateTag(TranslateSubTag(tag), macroColor);
        }
    }

    private void CreateTag(string text, Color bgColor)
    {
        if (string.IsNullOrEmpty(text)) return;
        GameObject tagObj = Instantiate(TagPrefab, TagsContainer);
        Image bgImg = tagObj.GetComponent<Image>();
        TMP_Text txt = tagObj.GetComponentInChildren<TMP_Text>();

        if (bgImg != null) bgImg.color = bgColor;
        if (txt != null) txt.text = text;
    }

    private string TranslateSubTag(SubTag tag)
    {
        switch (tag)
        {
            case SubTag.Ballistic: return "实弹";
            case SubTag.Energy: return "能量";
            case SubTag.Shield: return "护盾";
            case SubTag.Drone: return "无人机";
            case SubTag.Mutation: return "突变";
            case SubTag.Parasite: return "寄生";
            case SubTag.Acid: return "强酸";
            case SubTag.Biomass: return "生物质";
            case SubTag.Curse: return "诅咒";
            case SubTag.Summon: return "召唤";
            case SubTag.Economy: return "经济";
            case SubTag.Heavy: return "重型";
            default: return tag.ToString();
        }
    }

    private string TranslateComponentType(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Core: return "核心模块";
            case ComponentType.Weapon: return "武器系统";
            case ComponentType.Support: return "辅助插件";
            case ComponentType.Movement: return "移动装置";
            case ComponentType.Factory: return "辅助插件";
            default: return "未知组件";
        }
    }

    private void LateUpdate()
    {
        if (!isTooltipMode || !gameObject.activeSelf) return;

        if (Time.time - openTime < 0.05f)
        {
            UnityEngine.EventSystems.PointerEventData pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = Input.mousePosition
            };

            List<UnityEngine.EventSystems.RaycastResult> results = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

            targetAnchorRect = null;
            foreach (var result in results)
            {
                if (result.gameObject.GetComponentInParent<InventoryItemSlotUI>() != null)
                {
                    targetAnchorRect = result.gameObject.GetComponentInParent<RectTransform>();
                    break;
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

                float offsetX = pivotX == 0 ? 0f : 0f;
                float offsetY = pivotY == 0 ? (slotHeight / 2f + 10f) : -(slotHeight / 2f + 10f);

                myRect.position = new Vector3(slotCenter.x + offsetX, slotCenter.y + offsetY, 0);
            }
        }

        if (Input.GetMouseButtonDown(0) && (Time.time - openTime) > 0.1f)
        {
            HidePanel();
        }
    }

}