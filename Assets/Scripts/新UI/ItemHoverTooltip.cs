using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup))]
public sealed class ItemHoverTooltip : MonoBehaviour
{
    public static ItemHoverTooltip Instance { get; private set; }
    public const float Delay = .25f;
    [SerializeField] private RectTransform card;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text title, subtitle, body;
    private RectTransform source;
    private Func<ItemHoverContent> content;
    private CanvasGroup group;
    private ScrollRect scroll;
    private float requestedAt, refreshAt;
    private Rect sourceBounds;
    private Vector2 viewportSize;
    public bool Visible => group != null && group.alpha > 0;
    public RectTransform Card => card;
    public string BodyText => body != null ? body.text : "";
    public string TitleText => title != null ? title.text : "";

    private void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false; group.alpha = 0;
    }
    private void OnDestroy() { Clear(); if (Instance == this) Instance = null; }
    public static void Request(RectTransform owner, Func<ItemHoverContent> content, TMP_FontAsset font = null)
    {
        if (owner == null || content == null) return;
        if (Instance == null)
        {
            var prefab = Resources.Load<ItemHoverTooltip>("UI/ItemHoverTooltip");
            if (prefab != null) Instantiate(prefab);
            else
            {
                var go = new GameObject("ItemHoverTooltip", typeof(RectTransform));
                go.AddComponent<ItemHoverTooltip>().PrepareView(font);
            }
        }
        var view = Instance;
        view.Clear();
        view.source = owner; view.content = content; view.requestedAt = Time.unscaledTime;
        if (font != null) foreach (var text in view.GetComponentsInChildren<TMP_Text>(true)) text.font = ChimeraUITheme.Config.Font != null ? ChimeraUITheme.Config.Font : font;
        Canvas.ForceUpdateCanvases();
        view.sourceBounds = view.Bounds(owner);
        view.scroll = owner.GetComponentInParent<ScrollRect>();
        if (view.scroll != null) view.scroll.onValueChanged.AddListener(view.OnScroll);
        view.viewportSize = new Vector2(Screen.width, Screen.height);
    }
    public static void HideFor(RectTransform owner) { if (Instance != null && Instance.source == owner) Instance.Clear(); }
    public static void Hide() { if (Instance != null) Instance.Clear(); }
    private void OnScroll(Vector2 _) => Clear();
    private void Clear()
    {
        if (scroll != null) scroll.onValueChanged.RemoveListener(OnScroll);
        scroll = null; source = null; content = null; refreshAt = 0;
        if (group != null) group.alpha = 0;
    }
    private void LateUpdate()
    {
        if (source == null || !source.gameObject.activeInHierarchy || Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape) || Input.mouseScrollDelta.sqrMagnitude > 0)
        { Clear(); return; }
        // Closing/replacing a panel or scrolling must never leave detached information behind.
        if (Vector2.Distance(Bounds(source).center, sourceBounds.center) > 1 ||
            viewportSize != new Vector2(Screen.width, Screen.height)) { Clear(); return; }
        if (Time.unscaledTime - requestedAt < Delay ||
            (Visible && (refreshAt <= 0 || Time.unscaledTime < refreshAt))) return;
        var data = content?.Invoke();
        if (data == null) { Clear(); return; }
        refreshAt = data.RefreshInterval > 0 ? Time.unscaledTime + data.RefreshInterval : 0;
        title.text = data.Title; subtitle.text = data.Subtitle; body.text = data.Body; body.fontSize = 19;
        icon.sprite = data.Icon; icon.color = data.Icon != null ? Color.white : Color.clear;
        var safe = SafeBounds();
        float width = Mathf.Min(430, safe.width);
        float bodyHeight = Mathf.Clamp(body.GetPreferredValues(data.Body, width - 36, 0).y, 80, Mathf.Max(80, safe.height - 166));
        card.sizeDelta = new Vector2(width, Mathf.Min(safe.height, bodyHeight + 146));
        var avoid = new List<Rect>();
        var softAvoid = new List<Rect>();
        var workshop = source.GetComponentInParent<AssemblyWorkshopUI>();
        if (workshop != null)
        {
            if (workshop.LeftStatsPanel != null) avoid.Add(Bounds((RectTransform)workshop.LeftStatsPanel.transform));
            if (workshop.CenterPreviewArea != null) softAvoid.Add(Bounds((RectTransform)workshop.CenterPreviewArea.transform));
            foreach (var button in workshop.GetComponentsInChildren<Button>())
                if (button.onClick.GetPersistentEventCount() > 0) avoid.Add(Bounds((RectTransform)button.transform));
        }
        else if (SelectionContextHUD.Instance != null)
        {
            var hud = SelectionContextHUD.Instance;
            if (source.GetComponentInParent<FactoryUIModule>() != null && hud.BuildingRoot != null)
                avoid.Add(Bounds((RectTransform)hud.BuildingRoot.transform));
            foreach (var button in new[] { hud.StaffToggleButton, hud.BuildingUpgradeButton, hud.BuildingDismantleButton })
                if (button != null && button.gameObject.activeInHierarchy) avoid.Add(Bounds((RectTransform)button.transform));
        }
        Rect placement = Place(sourceBounds, card.sizeDelta, safe, avoid, softAvoid);
        card.anchoredPosition = new Vector2(placement.xMin, placement.yMax);
        group.alpha = 1;
    }
    private Rect SafeBounds()
    {
        var root = (RectTransform)transform;
        var area = Screen.safeArea;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, area.min, null, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, area.max, null, out var max);
        return Rect.MinMaxRect(min.x + 12, min.y + 12, max.x - 12, max.y - 12);
    }
    private Rect Bounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var point in corners)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(camera, point);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out var local);
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    // One deterministic rule for every entry point; called once, not on mouse movement.
    public static Rect Place(Rect target, Vector2 size, Rect safe, IReadOnlyList<Rect> avoid, IReadOnlyList<Rect> softAvoid = null)
    {
        const float gap = 14;
        size = Vector2.Min(size, safe.size);
        var origins = new[] {
            new Vector2(target.xMax + gap, target.yMax - size.y),
            new Vector2(target.xMin - gap - size.x, target.yMax - size.y),
            new Vector2(target.center.x - size.x / 2, target.yMax + gap),
            new Vector2(target.center.x - size.x / 2, target.yMin - gap - size.y),
            new Vector2(safe.xMax - size.x, safe.yMin), new Vector2(safe.xMin, safe.yMin) };
        Rect best = default; float bestScore = float.MaxValue;
        for (int i = 0; i < origins.Length; i++)
        {
            var rect = new Rect(new Vector2(Mathf.Clamp(origins[i].x, safe.xMin, safe.xMax - size.x),
                Mathf.Clamp(origins[i].y, safe.yMin, safe.yMax - size.y)), size);
            float score = Overlap(rect, target) * 100000 + i;
            if (avoid != null) foreach (var area in avoid) score += Overlap(rect, area) * 100;
            if (softAvoid != null) foreach (var area in softAvoid) score += Overlap(rect, area);
            if (score < bestScore) { bestScore = score; best = rect; }
        }
        return best;
    }
    private static float Overlap(Rect a, Rect b) => Mathf.Max(0, Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)) *
        Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));

    // Also used by editor authoring; the prefab contains the finished layout and preview text.
    public void PrepareView(TMP_FontAsset font)
    {
        var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        group = GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; group.alpha = 0;
        if (card != null) return;
        card = new GameObject("HoverCard",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
        card.SetParent(transform,false); card.anchorMin = card.anchorMax = new Vector2(.5f,.5f);
        card.pivot = new Vector2(0,1); card.sizeDelta = new Vector2(430,430);
        var background = card.GetComponent<Image>(); ChimeraUITheme.StyleSurface(background,ChimeraUITheme.Window); background.raycastTarget = false;
        icon = new GameObject("ItemIcon",typeof(RectTransform),typeof(Image)).GetComponent<Image>(); icon.transform.SetParent(card,false);
        Top(icon.rectTransform,16,16,82,82); icon.preserveAspect = true; icon.raycastTarget = false; icon.color = Color.clear;
        title = Label("ItemName",font,26); Top(title.rectTransform,112,18,300,40); title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true; title.fontSizeMin = 18; title.fontSizeMax = 26;
        subtitle = Label("ItemType",font,17); Top(subtitle.rectTransform,112,62,300,64); UIThemeBinding.Bind(subtitle, UIThemeRole.SecondaryText);
        body = Label("ItemDetails",font,19); body.rectTransform.anchorMin = Vector2.zero; body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.offsetMin = new Vector2(18,18); body.rectTransform.offsetMax = new Vector2(-18,-128);
        body.enableAutoSizing = true; body.fontSizeMin = 14; body.fontSizeMax = 19;
        title.text = "物品名称"; subtitle.text = "生产配方 / 实际物品"; body.text = "基础属性、生产需求与特殊效果";
    }
    private TMP_Text Label(string name,TMP_FontAsset font,float size)
    {
        var label = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        label.transform.SetParent(card,false); label.font = font; label.fontSize = size; label.color = ChimeraUITheme.PrimaryText;
        label.alignment = TextAlignmentOptions.TopLeft; label.raycastTarget = false; label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis; UIThemeBinding.Bind(label, UIThemeRole.PrimaryText); return label;
    }
    private static void Top(RectTransform rect,float x,float y,float width,float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height);
    }
}
