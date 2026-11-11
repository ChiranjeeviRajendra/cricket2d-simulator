using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Global preferences in the main menu; practice settings stay in DeliveryPanel.</summary>
public partial class MainSettingsPanel : VBoxContainer
{
    public GameplaySettings Settings { get; init; } = null!;
    public event Action? CloseRequested;
    private HSlider _speed = null!;
    private Button _back = null!;
    private Label _value = null!, _status = null!;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 16);
        var row = new HBoxContainer();
        AddChild(row);
        var title = VisualTheme.Label("Bowling speed", 20);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(title);
        _value = VisualTheme.Label("", 20);
        row.AddChild(_value);
        _speed = new HSlider
        {
            Name = "BowlingSpeedSlider", MinValue = GameplaySettings.MinBowlingSpeed,
            MaxValue = GameplaySettings.MaxBowlingSpeed, Step = GameplaySettings.BowlingSpeedStep,
            CustomMinimumSize = new(380, 40), FocusMode = FocusModeEnum.All
        };
        AddChild(_speed);
        _speed.ValueChanged += value => { Settings.BowlingSpeed = value; RefreshValue(); };
        var limits = new HBoxContainer();
        AddChild(limits);
        var minimum = VisualTheme.Label("0.50× · Half speed", 14, VisualTheme.Muted);
        minimum.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        limits.AddChild(minimum);
        limits.AddChild(VisualTheme.Label("1.00× · Normal speed", 14, VisualTheme.Muted));
        AddChild(VisualTheme.Label("Applies to every ball from release until bat contact.\nAfter a hit, the ball always returns to normal speed.", 14, VisualTheme.Muted));
        _status = VisualTheme.Label("Saved when you leave Settings or close the game.", 14, VisualTheme.Muted);
        AddChild(_status);
        _back = new Button { Name = "SettingsBack", Text = "Back", CustomMinimumSize = new(380, 56) };
        AddChild(_back);
        VisualTheme.NavigationButton(_back);
        _back.Pressed += () => CloseRequested?.Invoke();
    }

    public void Open()
    {
        Show();
        _speed.SetValueNoSignal(Settings.BowlingSpeed);
        RefreshValue();
        _status.Text = "Saved when you leave Settings or close the game.";
        _speed.GrabFocus();
    }

    public void ShowSaveError() => _status.Text = "Could not save settings. Please check access to the save folder.";

    private void RefreshValue() => _value.Text = $"{Settings.BowlingSpeed:0.00}×";

    public bool HandleInput(InputEvent evt)
    {
        if (evt is InputEventJoypadMotion) return true;
        int vertical = 0, horizontal = 0;
        bool accept = false, back = false;
        if (evt is InputEventJoypadButton { Pressed: true } joy)
        {
            vertical = joy.ButtonIndex == JoyButton.DpadDown ? 1 : joy.ButtonIndex == JoyButton.DpadUp ? -1 : 0;
            horizontal = joy.ButtonIndex == JoyButton.DpadRight ? 1 : joy.ButtonIndex == JoyButton.DpadLeft ? -1 : 0;
            accept = joy.ButtonIndex == JoyButton.A;
            back = joy.ButtonIndex == JoyButton.B;
        }
        else if (evt is InputEventKey { Pressed: true } key)
        {
            vertical = key.Keycode == Key.Down ? 1 : key.Keycode == Key.Up ? -1 : key.Keycode == Key.Tab ? 1 : 0;
            horizontal = key.Keycode == Key.Right ? 1 : key.Keycode == Key.Left ? -1 : 0;
            accept = !key.Echo && key.Keycode is Key.Enter or Key.KpEnter or Key.Space;
            back = !key.Echo && key.Keycode == Key.Escape;
        }
        if (back || accept && _back.HasFocus()) CloseRequested?.Invoke();
        else if (vertical != 0) { if (_speed.HasFocus()) _back.GrabFocus(); else _speed.GrabFocus(); }
        else if (horizontal != 0 && _speed.HasFocus()) _speed.Value += horizontal * _speed.Step;
        else return accept;
        return true;
    }
}
