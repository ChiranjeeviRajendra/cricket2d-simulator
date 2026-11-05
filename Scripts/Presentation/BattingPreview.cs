using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>Developer-only rehearsal, created instead of the delivery scene.</summary>
public partial class BattingPreview : Node2D
{
    public BattingPreviewSession Session { get; set; } = null!;
    private Label _telemetry = null!, _status = null!;
    private Button _pause = null!, _step = null!, _reload = null!;
    private OptionButton _demo = null!, _speed = null!;
    private Font _font = null!;
    private BatterModel _batter = null!;
    private HSlider _frontWeight = null!, _backWeight = null!;
    private bool _markers = true, _focusPaused, _automating, _automationStarted;
    private double _telemetryTime;
    private BattingInput _liveInput;
    private const float PreviewPixelsPerMetre = 210;

    public override void _Ready()
    {
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        _font = new SystemFont { FontNames = ["Bahnschrift", "Segoe UI"] };
        _batter = new BatterModel();
        AddChild(_batter);
        var panel = new PanelContainer { Position = new(1035, 100), Size = new(370, 730), Theme = VisualTheme.Create() };
        panel.AddThemeStyleboxOverride("panel", VisualTheme.Box(VisualTheme.Panel, VisualTheme.Border));
        AddChild(panel);
        var controls = new VBoxContainer();
        controls.AddThemeConstantOverride("separation", 14);
        panel.AddChild(controls);
        controls.AddChild(VisualTheme.Label("BATTING LAB", 25, VisualTheme.Accent));
        controls.AddChild(VisualTheme.Label("Rehearse • inspect • tune", 16, VisualTheme.Muted));
        _demo = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (BattingDemo demo in Enum.GetValues<BattingDemo>()) _demo.AddItem(demo switch
        {
            BattingDemo.Live => "Live sticks / keyboard",
            BattingDemo.FullDrive => "A — straight arc (default)",
            BattingDemo.Defence => "X — wall, stop at 2",
            BattingDemo.Push => "B — stop at 1:30, blade at 4",
            BattingDemo.HighFinish => "Y — sky face, stop at 12",
            _ => demo + " demonstration"
        });
        _demo.ItemSelected += index => Session.Select((BattingDemo)index);
        controls.AddChild(_demo);
        _speed = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (string name in new[] { "25% playback", "50% playback", "100% playback" }) _speed.AddItem(name);
        _speed.Select(2);
        _speed.ItemSelected += index => Session.PlaybackSpeed = new[] { .25, .5, 1 }[index];
        controls.AddChild(_speed);
        var loop = new CheckButton { Text = "Loop demonstration", ButtonPressed = true, FocusMode = Control.FocusModeEnum.None };
        loop.Toggled += value => Session.Loop = value;
        controls.AddChild(loop);
        controls.AddChild(VisualTheme.Label("R2 / E: front-foot weight", 16, VisualTheme.Muted));
        _frontWeight = new HSlider { MinValue = 0, MaxValue = 1, Step = .01, FocusMode = Control.FocusModeEnum.None };
        controls.AddChild(_frontWeight);
        controls.AddChild(VisualTheme.Label("L2 / Q: back-foot weight", 16, VisualTheme.Muted));
        _backWeight = new HSlider { MinValue = 0, MaxValue = 1, Step = .01, FocusMode = Control.FocusModeEnum.None };
        controls.AddChild(_backWeight);
        var row = new HBoxContainer();
        controls.AddChild(row);
        _pause = Button("Pause", () => Session.Paused = !Session.Paused, row);
        _step = Button("Single tick", () => Session.SingleStep(BattingControls.Read()), row);
        Button("Reset", Session.Reset, row);
        _reload = Button("Reload JSON", ReloadJson, controls);
        var markers = new CheckButton { Text = "Joint markers", ButtonPressed = true, FocusMode = Control.FocusModeEnum.None };
        markers.Toggled += value => _markers = value;
        controls.AddChild(markers);
        _telemetry = VisualTheme.Label("", 17);
        controls.AddChild(_telemetry);
        _status = VisualTheme.Label("Config/batsman.json\nEdit, save, then reload here.", 15, VisualTheme.Muted);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        controls.AddChild(_status);
        GetWindow().FocusExited += () => { _focusPaused = true; if (!_automating) Engine.MaxFps = 30; };
        GetWindow().FocusEntered += () => { _focusPaused = false; if (!_automating) Engine.MaxFps = 0; };
        _automating = OS.GetCmdlineUserArgs().Contains("--smoke-test") || OS.GetCmdlineUserArgs().Contains("--capture-preview");
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_focusPaused && !_automating) return;
        if (_automating)
        {
            if (!_automationStarted)
            {
                _automationStarted = true;
                if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) Smoke();
                else Capture();
            }
        }
        else if (!_focusPaused)
        {
            var input = BattingControls.Read();
            _liveInput=input;
            Session.Advance(Math.Min(delta, .25), input with
            { FrontWeight = Math.Max(input.FrontWeight, _frontWeight.Value), BackWeight = Math.Max(input.BackWeight, _backWeight.Value) });
        }
        _batter.UpdatePose(Session.RenderPose, Session.Physics, PreviewPixelsPerMetre, GetViewport().GetStretchTransform().Scale.X);
        _telemetryTime += delta;
        if (_telemetryTime >= .1) { _telemetryTime = 0; Refresh(); }
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent evt)
    {
        if (BattingControls.IsPause(evt))
        {
            Session.Paused = !Session.Paused;
            if(Session.Demo==BattingDemo.Live) Session.Rig.SuspendGesture();
        }
        else if (evt is InputEventKey { Pressed:true,Echo:false,Keycode:Key.Period }) Session.SingleStep(BattingControls.Read());
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        DrawRect(new(0, 0, 1440, 900), VisualTheme.Background);
        Text(new(55, 55), "BATTER / MOTION PREVIEW", 24, VisualTheme.OnDark);
        Text(new(55, 84), "Start left stick at 8:30 and rotate through 6 • no button selects A", 16, VisualTheme.MutedOnDark);
        for (int i = -3; i <= 3; i++)
        {
            float x = 535 + i * PreviewPixelsPerMetre * .5f;
            DrawLine(new(x, 125), new(x, 760), new Color(1, 1, 1, .035f));
        }
        for (int i = 0; i <= 6; i++)
            DrawLine(new(65, 760 - i * PreviewPixelsPerMetre * .5f), new(1000, 760 - i * PreviewPixelsPerMetre * .5f), new Color(1, 1, 1, .035f));
        DrawRect(new(65, 760, 935, 18), new("6c7053"));
        DrawLine(new(65, 760), new(1000, 760), VisualTheme.Muted, 1);
        _batter.Draw(this, W(new(PitchDimensions.BatterReferenceX, 0)), PreviewPixelsPerMetre);
        if (_markers) DrawMarkers();
        Text(new(65, 815), "LS: 8:30 → 6 → 3 → 12 • X/J: wall at 2  A/K: default  B/L: 1:30  Y/I: sky", 17, VisualTheme.OnDark);
        Text(new(65, 844), "R2/E: front  L2/Q: back • Menu/Space: pause • . : one tick • Bowler →", 16, VisualTheme.MutedOnDark);
    }

    private void Refresh()
    {
        BattingFrame frame = Session.RenderFrame;
        string mode=frame.Shot switch { ShotStyle.Defence=>"X", ShotStyle.Push=>"B", ShotStyle.HighFinish=>"Y", _=>"A" };
        var input=Session.Demo==BattingDemo.Live?_liveInput:Session.LastInput;
        double radius=Math.Sqrt(input.LeftX*input.LeftX+input.LeftY*input.LeftY);
        string stickClock=radius<Session.Settings.Input.LeftDeadZone?"centred":
            FormatClock(Math.Atan2(input.LeftX,-input.LeftY)*180/Math.PI);
        _pause.Text = Session.Paused ? "Resume" : "Pause";
        _telemetry.Text = $"{frame.State}   •   {Session.Time:0.000} s\n" +
            $"Stroke   {frame.SwingProgress:P0}\nWeight   {frame.WeightShift:+0.00;-0.00;0.00}\n" +
            $"Mode {mode}    Arm {FormatClock(Session.Rig.SwingClockDegrees)}\nStick {stickClock}    {(Session.Rig.GestureEngaged?"following":"waiting for arc")}\n" +
            $"Front foot   {frame.FrontFoot.Contact}\nRear foot   {frame.RearFoot.Contact}";
    }

    private void DrawMarkers()
    {
        var p = Session.RenderPose;
        var input=Session.Demo==BattingDemo.Live?_liveInput:Session.LastInput;
        Vec2 stick=new(-input.LeftX,-input.LeftY);
        if(stick.Length>Session.Settings.Input.LeftDeadZone)
            DrawLine(W(p.Shoulder),W(p.Shoulder+stick.Normalized*.72),VisualTheme.Coral,2);
        DrawLine(W(p.Shoulder),W(p.Hands),VisualTheme.Accent,2);
        DrawCircle(W(p.Hands), 3, VisualTheme.Coral);
        DrawCircle(W(p.BottomHand), 3, VisualTheme.Accent);
        var blade = p.Bat.Blade(Session.Physics);
        DrawLine(W(blade.A), W(blade.B), new Color(1, .4f, .2f, .55f), 1);
    }

    private static string FormatClock(double degrees)
    {
        int minutes=(int)Math.Round(ClockCurve.Wrap(degrees)*2)%720;
        int hour=minutes/60;
        return $"{(hour==0?12:hour)}:{minutes%60:00}";
    }

    private void ReloadJson()
    {
        try
        {
            using var file = Godot.FileAccess.Open("res://Config/batsman.json", Godot.FileAccess.ModeFlags.Read);
            if (file == null) throw new InvalidOperationException("Cannot read Config/batsman.json.");
            ApplyJson(file.GetAsText());
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException)
        { ShowStatus(ex.Message, true); }
    }

    private bool ApplyJson(string json)
    {
        bool ok = Session.TryReload(json, out string error);
        ShowStatus(ok ? "JSON reloaded. Preview reset." : "Reload failed; keeping current settings.\n" + error, !ok);
        return ok;
    }

    private void ShowStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? VisualTheme.Coral : VisualTheme.Accent);
    }

    private static Button Button(string title, Action action, Node parent)
    {
        var button = new Button { Text = title, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private void Smoke()
    {
        try
        {
            _demo.EmitSignal(OptionButton.SignalName.ItemSelected, (long)BattingDemo.Fast);
            _speed.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            Session.Advance(.4);
            Require(Session.TickCount == 12, "quarter-speed playback");
            foreach(bool expected in new[]{true,false})
            {
                foreach(bool pressed in new[]{true,false})
                {
                    Godot.Input.ParseInputEvent(new InputEventJoypadButton { Device=0,ButtonIndex=JoyButton.Start,Pressed=pressed });
                    Godot.Input.FlushBufferedEvents();
                }
                Require(Session.Paused==expected,"controller Start pause/resume");
            }
            _pause.EmitSignal(BaseButton.SignalName.Pressed);
            var frozen = Session.RenderFrame;
            Session.Advance(1);
            Require(Session.RenderFrame == frozen, "pause");
            _step.EmitSignal(BaseButton.SignalName.Pressed);
            Require(Session.TickCount == 13 && Session.Paused, "single tick");
            var rig = Session.Rig;
            Require(!ApplyJson("{\"Body\":{\"FrontStrideMetres\":20}}") && ReferenceEquals(rig, Session.Rig), "invalid reload");
            _reload.EmitSignal(BaseButton.SignalName.Pressed);
            Require(Session.TickCount == 0 && !ReferenceEquals(rig, Session.Rig), "reload button");
            GD.Print("BATTING PREVIEW SMOKE PASS: controls, playback, pause, step, invalid/valid reload");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
        static void Require(bool valid, string name) { if (!valid) throw new InvalidOperationException(name); }
    }

    private async void Capture()
    {
        try
        {
            string directory = ProjectSettings.GlobalizePath("res://artifacts/batsman-preview");
            System.IO.Directory.CreateDirectory(directory);
            _markers = false;
            CaptureRigMotion();
            foreach (var demo in new[] { BattingDemo.Fast, BattingDemo.FrontFoot, BattingDemo.BackFoot, BattingDemo.Defence, BattingDemo.FullDrive, BattingDemo.Push, BattingDemo.HighFinish })
            {
                Session.Select(demo);
                _demo.Select((int)demo);
                foreach (double target in new[] { 0, .55, .75, .80, .90, .92, .99, 1.05, 1.3, 1.75, 2.1, 3.4 })
                {
                    while (Session.Time + SimulationSettings.Tick / 2 < target) Session.Advance(1.0 / 120);
                    _batter.UpdatePose(Session.RenderPose, Session.Physics, PreviewPixelsPerMetre, GetViewport().GetStretchTransform().Scale.X);
                    Refresh();
                    QueueRedraw();
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    string path = System.IO.Path.Combine(directory, $"{demo}-{target:0.00}.png");
                    using var image = GetViewport().GetTexture().GetImage();
                    if (image.SavePng(path) != Error.Ok) throw new InvalidOperationException($"Capture failed: {path}");
                }
            }
            GD.Print("BATTING PREVIEW CAPTURE PASS: artifacts/batsman-preview");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private static Vector2 W(Vec2 point) => new(535 - (float)(point.X - PitchDimensions.BatterReferenceX) * PreviewPixelsPerMetre, 760 - (float)point.Y * PreviewPixelsPerMetre);
    private void Text(Vector2 at, string text, int size, Color color) => DrawString(_font, at, text, HorizontalAlignment.Left, -1, size, color);
}
