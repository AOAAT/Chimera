
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UISmartAudioTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("=== 配置对应的反馈音 === ")]
    public UISoundType HoverSound = UISoundType.Generic_Hover;
    public UISoundType ClickSound = UISoundType.Generic_Click;

    [Header("=== 开关 === ")]
    public bool UseHover = true;
    public bool UseClick = true;

    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 如果按钮是禁用状态，不播声音
        if (selectable != null && !selectable.interactable) return;

        if (UseHover && GlobalAudioManager.Instance != null)
            GlobalAudioManager.Instance.PlayUISound(HoverSound);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (selectable != null && !selectable.interactable) return;

        if (UseClick && GlobalAudioManager.Instance != null)
            GlobalAudioManager.Instance.PlayUISound(ClickSound);
    }
}