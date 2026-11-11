using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

// Opt-in smoke/capture routines kept separate from the interactive loop.
public partial class GameController
{
    private int _smokePreparationWaitTicks;
    private async void SmokeMenu()
    {
        // Mouse checks need real container layout; headless startup can run physics first.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        bool valid = _menu.Visible && !_menu.ShowingPractice && _simulation == null && _menu.UnavailableChoicesDisabled;
        Click("PlayMatch");
        Click("Settings");
        valid &= await CheckMainSettings();
        Click("Practice");
        valid &= _menu.ShowingPractice && _simulation == null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Click("Bowling");
        valid &= _menu.ShowingPractice && _simulation == null;
        SmokeButton(JoyButton.B);
        valid &= !_menu.ShowingPractice;
        valid &= SticksLeaveFocusUnchanged() && GetViewport().GuiGetFocusOwner()?.Name == "Practice";
        var focused = (Button)GetViewport().GuiGetFocusOwner();
        valid &= focused.GetThemeColor("font_color") == VisualTheme.OnDark &&
            ((StyleBoxFlat)focused.GetThemeStylebox("normal")).BgColor == VisualTheme.Ink;
        SmokeKey(Key.Enter);
        valid &= _menu.ShowingPractice;
        SmokeButton(JoyButton.DpadDown); // Skip disabled Bowling and select Back.
        SmokeButton(JoyButton.A);
        valid &= !_menu.ShowingPractice;
        SmokeButton(JoyButton.DpadDown); // Settings is now available.
        SmokeButton(JoyButton.DpadDown); // Exit.
        SmokeButton(JoyButton.A);
        valid &= _smokeExitRequested;
        SmokeButton(JoyButton.DpadUp);
        SmokeButton(JoyButton.DpadUp); // Return through Settings to Practice.
        SmokeButton(JoyButton.A);
        SmokeButton(JoyButton.A);
        valid &= !_menu.Visible && _simulation != null && _simulation.Phase == DeliveryPhase.Setup && !_hud.DeliveryPanel.Visible;
        valid &= _audio is { ReadyForPlayback: true } && _simulation != null && ReferenceEquals(_simulation.Gameplay, _gameplay) &&
            _simulation.Gameplay.BowlingSpeed == .75;
        SmokeKey(Key.Enter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        valid &= _hud.DeliveryPanel.Visible && await CheckDeliverySliders();
        var previousSettings = _practice!.Settings;
        SmokeKey(Key.Escape);
        valid &= _menu.Visible && _practice.Settings == new PracticeBowlingSettings() &&
            _hud.DeliveryPanel.Settings == _practice.Settings && _practice.Settings != previousSettings;
        SmokeButton(JoyButton.A);
        SmokeButton(JoyButton.A);
        valid &= !_hud.DeliveryPanel.Visible && !_menu.Visible && !_paused && !_hud.PauseMenu.Visible;
        valid &= await CheckDeliveryRequestCancellation();
        valid &= _simulation?.Gameplay.BowlingSpeed == .75;
        _hud.DeliveryPanel.SetSettings(new PracticeBowlingSettings().WithManualSpeed(115.2) with { RandomPitchLength = false, RandomSpeed = false });
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
        if ((_smokeTicks == 2 && !_hud.DeliveryPanel.CanBowl) || (_smokeTicks == 904 && _nextDeliveryRequested))
        {
            if (++_smokePreparationWaitTicks > 1200) { GD.Print("SMOKE FAIL: preparation timed out."); GetTree().Quit(1); }
            return;
        }
        _smokeTicks++;
        _smokeReleased |= _simulation.Phase == DeliveryPhase.Live;
        _smokeBounced |= _simulation.FirstBounceX.HasValue;
        if (_smokeTicks == 1)
        {
            _smokeLifecycleValid = !_hud.DeliveryPanel.Visible && _simulation.Phase == DeliveryPhase.Setup;
            SmokeButton(JoyButton.Back);
            _smokeLifecycleValid &= _hud.DeliveryPanel.Visible && _simulation.Phase == DeliveryPhase.Setup;
        }
        if (_smokeTicks == 3)
        {
            _smokeLifecycleValid &= ((Button)_hud.DeliveryPanel.FindChild("Bowl", true, false)).HasFocus();
            SmokeButton(JoyButton.A);
            _smokeLifecycleValid &= !_hud.DeliveryPanel.Visible && _simulation.Phase == DeliveryPhase.Windup &&
                Math.Abs(_simulation.Plan!.Settings.Speed - 32) < 1e-9;
        }
        if (_smokeTicks == 900)
        {
            _smokeLifecycleValid &= _smokeReleased && _smokeBounced && _simulation.Phase == DeliveryPhase.Live &&
                _simulation.PhaseTime > 5 && !_hud.DeliveryPanel.Visible;
            double time = _simulation.PhaseTime;
            SmokeButton(JoyButton.Start);
            _smokeLifecycleValid &= _userPaused && _paused && !_hud.DeliveryPanel.Visible && _hud.PauseMenu.Visible &&
                GetViewport().GuiGetFocusOwner()?.Name == "Resume" && SticksLeaveFocusUnchanged();
            _smokeTest = false; // Exercise the normal focus/power policy, then restore automation.
            SetFocusPaused(true);
            _PhysicsProcess(SimulationSettings.Tick);
            _smokeLifecycleValid &= Engine.MaxFps == 30 && _ground.InterpolationPaused && _simulation.PhaseTime == time;
            SetFocusPaused(false);
            _PhysicsProcess(SimulationSettings.Tick);
            _smokeLifecycleValid &= Engine.MaxFps == 0 && _ground.InterpolationPaused && _simulation.PhaseTime == time;
            // Up navigates the pause menu; it must not replace the current delivery.
            var plan = _simulation.Plan;
            var ball = _simulation.Ball;
            double peakSpeed = _simulation.Statistics.MaxSpeed;
            SmokeNextDelivery(); // The chord must not bowl through the pause menu.
            _smokeLifecycleValid &= !_nextDeliveryRequested && _simulation.Ball == ball && _simulation.PhaseTime == time;
            SmokeButton(JoyButton.DpadUp);
            _smokeLifecycleValid &= GetViewport().GuiGetFocusOwner()?.Name == "MainMenu" && ReferenceEquals(plan, _simulation.Plan);
            SmokeButton(JoyButton.DpadUp); SmokeButton(JoyButton.A);
            _smokeLifecycleValid &= _hud.DeliveryPanel.Visible && _hud.DeliveryPanel.EditingPausedDelivery && !_hud.PauseMenu.Visible;
            _hud.DeliveryPanel.SetSettings(_practice!.Settings.WithManualSpeed(115.3));
            SmokeNextDelivery(); // Paused settings must also consume gameplay input.
            var pitchGuide = (CheckBox)_hud.DeliveryPanel.FindChild("ShowPitchPosition", true, false);
            pitchGuide.ButtonPressed = false;
            _PhysicsProcess(SimulationSettings.Tick);
            _smokeLifecycleValid &= _paused && _simulation.Ball == ball && _simulation.PhaseTime == time &&
                _simulation.Statistics.MaxSpeed == peakSpeed && ReferenceEquals(plan, _simulation.Plan) &&
                SticksLeaveFocusUnchanged() && !_ground.Guides.ShowPitchPosition;
            SmokeButton(JoyButton.A); // Back at the top of paused settings.
            _smokeLifecycleValid &= !_hud.DeliveryPanel.Visible && _hud.PauseMenu.Visible && _paused &&
                GetViewport().GuiGetFocusOwner()?.Name == "Settings";
            SmokeButton(JoyButton.DpadUp); SmokeButton(JoyButton.A); // Resume.
            _smokeLifecycleValid &= !_paused && !_userPaused && !_ground.InterpolationPaused;
            SmokeButton(JoyButton.A); SmokeButton(JoyButton.B); SmokeButton(JoyButton.X); SmokeButton(JoyButton.Y);
            _smokeLifecycleValid &= !_menu.Visible && !_hud.DeliveryPanel.Visible && _simulation.PhaseTime == time;
            _smokeTest = true;
            SmokeButton(JoyButton.Start); SmokeButton(JoyButton.Start);
            _smokeLifecycleValid &= !_paused && !_hud.PauseMenu.Visible;
            SmokeKey(Key.Escape);
            _smokeLifecycleValid &= _paused && _hud.PauseMenu.Visible;
            SmokeButton(JoyButton.B);
            _smokeLifecycleValid &= !_paused && !_hud.PauseMenu.Visible;
        }
        if (_smokeTicks == 901)
        {
            double peakSpeed = _simulation.Statistics.MaxSpeed;
            var plan = _simulation.Plan;
            double time = _simulation.PhaseTime;
            SmokeButton(JoyButton.DpadUp);
            SmokeKey(Key.Up);
            SmokeButton(JoyButton.LeftShoulder);
            SmokeButton(JoyButton.RightShoulder);
            SmokeButton(JoyButton.LeftShoulder, true);
            SmokeButton(JoyButton.RightShoulder, true, device: 1);
            SmokeButton(JoyButton.LeftShoulder, false);
            SmokeButton(JoyButton.RightShoulder, false, device: 1);
            _smokeLifecycleValid &= !_nextDeliveryRequested && ReferenceEquals(plan, _simulation.Plan) &&
                _simulation.Phase == DeliveryPhase.Live && _simulation.PhaseTime == time;
            SmokeNextDelivery();
            int seed = _practice!.Seed;
            SmokeNextDelivery(); // Do not queue or reroll a second request while preparing/winding up.
            _smokeLifecycleValid &= !_hud.DeliveryPanel.Visible && _practice.Seed == seed &&
                _simulation.Statistics.MaxSpeed == peakSpeed;
        }
        if (_smokeTicks == 907)
        {
            // Test axes with the modal closed so GUI navigation cannot consume them.
            _smokeControlsValid = CheckControllerAxes();
            bool valid = _smokeControlsValid && _smokeLifecycleValid && _simulation.Phase == DeliveryPhase.Windup && !_hud.DeliveryPanel.Visible &&
                Math.Abs(_simulation.Plan!.Settings.Speed - SpeedUnits.ToMetresPerSecond(115.3)) < 1e-9 &&
                _simulation.Statistics.MaxSpeed == 0 && _simulation.Statistics.MaxHeight == 0;
            GD.Print($"SMOKE next delivery: phase={_simulation.Phase}, release={SpeedUnits.ToKilometresPerHour(_simulation.Plan!.Settings.Speed):0.0} km/h, modal={_hud.DeliveryPanel.Visible}");
            // Leave through the pause menu with changed settings/guides, then start fresh.
            SmokeButton(JoyButton.Start); SmokeButton(JoyButton.DpadUp); SmokeButton(JoyButton.A);
            valid &= _menu.Visible && !_paused && !_nextDeliveryRequested && _practice!.Settings == new PracticeBowlingSettings() &&
                _hud.DeliveryPanel.Settings == _practice.Settings && _ground.Guides == new PracticeGuides(true, true) &&
                _hud.DeliveryPanel.Guides == _ground.Guides;
            SmokeButton(JoyButton.A); SmokeButton(JoyButton.A);
            valid &= !_menu.Visible && _simulation.Phase == DeliveryPhase.Setup && !_hud.PauseMenu.Visible && !_hud.DeliveryPanel.Visible;
            GD.Print(valid ? "SMOKE PASS: shoulder-chord next ball, rejected Up/single/split-controller inputs, pause/resume/settings/main menu, frozen ball, session defaults and D-pad-only UI." : "SMOKE FAIL: delivery lifecycle or controller menu.");
            GD.Print($"Controller devices detected: {Godot.Input.GetConnectedJoypads().Count}");
            GetTree().Quit(valid ? 0 : 1);
        }
    }

    private static void SmokeButton(JoyButton button)
    {
        // A render frame may contain several physics ticks. Explicitly dispatch each
        // press/release rather than assuming a frame occurred between two ticks.
        SmokeButton(button, true);
        SmokeButton(button, false);
    }

    private static void SmokeButton(JoyButton button, bool pressed, int device = 0)
    {
        Godot.Input.ParseInputEvent(new InputEventJoypadButton { Device = device, ButtonIndex = button, Pressed = pressed });
        Godot.Input.FlushBufferedEvents();
    }

    private static void SmokeNextDelivery(bool rightFirst = false)
    {
        JoyButton first = rightFirst ? JoyButton.RightShoulder : JoyButton.LeftShoulder;
        JoyButton second = rightFirst ? JoyButton.LeftShoulder : JoyButton.RightShoulder;
        SmokeButton(first, true);
        SmokeButton(second, true);
        SmokeButton(first, false);
        SmokeButton(second, false);
    }

    private static void SmokeKey(Key key)
    {
        foreach (bool pressed in new[] { true, false })
        {
            Godot.Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = pressed });
            Godot.Input.FlushBufferedEvents();
        }
    }

    private bool CheckControllerAxes()
    {
        void Axis(JoyAxis axis,float value)
        {
            Godot.Input.ParseInputEvent(new InputEventJoypadMotion { Device=0,Axis=axis,AxisValue=value });
            Godot.Input.FlushBufferedEvents();
        }
        void Hold(JoyButton button,bool pressed)
        {
            Godot.Input.ParseInputEvent(new InputEventJoypadButton { Device=0,ButtonIndex=button,Pressed=pressed });
            Godot.Input.FlushBufferedEvents();
        }
        void Clock(double clock)
        {
            double angle=clock*Math.PI/180;
            Axis(JoyAxis.LeftX,(float)Math.Sin(angle));Axis(JoyAxis.LeftY,-(float)Math.Cos(angle));
        }
        bool valid=true;
        Axis(JoyAxis.RightX,1);Axis(JoyAxis.RightY,-1);
        valid &= BattingControls.Read().RightX==0 && BattingControls.Read().RightY==0;
        Axis(JoyAxis.RightX,0);Axis(JoyAxis.RightY,0);
        foreach(var (button,shot) in new[]{(JoyButton.X,ShotStyle.Defence),(JoyButton.A,ShotStyle.FullDrive),
            (JoyButton.B,ShotStyle.Push),(JoyButton.Y,ShotStyle.HighFinish)})
        {
            Hold(button,true);
            valid &= BattingControls.Read().Shot==shot && !_menu.Visible && !_hud.DeliveryPanel.Visible;
            var rig=new BattingRig(_settings,_batting);
            Clock(90);
            for(int i=0;i<120;i++)rig.Step(BattingControls.Read(),SimulationSettings.Tick,_settings);
            valid &= rig.Frame.SwingProgress==0;
            Axis(JoyAxis.LeftX,0);Axis(JoyAxis.LeftY,0);
            rig.Step(BattingControls.Read(),SimulationSettings.Tick,_settings);
            for(int clock=255;clock>=0;clock--)
            {
                Clock(clock);
                rig.Step(BattingControls.Read(),SimulationSettings.Tick,_settings);
                valid &= rig.BatTipSpeed<=136/3.6+1e-6;
            }
            for(int i=0;i<180;i++)rig.Step(BattingControls.Read(),SimulationSettings.Tick,_settings);
            double stop=shot==ShotStyle.Defence?60:shot==ShotStyle.Push?45:0;
            valid &= Math.Abs(rig.SwingClockDegrees-stop)<.001 && rig.Frame.Shot==shot;
            Hold(button,false);
            valid &= BattingControls.Read().Shot==null;
            for(int i=0;i<120;i++)rig.Step(BattingControls.Read(),SimulationSettings.Tick,_settings);
            var held=rig.Pose;
            Axis(JoyAxis.LeftX,0);Axis(JoyAxis.LeftY,0);
            for(int i=0;i<120;i++)rig.Step(BattingControls.Read(),SimulationSettings.Tick,_settings);
            valid &= rig.Pose==held && rig.Frame.Shot==shot;
        }
        Axis(JoyAxis.TriggerRight,.5f);Axis(JoyAxis.TriggerLeft,.25f);
        valid &= Math.Abs(BattingControls.Read().FrontWeight-.5)<.001 && Math.Abs(BattingControls.Read().BackWeight-.25)<.001;
        Axis(JoyAxis.TriggerRight,0);Axis(JoyAxis.TriggerLeft,0);
        var rehearsal=new Simulation(_settings,_batting);
        foreach(double clock in new[]{315.0,300,285,270,255,243,210,180})
        {
            Clock(clock);rehearsal.Tick(BattingControls.Read());
        }
        for(int i=0;i<240;i++)rehearsal.Tick(BattingControls.Read());
        valid &= Math.Abs(rehearsal.Batter.SwingClockDegrees-180)<.001 &&
            rehearsal.Phase==DeliveryPhase.Setup && rehearsal.TickCount==0;
        Axis(JoyAxis.LeftX,0);Axis(JoyAxis.LeftY,0);
        GD.Print(valid?"SMOKE PASS: capped swing, held shot release, shot endpoints, idle rehearsal and triggers.":
            "SMOKE FAIL: batting input or shot mapping.");
        return valid;
    }

    private void SaveCapture(string path)
    {
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(path) != Error.Ok) throw new InvalidOperationException($"Capture failed: {path}");
    }

    private async void CapturePreview()
    {
        // Developer-only render verification. Saves only this game's viewport.
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts"));
        SaveCapture("res://artifacts/main-menu.png");
        _menu.ShowSettings();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/main-settings.png");
        _menu.ShowMain();
        SmokeButton(JoyButton.DpadDown);
        SmokeButton(JoyButton.DpadDown);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/main-menu-selected-exit.png");
        _menu.ShowPractice();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/practice-menu.png");
        StartBatting();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/idle.png");
        foreach(ShotStyle shot in Enum.GetValues<ShotStyle>())
        {
            for(int i=0;i<240;i++)_simulation.Batter.Step(default,1.0/120,_settings);
            for(int i=0;i<=SwingGesture.StartClockDegrees;i++)
            {
                double angle=(SwingGesture.StartClockDegrees-i)*Math.PI/180;
                _simulation.Batter.Step(new(Math.Sin(angle),-Math.Cos(angle),0,0,Shot:shot),1.0/120,_settings);
            }
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            SaveCapture($"res://artifacts/batting-{shot}.png");
        }
        OpenSetup();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/setup.png");
        var bowler = (OptionButton)_hud.DeliveryPanel.FindChild("BowlerSelector", true, false);
        bowler.ShowPopup(); bowler.GetPopup().SetFocusedItem(bowler.Selected);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/setup-bowler.png");
        _hud.DeliveryPanel.CloseBowlerList();
        _hud.DeliveryPanel.SetSettings(_hud.DeliveryPanel.Settings.WithManualSpeed(115.2) with { RandomPitchLength = false, RandomSpeed = false });
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/setup-manual.png");
        var manualHeight = (HSlider)_hud.DeliveryPanel.FindChild("ReleaseHeightSlider", true, false);
        manualHeight.GrabFocus();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/setup-manual-scrolled.png");
        var length = (PitchLengthSlider)_hud.DeliveryPanel.FindChild("PitchLengthSlider", true, false);
        length.GrabFocus();
        foreach (double metres in new[] { 20.12, 25 })
        {
            length.Value = metres;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            SaveCapture($"res://artifacts/setup-length-{metres:0.00}.png");
        }
        length.Value = 16;
        if (!await WaitForDelivery()) { GD.PushError("Capture delivery preparation timed out."); GetTree().Quit(1); return; }
        _hud.DeliveryPanel.Hide();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/practice-guides-on.png");
        var pitchGuide = (CheckBox)_hud.DeliveryPanel.FindChild("ShowPitchPosition", true, false);
        var trajectoryGuide = (CheckBox)_hud.DeliveryPanel.FindChild("ShowPredictedTrajectory", true, false);
        pitchGuide.ButtonPressed = false; trajectoryGuide.ButtonPressed = false;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/practice-guides-off.png");
        pitchGuide.ButtonPressed = true; trajectoryGuide.ButtonPressed = true;
        Begin();
        for (int i = 0; i < 260; i++) _simulation.Tick(default);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/ground.png");
        OpenPauseMenu();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/pause-menu.png");
        OpenPracticeSettings();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        SaveCapture("res://artifacts/pause-settings.png");
        Resume();
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
            SaveCapture($"res://artifacts/{name}.png");
        }
        GetTree().Quit();
    }
}
