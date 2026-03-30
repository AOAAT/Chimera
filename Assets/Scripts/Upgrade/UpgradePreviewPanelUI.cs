using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradePreviewPanelUI : MonoBehaviour
{
    public static UpgradePreviewPanelUI Instance;

    [Header("=== 双栏 UI 容器 (复用详情页布局) ===")]
    public DetailCardUI LeftCard_Current;
    public DetailCardUI RightCard_Next;

    [Header("=== 交互按钮 ===")]
    public Button ConfirmButton;
    public Button CancelButton;

    // 内部数据卡片结构体（对应你 ItemDetailPanelUI 里的排版）
    [System.Serializable]
    public struct DetailCardUI
    {
        public Image IconImage;
        public TMP_Text NameText;
        public TMP_Text LevelText;
        public TMP_Text StatsText;
        public TMP_Text SpecialMechanicText;
    }

    private UpgradePreviewData currentPreviewData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        gameObject.SetActive(false);
    }

    private void Start()
    {
        ConfirmButton.onClick.AddListener(OnConfirmClicked);
        CancelButton.onClick.AddListener(ClosePanel);
    }

    public void OpenPreview(UpgradePreviewData previewData)
    {
        currentPreviewData = previewData;
        gameObject.SetActive(true);

        // 渲染左侧：当前状态 (素色)
        RenderCard(LeftCard_Current, previewData.TargetItem.BaseData, previewData.CurrentLevel, null);

        // 渲染右侧：下一级状态 (带 Diff 绿字红字着色！)
        RenderCard(RightCard_Next, previewData.TargetItem.BaseData, previewData.NextLevel, previewData);
    }

    private void RenderCard(DetailCardUI card, ComponentDataSO blueprint, int level, UpgradePreviewData diffData)
    {
        var levelData = blueprint.GetLevelData(level);
        if (levelData == null) return;

        card.IconImage.sprite = blueprint.ComponentIcon;
        card.IconImage.SetNativeSize();
        card.NameText.text = blueprint.ComponentName;
        card.LevelText.text = $"Lv.{level}";

        // ==========================================
        // 核心亮点：Diff 属性红绿着色渲染
        // ==========================================
        string statsStr = "";
        foreach (var stat in levelData.Stats)
        {
            string statName = TranslateStat(stat.StatID);
            float val = stat.Value;

            if (diffData != null) // 如果是右侧面板，需要做 Diff 对比
            {
                var diff = diffData.StatDiffs.Find(d => d.StatID == stat.StatID);
                if (diff.HasChanged)
                {
                    string colorHex = diff.IsBuff ? "#00FF00" : "#FF4500"; // 绿增红减
                    string sign = diff.Delta > 0 ? "+" : "";
                    statsStr += $"[{statName}] : {val} <color={colorHex}>({sign}{diff.Delta})</color>\n";
                }
                else
                {
                    statsStr += $"[{statName}] : {val}\n";
                }
            }
            else // 左侧面板，直接白字
            {
                statsStr += $"[{statName}] : {val}\n";
            }
        }
        card.StatsText.text = string.IsNullOrEmpty(statsStr) ? "无基础属性加成" : statsStr;

        // ==========================================
        // 特殊机制金色高亮
        // ==========================================
        if (diffData != null && !string.IsNullOrEmpty(diffData.NewMechanicDesc))
        {
            card.SpecialMechanicText.text = $"<color=#FFD700>{levelData.SpecialMechanicDesc}</color>"; // 进化带来的新机制变金！
        }
        else
        {
            card.SpecialMechanicText.text = levelData.SpecialMechanicDesc;
        }
    }

    private void OnConfirmClicked()
    {
        if (currentPreviewData != null)
        {
            // 呼叫核心大脑执行吞噬升级！
            ComponentUpgradeManager.Instance.ConfirmAndExecuteUpgrade(currentPreviewData);
        }
        ClosePanel();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        currentPreviewData = null;
    }

    // (直接复用之前的翻译字典)
    private string TranslateStat(StatType stat)
    {
        switch (stat)
        {
            case StatType.AddedHP: return "耐久";
            case StatType.PowerCost: return "耗电";
            case StatType.MaxDamage: return "最高伤害";
            case StatType.AttackSpeed: return "攻击速度";
            default: return stat.ToString();
        }
    }
}