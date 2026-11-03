using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>A single in-game modal. Keyboard, mouse and controller can all configure a ball.</summary>
public partial class DeliveryPanel : Control
{
    public event Action<DeliverySettings>? BeginRequested;
    public event Action<DeliverySettings>? SettingsChanged;
    public DeliverySettings Values => new(SpeedUnits.ToMetresPerSecond(_speed.Value), _length.Value, _height.Value);
    private HSlider _speed = null!;
    private PitchLengthSlider _length = null!;
    private HSlider _height = null!;
    private Label _error = null!;
    private readonly List<Godot.Range> _fields = [];
    private readonly List<PanelContainer> _rows = [];
    private int _selected;
    private PanelContainer _card = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.82f) };
        AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _card = new PanelContainer { CustomMinimumSize = new Vector2(800, 0) };
        _card.AddThemeStyleboxOverride("panel", VisualTheme.Box(VisualTheme.Panel, VisualTheme.Border, 18, 30));
        AddChild(_card);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 13);
        _card.AddChild(stack);
        stack.AddChild(VisualTheme.Label("Batting Practice Settings", 32));
        var rule = new HSeparator();
        stack.AddChild(rule);
        _length = new PitchLengthSlider
        {
            Name = "PitchLengthSlider", MinValue = DeliverySettings.MinPitchingLength,
            MaxValue = DeliverySettings.MaxPitchingLength, Step = .01, Value = 16,
            CustomMinimumSize = new(0, 164)
        };
        Field(stack, "Pitch length", "Distance from the bowler's stumps", _length);
        _speed = Slider("ReleaseSpeedSlider",
            SpeedUnits.ToKilometresPerHour(DeliverySettings.MinSpeed),
            SpeedUnits.ToKilometresPerHour(DeliverySettings.MaxSpeed), 0.1, 115.2);
        Field(stack, "Release speed", "Kilometres per hour at release", _speed, "0.0", "km/h");
        _height = Slider("ReleaseHeightSlider",
            DeliverySettings.MinReleaseHeight, DeliverySettings.MaxReleaseHeight, 0.01, 2.15);
        Field(stack, "Release height", "Height above the pitch", _height, "0.00", "m");
        for (int i = 0; i < _fields.Count; i++)
        {
            var field = _fields[i];
            field.FocusNext = field.FocusNeighborBottom = field.GetPathTo(_fields[(i + 1) % _fields.Count]);
            field.FocusPrevious = field.FocusNeighborTop = field.GetPathTo(_fields[(i + _fields.Count - 1) % _fields.Count]);
        }
        _error = VisualTheme.Label("", 14, VisualTheme.Coral);
        _error.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _error.CustomMinimumSize = new Vector2(720, 22);
        stack.AddChild(_error);
        Resized += PositionCard;
        _card.Resized += PositionCard;
        CallDeferred(MethodName.PositionCard);
    }

    public void Open()
    {
        Visible = true;
        _error.Text = "";
        _selected = 0;
        _length.GrabFocus();
        RefreshSelection();
        PositionCard();
    }

    public void ShowError(string error) => _error.Text = error;

    public void Begin() => BeginRequested?.Invoke(Values);

    public void HandleController(JoyButton button)
    {
        if (button == JoyButton.DpadDown) _selected = (_selected + 1) % _fields.Count;
        else if (button == JoyButton.DpadUp) _selected = (_selected + _fields.Count - 1) % _fields.Count;
        else if (button is JoyButton.DpadLeft or JoyButton.DpadRight)
        {
            double step = _selected == 0 ? .1 : _fields[_selected].Step;
            double direction = button == JoyButton.DpadRight ? 1 : -1;
            // The bowler is on the right: moving left on the pitch rail increases the distance.
            _fields[_selected].Value += direction * step * (_selected == 0 ? -1 : 1);
        }
        else return;
        _fields[_selected].GrabFocus();
        RefreshSelection();
    }

    private static HSlider Slider(string name, double min, double max, double step, double value)
    {
        var slider = new HSlider
        {
            Name = name, MinValue = min, MaxValue = max, Step = step, Value = value,
            CustomMinimumSize = new(0, 32), FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        slider.AddThemeStyleboxOverride("slider", VisualTheme.Box(VisualTheme.Border, VisualTheme.Border, 3, 3));
        slider.AddThemeStyleboxOverride("grabber_area", VisualTheme.Box(VisualTheme.Ink, VisualTheme.Ink, 3, 3));
        slider.AddThemeStyleboxOverride("grabber_area_highlight", VisualTheme.Box(VisualTheme.Ink, VisualTheme.Ink, 3, 3));
        return slider;
    }

    private void Field(VBoxContainer parent, string title, string hint, Godot.Range slider,
        string? format = null, string unit = "")
    {
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", VisualTheme.Box(Colors.Transparent, VisualTheme.Border, 6, 12));
        _rows.Add(frame);
        parent.AddChild(frame);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        frame.AddChild(content);
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) };
        row.AddThemeConstantOverride("separation", 16);
        content.AddChild(row);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(text);
        text.AddChild(VisualTheme.Label(title, 19));
        text.AddChild(VisualTheme.Label(hint, 13, VisualTheme.Muted));
        if (format != null)
        {
            string Display(double value) => $"{value.ToString(format)} {unit}";
            var value = VisualTheme.Label(Display(slider.Value), 20);
            value.Name = slider.Name + "Value";
            value.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(value);
            slider.ValueChanged += number => value.Text = Display(number);
        }
        content.AddChild(slider);
        if (format != null)
        {
            var limits = new HBoxContainer();
            var min = VisualTheme.Label($"{slider.MinValue.ToString(format)} {unit}", 12, VisualTheme.Muted);
            min.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            limits.AddChild(min);
            limits.AddChild(VisualTheme.Label($"{slider.MaxValue.ToString(format)} {unit}", 12, VisualTheme.Muted));
            content.AddChild(limits);
        }
        int fieldIndex = _fields.Count;
        _fields.Add(slider);
        slider.FocusEntered += () => { _selected = fieldIndex; RefreshSelection(); };
        slider.ValueChanged += _ =>
        {
            if (_error == null) return; // Fields are still being constructed.
            _error.Text = "";
            SettingsChanged?.Invoke(Values);
        };
    }

    private void PositionCard() => _card.Position = (Size - _card.Size) / 2;

    private void RefreshSelection()
    {
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].AddThemeStyleboxOverride("panel", VisualTheme.Box(Colors.Transparent,
                i == _selected ? VisualTheme.Ink : VisualTheme.Border, 6, 12));
    }
}
