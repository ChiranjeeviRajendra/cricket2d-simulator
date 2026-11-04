using Godot;

namespace Cricket2D;

public static class VisualTheme
{
    public static readonly Color Background = new("171716");
    public static readonly Color Panel = new("e6ddce");
    public static readonly Color Border = new("bcb5a9");
    public static readonly Color Ink = new("454440");
    public static readonly Color Muted = new("65625c");
    public static readonly Color Accent = new("454440");
    public static readonly Color OnDark = new("e6ddce");
    public static readonly Color MutedOnDark = new("b6b0a5");
    public static readonly Color Coral = new("a03729");

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 17 };
        theme.DefaultFont = new SystemFont { FontNames = ["Bahnschrift", "Segoe UI", "Arial"] };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", Ink);
        theme.SetColor("font_pressed_color", "Button", OnDark);
        theme.SetColor("font_focus_color", "Button", OnDark);
        theme.SetColor("font_disabled_color", "Button", new("999187"));
        theme.SetStylebox("normal", "Button", Box(Panel, Border, 8, 14));
        theme.SetStylebox("hover", "Button", Box(new("dcd4c6"), Ink, 8, 14));
        theme.SetStylebox("pressed", "Button", Box(Ink, Ink, 8, 14));
        theme.SetStylebox("focus", "Button", Box(Ink, Ink, 8, 14));
        theme.SetStylebox("disabled", "Button", Box(new("dcd5c9"), new("cbc3b6"), 8, 14));
        theme.SetStylebox("panel", "PopupMenu", Box(Panel, Border));
        theme.SetStylebox("hover", "PopupMenu", Box(Ink, Ink, 4, 8));
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", OnDark);
        return theme;
    }

    public static void NavigationButton(Button button)
    {
        var normal = Box(Panel, Border, 8, 14);
        var focused = Box(Ink, Ink, 8, 14);
        void Refresh()
        {
            bool selected = button.HasFocus();
            Color ink = selected ? OnDark : Ink;
            StyleBoxFlat style = selected ? focused : normal;
            button.AddThemeStyleboxOverride("normal", style);
            button.AddThemeStyleboxOverride("hover", style);
            button.AddThemeStyleboxOverride("pressed", style);
            button.AddThemeColorOverride("font_color", ink);
            button.AddThemeColorOverride("font_hover_color", ink);
            button.AddThemeColorOverride("font_pressed_color", ink);
        }
        button.FocusEntered += Refresh;
        button.FocusExited += Refresh;
        button.MouseEntered += () => { if (!button.Disabled) button.GrabFocus(); };
        Refresh();
    }

    public static StyleBoxFlat Box(Color fill, Color border, int radius = 12, int padding = 16) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = padding, ContentMarginRight = padding,
        ContentMarginTop = padding, ContentMarginBottom = padding
    };

    public static Label Label(string text, int size = 17, Color? color = null)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        return label;
    }
}
