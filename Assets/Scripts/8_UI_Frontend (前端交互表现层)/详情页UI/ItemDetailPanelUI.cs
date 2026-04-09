// --- START OF FILE ItemDetailPanelUI.cs ---
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailPanelUI : MonoBehaviour
{
    public static ItemDetailPanelUI Instance;

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
    [Tooltip("数组长度必须为 4，分别对应 1、2、3、4 级的武器背景")]
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
        public TMP_Text TacticalRoleText;    // 战术定位
        public TMP_Text SpecialMechanicText; // 机制描述
        public TMP_Text ScrapValueText;      // 回收估值
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

    [Header("=== 辅助面板专属绑定 ===")]
    public CommonUIElements Support_Common;
    public TMP_Text Support_PowerCostText;

    [Header("=== 底盘面板专属绑定 ===")]
    public CommonUIElements Chassis_Common;
    public TMP_Text Chassis_HPText;
    public TMP_Text Chassis_APText;
    public TMP_Text Chassis_PowerCostText;

    [Tooltip("如果你画了固定的接口图标槽位，请依次把它们拖进来 (最多支持的插槽数)")]
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
        // 👇【核心防冲突】：如果自己被嵌套在“强化预览界面”里，就乖乖做个子面板，绝不抢占全局单例！
        if (GetComponentInParent<UpgradePreviewPanelUI>() != null)
        {
            return;
        }

        // 只有独立在外的那个全局 Tooltip 才有资格成为 Instance
        Instance = this;
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

    // ==========================================
    // 5. 顶部标签系统
    // ==========================================
    [Header("=== 顶部标签流式布局 ===")]
    public Transform TagsContainer;
    public GameObject TagPrefab;

    public Color TagColor_Tech = new Color(0.2f, 0.5f, 0.8f, 1f);
    public Color TagColor_Flesh = new Color(0.8f, 0.2f, 0.2f, 1f);
    public Color TagColor_Magic = new Color(0.6f, 0.2f, 0.8f, 1f);
    public Color TagColor_Default = new Color(0.3f, 0.3f, 0.3f, 1f);

    // ==========================================
    // A. 路由中心：组件详情分发
    // ==========================================
    public void ShowComponentDetail(InstancedComponent instance)
    {
        if (instance == null || instance.BaseData == null) return;

        var data = instance.BaseData;

        HidePanel();
        gameObject.SetActive(true);

        ComponentType type = data.Type;

        switch (type)
        {
            case ComponentType.Weapon:
                Panel_Weapon.SetActive(true);
                FillWeaponData(instance);
                break;
            case ComponentType.Core:
                Panel_Core.SetActive(true);
                FillCoreData(instance);
                break;
            case ComponentType.Movement:
                Panel_Movement.SetActive(true);
                FillMovementData(instance);
                break;
            case ComponentType.Support:
            case ComponentType.Factory: // 👇 兼容底层枚举。如果枚举里还有Factory，直接按辅助面板显示！
                Panel_Support.SetActive(true);
                FillSupportData(instance, Support_Common, Bg_Support, Support_PowerCostText);
                break;
        }

        RenderTags(data.MacroCategory, data.Type, data.BaseSubTags, instance.CurrentLevel);
    }

    // ==========================================
    // B. 路由中心：底盘详情专属
    // ==========================================
    public void ShowChassisDetail(ChassisDataSO data)
    {
        if (data == null) return;
        HidePanel();
        gameObject.SetActive(true);

        if (Panel_Chassis != null) Panel_Chassis.SetActive(true);

        FillCommonData(Chassis_Common, data.ChassisName, "", data.ChassisSprite, data.Description, data.ScrapValue, "载具底盘", data.SpecialMechanicDesc);

        if (Chassis_Common.BackgroundImage != null && data.DetailBackgroundSprite != null)
            Chassis_Common.BackgroundImage.sprite = data.DetailBackgroundSprite;

        if (Chassis_HPText != null) Chassis_HPText.text = $"+{GetStat(data.BaseStats, StatType.AddedHP)}";
        if (Chassis_APText != null) Chassis_APText.text = $"+{GetStat(data.BaseStats, StatType.AddedAP)}";
        if (Chassis_PowerCostText != null) Chassis_PowerCostText.text = $"{GetStat(data.BaseStats, StatType.PowerCost)}";

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
                    else if (allowedCount > 1)
                    {
                        Chassis_SocketIcons[i].sprite = GenericSocketIcon;
                    }
                    else
                    {
                        Chassis_SocketIcons[i].sprite = EmptySocketIcon;
                    }
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

    // ==========================================
    // 数据灌入逻辑
    // ==========================================

    private void FillWeaponData(InstancedComponent instance)
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

        if (Weapon_DamageText != null) Weapon_DamageText.text = (minDmg == maxDmg) ? $"{maxDmg}" : $"{minDmg} ~ {maxDmg}";
        if (Weapon_RangeText != null) Weapon_RangeText.text = (minRng == 0) ? $"{maxRng}" : $"{minRng} ~ {maxRng}";
        if (Weapon_AttackSpeedText != null) Weapon_AttackSpeedText.text = $"{atkSpeed}";
        if (Weapon_CritText != null) Weapon_CritText.text = $"+{crit:P0}";
        if (Weapon_PowerCostText != null) Weapon_PowerCostText.text = $"{pwr}";
    }

    private void FillCoreData(InstancedComponent instance)
    {
        var data = instance.BaseData;
        var lvData = data.GetLevelData(instance.CurrentLevel);

        FillCommonData(Core_Common, data.ComponentName, instance.CurrentLevel.ToString(), data.ComponentIcon, data.Description, lvData.ScrapValue, data.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Core_Common.BackgroundImage, Bg_Core, instance.CurrentLevel);

        if (Core_HPText != null) Core_HPText.text = $"+{GetStat(lvData.Stats, StatType.AddedHP)}";
        if (Core_APText != null) Core_APText.text = $"+{GetStat(lvData.Stats, StatType.AddedAP)}";
        if (Core_PowerCostText != null) Core_PowerCostText.text = $"{GetStat(lvData.Stats, StatType.PowerCost)}";
    }

    private void FillMovementData(InstancedComponent instance)
    {
        var data = instance.BaseData;
        var lvData = data.GetLevelData(instance.CurrentLevel);

        FillCommonData(Movement_Common, data.ComponentName, instance.CurrentLevel.ToString(), data.ComponentIcon, data.Description, lvData.ScrapValue, data.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(Movement_Common.BackgroundImage, Bg_Movement, instance.CurrentLevel);

        if (Movement_SpeedText != null) Movement_SpeedText.text = $"+{GetStat(lvData.Stats, StatType.EnginePower)}";
        if (Movement_HPText != null) Movement_HPText.text = $"+{GetStat(lvData.Stats, StatType.AddedHP)}";
        if (Movement_MassText != null) Movement_MassText.text = $"+{GetStat(lvData.Stats, StatType.AddedMass)}";
        if (Movement_PowerCostText != null) Movement_PowerCostText.text = $"{GetStat(lvData.Stats, StatType.PowerCost)}";
    }

    private void FillSupportData(InstancedComponent instance, CommonUIElements common, Sprite[] bgArray, TMP_Text powerText)
    {
        var data = instance.BaseData;
        var lvData = data.GetLevelData(instance.CurrentLevel);

        FillCommonData(common, data.ComponentName, instance.CurrentLevel.ToString(), data.ComponentIcon, data.Description, lvData.ScrapValue, data.TacticalRoleDesc, lvData.SpecialMechanicDesc);
        SetLevelBackground(common.BackgroundImage, bgArray, instance.CurrentLevel);

        if (powerText != null) powerText.text = $"{GetStat(lvData.Stats, StatType.PowerCost)}";
    }

    // ==========================================
    // 辅助工具方法
    // ==========================================

    // --- 请替换 ItemDetailPanelUI.cs 中的 FillCommonData 方法 ---

    private void FillCommonData(CommonUIElements ui, string name, string lv, Sprite icon, string desc, int scrap, string role, string mechanic)
    {
        // 1. 全局字体变黑 (如果你在 Prefab 里已经调黑了，这里就是双重保险)
        Color defaultTextColor = Color.black;

        if (ui.NameText != null)
        {
            ui.NameText.text = name;
            ui.NameText.color = defaultTextColor;
        }

        if (ui.DescriptionText != null)
        {
            ui.DescriptionText.text = desc;
            ui.DescriptionText.color = defaultTextColor;
        }

        if (ui.TacticalRoleText != null)
        {
            ui.TacticalRoleText.text = role;
            ui.TacticalRoleText.color = defaultTextColor;
        }

        if (ui.SpecialMechanicText != null)
        {
            ui.SpecialMechanicText.text = mechanic;
            ui.SpecialMechanicText.color = defaultTextColor;
        }

        // 注意：回收估值我们之前用的富文本 <color=#888888>，所以它不受影响，依然是灰色。
        if (ui.ScrapValueText != null) ui.ScrapValueText.text = $"{scrap}";

        if (ui.IconImage != null)
        {
            ui.IconImage.sprite = icon;
            ui.IconImage.SetNativeSize();
        }

        // 2. 👇【核心新增】：等级专属颜色判定！
        if (ui.LevelText != null)
        {
            if (string.IsNullOrEmpty(lv))
            {
                ui.LevelText.text = ""; // 底盘无等级
            }
            else
            {
                ui.LevelText.text = $"Lv.{lv}";

                // 根据传入的字符串解析出等级数字
                int levelNum = 1;
                int.TryParse(lv, out levelNum);

                // 经典稀有度颜色映射
                switch (levelNum)
                {
                    case 1: ui.LevelText.color = Color.white; break;             // 1级：普通白
                    case 2: ui.LevelText.color = new Color(0.2f, 0.6f, 1f, 1f); break; // 2级：稀有蓝
                    case 3: ui.LevelText.color = new Color(0.7f, 0.2f, 0.9f, 1f); break; // 3级：史诗紫
                    case 4: ui.LevelText.color = new Color(1f, 0.6f, 0f, 1f); break;   // 4级：传说橙
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

        if (compType.HasValue)
        {
            CreateTag(TranslateComponentType(compType.Value), TagColor_Default);
        }

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
            foreach (var tag in subTags)
            {
                CreateTag(TranslateSubTag(tag), macroColor);
            }
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
            case ComponentType.Factory: return "辅助插件"; // 兼容旧枚举
            default: return "未知组件";
        }
    }
}