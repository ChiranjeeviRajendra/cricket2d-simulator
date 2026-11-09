using Godot;

namespace Cricket2D;

/// <summary>Front-end navigation only; entering batting is the sole gameplay action.</summary>
public partial class MainMenu : Control
{
    public event Action? BattingRequested;
    public event Action? ExitRequested;
    public bool ShowingPractice { get; private set; }
    private VBoxContainer _mainChoices = null!, _practiceChoices = null!;
    private Label _heading = null!;
    private Button _practice = null!, _exit = null!, _batting = null!, _back = null!;
    private Button _match = null!, _settings = null!, _bowling = null!;
    internal bool UnavailableChoicesDisabled => _match.Disabled && _settings.Disabled && _bowling.Disabled;

    public override void _Ready()
    {
        Theme = VisualTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var centre = new CenterContainer();
        AddChild(centre);
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var stack = new VBoxContainer { CustomMinimumSize = new(440, 0) };
        stack.AddThemeConstantOverride("separation", 20);
        centre.AddChild(stack);
        var title = VisualTheme.Label("CRICKET 2D", 48, VisualTheme.OnDark);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(title);
        var subtitle = VisualTheme.Label("THE PRACTICE GROUND", 16, VisualTheme.OnDark);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(subtitle);
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", VisualTheme.Box(VisualTheme.Panel, VisualTheme.Border, 18, 28));
        stack.AddChild(card);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 20);
        card.AddChild(content);
        _heading = VisualTheme.Label("Main Menu", 24);
        content.AddChild(_heading);
        _mainChoices = Choices(content);
        _match = Choice(_mainChoices, "Play Match", null);
        _practice = Choice(_mainChoices, "Practice", ShowPractice);
        _settings = Choice(_mainChoices, "Settings", null);
        _exit = Choice(_mainChoices, "Exit", () => ExitRequested?.Invoke());
        _practiceChoices = Choices(content);
        _batting = Choice(_practiceChoices, "Batting", () => BattingRequested?.Invoke());
        _bowling = Choice(_practiceChoices, "Bowling", null);
        _back = Choice(_practiceChoices, "Back", ShowMain);
        var hint = VisualTheme.Label("D-pad / arrows to move   ·   Enter / A to select", 14, VisualTheme.MutedOnDark);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(hint);
        Resized += QueueRedraw;
        ShowMain();
    }

    public override void _Draw()
    {
        DrawRect(new(Vector2.Zero, Size), VisualTheme.Background);
        Color line = new(1, 1, 1, .08f);
        float middle = Size.X / 2;
        DrawLine(new(middle - 430, Size.Y), new(middle - 260, 0), line, 1, true);
        DrawLine(new(middle + 430, Size.Y), new(middle + 260, 0), line, 1, true);
        DrawLine(new(70, Size.Y - 90), new(Size.X - 70, Size.Y - 90), line, 1, true);
    }

    public void ShowMain()
    {
        Show();
        ShowingPractice = false;
        _heading.Text = "Main Menu";
        _practiceChoices.Hide();
        _mainChoices.Show();
        _practice.GrabFocus();
    }

    public void ShowPractice()
    {
        ShowingPractice = true;
        _heading.Text = "Practice";
        _mainChoices.Hide();
        _practiceChoices.Show();
        _batting.GrabFocus();
    }

    public bool HandleInput(InputEvent evt)
    {
        if (evt is InputEventJoypadMotion) return true;
        int direction = 0;
        bool accept = false, back = false;
        if (evt is InputEventJoypadButton { Pressed: true } joy)
        {
            direction = joy.ButtonIndex == JoyButton.DpadDown ? 1 : joy.ButtonIndex == JoyButton.DpadUp ? -1 : 0;
            accept = joy.ButtonIndex == JoyButton.A;
            back = joy.ButtonIndex == JoyButton.B;
        }
        else if (evt is InputEventKey { Pressed: true, Echo: false } key)
        {
            direction = key.Keycode == Key.Down ? 1 : key.Keycode == Key.Up ? -1 : 0;
            accept = key.Keycode is Key.Enter or Key.KpEnter or Key.Space;
            back = key.Keycode == Key.Escape;
        }
        if (direction != 0 || accept)
        {
            Button[] enabled = ShowingPractice ? [_batting, _back] : [_practice, _exit];
            int selected = Array.IndexOf(enabled, GetViewport().GuiGetFocusOwner());
            if (direction != 0) MoveSelection(direction);
            else enabled[Math.Max(0, selected)].EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }
        if (back)
        {
            if (ShowingPractice) ShowMain();
            return true;
        }
        return false;
    }

    private void MoveSelection(int direction)
    {
        Button[] enabled = ShowingPractice ? [_batting, _back] : [_practice, _exit];
        int selected = Array.IndexOf(enabled, GetViewport().GuiGetFocusOwner());
        enabled[(Math.Max(0, selected) + direction + enabled.Length) % enabled.Length].GrabFocus();
    }

    private static VBoxContainer Choices(Node parent)
    {
        var choices = new VBoxContainer();
        choices.AddThemeConstantOverride("separation", 12);
        parent.AddChild(choices);
        return choices;
    }

    private static Button Choice(Node parent, string title, Action? action)
    {
        var button = new Button
        {
            Name = title.Replace(" ", ""), Text = title, CustomMinimumSize = new(380, 56), Disabled = action == null,
            FocusMode = action == null ? FocusModeEnum.None : FocusModeEnum.All,
            TooltipText = action == null ? "Not available yet" : ""
        };
        button.AddThemeFontSizeOverride("font_size", 20);
        if (action != null) button.Pressed += action;
        parent.AddChild(button);
        if (action != null) VisualTheme.NavigationButton(button);
        return button;
    }
}
