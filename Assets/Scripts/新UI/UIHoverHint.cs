using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverHint : MonoBehaviour, IPointerEnterHandler
{
    public string Message;
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(Message)) UIFeedback.Show(Message);
    }
    public static void Set(GameObject target, string message)
    {
        var hint = target.GetComponent<UIHoverHint>() ?? target.AddComponent<UIHoverHint>();
        hint.Message = message;
    }
}
