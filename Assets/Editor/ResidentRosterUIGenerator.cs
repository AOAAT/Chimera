using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ResidentRosterUIGenerator
{
    private const string PrefabDirectory = "Assets/Resources/UI";
    private const string PrefabPath = PrefabDirectory + "/ResidentRosterPanel.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/msyh SDF.asset";

    private static readonly Color WindowColor = ChimeraUITheme.Window;
    private static readonly Color HeaderColor = ChimeraUITheme.Header;
    private static readonly Color RowColor = ChimeraUITheme.Surface;
    private static readonly Color AccentColor = ChimeraUITheme.Accent;
    private static readonly Color PrimaryTextColor = ChimeraUITheme.PrimaryText;
    private static readonly Color SecondaryTextColor = ChimeraUITheme.SecondaryText;

    static ResidentRosterUIGenerator()
    {
        EditorApplication.delayCall += GenerateIfMissing;
    }

    private static void GenerateIfMissing()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponentInChildren<ResidentRosterRowUI>(true) != null) return;
        GeneratePrefab(false);
    }

    [MenuItem("Tools/Chimera/重新生成居民名册UI")]
    public static void RegeneratePrefab()
    {
        GeneratePrefab(true);
    }

    private static void GeneratePrefab(bool reportResult)
    {
        Directory.CreateDirectory(PrefabDirectory);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject root = new GameObject("ResidentRosterPanel", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ResidentRosterPanelUI));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect, 0f, 0f, 0f, 0f);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        ResidentRosterPanelUI panel = root.GetComponent<ResidentRosterPanelUI>();

        GameObject backdrop = CreateImage("Backdrop", root.transform, ChimeraUITheme.Backdrop, null);
        Stretch(backdrop.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Button backdropButton = backdrop.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.targetGraphic = backdrop.GetComponent<Image>();

        GameObject window = CreateImage("Window", root.transform, WindowColor, uiSprite);
        Anchor(window.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 650f));

        GameObject header = CreateImage("Header", window.transform, HeaderColor, uiSprite);
        Anchor(header.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 72f));
        TMP_Text title = CreateText("Title", header.transform, "殖民地居民名册", font, 28f, FontStyles.Bold,
            PrimaryTextColor, TextAlignmentOptions.MidlineLeft);
        Stretch(title.rectTransform, 28f, 0f, -120f, 0f);

        Button closeButton = CreateButton("Close", header.transform, "关闭", font, uiSprite,
            ChimeraUITheme.Button, new Vector2(88f, 36f));
        Anchor(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(88f, 36f));

        TMP_Text summary = CreateText("Summary", window.transform, "居民统计", font, 18f, FontStyles.Normal,
            SecondaryTextColor, TextAlignmentOptions.MidlineLeft);
        Anchor(summary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-66f, -90f), new Vector2(-190f, 42f));

        Button filterButton = CreateButton("Filter", window.transform, "全部居民", font, uiSprite,
            ChimeraUITheme.Button, new Vector2(150f, 38f));
        Anchor(filterButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-24f, -90f), new Vector2(150f, 38f));
        TMP_Text filterText = filterButton.GetComponentInChildren<TMP_Text>();

        GameObject scrollObject = new GameObject("ResidentList", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(window.transform, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        Stretch(scrollRectTransform, 24f, 24f, -24f, -128f);

        GameObject viewportObject = CreateImage("Viewport", scrollObject.transform, ChimeraUITheme.SurfaceDark, uiSprite);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport, 0f, 0f, 0f, 0f);
        Mask mask = viewportObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vertical = contentObject.GetComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(12, 12, 12, 12);
        vertical.spacing = 10f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        TMP_Text emptyText = CreateText("Empty", viewportObject.transform, "当前没有居民", font, 22f,
            FontStyles.Normal, SecondaryTextColor, TextAlignmentOptions.Center);
        Stretch(emptyText.rectTransform, 24f, 24f, -24f, -24f);

        ResidentRosterRowUI row = CreateRowTemplate(content, font, uiSprite);
        row.gameObject.SetActive(false);

        panel.BackdropButton = backdropButton;
        panel.CloseButton = closeButton;
        panel.FilterButton = filterButton;
        panel.FilterButtonText = filterText;
        panel.TitleText = title;
        panel.SummaryText = summary;
        panel.EmptyText = emptyText;
        panel.ListContent = content;
        panel.RowTemplate = row;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (reportResult) Debug.Log($"居民名册 UI 已重新生成：{PrefabPath}");
    }

    private static ResidentRosterRowUI CreateRowTemplate(RectTransform parent, TMP_FontAsset font, Sprite uiSprite)
    {
        GameObject rowObject = CreateImage("ResidentRowTemplate", parent, RowColor, uiSprite);
        LayoutElement element = rowObject.AddComponent<LayoutElement>();
        element.preferredHeight = 104f;
        element.minHeight = 104f;
        ResidentRosterRowUI row = rowObject.AddComponent<ResidentRosterRowUI>();

        TMP_Text name = CreateText("Name", rowObject.transform, "居民姓名", font, 23f, FontStyles.Bold,
            PrimaryTextColor, TextAlignmentOptions.MidlineLeft);
        Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(18f, -12f), new Vector2(240f, 34f));

        TMP_Text meta = CreateText("Meta", rowObject.transform, "Lv.1 · 赋闲", font, 15f, FontStyles.Normal,
            AccentColor, TextAlignmentOptions.MidlineLeft);
        Anchor(meta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -48f), new Vector2(280f, 24f));

        TMP_Text detail = CreateText("Detail", rowObject.transform, "居民能力与特性", font, 16f, FontStyles.Normal,
            SecondaryTextColor, TextAlignmentOptions.MidlineLeft);
        Anchor(detail.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(18f, 12f), new Vector2(-170f, 30f));
        detail.enableAutoSizing = true;
        detail.fontSizeMin = 12f;
        detail.fontSizeMax = 16f;

        Button actionButton = CreateButton("Action", rowObject.transform, "查看", font, uiSprite, AccentColor,
            new Vector2(126f, 44f));
        Anchor(actionButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(126f, 44f));

        row.NameText = name;
        row.MetaText = meta;
        row.DetailText = detail;
        row.ActionButton = actionButton;
        row.ActionText = actionButton.GetComponentInChildren<TMP_Text>();
        return row;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        return go;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, TMP_FontAsset font, float size,
        FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font, Sprite sprite,
        Color normalColor, Vector2 size)
    {
        GameObject go = CreateImage(name, parent, normalColor, sprite);
        go.GetComponent<RectTransform>().sizeDelta = size;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        TMP_Text text = CreateText("Label", go.transform, label, font, 17f, FontStyles.Bold,
            PrimaryTextColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 4f, 2f, -4f, -2f);
        return button;
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
