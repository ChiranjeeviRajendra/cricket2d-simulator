using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>A distance-scaled rail. Both wickets and the thumb use the same metres-to-pixels transform.</summary>
public partial class PitchLengthSlider : Godot.Range
{
    private const float Inset = 28, RailY = 72;
    private bool _dragging;
    private Font _font = null!;
    internal float XForMetres(double metres) => Inset + (float)PitchLengthScale.FractionForMetres(metres) * (Size.X - 2 * Inset);

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _font = GetThemeDefaultFont();
        ValueChanged += _ => QueueRedraw();
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        float left = XForMetres(25), right = XForMetres(0), minimum = XForMetres(8), thumb = XForMetres(Value);
        Color ink = VisualTheme.Ink;
        // The enclosing pitch-length group provides the focus frame for the rail.
        // The unavailable 0–8 m stretch remains visible so the two wickets retain their true scale.
        DrawLine(new(left, RailY), new(right, RailY), VisualTheme.Border, 2, true);
        DrawLine(new(left, RailY), new(minimum, RailY), ink, 4, true);
        for (int metre = 0; metre <= 25; metre++)
        {
            float x = XForMetres(metre);
            DrawLine(new(x, RailY + 7), new(x, RailY + (metre % 5 == 0 ? 15 : 10)), VisualTheme.Border, 1);
        }
        Stumps(0);
        Stumps(PitchDimensions.WicketSeparation);
        LabelAt("Bowler", right, 45, true);
        LabelAt("Batter", XForMetres(PitchDimensions.WicketSeparation), 45);
        LabelAt("25 m", left, 105);
        LabelAt("20.12 m", XForMetres(PitchDimensions.WicketSeparation), 105);
        LabelAt("8 m min", minimum, 105);
        LabelAt("0 m", right, 105, true);
        float batter = XForMetres(PitchDimensions.WicketSeparation);
        DrawLine(new(batter, 123), new(right, 123), VisualTheme.Border, 1);
        DrawLine(new(batter, 119), new(batter, 127), VisualTheme.Border, 1);
        DrawLine(new(right, 119), new(right, 127), VisualTheme.Border, 1);
        LabelAt("20.12 m between stumps", (batter + right) / 2, 149);
        DrawLine(new(thumb, 50), new(thumb, RailY), ink, 2, true);
        DrawCircle(new(thumb, RailY), 10, ink);
        DrawCircle(new(thumb, RailY), 4, VisualTheme.Panel);
        string value = $"{Value:0.00} m";
        Vector2 textSize = _font.GetStringSize(value, fontSize: 16);
        float badgeX = Math.Clamp(thumb - textSize.X / 2 - 8, 2, Size.X - textSize.X - 18);
        DrawStyleBox(VisualTheme.Box(ink, ink, 4, 0), new(badgeX, 4, textSize.X + 16, 26));
        DrawString(_font, new(badgeX + 8, 23), value, fontSize: 16, modulate: VisualTheme.OnDark);

        void Stumps(double metres)
        {
            float x = XForMetres(metres), pixelsPerMetre = (right - left) / 25;
            float height = (float)PitchDimensions.StumpHeight * pixelsPerMetre;
            for (int i = -1; i <= 1; i++)
                DrawLine(new(x + i * 3, RailY - 2), new(x + i * 3, RailY - height), ink, 2, true);
            DrawLine(new(x - 4, RailY - height), new(x + 4, RailY - height), ink, 2, true);
        }
        void LabelAt(string label, float x, float y, bool alignRight = false)
        {
            float width = _font.GetStringSize(label, fontSize: 13).X;
            float origin = Math.Clamp(x - (alignRight ? width : width / 2), 2, Size.X - width - 2);
            DrawString(_font, new(origin, y), label, fontSize: 13, modulate: VisualTheme.Muted);
        }
    }

    public override void _GuiInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed) { GrabFocus(); _dragging = true; }
            if (_dragging) SelectAt(mouse.Position.X);
            if (!mouse.Pressed) _dragging = false;
            AcceptEvent();
        }
        else if (evt is InputEventMouseMotion motion && _dragging)
        {
            SelectAt(motion.Position.X);
            AcceptEvent();
        }
        else if (evt is InputEventKey { Pressed: true } key && key.Keycode is Key.Left or Key.Right)
        {
            Value += key.Keycode == Key.Left ? .1 : -.1;
            AcceptEvent();
        }
    }

    private void SelectAt(float x) => Value = PitchLengthScale.SelectMetres((x - Inset) / Math.Max(1, Size.X - 2 * Inset));
}
