// --- START OF FILE ItemDetailPanelUI.cs ---
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailPanelUI : MonoBehaviour
{
    public static ItemDetailPanelUI Instance;

    [Header("=== 左侧通用区 ===")]
    public Image IconImage;
    public TMP_Text NameText;
    public TMP_Text DescriptionText;

    [Header("=== 右侧动态区 ===")]
    public TMP_Text StatsText;
    public TMP_Text SpecialMechanicText;
    public TMP_Text SocketsText;
    public GameObject SocketsGroup;

    [Header("=== 顶部标签系统 (流式布局) ===")]
    public Transform TagsContainer;
    public GameObject TagPrefab;

    private void Awake()
    {
        Instance = this;
        HidePanel();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    // 👇【核心修复 1】：参数从 ComponentDataSO 变成了 InstancedComponent 实体！
    public void ShowComponentDetail(InstancedComponent instance)
    {
        if (instance == null || instance.BaseData == null) return;

        gameObject.SetActive(true);
        var data = instance.BaseData;

        IconImage.sprite = data.ComponentIcon;
        IconImage.SetNativeSize();
        // 👇【体验升级】：在详情页标题也加上当前的星级！
        NameText.text = $"<color=#00FFFF>Lv.{instance.CurrentLevel}</color> {data.ComponentName}";
        DescriptionText.text = data.Description;



        SocketsGroup.SetActive(false);

        // 👇【核心修复 2】：精准读取实体当前的真实星级数据！
        var currentLvData = data.GetLevelData(instance.CurrentLevel);
        if (currentLvData != null)
        {
            DescriptionText.text += $"\n\n<color=#888888>[拆解估值] : {currentLvData.ScrapValue} 废料</color>";
        }
        SpecialMechanicText.text = currentLvData != null ? currentLvData.SpecialMechanicDesc : "无特殊机制";

        string stats = "";
        if (currentLvData != null)
        {
            foreach (var stat in currentLvData.Stats)
            {
                stats += $"[{TranslateStat(stat.StatID)}] : +{stat.Value}\n";
            }
        }
        StatsText.text = string.IsNullOrEmpty(stats) ? "无基础属性加成" : stats;

        ClearTags();
        GenerateTag("组件", new Color(0.3f, 0.3f, 0.3f));
        GenerateTag(TranslateComponentType(data.Type), new Color(0.2f, 0.5f, 0.8f));
        foreach (var tag in data.BaseSubTags) GenerateTag(TranslateFactionTag(tag), GetFactionColor(tag));
    }

    // 底盘没有等级，依然传图纸即可
    public void ShowChassisDetail(ChassisDataSO data)
    {
        if (data == null) return;
        gameObject.SetActive(true);

        IconImage.sprite = data.ChassisSprite;
        IconImage.SetNativeSize();
        NameText.text = data.ChassisName;
        DescriptionText.text = data.Description;

        DescriptionText.text += $"\n\n<color=#888888>[拆解估值] : {data.ScrapValue} 废料</color>";

        SocketsGroup.SetActive(true);
        SpecialMechanicText.text = data.SpecialMechanicDesc;

        string stats = "";
        foreach (var stat in data.BaseStats) stats += $"[{TranslateStat(stat.StatID)}] : {stat.Value}\n";
        StatsText.text = string.IsNullOrEmpty(stats) ? "无基础属性加成" : stats;

        SocketsText.text = GenerateSocketsReport(data.Sockets);

        ClearTags();
        GenerateTag("底盘", new Color(0.8f, 0.6f, 0.1f));
        foreach (var tag in data.SubTags) GenerateTag(TranslateFactionTag(tag), GetFactionColor(tag));
    }

    private string GenerateSocketsReport(List<SlotDefinition> sockets)
    {
        Dictionary<string, int> socketCounts = new Dictionary<string, int>();

        foreach (var slot in sockets)
        {
            string labelName = "";
            int allowedCount = slot.AllowedTypes.Count;

            if (allowedCount == 0) continue;

            if (allowedCount >= 4) labelName = "万能接口";
            else if (allowedCount == 1) labelName = TranslateComponentType(slot.AllowedTypes[0]);
            else
            {
                var names = slot.AllowedTypes.Select(t => TranslateComponentType(t));
                labelName = string.Join("/", names) + " (通用)";
            }

            if (socketCounts.ContainsKey(labelName)) socketCounts[labelName]++;
            else socketCounts[labelName] = 1;
        }

        string report = "";
        foreach (var kvp in socketCounts) report += $"{kvp.Value}x {kvp.Key}\n";
        return string.IsNullOrEmpty(report) ? "无可用接口" : report;
    }

    private void ClearTags()
    {
        foreach (Transform child in TagsContainer) Destroy(child.gameObject);
    }

    private void GenerateTag(string text, Color bgColor)
    {
        if (string.IsNullOrEmpty(text) || text == "无") return;
        GameObject tagObj = Instantiate(TagPrefab, TagsContainer);
        tagObj.GetComponent<Image>().color = bgColor;
        tagObj.GetComponentInChildren<TMP_Text>().text = text;
    }

    private string TranslateComponentType(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Core: return "核心";
            case ComponentType.Weapon: return "武器";
            case ComponentType.Support: return "辅助插件";
            case ComponentType.Factory: return "工厂模块";
            case ComponentType.Movement: return "移动装置";
            default: return type.ToString();
        }
    }

    private string TranslateFactionTag(SubTag tag)
    {
        switch (tag)
        {
            case SubTag.Mutation: return "突变";
            case SubTag.Parasite: return "寄生";
            case SubTag.Acid: return "强酸";
            case SubTag.Ballistic: return "实弹";
            case SubTag.Energy: return "能量";
            case SubTag.Shield: return "护盾";
            case SubTag.Curse: return "诅咒";
            case SubTag.Economy: return "经济";
            case SubTag.Heavy: return "重型";
            default: return tag.ToString();
        }
    }

    private Color GetFactionColor(SubTag tag)
    {
        switch (tag)
        {
            case SubTag.Mutation:
            case SubTag.Parasite:
            case SubTag.Acid: return new Color(0.6f, 0.1f, 0.1f);
            case SubTag.Ballistic:
            case SubTag.Energy:
            case SubTag.Shield: return new Color(0.1f, 0.6f, 0.8f);
            case SubTag.Curse:
            case SubTag.Summon: return new Color(0.5f, 0.1f, 0.8f);
            default: return Color.gray;
        }
    }

    private string TranslateStat(StatType stat)
    {
        switch (stat)
        {
            case StatType.AddedHP: return "耐久";
            case StatType.AddedAP: return "装甲";
            case StatType.PowerCost: return "耗电";
            case StatType.MaxDamage: return "最高伤害";
            case StatType.MinDamage: return "基础伤害";
            case StatType.AttackSpeed: return "攻击速度";
            case StatType.MaxRange: return "最大射程";
            default: return stat.ToString();
        }
    }
}