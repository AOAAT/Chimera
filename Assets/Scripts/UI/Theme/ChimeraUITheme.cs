using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chimera 的统一 UI 样式工具。编辑器显式应用后保存，运行时仅供动态内容创建使用。
/// </summary>
public static class ChimeraUITheme
{
    private static ChimeraUIThemeConfig config;
    public static ChimeraUIThemeConfig Config
    {
        get
        {
            if(config == null) config = Resources.Load<ChimeraUIThemeConfig>(ChimeraUIThemeConfig.ResourcePath);
            if(config == null)
            {
                config = ScriptableObject.CreateInstance<ChimeraUIThemeConfig>();
                config.hideFlags = HideFlags.HideAndDontSave;
            }
            return config;
        }
    }
    public static void ReloadConfig() { config = null; }

    public static Color Backdrop => Config.Backdrop;
    public static Color Window => Config.Window;
    public static Color Header => Config.Header;
    public static Color Surface => Config.Surface;
    public static Color SurfaceDark => Config.SurfaceDark;
    public static Color Button => Config.Button;
    public static Color Accent => Config.Accent;
    public static Color Danger => Config.Danger;
    public static Color PrimaryText => Config.PrimaryText;
    public static Color SecondaryText => Config.SecondaryText;
    public static Color MutedText => Config.MutedText;
    public static Color HP => Config.HP;
    public static Color AP => Config.AP;
    public static Color QualityTextColor(ComponentQuality quality) =>
        Color.Lerp(ComponentQualityUtility.GetColor(quality), PrimaryText, .75f);

    public static void ApplyPanel(GameObject root, bool addRootSurface = true, bool styleNamedSurfaces = true)
    {
        if (root == null || root.GetComponentInParent<LogisticsPanelUI>(true) != null) return;
        // 仓库自行维护布局和配色；周期主题扫描不能改写下拉菜单、遮罩与品质色。
        if (root.GetComponentInParent<GlobalWarehouseUI>(true) != null) return;

        if (addRootSurface)
        {
            Image rootImage = root.GetComponent<Image>();
            if (rootImage == null && root.GetComponent<RectTransform>() != null)
            {
                rootImage = root.AddComponent<Image>();
                rootImage.raycastTarget = false;
            }
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
        foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (Protected(input)) continue;
            StyleSurface(input.GetComponent<Image>(), Surface);
            if (input.textComponent != null) input.textComponent.color = PrimaryText;
            if (input.placeholder != null) input.placeholder.color = MutedText;
        }
        foreach (Slider slider in root.GetComponentsInChildren<Slider>(true))
            StyleSlider(slider);
        foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
            StyleToggle(toggle);
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            StyleText(text);
    }

    public static void StyleButton(Button button)
    {
        if (button == null || button.GetComponentInParent<LogisticsPanelUI>(true) != null) return;
        // TMP 下拉菜单的全屏点击拦截层必须透明，不能当普通按钮填色。
        if (button.name == "Blocker" || button.name == "Backdrop") return;
        if (button.GetComponentInParent<GlobalWarehouseUI>(true) != null) return;
        Image image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();

        string key = GetSemanticKey(button.gameObject);
        bool visualButton = ContainsAny(key, "icon", "sprite", "visual", "chassis", "component", "preview", "图标", "预览");
        if (image != null && !visualButton)
        {
            Color color = ContainsAny(key, "pause", "暂停")
                ? Button
                : ContainsAny(key, "quit", "delete", "remove", "dismiss", "recycle", "dismantle", "退出", "拆除", "遣散", "下岗", "回收", "放逐")
                    ? Danger
                    : ContainsAny(key, "continue", "confirm", "start", "detail", "staff", "assign", "save", "继续", "确认", "开始", "详情", "工作人员", "派遣", "保存", "查看", "改名")
                        ? Accent
                        : Button;
            UIThemeBinding.Bind(image, color == Danger ? UIThemeRole.DangerButton : color == Accent ? UIThemeRole.AccentButton : UIThemeRole.Button, true);
            image.raycastTarget = true;
            button.targetGraphic = image;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.76f, 0.80f, 0.84f, 1f);
        colors.selectedColor = new Color(0.92f, 1.06f, 0.98f, 1f);
        colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        image?.GetComponent<UIThemeBinding>()?.Apply();

        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            var role = image != null ? image.GetComponent<UIThemeBinding>()?.Role : null;
            UIThemeBinding.Bind(label, role == UIThemeRole.AccentButton || role == UIThemeRole.DangerButton ? UIThemeRole.OnAccent : UIThemeRole.PrimaryText);
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
        if (Protected(slider)) return;
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
        if (Protected(dropdown)) return;
        StyleSurface(dropdown.targetGraphic as Image ?? dropdown.GetComponent<Image>(), Button);
        if (dropdown.captionText != null)
            dropdown.captionText.color = dropdown.interactable ? PrimaryText : MutedText;
        if (dropdown.itemText != null) dropdown.itemText.color = PrimaryText;
        if (dropdown.template != null)
        {
            Image templateImage = dropdown.template.GetComponent<Image>();
            StyleSurface(templateImage, Window);
        }
    }

    private static void StyleToggle(Toggle toggle)
    {
        if (Protected(toggle)) return;
        if (toggle.targetGraphic is Image background) StyleSurface(background, Button);
        if (toggle.graphic is Image check) check.color = Accent;
    }

    private static void StyleText(TMP_Text text)
    {
        if (Protected(text) || text.GetComponentInParent<Button>() != null) return;

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

        text.color = ContainsAny(key, "description", "summary", "status", "hint", "empty", "描述", "状态", "提示")
            ? SecondaryText : PrimaryText;
    }

    private static void StyleNamedSurface(Image image)
    {
        if (image == null || image.GetComponentInParent<LogisticsPanelUI>(true) != null || image.GetComponentInParent<Slider>() != null || image.GetComponent<Mask>() != null) return;
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

    public static void StyleSurface(Image image, Color color)
    {
        if (image == null) return;
        var role = RoleForColor(color);
        if(role == UIThemeRole.None) { image.color = color; return; }
        UIThemeBinding.Bind(image, role, true);
    }

    public static UIThemeRole RoleForColor(Color color)
    {
        foreach(var role in new[]{UIThemeRole.Backdrop, UIThemeRole.Window, UIThemeRole.Header,
            UIThemeRole.Surface, UIThemeRole.Inset, UIThemeRole.Button, UIThemeRole.Accent,
            UIThemeRole.DangerButton, UIThemeRole.PrimaryText, UIThemeRole.SecondaryText,
            UIThemeRole.MutedText, UIThemeRole.OnAccent, UIThemeRole.Health, UIThemeRole.Armor, UIThemeRole.Border})
            if(color == Config.ColorFor(role)) return role;
        return UIThemeRole.None;
    }

    private static bool Protected(Component value) => value == null || value.GetComponentInParent<LogisticsPanelUI>(true) != null || value.GetComponentInParent<GlobalWarehouseUI>(true) != null;

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
