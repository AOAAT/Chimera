// --- START OF FILE EventOptionUI.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventOptionUI : MonoBehaviour
{
    [Header("=== 核心组件 ===")]
    public Button ClickButton;
    public Image BackgroundImage;
    public TMP_Text CombinedText;

    // 缓存基础数据，方便随时刷新富文本
    private string rawTitle;
    private string rawFlavor;

    // 1. 初始化绑定基础数据和点击事件 (只执行一次)
    public void Initialize(string title, string flavor, UnityEngine.Events.UnityAction onClickAction)
    {
        rawTitle = title;
        rawFlavor = flavor;

        if (ClickButton != null)
        {
            ClickButton.onClick.RemoveAllListeners();
            if (onClickAction != null) ClickButton.onClick.AddListener(onClickAction);
        }
    }

    // 2. 动态刷新状态 (资源变动时可随时调用！)
    public void UpdateState(bool interactable, string warning)
    {
        // 拼接魔法
        string finalString = $"<b><size=110%>{rawTitle}</size></b>";

        if (!string.IsNullOrEmpty(rawFlavor))
            finalString += $"  <color=#A0A0A0><i>- {rawFlavor}</i></color>";

        if (!string.IsNullOrEmpty(warning))
        {
            string flatWarning = warning.Replace("\n", " , "); // 将多行警告拍扁成一行
            finalString += $"  <color=#FF3333>[ {flatWarning} ]</color>";
        }

        if (CombinedText != null)
        {
            CombinedText.text = finalString;
            CombinedText.alpha = interactable ? 1.0f : 0.6f;
        }

        if (ClickButton != null) ClickButton.interactable = interactable;

        if (BackgroundImage != null)
        {
            Color c = BackgroundImage.color;
            c.a = interactable ? 1.0f : 0.4f;
            BackgroundImage.color = c;
        }
    }
}