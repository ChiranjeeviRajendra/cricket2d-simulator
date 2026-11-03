using Cricket.Physics;
using System.Globalization;
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
    private PitchLengthSlider _lengthSlider = null!;
    private Label _error = null!;
    private readonly List<SpinBox> _fields = [];
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
        _lengthSlider = new PitchLengthSlider
        {
            Name = "PitchLengthSlider", MinValue = DeliverySettings.MinPitchingLength,
            MaxValue = DeliverySettings.MaxPitchingLength, Step = .01, Value = 16,
            CustomMinimumSize = new(0, 164)
        };
        _length = Field(stack, "01", "Pitch length", "Distance from the bowler's stumps", "m",
            DeliverySettings.MinPitchingLength, DeliverySettings.MaxPitchingLength, 0.01, 16, _lengthSlider);
        _length.Name = "PitchLengthInput";
        _length.ValueChanged += value => _lengthSlider.Value = value;
        _lengthSlider.ValueChanged += value => _length.Value = value;
        _length.GetLineEdit().TextChanged += PreviewTypedLength;
        _lengthSlider.FocusEntered += () => { _selected = 0; RefreshSelection(); };
        _speed = Field(stack, "02", "Release speed", "Kilometres per hour at release", "km/h",
            SpeedUnits.ToKilometresPerHour(DeliverySettings.MinSpeed),
            SpeedUnits.ToKilometresPerHour(DeliverySettings.MaxSpeed), 0.1, 115.2);
        _height = Field(stack, "03", "Release height", "Height above the pitch · 1.53–2.16 m", "m",
            DeliverySettings.MinReleaseHeight, DeliverySettings.MaxReleaseHeight, 0.01, 2.15);
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
        _lengthSlider.GrabFocus();
        RefreshSelection();
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
        if (_selected == 0) _lengthSlider.GrabFocus();
        else
        {
            _fields[_selected].GetLineEdit().GrabFocus();
            _fields[_selected].GetLineEdit().SelectAll();
        }
        RefreshSelection();
    }

    private SpinBox Field(VBoxContainer parent, string index, string title, string hint,
        string unit, double min, double max, double step, double value, Control? detail = null)
    {
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", VisualTheme.Box(Colors.Transparent,
            detail != null ? VisualTheme.Border : Colors.Transparent, 6, detail != null ? 12 : 8));
        _rows.Add(frame);
        parent.AddChild(frame);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        frame.AddChild(content);
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 64) };
        row.AddThemeConstantOverride("separation", 16);
        content.AddChild(row);
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
        spin.GetLineEdit().FocusEntered += () => { _selected = fieldIndex; RefreshSelection(); };
        spin.ValueChanged += _ =>
        {
            if (_error == null) return; // Fields are still being constructed.
            _error.Text = "";
            SettingsChanged?.Invoke(Values);
        };
        row.AddChild(spin);
        if (detail != null) content.AddChild(detail);
        return spin;
    }

    private void PositionCard() => _card.Position = (Size - _card.Size) / 2;

    private void PreviewTypedLength(string text)
    {
        // Keep incomplete edits such as "2" or "20." intact. Native live clamping would
        // replace the first digit of "25" with the 8 m minimum before the user finishes.
        string number = text.Trim().TrimEnd('m').Trim();
        if (!(double.TryParse(number, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ||
            double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) ||
            !double.IsFinite(value) || value < _length.MinValue || value > _length.MaxValue) return;
        LineEdit edit = _length.GetLineEdit();
        int caret = edit.CaretColumn;
        _length.Value = value;
        edit.Text = text;
        edit.CaretColumn = caret;
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].AddThemeStyleboxOverride("panel", VisualTheme.Box(Colors.Transparent,
                i == _selected ? VisualTheme.Ink : i == 0 ? VisualTheme.Border : Colors.Transparent, 6, i == 0 ? 12 : 8));
    }
}
