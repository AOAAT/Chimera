using UnityEngine;
using TMPro;

public class GlobalResourceHUD : MonoBehaviour
{
    public TMP_Text ScrapText;
    public TMP_Text BiomassText;
    public TMP_Text ManaStoneText;
    public TMP_Text PopulationText;
    private void Start()
    {
        if (GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.OnResourceChanged += RefreshUI;
        if (PopulationManager.Instance != null)
            PopulationManager.Instance.OnPopulationChanged += RefreshUI;

        RefreshUI();
    }

    private void RefreshUI()
    {
        var mgr = GlobalResourceManager.Instance;
        if (mgr == null) return;

        if (ScrapText) ScrapText.text = $"废料: {mgr.CurrentScrap:F0}";
        if (BiomassText) BiomassText.text = $"生物质: {mgr.CurrentBiomass:F0}";
        if (ManaStoneText) ManaStoneText.text = $"魔石: {mgr.CurrentManaStone:F0}";

        if (PopulationText != null && PopulationManager.Instance != null)
        {
            int current = PopulationManager.Instance.TotalResidents.Count;
            int max = PopulationManager.Instance.GetCurrentMaxCapacity();

            PopulationText.text = $"人口: {current}/{max}";

            // 满员变色警告
            PopulationText.color = (current >= max) ? Color.red : Color.white;
        }
    }
}