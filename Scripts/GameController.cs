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
        try
        {
            using var file = Godot.FileAccess.Open("res://Config/physics.json", Godot.FileAccess.ModeFlags.Read);
            settings = JsonSerializer.Deserialize<SimulationSettings>(file.GetAsText()) ?? new();
            settings.Validate();
        }
        catch (Exception ex)
        {
            GD.PushError($"Cannot load physics configuration: {ex.Message}");
            GetTree().Quit(1);
            return;
        }
        _simulation = new(settings);
        _ground = new GroundView { Simulation = _simulation };
        AddChild(_ground);
        var canvas = new CanvasLayer();
        AddChild(canvas);
        _hud = new GameHud();
        canvas.AddChild(_hud);
        _hud.DeliveryPanel.BeginRequested += Begin;
        _hud.DeliveryPanel.SettingsChanged += UpdatePreview;
        _hud.SetupRequested += OpenSetup;
        UpdatePreview(_hud.DeliveryPanel.Values);
        GetWindow().FocusExited += () => _paused = true;
        GetWindow().FocusEntered += () => _paused = false;
        var args = OS.GetCmdlineUserArgs();
        _smokeTest = args.Contains("--smoke-test");
        _capturePreview = args.Contains("--capture-preview");
        if (_capturePreview) CapturePreview();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_simulation == null || _paused && !_smokeTest) return;
        _simulation.Tick(_smokeTest ? default : BattingControls.Read());
        if (_smokeTest) SmokeTick();
    }

    public override void _Process(double delta)
    {
        if (_simulation != null) _hud.Refresh(_simulation);
    }

    public override void _Input(InputEvent evt)
    {
        if (_hud == null) return;
        if (evt is InputEventJoypadButton { Pressed: true } joy)
        {
            if (_hud.DeliveryPanel.Visible) _hud.DeliveryPanel.HandleController(joy.ButtonIndex);
            else if (joy.ButtonIndex == JoyButton.A) OpenSetup();
            else return;
            GetViewport().SetInputAsHandled();
            return; // One press either opens the panel or begins a delivery, never both.
        }
        if (evt is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Enter && _hud.DeliveryPanel.Visible)
        {
            _hud.DeliveryPanel.Begin();
            GetViewport().SetInputAsHandled();
        }
        if (key.Keycode == Key.T && !_hud.DeliveryPanel.Visible) _ground.ShowTrajectory = !_ground.ShowTrajectory;
        if (key.Keycode == Key.F11)
            DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen ?
                DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
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
            GD.Print($"SMOKE waiting for A: phase={_simulation.Phase}, time={_simulation.PhaseTime:0.00} s, modal={_hud.DeliveryPanel.Visible}");
        }
        if (_smokeTicks == 901)
        {
            double peakSpeed = _simulation.Statistics.MaxSpeed;
            SmokeButton(JoyButton.A);
            _smokeLifecycleValid &= _hud.DeliveryPanel.Visible && _simulation.Phase == DeliveryPhase.Setup &&
                _simulation.Statistics.MaxSpeed == peakSpeed;
        }
        if (_smokeTicks == 903)
        {
            SmokeButton(JoyButton.DpadRight);
        }
        if (_smokeTicks == 905)
        {
            SmokeButton(JoyButton.A);
        }
        if (_smokeTicks == 907)
        {
            // Test axes with the modal closed so GUI navigation cannot consume them.
            _smokeControlsValid = CheckControllerAxes();
            bool valid = _smokeControlsValid && _smokeLifecycleValid && _simulation.Phase == DeliveryPhase.Windup && !_hud.DeliveryPanel.Visible &&
                Math.Abs(_simulation.Plan!.Settings.Speed - SpeedUnits.ToMetresPerSecond(115.3)) < 1e-9 &&
                _simulation.Statistics.MaxSpeed == 0 && _simulation.Statistics.MaxHeight == 0;
            GD.Print($"SMOKE next delivery: phase={_simulation.Phase}, release={SpeedUnits.ToKilometresPerHour(_simulation.Plan!.Settings.Speed):0.0} km/h, modal={_hud.DeliveryPanel.Visible}");
            GD.Print(valid ? "SMOKE PASS: A-only setup, no timed reset, km/h entry, retained/reset maxima, second delivery." : "SMOKE FAIL: delivery lifecycle or controller menu.");
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

    private static bool CheckControllerAxes()
    {
        void Axis(JoyAxis axis, float value)
        {
            Godot.Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = axis, AxisValue = value });
            Godot.Input.FlushBufferedEvents();
        }
        Axis(JoyAxis.RightX, 1);
        bool valid = BattingControls.Read().Elbow < -.9;
        Axis(JoyAxis.RightX, -1);
        valid &= BattingControls.Read().Elbow > .9;
        Axis(JoyAxis.RightX, 0);
        Axis(JoyAxis.LeftX, 1);
        valid &= BattingControls.Read().Swing > .9;
        Axis(JoyAxis.LeftX, 0);
        Axis(JoyAxis.RightY, 1);
        valid &= BattingControls.Read().Wrist > .9;
        Axis(JoyAxis.RightY, 0);
        GD.Print(valid ? "SMOKE PASS: elbow axis reversed; swing and wrist mappings preserved." : "SMOKE FAIL: controller axis mappings.");
        return valid;
    }

    private async void CapturePreview()
    {
        // Developer-only render verification. Saves only this game's viewport.
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/idle.png");
        OpenSetup();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/setup.png");
        Begin(_hud.DeliveryPanel.Values);
        for (int i = 0; i < 260; i++) _simulation.Tick(default);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/ground.png");
        foreach (var (name, position) in new (string, Vec2)[]
        {
            ("camera-left", new(FieldLayout.BatterReferenceX + 80, 4)),
            ("camera-right", new(FieldLayout.BatterReferenceX - 95, 12)),
            ("camera-high", new(FieldLayout.BatterReferenceX + 10, 50))
        })
        {
            _ground.SetCaptureBall(position);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"res://artifacts/{name}.png");
        }
        GetTree().Quit();
    }
}
