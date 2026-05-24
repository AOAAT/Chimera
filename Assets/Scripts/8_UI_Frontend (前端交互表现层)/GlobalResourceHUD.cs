using UnityEngine;
using TMPro;

public class GlobalResourceHUD : MonoBehaviour
{
    public TMP_Text ScrapText;
    public TMP_Text BiomassText;
    public TMP_Text ManaStoneText;

    private void Start()
    {
        if (GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.OnResourceChanged += RefreshUI;

        RefreshUI();
    }

    private void RefreshUI()
    {
        var mgr = GlobalResourceManager.Instance;
        if (mgr == null) return;

        if (ScrapText) ScrapText.text = $"废料: {mgr.CurrentScrap:F0}";
        if (BiomassText) BiomassText.text = $"生物质: {mgr.CurrentBiomass:F0}";
        if (ManaStoneText) ManaStoneText.text = $"魔石: {mgr.CurrentManaStone:F0}";
    }
}