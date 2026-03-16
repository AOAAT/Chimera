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
    public TMP_Text StatsText;         // 属性加成
    public TMP_Text SpecialMechanicText; // 特殊机制
    public TMP_Text SocketsText;       // 接口种类 (仅底盘用)
    public GameObject SocketsGroup;    // 接口显示的整个父节点 (组件不显示这个)

    [Header("=== 顶部标签系统 (流式布局) ===")]
    public Transform TagsContainer;    // 挂载了 Horizontal Layout Group 的节点
    public GameObject TagPrefab;       // 预制体：一个带有 Text 和 Image 背景的小方块

    private void Awake()
    {
        Instance = this;
        HidePanel(); // 默认隐藏
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    // ==========================================
    // 显示【组件】详情
    // ==========================================
    public void ShowComponentDetail(ComponentDataSO data)
    {
        gameObject.SetActive(true);

        // 1. 基础信息
        IconImage.sprite = data.ComponentIcon;
        IconImage.SetNativeSize();
        NameText.text = data.ComponentName;
        DescriptionText.text = data.Description;

        // 2. 右侧动态数据
        SocketsGroup.SetActive(false); // 组件没有插槽，隐藏这一块
        SpecialMechanicText.text = data.SpecialMechanicDesc;

        // 拼装属性字符串
        string stats = "";
        foreach (var stat in data.BaseStats)
        {
            stats += $"[{TranslateStat(stat.StatID)}] : +{stat.Value}\n";
        }
        StatsText.text = string.IsNullOrEmpty(stats) ? "无基础属性加成" : stats;

        // 3. 生成顶部流式标签
        ClearTags();
        GenerateTag("组件", new Color(0.3f, 0.3f, 0.3f)); // 一级分类
        GenerateTag(TranslateComponentType(data.Type), new Color(0.2f, 0.5f, 0.8f)); // 二级分类
        foreach (var tag in data.Tags) GenerateTag(TranslateFactionTag(tag), GetFactionColor(tag)); // 三级分类
    }

    // ==========================================
    // 显示【底盘】详情
    // ==========================================
    public void ShowChassisDetail(ChassisDataSO data)
    {
        gameObject.SetActive(true);

        // 1. 基础信息
        IconImage.sprite = data.ChassisSprite;
        IconImage.SetNativeSize();
        NameText.text = data.ChassisName;
        DescriptionText.text = data.Description;

        // 2. 右侧动态数据
        SocketsGroup.SetActive(true); // 底盘必须显示插槽统计
        SpecialMechanicText.text = data.SpecialMechanicDesc;

        string stats = "";
        foreach (var stat in data.BaseStats) stats += $"[{TranslateStat(stat.StatID)}] : {stat.Value}\n";
        StatsText.text = string.IsNullOrEmpty(stats) ? "无基础属性加成" : stats;

        // 【极度核心】：智能翻译并合并万能接口
        SocketsText.text = GenerateSocketsReport(data.Sockets);

        // 3. 生成顶部流式标签 (底盘没有二级分类，直接接三级)
        ClearTags();
        GenerateTag("底盘", new Color(0.8f, 0.6f, 0.1f)); // 一级分类
        foreach (var tag in data.Tags) GenerateTag(TranslateFactionTag(tag), GetFactionColor(tag)); // 无缝接三级分类
    }

    // ==========================================
    // 万能接口智能统计算法
    // ==========================================
    private string GenerateSocketsReport(List<SlotDefinition> sockets)
    {
        Dictionary<string, int> socketCounts = new Dictionary<string, int>();

        foreach (var slot in sockets)
        {
            string labelName = "";
            int allowedCount = slot.AllowedTypes.Count;

            if (allowedCount == 0) continue;

            // 判断是否是终极万能槽 (假设当前有5种基本类型)
            if (allowedCount >= 4)
            {
                labelName = "万能接口";
            }
            else if (allowedCount == 1)
            {
                labelName = TranslateComponentType(slot.AllowedTypes[0]);
            }
            else
            {
                // 复合槽位，比如 "武器/辅助"
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

    // ==========================================
    // 标签 UI 生成器
    // ==========================================
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

    // ==========================================
    // 翻译字典 (把英文变中文)
    // ==========================================
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

    private string TranslateFactionTag(ComponentTag tag)
    {
        switch (tag)
        {
            case ComponentTag.Factory: return "工厂";
            case ComponentTag.Tech: return "科技";
            case ComponentTag.Flesh: return "血肉";
            default: return "无";
        }
    }

    private Color GetFactionColor(ComponentTag tag)
    {
        switch (tag)
        {
            case ComponentTag.Flesh: return new Color(0.6f, 0.1f, 0.1f); // 暗红
            case ComponentTag.Tech: return new Color(0.1f, 0.6f, 0.8f);  // 赛博蓝
            case ComponentTag.Factory: return new Color(0.7f, 0.4f, 0.1f); // 铁锈橙
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
            // TODO: 你可以在这里把所有词条补齐
            default: return stat.ToString();
        }
    }
}