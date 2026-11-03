using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>A single in-game modal. Keyboard, mouse and controller can all configure a ball.</summary>
public partial class DeliveryPanel : Control
{
    public event Action<DeliverySettings>? BeginRequested;
    public event Action<DeliverySettings>? SettingsChanged;
    public DeliverySettings Values => new(SpeedUnits.ToMetresPerSecond(_speed.Value), _length.Value, _height.Value);
    private SpinBox _speed = null!;
    private SpinBox _length = null!;
    private SpinBox _height = null!;
    private Label _error = null!;
    private readonly List<SpinBox> _fields = [];
    private int _selected;
    private PanelContainer _card = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0.02f, 0.06f, 0.08f, 0.82f) };
        AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _card = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        _card.AddThemeStyleboxOverride("panel", VisualTheme.Box(VisualTheme.Panel, VisualTheme.Border, 18, 30));
        AddChild(_card);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 13);
        _card.AddChild(stack);
        stack.AddChild(VisualTheme.Label("PRACTICE SESSION     /     DELIVERY SETUP", 13, VisualTheme.Accent));
        stack.AddChild(VisualTheme.Label("Build your delivery", 32));
        var rule = new HSeparator();
        stack.AddChild(rule);
        _speed = Field(stack, "01", "Release speed", "Kilometres per hour at release", "km/h",
            SpeedUnits.ToKilometresPerHour(DeliverySettings.MinSpeed),
            SpeedUnits.ToKilometresPerHour(DeliverySettings.MaxSpeed), 0.1, 115.2);
        _length = Field(stack, "02", "Pitching length", "Metres from the bowler-end wicket", "m",
            DeliverySettings.MinPitchingLength, DeliverySettings.MaxPitchingLength, 0.01, 16);
        _height = Field(stack, "03", "Release height", "Height above the pitch · 1.53–2.16 m", "m",
            DeliverySettings.MinReleaseHeight, DeliverySettings.MaxReleaseHeight, 0.01, 2.15);
        _error = VisualTheme.Label("", 14, VisualTheme.Coral);
        _error.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _error.CustomMinimumSize = new Vector2(550, 22);
        stack.AddChild(_error);
        var button = new Button { Text = "Begin delivery     →", CustomMinimumSize = new Vector2(0, 52) };
        button.AddThemeFontSizeOverride("font_size", 19);
        button.Pressed += Begin;
        stack.AddChild(button);
        Resized += PositionCard;
        _card.Resized += PositionCard;
        CallDeferred(MethodName.PositionCard);
    }

    public void Open()
    {
        Visible = true;
        _error.Text = "";
        _selected = 0;
        _speed.GetLineEdit().GrabFocus();
        _speed.GetLineEdit().SelectAll();
        PositionCard();
    }

    public void ShowError(string error) => _error.Text = error;

    public void Begin()
    {
        foreach (var field in _fields) field.Apply();
        BeginRequested?.Invoke(Values);
    }

    public void HandleController(JoyButton button)
    {
        if (button == JoyButton.A || button == JoyButton.Start) { Begin(); return; }
        if (button == JoyButton.DpadDown) _selected = (_selected + 1) % _fields.Count;
        else if (button == JoyButton.DpadUp) _selected = (_selected + _fields.Count - 1) % _fields.Count;
        else if (button is JoyButton.DpadLeft or JoyButton.DpadRight)
        {
            double step = _selected == 1 ? 0.1 : _fields[_selected].Step;
            _fields[_selected].Value += button == JoyButton.DpadRight ? step : -step;
        }
        else return;
        _fields[_selected].GetLineEdit().GrabFocus();
        _fields[_selected].GetLineEdit().SelectAll();
    }

    private SpinBox Field(VBoxContainer parent, string index, string title, string hint,
        string unit, double min, double max, double step, double value)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 67) };
        row.AddThemeConstantOverride("separation", 16);
        parent.AddChild(row);
        var number = VisualTheme.Label(index, 14, VisualTheme.Muted);
        number.CustomMinimumSize = new Vector2(24, 0);
        row.AddChild(number);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(text);
        text.AddChild(VisualTheme.Label(title, 19));
        text.AddChild(VisualTheme.Label(hint, 13, VisualTheme.Muted));
        var spin = new SpinBox
        {
            MinValue = min, MaxValue = max, Step = step, Value = value, Suffix = unit,
            CustomMinimumSize = new Vector2(156, 48), SizeFlagsVertical = SizeFlags.ShrinkCenter,
            UpdateOnTextChanged = false
        };
        spin.GetLineEdit().AddThemeFontSizeOverride("font_size", 20);
        spin.GetLineEdit().Alignment = HorizontalAlignment.Center;
        int fieldIndex = _fields.Count;
        _fields.Add(spin);
        spin.GetLineEdit().FocusEntered += () => _selected = fieldIndex;
        spin.ValueChanged += _ =>
        {
            if (_error == null) return; // Fields are still being constructed.
            _error.Text = "";
            SettingsChanged?.Invoke(Values);
        };
        row.AddChild(spin);
        return spin;
    }

    private void PositionCard() => _card.Position = (Size - _card.Size) / 2;
}
