// --- START OF FILE CombatCPBarUI.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatCPBarUI : MonoBehaviour
{
    [Header("=== 皇室战争级 CP 能量条 ===")]
    public Slider CPBarSlider;      // 滑动条
    public TMP_Text CPText;         // 显示 "3 / 10"
    public HealthBarGrid CPGrid;    // 直接复用我们之前写的格栅系统！

    private void Start()
    {
        // 战斗一开始，先用网格把能量条切成 1块1块 的！
        if (CPGrid != null && GlobalCPManager.Instance != null)
        {
            CPGrid.ValuePerGrid = 1f; // 每一刀切 1 点 CP
            CPGrid.UpdateGrid(GlobalCPManager.Instance.GetActualMaxCP());
        }
    }

    private void Update()
    {
        // 实时平滑地更新水池的上涨动画
        if (GlobalCPManager.Instance != null && CombatDirector.Instance != null && CombatDirector.Instance.IsCombatActive)
        {
            float current = GlobalCPManager.Instance.CurrentCP;
            float max = GlobalCPManager.Instance.GetActualMaxCP();

            if (CPBarSlider != null)
            {
                CPBarSlider.maxValue = max;
                CPBarSlider.value = current;
            }

            if (CPText != null)
            {
                CPText.text = $"{Mathf.FloorToInt(current)} / {max}";
            }
        }
    }
}