using System.Text.Json;
using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>Composition root: connects input, simulation, and presentation.</summary>
public partial class GameController : Node2D
{
    private Simulation _simulation = null!;
    private GroundView _ground = null!;
    private GameHud _hud = null!;
    private MainMenu _menu = null!;
    private SimulationSettings _settings = null!;
    private BattingSettings _batting = null!;
    private FieldSettings _field = null!;
    private bool _smokeMenuStarted, _smokeMenuComplete, _smokeExitRequested;
    private bool _paused;
    private bool _smokeTest;
    private int _smokeTicks;
    private bool _smokeReleased;
    private bool _smokeBounced;
    private bool _smokeLifecycleValid;
    private bool _smokeControlsValid;
    private bool _capturePreview;

    public override void _Ready()
    {
        BattingControls.Register();
        SimulationSettings settings;
        BattingSettings batting;
        try
        {
            using var file = Godot.FileAccess.Open("res://Config/physics.json", Godot.FileAccess.ModeFlags.Read);
            settings = JsonSerializer.Deserialize<SimulationSettings>(file.GetAsText()) ?? new();
            settings.Validate();
            using var battingFile = Godot.FileAccess.Open("res://Config/batsman.json", Godot.FileAccess.ModeFlags.Read);
            if (battingFile == null) throw new InvalidOperationException("Cannot read Config/batsman.json.");
            batting = BattingSettings.FromJson(battingFile.GetAsText(), settings);
        }
        catch (Exception ex)
        {
            GD.PushError($"Cannot load simulation configuration: {ex.Message}");
            GetTree().Quit(1);
            return;
        }
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--batting-preview"))
        {
            AddChild(new BattingPreview { Session = new(settings, batting) });
            return;
        }
        _settings = settings;
        _batting = batting;
        try
        {
            using var fieldFile = Godot.FileAccess.Open("res://Config/field.json", Godot.FileAccess.ModeFlags.Read);
            if (fieldFile == null) throw new InvalidOperationException("Cannot read Config/field.json.");
            _field = FieldSettings.FromJson(fieldFile.GetAsText());
        }
        catch (Exception ex)
        {
            GD.PushError($"Cannot load field configuration: {ex.Message}");
            GetTree().Quit(1);
            return;
        }
        _smokeTest = args.Contains("--smoke-test");
        _capturePreview = args.Contains("--capture-preview");
        var menuCanvas = new CanvasLayer { Layer = 10 };
        AddChild(menuCanvas);
        _menu = new MainMenu();
        menuCanvas.AddChild(_menu);
        _menu.BattingRequested += StartBatting;
        _menu.ExitRequested += () =>
        {
            if (_smokeTest) _smokeExitRequested = true;
            else GetTree().Quit();
        };
        GetWindow().FocusExited += () => { _paused = true; if (_ground != null) _ground.InterpolationPaused = true; };
        GetWindow().FocusEntered += () => { _paused = false; if (_ground != null) _ground.InterpolationPaused = false; };
        if (_capturePreview) CapturePreview();
    }

    private void StartBatting()
    {
        _menu.Hide();
        if (_simulation != null)
        {
            _ground.Show();
            _hud.Show();
            _ground.InterpolationPaused = _paused;
            return;
        }
        _simulation = new(_settings, _batting, _field);
        _ground = new GroundView { Simulation = _simulation };
        AddChild(_ground);
        var canvas = new CanvasLayer();
        AddChild(canvas);
        _hud = new GameHud();
        canvas.AddChild(_hud);
        _hud.DeliveryPanel.BeginRequested += Begin;
        _hud.DeliveryPanel.SettingsChanged += UpdatePreview;
        UpdatePreview(_hud.DeliveryPanel.Values);
    }

