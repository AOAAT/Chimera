using UnityEngine;
using TMPro;

public class GlobalResourceHUD : MonoBehaviour
{
    [Header("=== UI 文本绑定 ===")]
    public TMP_Text SanText;
    public TMP_Text MaterialText;
    public TMP_Text PowerText;
    public TMP_Text DepthText;

    private void Start()
    {
        if (GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.OnResourceChanged += UpdateHUD;

        // 👇【核心新增】：监听机库的变动，当玩家拖拽部署机甲时，电量显示能实时刷新！
        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged += UpdateHUD;

        UpdateHUD();
    }

    private void OnDestroy()
    {
        if (GlobalResourceManager.Instance != null)
            GlobalResourceManager.Instance.OnResourceChanged -= UpdateHUD;

        if (PlayerInventoryManager.Instance != null)
            PlayerInventoryManager.Instance.OnInventoryChanged -= UpdateHUD;
    }
    public void UpdateHUD()
    {
        if (GlobalResourceManager.Instance != null)
        {
            SanText.text = $"SAN值: {GlobalResourceManager.Instance.CurrentSAN} / {GlobalResourceManager.Instance.MaxSAN}";
            MaterialText.text = $"废料存量: {GlobalResourceManager.Instance.Materials}";

            // 电网负载监控
            int usedPower = GlobalResourceManager.Instance.GetTotalUsedPower();
            int maxPower = GlobalResourceManager.Instance.MaxPowerCapacity;

            string colorHex = usedPower > maxPower ? "#FF0000" : "#00FF00";
            PowerText.text = $"电网负载: <color={colorHex}>{usedPower}</color> / {maxPower}";
        }

        // 地图深度监控
        if (MapManager.Instance != null)
        {
            DepthText.text = $"当前层数: {MapManager.Instance.CurrentLayer}";
        }

        // --- 👇【新增】：增援进度监控 ---
        if (ReinforcementManager.Instance != null && CombatDirector.Instance != null && CombatDirector.Instance.IsCombatActive)
        {
            float currentProgress = ReinforcementManager.Instance.Progress;
            int currentPhase = ReinforcementManager.Instance.CurrentPhaseDisplay;

            // 可以在这里输出到日志，或者更新你的进度条 UI
            // 例如：Debug.Log($"【实时进度】阶段:{currentPhase} | 进度:{currentProgress:P0}");
        }
    }
}