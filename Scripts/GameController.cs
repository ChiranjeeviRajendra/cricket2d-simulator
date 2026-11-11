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
    private GameplaySettings _gameplay = null!;
    private string _userSettingsPath = "";
    private double _savedBowlingSpeed;
    private BattingSettings _batting = null!;
    private FieldSettings _field = null!;
    private bool _smokeMenuStarted, _smokeMenuComplete, _smokeExitRequested;
    private bool _paused, _userPaused, _focusPaused;
    private bool _smokeTest;
    private int _smokeTicks;
    private bool _smokeReleased;
    private bool _smokeBounced;
    private bool _smokeLifecycleValid;
    private bool _smokeControlsValid;
    private bool _capturePreview;
    private PerformanceRun? _performanceRun;
    private PracticeSession? _practice;
    private ImpactAudio? _audio;
    private bool _quitting;
    private bool _nextDeliveryRequested;

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
        if (args.Contains("--audio-smoke-test"))
        {
            AddChild(new AudioSmokeRun());
            return;
        }
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
        bool automation = _smokeTest || _capturePreview || args.Contains("--performance-test");
        _userSettingsPath = ProjectSettings.GlobalizePath(automation ? "res://artifacts/settings-automation.json" : "user://settings.json");
        _gameplay = automation ? new() : UserSettingsStore.Load(_userSettingsPath);
        _savedBowlingSpeed = _gameplay.BowlingSpeed;
        var menuCanvas = new CanvasLayer { Layer = 10 };
        AddChild(menuCanvas);
        _menu = new MainMenu { Settings = _gameplay };
        menuCanvas.AddChild(_menu);
        _menu.BattingRequested += StartBatting;
        _menu.SettingsCloseRequested += () =>
        {
            if (SaveUserSettings()) _menu.CloseSettings();
            else _menu.SettingsPanel.ShowSaveError();
        };
        _menu.ExitRequested += () =>
        {
            if (_smokeTest) _smokeExitRequested = true;
            else QuitGame();
        };
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += QuitGame;
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
            if (_hud.DeliveryPanel.Visible) _hud.DeliveryPanel.Open();
            return;
        }
        _simulation = new(_settings, _batting, _field, _gameplay);
        _audio = new ImpactAudio();
        AddChild(_audio);
        _audio.SetPaused(_paused);
        _practice = new(_settings, _smokeTest || _capturePreview ? 8128 : null);
        _ground = new GroundView { Simulation = _simulation };
        AddChild(_ground);
        var canvas = new CanvasLayer();
        AddChild(canvas);
        _hud = new GameHud();
        canvas.AddChild(_hud);
        _hud.DeliveryPanel.BeginRequested += Begin;
        _hud.DeliveryPanel.CloseRequested += ClosePracticeSettings;
        _hud.DeliveryPanel.SettingsChanged += UpdatePreview;
        _hud.DeliveryPanel.GuidesChanged += guides => { _practice.Guides = guides; _ground.Guides = guides; };
        _hud.PauseMenu.ResumeRequested += Resume;
        _hud.PauseMenu.SettingsRequested += OpenPracticeSettings;
        _hud.PauseMenu.MainMenuRequested += OpenMainMenu;
        SyncPendingDelivery();
        RefreshPause();
    }

    private void OpenMainMenu()
    {
        _simulation.Batter.SuspendGesture();
        _simulation.ReturnToSetup();
        _simulation.Statistics.Reset();
        _nextDeliveryRequested = _userPaused = false;
        _audio?.Stop();
        _hud.DeliveryPanel.Hide();
        _hud.PauseMenu.Hide();
        _practice!.Reset();
        _hud.DeliveryPanel.ResetSettings();
        _ground.Guides = _practice.Guides;
        SyncPendingDelivery();
        _ground.Hide();
        _hud.Hide();
        _menu.ShowMain();
        RefreshPause();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_quitting) return;
        if (_smokeTest && !_smokeMenuComplete)
        {
            if (!_smokeMenuStarted) { _smokeMenuStarted = true; SmokeMenu(); }
            return;
        }
        if (_simulation == null || _menu.Visible || _paused && !_smokeTest && _performanceRun == null) return;
        // Settings navigation must not become a batting gesture. Idle practice outside the panel can swing.
        if (!_hud.DeliveryPanel.Visible)
        {
            _simulation.Tick(_smokeTest ? default : _performanceRun?.Input ?? BattingControls.Read());
            _audio?.ConsumeContacts(_simulation);
        }
        if (_smokeTest) SmokeTick();
    }

    public override void _Process(double delta)
    {
        if (_practice != null && _practice.Poll()) SyncPendingDelivery();
        if (_nextDeliveryRequested && !_paused && !_menu.Visible && !_hud.DeliveryPanel.Visible && !_practice!.IsPreparing)
        {
            _nextDeliveryRequested = false;
            if (_practice.CanBowl) Begin();
            else OpenSetup(); // Keep invalid settings and their error accessible for correction.
        }
        if (_simulation != null && _hud.IsVisibleInTree()) _hud.Refresh(_simulation);
    }

    public override void _ExitTree() => _practice?.Dispose();

    private void SetFocusPaused(bool paused)
    {
        _focusPaused=paused;
        RefreshPause();
        if (!_capturePreview && !_smokeTest && _performanceRun == null)
            Engine.MaxFps=paused?30:0;
    }

    private void RefreshPause()
    {
        _paused=_focusPaused || _userPaused;
        if(_paused && _simulation != null) _simulation.Batter.SuspendGesture();
        if(_ground != null) _ground.InterpolationPaused=_paused || _menu.Visible || _hud.DeliveryPanel.Visible;
        _audio?.SetPaused(_paused);
    }

    public override void _Input(InputEvent evt)
    {
        if (_quitting) return;
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
        if (BattingControls.IsPause(evt) &&
            ((!_hud.DeliveryPanel.Visible && !_hud.PauseMenu.Visible) || evt is InputEventJoypadButton))
        {
            if (_userPaused) Resume();
            else OpenPauseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_hud.PauseMenu.Visible)
        {
            if (_hud.PauseMenu.HandleInput(evt)) GetViewport().SetInputAsHandled();
            return;
        }
        bool back = evt is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.B } ||
            evt is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape };
        if (_hud.DeliveryPanel.Visible)
        {
            if (back)
            {
                if (!_hud.DeliveryPanel.CloseBowlerList())
                {
                    if (_hud.DeliveryPanel.EditingPausedDelivery) ClosePracticeSettings();
                    else OpenMainMenu();
                }
                GetViewport().SetInputAsHandled();
            }
            else if (_hud.DeliveryPanel.HandleInput(evt)) GetViewport().SetInputAsHandled();
            return;
        }
        if (_paused) return;
        if (BattingControls.IsNextDelivery(evt))
        {
            RequestNextDelivery();
            GetViewport().SetInputAsHandled();
            return;
        }
        bool setup=evt is InputEventJoypadButton { Pressed:true,ButtonIndex:JoyButton.Back } ||
            evt is InputEventKey { Pressed:true,Echo:false,Keycode:Key.Enter or Key.KpEnter };
        bool menu=evt is InputEventKey { Pressed:true,Echo:false,Keycode:Key.Escape };
        if (menu || setup)
        {
            if (menu) OpenPauseMenu();
            else OpenSetup();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (evt is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.T) _ground.ShowBallTrail = !_ground.ShowBallTrail;
    }

    private void Begin()
    {
        if (!_practice!.TryBegin(_simulation, out string error))
        { _hud.DeliveryPanel.SetPreparation(_practice.IsPreparing, false, error); return; }
        _userPaused=false;
        _nextDeliveryRequested = false;
        _audio?.ResetDelivery();
        _hud.DeliveryPanel.Hide();
        _hud.PauseMenu.Hide();
        RefreshPause();
    }

    private void RequestNextDelivery()
    {
        if (_nextDeliveryRequested || _simulation.Phase == DeliveryPhase.Windup) return;
        _simulation.Batter.SuspendGesture();
        _simulation.ReturnToSetup();
        _audio?.Stop();
        _practice!.OpenNextDelivery();
        SyncPendingDelivery();
        if (_practice.CanBowl) Begin();
        else _nextDeliveryRequested = true;
    }

    private void OpenPauseMenu()
    {
        _nextDeliveryRequested = false;
        _userPaused = true;
        _hud.DeliveryPanel.CloseBowlerList();
        _hud.DeliveryPanel.Hide();
        _hud.PauseMenu.Open();
        RefreshPause();
    }

    private void Resume()
    {
        _hud.DeliveryPanel.CloseBowlerList();
        _hud.DeliveryPanel.Hide();
        _hud.PauseMenu.Hide();
        _userPaused = false;
        RefreshPause();
    }

    private void OpenPracticeSettings()
    {
        _hud.PauseMenu.Hide();
        _hud.DeliveryPanel.Open(editingPausedDelivery: true);
        RefreshPause();
    }

    private void ClosePracticeSettings()
    {
        _hud.DeliveryPanel.CloseBowlerList();
        _hud.DeliveryPanel.Hide();
        _hud.PauseMenu.Open(focusSettings: true);
    }

    private void OpenSetup()
    {
        _nextDeliveryRequested = false;
        _simulation.Batter.SuspendGesture();
        _audio?.Stop();
        _simulation.ReturnToSetup();
        _practice!.OpenNextDelivery();
        SyncPendingDelivery();
        _hud.DeliveryPanel.Open();
        RefreshPause();
    }

    private void UpdatePreview(PracticeBowlingSettings request)
    {
        if (_practice!.Configure(request)) SyncPendingDelivery();
    }

    private void SyncPendingDelivery()
    {
        _ground.Preview = _practice!.PendingPlan;
        _hud.DeliveryPanel.SetPreparation(_practice.IsPreparing, _practice.CanBowl, _practice.Error);
    }

    private bool SaveUserSettings()
    {
        if (_gameplay == null || _gameplay.BowlingSpeed == _savedBowlingSpeed) return true;
        if (!UserSettingsStore.TrySave(_userSettingsPath, _gameplay, out string error))
        {
            GD.PushWarning($"Cannot save game settings: {error}");
            return false;
        }
        _savedBowlingSpeed = _gameplay.BowlingSpeed;
        return true;
    }

    private async void QuitGame()
    {
        if (_quitting) return;
        SaveUserSettings();
        _quitting = true;
        _audio?.Stop();
        // Godot's audio mixer releases stopped playback asynchronously. Let it
        // finish before engine shutdown, including closing the window during a hit.
        if (_audio is { PlayedCount: > 0 })
            await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit();
    }

}
