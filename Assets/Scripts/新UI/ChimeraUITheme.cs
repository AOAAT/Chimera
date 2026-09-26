using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chimera 的统一运行时 UI 主题。旧界面只保留结构和业务绑定，视觉由这里集中管理。
/// </summary>
public static class ChimeraUITheme
{
    public static readonly Color32 Backdrop = new Color32(5, 9, 13, 184);
    public static readonly Color32 Window = new Color32(31, 42, 55, 252);
    public static readonly Color32 Header = new Color32(40, 55, 72, 255);
    public static readonly Color32 Surface = new Color32(48, 64, 81, 245);
    public static readonly Color32 SurfaceDark = new Color32(22, 31, 41, 225);
    public static readonly Color32 Button = new Color32(65, 84, 104, 255);
    public static readonly Color32 Accent = new Color32(92, 190, 145, 255);
    public static readonly Color32 Danger = new Color32(173, 74, 76, 255);
    public static readonly Color32 PrimaryText = new Color32(235, 241, 246, 255);
    public static readonly Color32 SecondaryText = new Color32(171, 187, 201, 255);
    public static readonly Color32 MutedText = new Color32(126, 145, 160, 255);
    public static readonly Color32 HP = new Color32(91, 190, 111, 255);
    public static readonly Color32 AP = new Color32(82, 151, 214, 255);

    private static Sprite uiSprite;
    private static bool spriteLoadAttempted;

