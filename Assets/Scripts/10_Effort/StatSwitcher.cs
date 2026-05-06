using UnityEngine;
using TMPro;

public class StatSwitcher : MonoBehaviour
{
    [Header("=== 模式选择 ===")]
    public bool IsCompound = false; // 是否为合并显示行？

    [Tooltip("单一模式下选这个")]
    public StatType TargetStat;

    [Tooltip("复合模式下选这个ID：DamageRange, RangeInterval 等")]
    public string CompoundKey;

    private TMP_Text valueText;
    private GameObject labelObj;
    private TMP_Text labelText;

    public void Initialize()
    {
        // 防止重复初始化
        if (labelText != null) return;

        valueText = GetComponent<TMP_Text>();

        if (labelObj == null)
        {
            labelObj = new GameObject("Alt_Label");
            labelObj.transform.SetParent(this.transform, false);

            labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.fontSize = valueText.fontSize;
            labelText.alignment = valueText.alignment;
            labelText.font = valueText.font;
            labelText.color = new Color(0f, 0f, 0f, 255f); 
            // --- 🌟 核心逻辑：判定使用哪种翻译 ---
            if (IsCompound)
                labelText.text = StatTranslation.GetCompound(CompoundKey);
            else
                labelText.text = StatTranslation.Get(TargetStat);

            labelObj.SetActive(false);
        }
    }

    public void SetMode(bool showLabel)
    {
        if (valueText == null) return;
        valueText.enabled = !showLabel;
        if (labelObj != null) labelObj.SetActive(showLabel);
    }
}