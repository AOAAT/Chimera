using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Overlay scrollbar; fades without resizing the viewport.</summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class ScrollActivityFade : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ScrollRect Scroll;
    private CanvasGroup group;
    private float lastActivity;
    private bool hovering;
    private Vector2 lastPosition;
    private void Awake() { group = GetComponent<CanvasGroup>(); }
    private void OnEnable()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = 0;
        if (Scroll != null) Scroll.onValueChanged.AddListener(OnScroll);
    }
    private void OnDisable() { if (Scroll != null) Scroll.onValueChanged.RemoveListener(OnScroll); }
    private void OnScroll(Vector2 _) { lastActivity = Time.unscaledTime; }
    public void OnPointerEnter(PointerEventData _) { hovering = true; }
    public void OnPointerExit(PointerEventData _) { hovering = false; }
    private void Update()
    {
        if (Scroll != null && Scroll.content.anchoredPosition != lastPosition)
        { lastPosition = Scroll.content.anchoredPosition; lastActivity = Time.unscaledTime; }
        bool overflow = Scroll != null && Scroll.content.rect.height > Scroll.viewport.rect.height + 1;
        float target = overflow && (hovering || Time.unscaledTime - lastActivity < 1) ? 1 : 0;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 5);
        group.blocksRaycasts = overflow;
    }
}
