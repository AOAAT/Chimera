using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProductionTaskUIItem : MonoBehaviour
{
    public Image ItemIcon;
    public TMP_Text NameText;
    public TMP_Text TimeText;
    public Slider ProgressSlider;
    public GameObject PauseOverlay;
    public Image PlayPauseButtonIcon;

    public Sprite PlaySprite;
    public Sprite PauseSprite;

    private ProductionTask bindedTask;
    private System.Action onCancel;
    public ProductionTask BindedTask => bindedTask; // 🌟 暴露属性
    public void Initialize(ProductionTask task, System.Action cancelCallback)
    {
        bindedTask = task;
        onCancel = cancelCallback;

        if (NameText != null) NameText.text = task.ItemName;
        if (ItemIcon != null) ItemIcon.sprite = task.Icon;
    }

    private void Update()
    {
        if (bindedTask == null) return;

        if (ProgressSlider != null) ProgressSlider.value = bindedTask.NormalizedProgress;
        if (TimeText != null) TimeText.text = $"{bindedTask.RemainingTime:F1}s";

        // 🌟 视觉同步
        if (PauseOverlay != null) PauseOverlay.SetActive(bindedTask.IsPaused);
        if (PlayPauseButtonIcon != null)
            PlayPauseButtonIcon.sprite = bindedTask.IsPaused ? PlaySprite : PauseSprite;
    }

    // 🌟 诊断接口：点击暂停
    public void OnClickTogglePause()
    {
        if (bindedTask == null) return;

        bindedTask.IsPaused = !bindedTask.IsPaused;
        Debug.Log($"<color=yellow>【UI交互】</color> 任务 {bindedTask.ItemName} 暂停状态变为: {bindedTask.IsPaused}");
    }

    // 🌟 诊断接口：点击取消
    public void OnClickCancel()
    {
        Debug.Log($"<color=red>【UI交互】</color> 请求取消任务: {(bindedTask != null ? bindedTask.ItemName : "NULL")}");
        onCancel?.Invoke();
    }
}