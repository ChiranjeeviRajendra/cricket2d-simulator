using Godot;

namespace Cricket2D;

public static class VisualTheme
{
    public static readonly Color Background = new("101f26");
    public static readonly Color Panel = new("192e35");
    public static readonly Color Border = new("355057");
    public static readonly Color Ink = new("ecf1df");
    public static readonly Color Muted = new("99b6b4");
    public static readonly Color Accent = new("c9ec94");
    public static readonly Color Coral = new("f18d75");

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 17 };
        theme.DefaultFont = new SystemFont { FontNames = ["Bahnschrift", "Segoe UI", "Arial"] };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Background);
        theme.SetColor("font_hover_color", "Button", Background);
        theme.SetColor("font_pressed_color", "Button", Background);
        theme.SetStylebox("normal", "Button", Box(Accent, Accent, 8, 14));
        theme.SetStylebox("hover", "Button", Box(new("def5bb"), Accent, 8, 14));
        theme.SetStylebox("pressed", "Button", Box(new("accc7e"), Accent, 8, 14));
        theme.SetStylebox("focus", "Button", Box(Colors.Transparent, Ink, 8, 2));
        theme.SetStylebox("normal", "LineEdit", Box(new("0f2229"), Border, 6, 8));
        theme.SetStylebox("focus", "LineEdit", Box(new("0f2229"), Accent, 6, 8));
        theme.SetColor("font_color", "LineEdit", Ink);
        theme.SetColor("caret_color", "LineEdit", Accent);
        theme.SetColor("selection_color", "LineEdit", new("466653"));
        return theme;
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
