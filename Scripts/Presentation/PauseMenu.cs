using Godot;

namespace Cricket2D;

/// <summary>Event-driven pause navigation; the controller owns simulation pause state.</summary>
public partial class PauseMenu : Control
{
    public event Action? ResumeRequested;
    public event Action? SettingsRequested;
    public event Action? MainMenuRequested;
    private Button[] _choices = [];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new(0, 0, 0, .72f) };
        AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var centre = new CenterContainer();
        AddChild(centre);
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", VisualTheme.Box(VisualTheme.Panel, VisualTheme.Border, 18, 28));
        centre.AddChild(card);
        var stack = new VBoxContainer { CustomMinimumSize = new(380, 0) };
        stack.AddThemeConstantOverride("separation", 16);
        card.AddChild(stack);
        stack.AddChild(VisualTheme.Label("Paused", 30));
        _choices =
        [
            Choice("Resume", () => ResumeRequested?.Invoke()),
            Choice("Settings", () => SettingsRequested?.Invoke()),
            Choice("Main Menu", () => MainMenuRequested?.Invoke())
        ];
        stack.AddChild(VisualTheme.Label("D-pad / arrows to move · A / Enter to select", 14, VisualTheme.Muted));
        Hide();

        Button Choice(string title, Action action)
        {
            var button = new Button { Name = title.Replace(" ", ""), Text = title, CustomMinimumSize = new(0, 52) };
            button.Pressed += action;
            stack.AddChild(button);
            VisualTheme.NavigationButton(button);
            return button;
        }
    }

    public void Open(bool focusSettings = false)
    {
        Show();
        _choices[focusSettings ? 1 : 0].GrabFocus();
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
            direction = key.Keycode == Key.Down ? 1 : key.Keycode == Key.Up ? -1 :
                key.Keycode == Key.Tab ? (key.ShiftPressed ? -1 : 1) : 0;
            accept = key.Keycode is Key.Enter or Key.KpEnter or Key.Space;
            back = key.Keycode == Key.Escape;
        }
        int selected = Math.Max(0, Array.IndexOf(_choices, GetViewport().GuiGetFocusOwner()));
        if (direction != 0) _choices[(selected + direction + _choices.Length) % _choices.Length].GrabFocus();
        else if (accept) _choices[selected].EmitSignal(BaseButton.SignalName.Pressed);
        else if (back) ResumeRequested?.Invoke();
        else return false;
        return true;
    }
}
