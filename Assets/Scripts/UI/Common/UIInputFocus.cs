using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIInputFocus
{
    public static bool IsEditingText
    {
        get
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && (selected.GetComponent<TMP_InputField>()?.isFocused == true ||
                selected.GetComponent<InputField>()?.isFocused == true);
        }
    }
}
