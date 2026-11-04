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
    private PerformanceRun? _performanceRun;
    private DeliveryPreviewWorker? _previewWorker;

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
        GetWindow().FocusExited += () => SetFocusPaused(true);
        GetWindow().FocusEntered += () => SetFocusPaused(false);
        if (_capturePreview) CapturePreview();
        if (args.Contains("--performance-test"))
        {
            StartBatting();
            _performanceRun = new PerformanceRun(_simulation, _ground, _hud, _menu);
            AddChild(_performanceRun);
        }
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
        _previewWorker = new(_settings);
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
        if (_simulation == null || _menu.Visible || _paused && !_smokeTest && _performanceRun == null) return;
        _simulation.Tick(_smokeTest ? default : _performanceRun?.Input ?? BattingControls.Read());
        if (_smokeTest) SmokeTick();
    }

    public override void _Process(double delta)
    {
        if (_previewWorker != null && _previewWorker.Poll(out var plan)) _ground.Preview = plan;
        if (_simulation != null && _hud.IsVisibleInTree()) _hud.Refresh(_simulation);
    }

    public override void _ExitTree() => _previewWorker?.Dispose();

    private void SetFocusPaused(bool paused)
    {
        _paused = paused;
        if (_ground != null) _ground.InterpolationPaused = paused;
        // Automated captures/benchmarks deliberately run without user interaction.
        if (!_capturePreview && !_smokeTest && _performanceRun == null)
            Engine.MaxFps = paused ? 30 : 120;
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
        _previewWorker!.Request(request);
    }

}