    private void OpenMainMenu()
    {
        _ground.InterpolationPaused = true;
        _ground.Hide();
        _hud.Hide();
        _menu.ShowMain();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_smokeTest && !_smokeMenuComplete)
        {
            if (!_smokeMenuStarted) { _smokeMenuStarted = true; SmokeMenu(); }
            return;
        }
        if (_simulation == null || _menu.Visible || _paused && !_smokeTest) return;
        _simulation.Tick(_smokeTest ? default : BattingControls.Read());
        if (_smokeTest) SmokeTick();
    }

    public override void _Process(double delta)
    {
        if (_simulation != null) _hud.Refresh(_simulation);
    }

    public override void _Input(InputEvent evt)
    {
        if (evt is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen ?
                DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_menu != null && _menu.Visible)
        {
            if (_menu.HandleInput(evt)) GetViewport().SetInputAsHandled();
            return;
        }
        if (_hud == null) return;
        bool accept = evt is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.A } ||
            evt is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter };
        bool back = evt is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.B } ||
            evt is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape };
        if (back || accept)
        {
            if (back) OpenMainMenu();
            else if (_hud.DeliveryPanel.Visible) _hud.DeliveryPanel.Begin();
            else OpenSetup();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (evt is InputEventJoypadButton { Pressed: true } joy)
        {
            if (_hud.DeliveryPanel.Visible) _hud.DeliveryPanel.HandleController(joy.ButtonIndex);
            else return;
            GetViewport().SetInputAsHandled();
            return; // One press either opens the panel or begins a delivery, never both.
        }
        if (evt is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.T && !_hud.DeliveryPanel.Visible) _ground.ShowTrajectory = !_ground.ShowTrajectory;
    }

    private void Begin(DeliverySettings request)
    {
        if (!_simulation.Begin(request, out string error)) { _hud.DeliveryPanel.ShowError(error); return; }
        _hud.DeliveryPanel.Hide();
    }

    private void OpenSetup()
    {
        _simulation.ReturnToSetup();
        _hud.DeliveryPanel.Open();
    }

    private void UpdatePreview(DeliverySettings request)
    {
        DeliverySolver.TrySolve(request, _simulation.Settings, out var plan, out _);
        _ground.Preview = plan;
    }

    private async void SmokeMenu()
    {
        // Mouse checks need real container layout; headless startup can run physics first.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        bool valid = _menu.Visible && !_menu.ShowingPractice && _simulation == null && _menu.UnavailableChoicesDisabled;
        Click("PlayMatch");
        Click("Settings");
        valid &= !_menu.ShowingPractice && _simulation == null;
        Click("Practice");
        valid &= _menu.ShowingPractice && _simulation == null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Click("Bowling");
        valid &= _menu.ShowingPractice && _simulation == null;
        SmokeButton(JoyButton.B);
        valid &= !_menu.ShowingPractice;
        foreach (float value in new[] { 1f, 0f })
        {
            Godot.Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = JoyAxis.LeftY, AxisValue = value });
            Godot.Input.FlushBufferedEvents();
        }
        valid &= GetViewport().GuiGetFocusOwner()?.Name == "Exit";
        SmokeButton(JoyButton.DpadUp);
        var focused = (Button)GetViewport().GuiGetFocusOwner();
        valid &= focused.GetThemeColor("font_color") == VisualTheme.OnDark &&
            ((StyleBoxFlat)focused.GetThemeStylebox("normal")).BgColor == VisualTheme.Ink;
        SmokeKey(Key.Enter);
        valid &= _menu.ShowingPractice;
        SmokeButton(JoyButton.DpadDown); // Skip disabled Bowling and select Back.
        SmokeButton(JoyButton.A);
        valid &= !_menu.ShowingPractice;
        SmokeButton(JoyButton.DpadDown); // Skip disabled Settings and select Exit.
        SmokeButton(JoyButton.A);
        valid &= _smokeExitRequested;
        SmokeButton(JoyButton.DpadUp);
        SmokeButton(JoyButton.A);
        SmokeButton(JoyButton.A);
        valid &= !_menu.Visible && _simulation != null && _simulation.Phase == DeliveryPhase.Setup && !_hud.DeliveryPanel.Visible;
        SmokeKey(Key.Enter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        valid &= _hud.DeliveryPanel.Visible && await CheckDeliverySliders();
        SmokeKey(Key.Escape);
        valid &= _menu.Visible;
        SmokeButton(JoyButton.A);
        SmokeButton(JoyButton.A);
        valid &= _hud.DeliveryPanel.Visible && !_menu.Visible;
        _hud.DeliveryPanel.Hide(); // Leave the existing delivery lifecycle smoke at its initial idle state.
        GD.Print(valid ? "SMOKE PASS: main menu, disabled modes, mouse/keyboard/controller navigation, Back, Exit and Practice > Batting." :
            "SMOKE FAIL: main menu navigation.");
        _smokeMenuComplete = true;
        if (!valid) GetTree().Quit(1);

        void Click(string name)
        {
            var control = (Control)_menu.FindChild(name, true, false);
            Vector2 position = control.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
            foreach (bool pressed in new[] { true, false })
            {
                GetViewport().PushInput(new InputEventMouseButton
                {
                    Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left,
                    ButtonMask = pressed ? MouseButtonMask.Left : 0, Pressed = pressed
                }, true);
            }
        }
    }

    private void SmokeTick()
    {
        _smokeTicks++;
        _smokeReleased |= _simulation.Phase == DeliveryPhase.Live;
        _smokeBounced |= _simulation.FirstBounceX.HasValue;
        if (_smokeTicks == 1)
        {
            _smokeLifecycleValid = !_hud.DeliveryPanel.Visible && _simulation.Phase == DeliveryPhase.Setup;
            SmokeButton(JoyButton.A);
            _smokeLifecycleValid &= _hud.DeliveryPanel.Visible && _simulation.Phase == DeliveryPhase.Setup;
        }
        if (_smokeTicks == 3)
        {
            SmokeButton(JoyButton.A);
            _smokeLifecycleValid &= !_hud.DeliveryPanel.Visible && _simulation.Phase == DeliveryPhase.Windup &&
                Math.Abs(_simulation.Plan!.Settings.Speed - 32) < 1e-9;
        }
        if (_smokeTicks == 900)
        {
            _smokeLifecycleValid &= _smokeReleased && _smokeBounced && _simulation.Phase == DeliveryPhase.Live &&
                _simulation.PhaseTime > 5 && !_hud.DeliveryPanel.Visible;
            SmokeButton(JoyButton.Start);
            _smokeLifecycleValid &= !_hud.DeliveryPanel.Visible;
            double time = _simulation.PhaseTime;
            SmokeButton(JoyButton.B);
            _PhysicsProcess(SimulationSettings.Tick);
            _smokeLifecycleValid &= _menu.Visible && _simulation.PhaseTime == time;
            SmokeButton(JoyButton.A);
            SmokeButton(JoyButton.A);
            _smokeLifecycleValid &= !_menu.Visible && _simulation.PhaseTime == time && !_hud.DeliveryPanel.Visible;
            GD.Print($"SMOKE waiting for A: phase={_simulation.Phase}, time={_simulation.PhaseTime:0.00} s, modal={_hud.DeliveryPanel.Visible}");
        }
        if (_smokeTicks == 901)
        {
            double peakSpeed = _simulation.Statistics.MaxSpeed;
            SmokeKey(Key.Enter);
            _smokeLifecycleValid &= _hud.DeliveryPanel.Visible && _simulation.Phase == DeliveryPhase.Setup &&
                _simulation.Statistics.MaxSpeed == peakSpeed;
        }
        if (_smokeTicks == 903)
        {
            SmokeButton(JoyButton.DpadDown); // Pitch length is first; release speed is second.
            SmokeButton(JoyButton.DpadRight);
        }
        if (_smokeTicks == 905)
        {
            SmokeKey(Key.Enter);
        }
        if (_smokeTicks == 907)
        {
            // Test axes with the modal closed so GUI navigation cannot consume them.
            _smokeControlsValid = CheckControllerAxes();
            bool valid = _smokeControlsValid && _smokeLifecycleValid && _simulation.Phase == DeliveryPhase.Windup && !_hud.DeliveryPanel.Visible &&
                Math.Abs(_simulation.Plan!.Settings.Speed - SpeedUnits.ToMetresPerSecond(115.3)) < 1e-9 &&
                _simulation.Statistics.MaxSpeed == 0 && _simulation.Statistics.MaxHeight == 0;
            GD.Print($"SMOKE next delivery: phase={_simulation.Phase}, release={SpeedUnits.ToKilometresPerHour(_simulation.Plan!.Settings.Speed):0.0} km/h, modal={_hud.DeliveryPanel.Visible}");
            GD.Print(valid ? "SMOKE PASS: A/Enter settings, B/Escape menu, no timed reset, km/h entry, retained/reset maxima, second delivery." : "SMOKE FAIL: delivery lifecycle or controller menu.");
            GD.Print($"Controller devices detected: {Godot.Input.GetConnectedJoypads().Count}");
            GetTree().Quit(valid ? 0 : 1);
        }
    }

    private static void SmokeButton(JoyButton button)
    {
        // A render frame may contain several physics ticks. Explicitly dispatch each
        // press/release rather than assuming a frame occurred between two ticks.
        foreach (bool pressed in new[] { true, false })
        {
            Godot.Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = pressed });
            Godot.Input.FlushBufferedEvents();
        }
    }

    private static void SmokeKey(Key key)
    {
        foreach (bool pressed in new[] { true, false })
        {
            Godot.Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = pressed });
            Godot.Input.FlushBufferedEvents();
        }
    }

    private async System.Threading.Tasks.Task<bool> CheckDeliverySliders()
    {
        var panel = _hud.DeliveryPanel;
        var length = (PitchLengthSlider)panel.FindChild("PitchLengthSlider", true, false);
        var speed = (HSlider)panel.FindChild("ReleaseSpeedSlider", true, false);
        var height = (HSlider)panel.FindChild("ReleaseHeightSlider", true, false);
        DeliverySettings? lastChange = null;
        void Changed(DeliverySettings values) => lastChange = values;
        panel.SettingsChanged += Changed;
        bool valid = panel.FindChildren("*", "SpinBox", true, false).Count == 0 &&
            panel.FindChildren("*", "LineEdit", true, false).Count == 0;
        float zero = length.XForMetres(0), maximum = length.XForMetres(25), wicket = length.XForMetres(PitchDimensions.WicketSeparation);
        valid &= Math.Abs((zero - wicket) / (zero - maximum) - PitchDimensions.WicketSeparation / 25) < 1e-6;
        foreach (double value in new[] { 8.0, 16, 20.12, 25 })
        {
            DragLength(value);
            valid &= Math.Abs(panel.Values.PitchingLength - value) < 1e-8 && lastChange == panel.Values;
        }
        DragLength(0);
        valid &= length.Value == 8;
        DragLength(23.45);
        SmokeButton(JoyButton.DpadLeft);
        valid &= Math.Abs(length.Value - 23.55) < 1e-8;
        SmokeButton(JoyButton.DpadRight);
        valid &= Math.Abs(length.Value - 23.45) < 1e-8;
        SmokeKey(Key.Left);
        valid &= Math.Abs(length.Value - 23.55) < 1e-8;
        SmokeKey(Key.Right);
        valid &= Math.Abs(length.Value - 23.45) < 1e-8;
        foreach (double value in new[] { 18.0, 115.2, 180 })
        {
            DragSlider(speed, value);
            valid &= Math.Abs(panel.Values.Speed - SpeedUnits.ToMetresPerSecond(value)) < 1e-8 && lastChange == panel.Values;
            valid &= ((Label)panel.FindChild("ReleaseSpeedSliderValue", true, false)).Text == $"{value:0.0} km/h";
        }
        SmokeButton(JoyButton.DpadRight);
        valid &= speed.Value == 180;
        foreach (double value in new[] { 1.53, 2.15, 2.16 })
        {
            DragSlider(height, value);
            valid &= Math.Abs(panel.Values.ReleaseHeight - value) < 1e-8 && lastChange == panel.Values;
            valid &= ((Label)panel.FindChild("ReleaseHeightSliderValue", true, false)).Text == $"{value:0.00} m";
        }
        SmokeButton(JoyButton.DpadRight);
        valid &= Math.Abs(height.Value - 2.16) < 1e-8;
        // Restore the normal delivery and verify keyboard/controller focus follows each box.
        length.Value = 16; speed.Value = 115.2; height.Value = 2.15;
        length.GrabFocus();
        SmokeKey(Key.Down);
        valid &= speed.HasFocus();
        SmokeKey(Key.Right);
        valid &= Math.Abs(speed.Value - 115.3) < 1e-8;
        SmokeKey(Key.Left);
        SmokeKey(Key.Tab);
        valid &= height.HasFocus();
        SmokeKey(Key.Left);
        valid &= Math.Abs(height.Value - 2.14) < 1e-8;
        SmokeKey(Key.Right);
        SmokeKey(Key.Tab);
        valid &= length.HasFocus();
        SmokeButton(JoyButton.DpadDown);
        valid &= speed.HasFocus();
        SmokeButton(JoyButton.DpadRight);
        valid &= Math.Abs(speed.Value - 115.3) < 1e-8;
        SmokeButton(JoyButton.DpadLeft);
        SmokeButton(JoyButton.DpadDown);
        valid &= height.HasFocus();
        SmokeButton(JoyButton.DpadLeft);
        valid &= Math.Abs(height.Value - 2.14) < 1e-8;
        SmokeButton(JoyButton.DpadRight);
        SmokeButton(JoyButton.DpadDown);
        valid &= length.HasFocus() && Math.Abs(panel.Values.Speed - 32) < 1e-8 &&
            Math.Abs(panel.Values.ReleaseHeight - 2.15) < 1e-8;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        valid &= panel.GetGlobalRect().Encloses(length.GetGlobalRect()) &&
            panel.GetGlobalRect().Encloses(height.GetGlobalRect());
        panel.SettingsChanged -= Changed;
        GD.Print(valid ? "SMOKE PASS: three boxed sliders, scaled wickets, mouse drag/bounds, live values, keyboard/controller adjustments." :
            "SMOKE FAIL: delivery sliders or keyboard/controller navigation.");
        return valid;

        void DragLength(double value) => Drag(length, new(length.XForMetres(length.Value), 72), new(length.XForMetres(value), 72));
        void DragSlider(HSlider slider, double value)
        {
            float thumbWidth = slider.GetThemeIcon("grabber").GetWidth();
            Vector2 Point(double v) => new(thumbWidth / 2 + (float)((v - slider.MinValue) /
                (slider.MaxValue - slider.MinValue)) * (slider.Size.X - thumbWidth), slider.Size.Y / 2);
            Drag(slider, Point(slider.Value), Point(value));
        }
        void Drag(Control slider, Vector2 from, Vector2 to)
        {
            Vector2 start = slider.GlobalPosition + from, end = slider.GlobalPosition + to;
            GetViewport().PushInput(new InputEventMouseMotion { Position = start, GlobalPosition = start }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = start, GlobalPosition = start,
                ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseMotion { Position = end, GlobalPosition = end, ButtonMask = MouseButtonMask.Left }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = end, GlobalPosition = end,
                ButtonIndex = MouseButton.Left, Pressed = false }, true);
        }
    }

    private bool CheckControllerAxes()
    {
        void Axis(JoyAxis axis, float value)
        {
            Godot.Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = axis, AxisValue = value });
            Godot.Input.FlushBufferedEvents();
        }
        Axis(JoyAxis.RightX, 1);
        bool valid = BattingControls.Read().RightX > .9;
        Axis(JoyAxis.RightX, -1);
        valid &= BattingControls.Read().RightX < -.9;
        Axis(JoyAxis.RightX, 0);
        Axis(JoyAxis.LeftX, 1);
        valid &= BattingControls.Read().LeftX > .9;
        Axis(JoyAxis.LeftX, 0);
        Axis(JoyAxis.RightY, 1);
        valid &= BattingControls.Read().RightY > .9;
        Axis(JoyAxis.RightY, 0);
        Axis(JoyAxis.LeftY, -1);
        valid &= BattingControls.Read().LeftY < -.9;
        Axis(JoyAxis.LeftY, 0);
        foreach (double degrees in new[] { 0.0, 45, 90, 135, 180, 225, 270, 315 })
        {
            float x = (float)Math.Sin(degrees * Math.PI / 180), y = -(float)Math.Cos(degrees * Math.PI / 180);
            Axis(JoyAxis.LeftX, x); Axis(JoyAxis.LeftY, y);
            Axis(JoyAxis.RightX, x); Axis(JoyAxis.RightY, y);
            var input = BattingControls.Read();
            valid &= Math.Abs(input.LeftX - x) < .001 && Math.Abs(input.LeftY - y) < .001 &&
                Math.Abs(input.RightX - x) < .001 && Math.Abs(input.RightY - y) < .001;
        }
        Axis(JoyAxis.LeftX, 1); Axis(JoyAxis.LeftY, 0);
        Axis(JoyAxis.RightX, 0); Axis(JoyAxis.RightY, 0);
        var rig = new BattingRig(_simulation.Settings, _simulation.Batting);
        for (int i = 0; i < 960; i++) rig.Step(BattingControls.Read(), 1.0 / 960, _simulation.Settings);
        var hands = rig.Pose.Hands;
        var shoulder = rig.ShoulderAngle;
        Axis(JoyAxis.RightY, -1);
        for (int i = 0; i < 960; i++) rig.Step(BattingControls.Read(), 1.0 / 960, _simulation.Settings);
        valid &= Math.Abs(rig.ShoulderAngle - shoulder) < 1e-6 && (rig.Pose.Hands - hands).Length < 1e-6 &&
            rig.WristAngleDegrees < -30 && Math.Abs(rig.SwingClockDegrees - 90) < .001;
        Axis(JoyAxis.RightY, 0); Axis(JoyAxis.LeftX, -1);
        rig = new BattingRig(_simulation.Settings, _simulation.Batting);
        for (int i = 0; i < 960; i++) rig.Step(BattingControls.Read(), 1.0 / 960, _simulation.Settings);
        valid &= Math.Abs(rig.SwingClockDegrees - 240) < .001 && !rig.IsSnapping;
        Axis(JoyAxis.LeftX, 0); Axis(JoyAxis.LeftY, -1);
        for (int i = 0; i < 2400 && !rig.IsSnapping; i++) rig.Step(BattingControls.Read(), 1.0 / 960, _simulation.Settings);
        valid &= rig.IsSnapping && Math.Abs(rig.SwingClockDegrees) < .001;
        Axis(JoyAxis.LeftY, 0); Axis(JoyAxis.LeftX, 1);
        for (int i = 0; i < 200 && rig.IsSnapping; i++) rig.Step(BattingControls.Read(), 1.0 / 960, _simulation.Settings);
        valid &= !rig.IsSnapping && Math.Abs(rig.SwingClockDegrees - 240) < .001;
        Axis(JoyAxis.LeftX, 0);
        GD.Print(valid ? "SMOKE PASS: both stick circles, independent wrist, 12-to-8 snap and reverse stop." : "SMOKE FAIL: controller axes, wrist pivot or swing boundaries.");
        return valid;
    }

    private async void CapturePreview()
    {
        // Developer-only render verification. Saves only this game's viewport.
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/main-menu.png");
        SmokeButton(JoyButton.DpadDown);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/main-menu-selected-exit.png");
        _menu.ShowPractice();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/practice-menu.png");
        StartBatting();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/idle.png");
        foreach (var (name, clock, wristClock) in new (string, double, double?)[]
        {
            ("batting-8", 240, null), ("batting-6", 180, null), ("batting-3", 90, null),
            ("batting-1", 30, null), ("batting-wrist", 90, 30), ("batting-follow-through", 5, null)
        })
        {
            double angle = clock * Math.PI / 180, wrist = (wristClock ?? 0) * Math.PI / 180;
            var input = new BattingInput(Math.Sin(angle), -Math.Cos(angle),
                wristClock.HasValue ? Math.Sin(wrist) : 0, wristClock.HasValue ? -Math.Cos(wrist) : 0);
            for (int i = 0; i < 1600; i++) _simulation.Batter.Step(input, 1.0 / 960, _simulation.Settings);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"res://artifacts/{name}.png");
        }
        OpenSetup();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/setup.png");
        var length = (PitchLengthSlider)_hud.DeliveryPanel.FindChild("PitchLengthSlider", true, false);
        foreach (double metres in new[] { 20.12, 25 })
        {
            length.Value = metres;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"res://artifacts/setup-length-{metres:0.00}.png");
        }
        length.Value = 16;
        Begin(_hud.DeliveryPanel.Values);
        for (int i = 0; i < 260; i++) _simulation.Tick(default);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/ground.png");
        foreach (var (name, position) in new (string, Vec2)[]
        {
            ("camera-left", new(FieldLayout.BatterReferenceX + 80, 4)),
            ("camera-right", new(FieldLayout.BatterReferenceX - 95, 12)),
            ("camera-high", new(FieldLayout.BatterReferenceX + 10, 50)),
            ("field-overview", new(PitchDimensions.WicketSeparation / 2, 180))
        })
        {
            _ground.SetCaptureBall(position);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"res://artifacts/{name}.png");
        }
        GetTree().Quit();
    }
}
