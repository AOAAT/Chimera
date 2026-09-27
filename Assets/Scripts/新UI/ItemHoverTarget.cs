using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Does not implement IScrollHandler: wheel events must continue to the parent ScrollRect.
public sealed class ItemHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Func<ItemHoverContent> getContent;
    private TMP_FontAsset font;
    public static void Bind(GameObject target, Func<ItemHoverContent> content, TMP_FontAsset font = null)
    {
        var hint = target.GetComponent<ItemHoverTarget>() ?? target.AddComponent<ItemHoverTarget>();
        ItemHoverTooltip.HideFor(hint.transform as RectTransform);
        hint.getContent = content; hint.font = font;
    }
    public void OnPointerEnter(PointerEventData data)
    {
        // A component visual can be a child of a socket: the deepest hovered target owns the card.
        if (data.pointerEnter != null && data.pointerEnter.GetComponentInParent<ItemHoverTarget>() != this) return;
        ItemHoverTooltip.Request(transform as RectTransform, getContent, font);
    }
    public void OnPointerExit(PointerEventData _) => ItemHoverTooltip.HideFor(transform as RectTransform);
    public void OnPointerClick(PointerEventData _) => ItemHoverTooltip.HideFor(transform as RectTransform);
    private void OnDisable() => ItemHoverTooltip.HideFor(transform as RectTransform);
}
