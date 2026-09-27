using System;
using TMPro;
using UnityEngine;

public enum UIThemeRole
{
    None, Backdrop, Window, Header, Surface, Inset, Button, AccentButton, DangerButton,
    PrimaryText, SecondaryText, MutedText, OnAccent, Accent, Health, Armor, Border
}

[Serializable]
public class UIThemeArtwork
{
    public Sprite Normal;
    [Tooltip("留空时使用颜色反馈；状态图应与普通图尺寸及九宫格边距一致。")]
    public Sprite Hover, Pressed, Selected, Disabled;
    [Tooltip("彩色原画通常关闭此项；单色底板可打开，使用主题色染色。")]
    public bool TintArtwork;
}

[CreateAssetMenu(menuName = "Chimera/UI 主题", fileName = "ChimeraTheme")]
public sealed class ChimeraUIThemeConfig : ScriptableObject
{
    public const string ResourcePath = "UI/ChimeraTheme";
    [Header("文字（留空保留各界面的字体）")]
    public TMP_FontAsset Font;
    [Header("米白与铜色调色板")]
    public Color Backdrop = new Color32(40,35,30,190);
    public Color Window = new Color32(246,235,214,255);
    public Color Header = new Color32(232,214,181,255);
    public Color Surface = new Color32(251,244,228,255);
    public Color SurfaceDark = new Color32(217,207,188,255);
    public Color Button = new Color32(231,216,191,255);
    public Color Accent = new Color32(173,102,59,255);
    public Color Danger = new Color32(151,56,48,255);
    public Color PrimaryText = new Color32(62,56,47,255);
    public Color SecondaryText = new Color32(89,78,63,255);
    public Color MutedText = new Color32(112,101,85,255);
    public Color OnAccent = Color.white;
    public Color HP = new Color32(54,137,86,255);
    public Color AP = new Color32(51,117,164,255);
    public Color Border = new Color32(139,123,100,255);
    [Header("可选美术：留空使用简洁底板，支持九宫格 Sprite")]
    public UIThemeArtwork WindowArt = new UIThemeArtwork();
    public UIThemeArtwork HeaderArt = new UIThemeArtwork();
    public UIThemeArtwork CardArt = new UIThemeArtwork();
    public UIThemeArtwork InsetArt = new UIThemeArtwork();
    public UIThemeArtwork ButtonArt = new UIThemeArtwork();
    public UIThemeArtwork AccentButtonArt = new UIThemeArtwork();
    public UIThemeArtwork DangerButtonArt = new UIThemeArtwork();
    [Header("无状态图时的按钮反馈")]
    public Color HoverTint = new Color(1.08f,1.08f,1.08f,1);
    public Color PressedTint = new Color(.78f,.78f,.78f,1);
    public Color SelectedTint = new Color(.94f,1.02f,1.02f,1);
    public Color DisabledTint = new Color(.65f,.65f,.65f,.65f);

    public Color ColorFor(UIThemeRole role)
    {
        switch(role)
        {
            case UIThemeRole.Backdrop:return Backdrop;
            case UIThemeRole.Window:return Window;
            case UIThemeRole.Header:return Header;
            case UIThemeRole.Surface:return Surface;
            case UIThemeRole.Inset:return SurfaceDark;
            case UIThemeRole.Button:return Button;
            case UIThemeRole.AccentButton:case UIThemeRole.Accent:return Accent;
            case UIThemeRole.DangerButton:return Danger;
            case UIThemeRole.SecondaryText:return SecondaryText;
            case UIThemeRole.MutedText:return MutedText;
            case UIThemeRole.OnAccent:return OnAccent;
            case UIThemeRole.Health:return HP;
            case UIThemeRole.Armor:return AP;
            case UIThemeRole.Border:return Border;
            default:return PrimaryText;
        }
    }
    public UIThemeArtwork ArtworkFor(UIThemeRole role)
    {
        switch(role)
        {
            case UIThemeRole.Window:return WindowArt;
            case UIThemeRole.Header:return HeaderArt;
            case UIThemeRole.Surface:return CardArt;
            case UIThemeRole.Inset:return InsetArt;
            case UIThemeRole.Button:return ButtonArt;
            case UIThemeRole.AccentButton:return AccentButtonArt;
            case UIThemeRole.DangerButton:return DangerButtonArt;
            default:return null;
        }
    }
}
