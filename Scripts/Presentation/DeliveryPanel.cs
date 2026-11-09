using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>Practice controls; sampling and solving belong to PracticeSession.</summary>
public partial class DeliveryPanel : Control
{
    public event Action? BeginRequested;
    public event Action? CloseRequested;
    public event Action<PracticeBowlingSettings>? SettingsChanged;
    public event Action<PracticeGuides>? GuidesChanged;
    public PracticeBowlingSettings Settings { get; private set; } = new();
    public DeliverySettings Values => Settings.ManualDelivery;
    public PracticeGuides Guides => new(_pitchGuide.ButtonPressed, _trajectoryGuide.ButtonPressed);
    public bool CanBowl { get; private set; }
    public bool EditingPausedDelivery { get; private set; }
    private OptionButton _bowler = null!;
    private CheckBox _randomLength = null!, _randomSpeed = null!, _pitchGuide = null!, _trajectoryGuide = null!;
    private PitchLengthSlider _length = null!;
    private HSlider _speed = null!, _height = null!;
    private VBoxContainer _manualLength = null!, _manualSpeed = null!, _content = null!;
    private Label _lengthRange = null!, _speedRange = null!, _status = null!;
    private Button _bowl = null!;
    private PanelContainer _card = null!;
    private ScrollContainer _scroll = null!;
    private readonly List<(Control Control, PanelContainer? Frame)> _entries = [];
    private readonly List<Control> _focusOrder = [];
    private readonly List<PanelContainer> _rows = [];
    private readonly StyleBoxFlat _rowNormal = VisualTheme.Box(Colors.Transparent, VisualTheme.Border, 6, 12);
    private readonly StyleBoxFlat _rowSelected = VisualTheme.Box(Colors.Transparent, VisualTheme.Ink, 6, 12);
    private bool _changing;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new(0, 0, 0, .82f) };
        AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _card = new() { Name = "SettingsCard" };
        _card.AddThemeStyleboxOverride("panel", VisualTheme.Box(VisualTheme.Panel, VisualTheme.Border, 18, 24));
        AddChild(_card);
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 12); _card.AddChild(stack);
        stack.AddChild(VisualTheme.Label("Batting Practice Settings", 30)); stack.AddChild(new HSeparator());
        _bowl = new() { Name = "Bowl", Text = "Bowl", Disabled = true, CustomMinimumSize = new(0, 44) };
        stack.AddChild(_bowl); Register(_bowl, null); VisualTheme.NavigationButton(_bowl);
        _bowl.Pressed += ActivatePrimary;
        _scroll = new() { Name = "SettingsScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        stack.AddChild(_scroll);
        _content = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 10); _scroll.AddChild(_content);

        var box = Box("Bowler", out var frame);
        _bowler = new() { Name = "BowlerSelector", CustomMinimumSize = new(0, 40) };
        foreach (var profile in BowlerProfiles.All) _bowler.AddItem(profile.Name, (int)profile.Type);
        _bowler.Select((int)Settings.Bowler);
        box.AddChild(_bowler); Register(_bowler, frame); VisualTheme.NavigationButton(_bowler);
        _bowler.ItemSelected += index => SetSettings(Settings.WithBowler((BowlerType)index));
        var popup = _bowler.GetPopup();
        // The popup owns input while open. Keep its native navigation, and handle
        // controller activation/back in that window rather than the game viewport.
        popup.WindowInput += evt =>
        {
            if (evt is InputEventJoypadMotion) { popup.SetInputAsHandled(); return; }
            if (!popup.Visible || evt is not InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.A or JoyButton.B }) return;
            if (evt is InputEventJoypadButton { ButtonIndex: JoyButton.A } && popup.GetFocusedItem() >= 0)
                SetSettings(Settings.WithBowler((BowlerType)popup.GetFocusedItem()));
            CloseBowlerList();
            popup.SetInputAsHandled();
        };

        box = Box("Pitch length", out frame);
        _randomLength = Check(box, frame, "RandomPitchLength", "Random pitch length");
        _lengthRange = VisualTheme.Label("8–25 m from the bowler's stumps", 14, VisualTheme.Muted); box.AddChild(_lengthRange);
        _manualLength = new(); box.AddChild(_manualLength);
        _length = new() { Name = "PitchLengthSlider", MinValue = DeliverySettings.MinPitchingLength,
            MaxValue = DeliverySettings.MaxPitchingLength, Step = .01, Value = Settings.ManualLength, CustomMinimumSize = new(0, 164) };
        _manualLength.AddChild(_length); Register(_length, frame);
        _length.ValueChanged += value => { if (!_changing) SetSettings(Settings with { ManualLength = value }); };
        _randomLength.Toggled += value => { if (!_changing) SetSettings(Settings with { RandomPitchLength = value }); };

        box = Box("Release speed", out frame);
        _randomSpeed = Check(box, frame, "RandomReleaseSpeed", "Random release speed");
        _speedRange = VisualTheme.Label("", 14, VisualTheme.Muted); box.AddChild(_speedRange);
        _manualSpeed = new(); box.AddChild(_manualSpeed);
        _speed = Slider(_manualSpeed, frame, "ReleaseSpeedSlider", 18, 180, .1, Settings.ManualSpeedKph, "0.0", "km/h");
        _speed.ValueChanged += value => { if (!_changing) SetSettings(Settings.WithManualSpeed(value)); };
        _randomSpeed.Toggled += value => { if (!_changing) SetSettings(Settings with { RandomSpeed = value }); };

        box = Box("Release height", out frame);
        _height = Slider(box, frame, "ReleaseHeightSlider", DeliverySettings.MinReleaseHeight,
            DeliverySettings.MaxReleaseHeight, .01, Settings.ReleaseHeight, "0.00", "m");
        _height.ValueChanged += value => { if (!_changing) SetSettings(Settings with { ReleaseHeight = value }); };

        box = Box("Practice guides", out frame);
        _pitchGuide = Check(box, frame, "ShowPitchPosition", "Show pitch position");
        _trajectoryGuide = Check(box, frame, "ShowPredictedTrajectory", "Show predicted bowling trajectory");
        _pitchGuide.Toggled += _ => GuidesChanged?.Invoke(Guides);
        _trajectoryGuide.Toggled += _ => GuidesChanged?.Invoke(Guides);

        _status = VisualTheme.Label("Preparing delivery…", 14, VisualTheme.Muted);
        _status.Name = "DeliveryStatus"; _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new(0, 38); stack.AddChild(_status);
        Resized += PositionCard;
        RefreshControls(); CallDeferred(MethodName.PositionCard);
    }

    public void SetSettings(PracticeBowlingSettings settings)
    {
        if (_changing || Settings == settings) return;
        Settings = settings; RefreshControls(); SettingsChanged?.Invoke(settings);
    }

    private void RefreshControls()
    {
        bool moveFromLength = _length.HasFocus() && Settings.RandomPitchLength;
        bool moveFromSpeed = _speed.HasFocus() && Settings.RandomSpeed;
        _changing = true;
        _bowler.Select((int)Settings.Bowler);
        _randomLength.SetPressedNoSignal(Settings.RandomPitchLength); _randomSpeed.SetPressedNoSignal(Settings.RandomSpeed);
        _length.Value = Settings.ManualLength; _speed.Value = Settings.ManualSpeedKph; _height.Value = Settings.ReleaseHeight;
        _changing = false;
        var profile = BowlerProfiles.Get(Settings.Bowler);
        _speedRange.Text = $"{profile.MinSpeedKph:0.#}–{profile.MaxSpeedKph:0.#} km/h";
        _lengthRange.Visible = Settings.RandomPitchLength; _manualLength.Visible = !Settings.RandomPitchLength;
        _speedRange.Visible = Settings.RandomSpeed; _manualSpeed.Visible = !Settings.RandomSpeed;
        RebuildFocus();
        if (moveFromLength) _randomLength.GrabFocus();
        if (moveFromSpeed) _randomSpeed.GrabFocus();
        CallDeferred(MethodName.PositionCard);
    }

    public void SetPreparation(bool preparing, bool ready, string error)
    {
        _status.Text = preparing ? "Preparing delivery…" : error;
        _status.AddThemeColorOverride("font_color", error.Length == 0 ? VisualTheme.Muted : VisualTheme.Coral);
        bool wasReady = CanBowl; CanBowl = ready; _bowl.Disabled = !EditingPausedDelivery && !ready;
        if (wasReady != ready) RebuildFocus();
    }

    public void Open(bool editingPausedDelivery = false)
    {
        EditingPausedDelivery = editingPausedDelivery;
        _bowl.Text = editingPausedDelivery ? "Back" : "Bowl";
        _bowl.Disabled = !editingPausedDelivery && !CanBowl;
        Show(); RebuildFocus(); _bowl.GrabFocus();
        _scroll.ScrollVertical = 0; PositionCard();
    }

    public void ResetSettings()
    {
        CloseBowlerList();
        Settings = new();
        _pitchGuide.SetPressedNoSignal(true);
        _trajectoryGuide.SetPressedNoSignal(true);
        EditingPausedDelivery = false;
        _bowl.Text = "Bowl";
        RefreshControls();
    }

    private void ActivatePrimary()
    {
        if (EditingPausedDelivery) CloseRequested?.Invoke();
        else if (CanBowl) BeginRequested?.Invoke();
    }

    public bool CloseBowlerList()
    {
        if (!_bowler.GetPopup().Visible) return false;
        _bowler.GetPopup().Hide(); _bowler.GrabFocus(); return true;
    }

    public bool HandleInput(InputEvent evt)
    {
        if (evt is InputEventJoypadMotion) return true;
        if (_bowler.GetPopup().Visible) return false;
        int vertical = 0, horizontal = 0; bool accept = false;
        if (evt is InputEventJoypadButton { Pressed: true } joy)
        {
            vertical = joy.ButtonIndex == JoyButton.DpadDown ? 1 : joy.ButtonIndex == JoyButton.DpadUp ? -1 : 0;
            horizontal = joy.ButtonIndex == JoyButton.DpadRight ? 1 : joy.ButtonIndex == JoyButton.DpadLeft ? -1 : 0;
            accept = joy.ButtonIndex == JoyButton.A;
        }
        else if (evt is InputEventKey { Pressed: true } key)
        {
            vertical = key.Keycode == Key.Down ? 1 : key.Keycode == Key.Up ? -1 : key.Keycode == Key.Tab ? (key.ShiftPressed ? -1 : 1) : 0;
            horizontal = key.Keycode == Key.Right ? 1 : key.Keycode == Key.Left ? -1 : 0;
            accept = !key.Echo && key.Keycode is Key.Enter or Key.KpEnter or Key.Space;
        }
        if (vertical != 0) MoveFocus(vertical);
        else if (horizontal != 0) Adjust(horizontal);
        else if (accept)
        {
            var focus = GetViewport().GuiGetFocusOwner();
            if (focus == _bowler) { _bowler.ShowPopup(); _bowler.GetPopup().SetFocusedItem(_bowler.Selected); }
            else if (focus is CheckBox check) check.ButtonPressed = !check.ButtonPressed;
            else if (focus == _bowl) ActivatePrimary();
        }
        else return false;
        return true;
    }

    private void Adjust(int direction)
    {
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus == _bowler) SetSettings(Settings.WithBowler((BowlerType)Math.Clamp((int)Settings.Bowler + direction, 0, BowlerProfiles.All.Length - 1)));
        else if (focus == _length) _length.Value -= direction * .1;
        else if (focus is Godot.Range slider) slider.Value += direction * slider.Step;
        else if (focus is CheckBox check) check.ButtonPressed = direction > 0;
    }

    private void MoveFocus(int direction)
    {
        if (_focusOrder.Count == 0) return;
        int selected = _focusOrder.IndexOf(GetViewport().GuiGetFocusOwner());
        _focusOrder[(Math.Max(0, selected) + direction + _focusOrder.Count) % _focusOrder.Count].GrabFocus();
    }

    private void RebuildFocus()
    {
        _focusOrder.Clear();
        foreach (var (control, _) in _entries)
        {
            // Bowl stays focused during preparation, but cannot activate until ready.
            bool enabled = control.IsVisibleInTree() && (control == _bowl || control is not BaseButton button || !button.Disabled);
            control.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
            if (enabled) _focusOrder.Add(control);
        }
        for (int i = 0; i < _focusOrder.Count; i++)
        {
            var control = _focusOrder[i];
            // Native controls copy the paths; release these temporary managed wrappers now.
            using var next = control.GetPathTo(_focusOrder[(i + 1) % _focusOrder.Count]);
            using var previous = control.GetPathTo(_focusOrder[(i + _focusOrder.Count - 1) % _focusOrder.Count]);
            control.FocusNext = control.FocusNeighborBottom = next;
            control.FocusPrevious = control.FocusNeighborTop = previous;
        }
        RefreshSelection();
    }

    private void Register(Control control, PanelContainer? frame)
    {
        _entries.Add((control, frame));
        control.FocusEntered += () => { RefreshSelection(); if (frame != null) _scroll.EnsureControlVisible(frame); };
    }

    private VBoxContainer Box(string title, out PanelContainer frame)
    {
        frame = new(); frame.AddThemeStyleboxOverride("panel", _rowNormal); _rows.Add(frame); _content.AddChild(frame);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 5); frame.AddChild(content);
        content.AddChild(VisualTheme.Label(title, 18)); return content;
    }

    private CheckBox Check(VBoxContainer parent, PanelContainer frame, string name, string caption)
    {
        var check = new CheckBox { Name = name, Text = caption, ButtonPressed = true, CustomMinimumSize = new(0, 32) };
        parent.AddChild(check); Register(check, frame); VisualTheme.NavigationButton(check, 8); return check;
    }

    private HSlider Slider(VBoxContainer parent, PanelContainer frame, string name,
        double min, double max, double step, double value, string format, string unit)
    {
        var badge = VisualTheme.Label($"{value.ToString(format)} {unit}", 20); badge.Name = name + "Value"; parent.AddChild(badge);
        var slider = new HSlider { Name = name, MinValue = min, MaxValue = max, Step = step, Value = value,
            CustomMinimumSize = new(0, 30), MouseDefaultCursorShape = CursorShape.PointingHand };
        slider.AddThemeStyleboxOverride("slider", VisualTheme.Box(VisualTheme.Border, VisualTheme.Border, 3, 3));
        slider.AddThemeStyleboxOverride("grabber_area", VisualTheme.Box(VisualTheme.Ink, VisualTheme.Ink, 3, 3));
        slider.AddThemeStyleboxOverride("grabber_area_highlight", VisualTheme.Box(VisualTheme.Ink, VisualTheme.Ink, 3, 3));
        parent.AddChild(slider); Register(slider, frame);
        slider.ValueChanged += number => badge.Text = $"{number.ToString(format)} {unit}";
        var limits = new HBoxContainer(); parent.AddChild(limits);
        var minimum = VisualTheme.Label($"{min.ToString(format)} {unit}", 12, VisualTheme.Muted);
        minimum.SizeFlagsHorizontal = SizeFlags.ExpandFill; limits.AddChild(minimum);
        limits.AddChild(VisualTheme.Label($"{max.ToString(format)} {unit}", 12, VisualTheme.Muted)); return slider;
    }

    private void PositionCard()
    {
        _card.CustomMinimumSize = new(Math.Min(820, Size.X - 48), 0);
        _card.Size = new(_card.CustomMinimumSize.X, Math.Min(Size.Y - 48, _content.GetCombinedMinimumSize().Y + 240));
        _card.Position = (Size - _card.Size) / 2;
    }

    private void RefreshSelection()
    {
        var focus = GetViewport().GuiGetFocusOwner(); PanelContainer? selected = null;
        foreach (var entry in _entries) if (entry.Control == focus) { selected = entry.Frame; break; }
        foreach (var frame in _rows) frame.AddThemeStyleboxOverride("panel", frame == selected ? _rowSelected : _rowNormal);
    }

    public override void _ExitTree() { _rowNormal.Dispose(); _rowSelected.Dispose(); }
}
