using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Non-blocking scene-local feedback, also works while paused.
public class UIFeedback : MonoBehaviour
{
    private static UIFeedback instance;
    private TMP_Text label;
    private GameObject panel;
    private float expiresAt;
    public static string CurrentMessage => instance != null && instance.panel.activeSelf ? instance.label.text : "";

    public static void Show(string message)
    {
        if (instance == null) Create();
        instance.label.text = message;
        instance.panel.SetActive(true);
        instance.expiresAt = Time.unscaledTime + 4f;
    }

    private static void Create()
    {
        var root = new GameObject("UI_Feedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        instance = root.AddComponent<UIFeedback>();
        instance.panel = new GameObject("Message", typeof(RectTransform), typeof(Image));
        instance.panel.transform.SetParent(root.transform, false);
        var rect = (RectTransform)instance.panel.transform;
        rect.anchorMin = new Vector2(0.15f, 0.82f);
        rect.anchorMax = new Vector2(0.85f, 0.92f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var bg = instance.panel.GetComponent<Image>();
        bg.color = ChimeraUITheme.Window;
        bg.raycastTarget = false;
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(rect, false);
        instance.label = text.GetComponent<TMP_Text>();
        // Reuse the project's configured Chinese font and its fallback assets.
        var source = SelectionContextHUD.Instance != null ? SelectionContextHUD.Instance.BuildingNameDisplay : null;
        if (source != null) instance.label.font = source.font;
        instance.label.fontSize = 24;
        instance.label.enableAutoSizing = true;
        instance.label.fontSizeMin = 18;
        instance.label.fontSizeMax = 24;
        instance.label.alignment = TextAlignmentOptions.Center;
        instance.label.color = ChimeraUITheme.PrimaryText;
        instance.label.richText = false;
        instance.label.raycastTarget = false;
        var tr = (RectTransform)text.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(20, 12);
        tr.offsetMax = new Vector2(-20, -12);
    }

    public static string Cost(ResourceSet cost) => $"废料 {cost.Scrap:0.#} / 生物质 {cost.Biomass:0.#} / 魔石 {cost.ManaStone:0.#}";

    public static string Shortage(ResourceSet cost)
    {
        var mgr = GlobalResourceManager.Instance;
        if (mgr == null) return "资源系统尚未就绪";
        var missing = new List<string>();
        if (cost.Scrap > mgr.CurrentScrap) missing.Add($"废料 {cost.Scrap - mgr.CurrentScrap:0.#}");
        if (cost.Biomass > mgr.CurrentBiomass) missing.Add($"生物质 {cost.Biomass - mgr.CurrentBiomass:0.#}");
        if (cost.ManaStone > mgr.CurrentManaStone) missing.Add($"魔石 {cost.ManaStone - mgr.CurrentManaStone:0.#}");
        return "资源不足，还缺：" + string.Join("、", missing);
    }

    private void Update() { if (panel != null && Time.unscaledTime >= expiresAt) panel.SetActive(false); }
    private void OnDestroy() { if (instance == this) instance = null; }
}
