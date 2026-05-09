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

            // 👇【完美修复】：读取已用电量和总产能！
            int usedPower = GlobalResourceManager.Instance.GetTotalUsedPower();
            int maxPower = GlobalResourceManager.Instance.MaxPowerCapacity;

            // 如果超载了，把字变红报警！
            string colorHex = usedPower > maxPower ? "#FF0000" : "#00FF00";
            PowerText.text = $"电网负载: <color={colorHex}>{usedPower}</color> / {maxPower}";
        }

        if (MapManager.Instance != null)
        {
            DepthText.text = $"当前层数: {MapManager.Instance.CurrentLayer}";
        }
    }
}