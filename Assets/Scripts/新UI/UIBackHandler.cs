using System;
using System.Collections.Generic;
using UnityEngine;

// Register windows in opening order; Escape closes exactly one window.
public class UIBackHandler : MonoBehaviour
{
    private static readonly List<UIBackHandler> stack = new List<UIBackHandler>();
    private Action close;
    public static void Attach(GameObject window, Action onClose)
    {
        var handler = window.GetComponent<UIBackHandler>() ?? window.AddComponent<UIBackHandler>();
        handler.close = onClose;
    }
    private void OnEnable()
    {
        transform.SetAsLastSibling();
        stack.Remove(this);
        stack.Add(this);
    }
    private void OnDisable() { stack.Remove(this); }
    private void OnDestroy() { stack.Remove(this); }
    public static bool TryCloseTop()
    {
        while (stack.Count > 0)
        {
            var top = stack[stack.Count - 1];
            if (top == null || !top.isActiveAndEnabled || top.close == null) { stack.RemoveAt(stack.Count - 1); continue; }
            top.close();
            return true;
        }
        return false;
    }
}