    public static void ApplyPanel(GameObject root, bool addRootSurface = true, bool styleNamedSurfaces = true)
    {
        if (root == null) return;

        if (addRootSurface)
        {
            Image rootImage = root.GetComponent<Image>();
            if (rootImage == null && root.GetComponent<RectTransform>() != null)
                rootImage = root.AddComponent<Image>();
            StyleSurface(rootImage, Window);
        }

        if (styleNamedSurfaces)
        {
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                StyleNamedSurface(image);
        }
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            StyleButton(button);
        foreach (TMP_Dropdown dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            StyleDropdown(dropdown);
        foreach (Slider slider in root.GetComponentsInChildren<Slider>(true))
            StyleSlider(slider);
        foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
            StyleToggle(toggle);
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            StyleText(text);
    }

    public static void StyleButton(Button button)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();

        string key = GetSemanticKey(button.gameObject);
        bool visualButton = ContainsAny(key, "icon", "sprite", "visual", "chassis", "component", "preview", "图标", "预览");
        if (image != null && !visualButton)
        {
            Color color = ContainsAny(key, "pause", "暂停")
                ? Button
                : ContainsAny(key, "quit", "delete", "remove", "dismiss", "recycle", "dismantle", "退出", "拆除", "遣散", "下岗", "回收")
                    ? Danger
                    : ContainsAny(key, "continue", "confirm", "start", "detail", "staff", "assign", "save", "继续", "确认", "开始", "详情", "工作人员", "派遣", "保存")
                        ? Accent
                        : Button;
            StyleSurface(image, color);
            image.raycastTarget = true;
            button.targetGraphic = image;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.76f, 0.80f, 0.84f, 1f);
        colors.selectedColor = new Color(0.92f, 1.06f, 0.98f, 1f);
        colors.disabledColor = new Color(0.45f, 0.48f, 0.52f, 0.68f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.color = PrimaryText;
            label.fontStyle |= FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = Mathf.Min(label.fontSizeMax > 0f ? label.fontSizeMax : label.fontSize, 18f);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    public static void StyleSlider(Slider slider)
    {
        if (slider == null) return;
        Transform background = slider.transform.Find("Background");
        if (background != null)
            StyleSurface(background.GetComponent<Image>(), SurfaceDark);

        if (slider.fillRect != null)
        {
            Image fill = slider.fillRect.GetComponent<Image>();
            if (fill != null)
            {
                string key = slider.name.ToLowerInvariant();
                fill.color = key.Contains("ap") || key.Contains("armor") || key.Contains("护甲") ? AP : HP;
                fill.raycastTarget = false;
            }
        }
        if (slider.handleRect != null)
        {
            Image handle = slider.handleRect.GetComponent<Image>();
            if (handle != null) handle.color = PrimaryText;
        }
    }

    private static void StyleDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;
        StyleSurface(dropdown.targetGraphic as Image ?? dropdown.GetComponent<Image>(), Button);
        if (dropdown.captionText != null)
            dropdown.captionText.color = dropdown.interactable ? PrimaryText : new Color32(126, 145, 160, 150);
        if (dropdown.itemText != null) dropdown.itemText.color = PrimaryText;
        if (dropdown.template != null)
        {
            Image templateImage = dropdown.template.GetComponent<Image>();
            StyleSurface(templateImage, Window);
        }
    }

    private static void StyleToggle(Toggle toggle)
    {
        if (toggle == null) return;
        if (toggle.targetGraphic is Image background) StyleSurface(background, Button);
        if (toggle.graphic is Image check) check.color = Accent;
    }

    private static void StyleText(TMP_Text text)
    {
        if (text == null || text.GetComponentInParent<Button>() != null) return;
        string key = text.name.ToLowerInvariant();
        if (ContainsAny(key, "name", "title", "header", "标题", "名称"))
        {
            text.color = PrimaryText;
            text.fontStyle |= FontStyles.Bold;
            return;
        }
        if (ContainsAny(key, "level", "rarity", "quality", "等级", "品质"))
        {
            text.color = Accent;
            return;
        }

        Color current = text.color;
        Color.RGBToHSV(current, out _, out float saturation, out float value);
        bool semanticColor = saturation > 0.45f && value > 0.45f;
        if (!semanticColor)
            text.color = ContainsAny(key, "description", "summary", "status", "hint", "empty", "描述", "状态", "提示")
                ? SecondaryText
                : PrimaryText;
    }

    private static void StyleNamedSurface(Image image)
    {
        if (image == null || image.GetComponentInParent<Slider>() != null) return;
        if (image.GetComponent<Button>() != null || image.GetComponent<TMP_Dropdown>() != null) return;

        string key = image.name.ToLowerInvariant();
        if (ContainsAny(key, "icon", "sprite", "visual", "portrait", "preview", "fill", "handle", "arrow",
                "图标", "头像", "立绘", "预览"))
            return;

        if (ContainsAny(key, "header", "toolbar", "titlebar", "页眉", "标题栏"))
            StyleSurface(image, Header);
        else if (ContainsAny(key, "viewport", "scroll", "shelf", "contentbackground", "视口", "货架"))
            StyleSurface(image, SurfaceDark);
        else if (ContainsAny(key, "card", "entry", "slot", "item", "头像卡", "条目", "槽位"))
            StyleSurface(image, Surface);
        else if (ContainsAny(key, "panel", "window", "root", "background", "bg", "面板", "背景"))
            StyleSurface(image, Window);
    }

    private static void StyleSurface(Image image, Color color)
    {
        if (image == null) return;
        Sprite sprite = GetUISprite();
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
        image.color = color;
        if (image.GetComponent<Selectable>() == null) image.raycastTarget = false;
    }

    private static Sprite GetUISprite()
    {
        if (!spriteLoadAttempted)
        {
            spriteLoadAttempted = true;
            uiSprite = Resources.Load<Sprite>("UI/ChimeraRounded");
        }
        return uiSprite;
    }

    private static string GetSemanticKey(GameObject gameObject)
    {
        string key = gameObject != null ? gameObject.name : string.Empty;
        if (gameObject == null) return key.ToLowerInvariant();
        TMP_Text label = gameObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null) key += " " + label.text;
        return key.ToLowerInvariant();
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        if (string.IsNullOrEmpty(source)) return false;
        return Array.Exists(values, source.Contains);
    }
}
