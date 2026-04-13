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
        // 👇【核心修复】：删除了 IsCombatActive 的限制！
        // UI 就是个无情的显示器，只要 GlobalCPManager 活着，它就疯狂刷新显示真实数据！
        if (GlobalCPManager.Instance != null)
        {
            float current = GlobalCPManager.Instance.CurrentCP;
            float max = GlobalCPManager.Instance.GetActualMaxCP();

            if (CPBarSlider != null)
            {
                CPBarSlider.maxValue = max; // 动态适应最大值
                CPBarSlider.value = current; // 动态适应当前值
            }

            if (CPText != null)
            {
                CPText.text = $"{Mathf.FloorToInt(current)} / {max}";
            }
        }
    }
}