using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuffIconUI : MonoBehaviour
{
    public Image IconImage;
    public Image CooldownFill; // 👈 遮罩层 (Image Type: Filled)
    public TMP_Text StackText;

    private ActiveBuff bindedBuff;

    public void Initialize(ActiveBuff buff)
    {
        bindedBuff = buff;
        if (IconImage != null) IconImage.sprite = buff.Blueprint.BuffIcon;
        UpdateVisuals();
    }

    private void Update()
    {
        // 实时更新进度
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (bindedBuff == null || bindedBuff.Blueprint == null) return;

        // 1. 处理层数
        if (StackText != null)
            StackText.text = (bindedBuff.CurrentStacks > 1) ? bindedBuff.CurrentStacks.ToString() : "";

        // 2. 处理时长遮罩 (如果是永久 Buff，遮罩置为 0)
        if (CooldownFill != null)
        {
            if (bindedBuff.Blueprint.DurationType == BuffDurationType.Permanent)
            {
                CooldownFill.fillAmount = 0;
            }
            else
            {
                // 计算剩余比例：剩余时间 / 初始时间
                float ratio = bindedBuff.RemainingTime / bindedBuff.Blueprint.BaseDuration;
                CooldownFill.fillAmount = Mathf.Clamp01(ratio);
            }
        }
    }
}