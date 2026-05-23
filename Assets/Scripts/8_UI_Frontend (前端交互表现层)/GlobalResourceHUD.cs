using UnityEngine;
using TMPro;

public class GlobalResourceHUD : MonoBehaviour
{

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

    }
}