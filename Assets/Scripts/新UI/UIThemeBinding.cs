using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Explicit roles replace name-based style inference after the initial editor migration.
// No polling, layout rebuilding, or changes to content sprites/raycast settings.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class UIThemeBinding : MonoBehaviour
{
    public UIThemeRole Role;
    public bool DrawBorder;
    [Tooltip("关闭后保留业务代码控制的颜色，例如品质、占用与选中状态。")]
    public bool ApplyColor = true;
    [SerializeField, HideInInspector] private bool captured;
    [SerializeField, HideInInspector] private Sprite authoredSprite;
    [SerializeField, HideInInspector] private Sprite lastAppliedSprite;
    [SerializeField, HideInInspector] private Image.Type authoredType;
    [SerializeField, HideInInspector] private TMP_FontAsset authoredFont;

    public static UIThemeBinding Bind(Graphic graphic, UIThemeRole role, bool border = false, bool color = true)
    {
        if (graphic == null || graphic.GetComponentInParent<LogisticsPanelUI>(true) != null) return null;
        var binding = graphic.GetComponent<UIThemeBinding>() ?? graphic.gameObject.AddComponent<UIThemeBinding>();
        binding.Role = role; binding.DrawBorder = border; binding.ApplyColor = color;
        binding.Apply();
        return binding;
    }
    private void OnEnable() { Apply(); }
    public void Apply()
    {
        if (GetComponentInParent<LogisticsPanelUI>(true) != null) return;
        var graphic = GetComponent<Graphic>();
        if (graphic == null) return;
        var config = ChimeraUITheme.Config;
        var image = graphic as Image;
        var text = graphic as TMP_Text;
        if (!captured)
        {
            captured = true;
            if(image != null) { authoredSprite = image.sprite; authoredType = image.type; }
            if(text != null) authoredFont = text.font;
        }
        if(text != null) text.font = config.Font != null ? config.Font : authoredFont;
        if(Role == UIThemeRole.None) return; // Font-only binding for dynamic quality labels.
        Color tint = config.ColorFor(Role);
        if(ApplyColor) graphic.color = tint;
        if(image == null) return;
        var art = config.ArtworkFor(Role);
        if(art != null)
        {
            // A manually replaced sprite becomes the new fallback, rather than being erased.
            if(image.sprite != lastAppliedSprite)
            { authoredSprite = image.sprite; authoredType = image.type; }
            image.overrideSprite = null;
            image.sprite = art.Normal != null ? art.Normal : authoredSprite;
            lastAppliedSprite = image.sprite;
            image.type = art.Normal != null ? (art.Normal.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple) : authoredType;
            if(ApplyColor && image.sprite != null && !art.TintArtwork) image.color = Color.white;
        }
        var outline = image.GetComponent<Outline>();
        if(DrawBorder && outline == null) outline = image.gameObject.AddComponent<Outline>();
        if(outline != null && DrawBorder)
        {
            outline.enabled = image.sprite == null;
            outline.effectColor = config.Border; outline.effectDistance = new Vector2(1,-1);
            outline.useGraphicAlpha = true;
        }
        var selectable = image.GetComponent<Selectable>();
        if(selectable == null || selectable.targetGraphic != image) return;
        // Toggle checkmarks and scrollbar handles have their own semantics.
        if(!(selectable is Button) && !(selectable is TMP_Dropdown) && !(selectable is TMP_InputField)) return;
        bool hasStates = art != null && art.Normal != null &&
            (art.Hover != null || art.Pressed != null || art.Selected != null || art.Disabled != null);
        var transition = hasStates ? Selectable.Transition.SpriteSwap : Selectable.Transition.ColorTint;
        if(selectable.transition != transition) image.CrossFadeColor(Color.white,0,true,true);
        selectable.transition = transition;
        var state = selectable.spriteState;
        state.highlightedSprite = hasStates ? art.Hover ?? art.Normal : null;
        state.pressedSprite = hasStates ? art.Pressed ?? art.Normal : null;
        state.selectedSprite = hasStates ? art.Selected ?? art.Hover ?? art.Normal : null;
        state.disabledSprite = hasStates ? art.Disabled ?? art.Normal : null;
        selectable.spriteState = state;
        var colors = selectable.colors;
        colors.normalColor = Color.white; colors.highlightedColor = config.HoverTint;
        colors.pressedColor = config.PressedTint; colors.selectedColor = config.SelectedTint;
        colors.disabledColor = config.DisabledTint; colors.colorMultiplier = 1; colors.fadeDuration = .08f;
        selectable.colors = colors;
    }
}
