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
    private bool _markers = true, _focusPaused, _automating, _automationStarted;
    private const float PreviewPixelsPerMetre = 210;

    public override void _Ready()
    {
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        _font = new SystemFont { FontNames = ["Bahnschrift", "Segoe UI"] };
        var panel = new PanelContainer { Position = new(1035, 100), Size = new(370, 730), Theme = VisualTheme.Create() };
        panel.AddThemeStyleboxOverride("panel", VisualTheme.Box(VisualTheme.Panel, VisualTheme.Border));
        AddChild(panel);
        var controls = new VBoxContainer();
        controls.AddThemeConstantOverride("separation", 14);
        panel.AddChild(controls);
        controls.AddChild(VisualTheme.Label("BATTING LAB", 25, VisualTheme.Accent));
        controls.AddChild(VisualTheme.Label("Rehearse • inspect • tune", 16, VisualTheme.Muted));
        _demo = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (BattingDemo demo in Enum.GetValues<BattingDemo>()) _demo.AddItem(demo == BattingDemo.Live ? "Live sticks / keyboard" : demo + " demonstration");
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
        GetWindow().FocusExited += () => _focusPaused = true;
        GetWindow().FocusEntered += () => _focusPaused = false;
        _automating = OS.GetCmdlineUserArgs().Contains("--smoke-test") || OS.GetCmdlineUserArgs().Contains("--capture-preview");
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_automating)
        {
            if (!_automationStarted)
            {
                _automationStarted = true;
                if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) Smoke();
                else Capture();
            }
        }
        else if (!_focusPaused) Session.Advance(Math.Min(delta, .25), BattingControls.Read());
        Refresh();
        QueueRedraw();
    }

    public override void _UnhandledKeyInput(InputEvent evt)
    {
        if (evt is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Space) Session.Paused = !Session.Paused;
        else if (key.Keycode == Key.Period) Session.SingleStep(BattingControls.Read());
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        DrawRect(new(0, 0, 1440, 900), VisualTheme.Background);
        Text(new(55, 55), "BATTER / MOTION PREVIEW", 24, VisualTheme.OnDark);
        Text(new(55, 84), "Shoulder and wrist controls • isolated rehearsal", 16, VisualTheme.MutedOnDark);
        for (int i = -3; i <= 3; i++)
        {
            float x = 535 + i * PreviewPixelsPerMetre * .5f;
            DrawLine(new(x, 125), new(x, 760), new Color(1, 1, 1, .035f));
        }
        for (int i = 0; i <= 6; i++)
            DrawLine(new(65, 760 - i * PreviewPixelsPerMetre * .5f), new(1000, 760 - i * PreviewPixelsPerMetre * .5f), new Color(1, 1, 1, .035f));
        DrawRect(new(65, 760, 935, 18), new("6c7053"));
        DrawLine(new(65, 760), new(1000, 760), VisualTheme.Muted, 1);
        BatterDrawing.Draw(this, Session.RenderPose, Session.Physics, W, PreviewPixelsPerMetre, _markers);
        Text(new(65, 815), "WASD / left stick: swing     Arrows / right stick: wrist", 17, VisualTheme.OnDark);
        Text(new(65, 844), "Space: pause     . : one physics tick     Bowler →", 16, VisualTheme.MutedOnDark);
    }

    private void Refresh()
    {
        BattingFrame frame = Session.RenderFrame;
        double clock = ClockCurve.Wrap(frame.ShoulderAngle * 180 / Math.PI - 90);
        double min = ClockCurve.Sample(Session.Settings.Wrist.AnglesBySwing, clock, k => k.MinDegrees);
        double max = ClockCurve.Sample(Session.Settings.Wrist.AnglesBySwing, clock, k => k.MaxDegrees);
        _pause.Text = Session.Paused ? "Resume" : "Pause";
        _telemetry.Text = $"{frame.State}   •   {Session.Time:0.000} s\n" +
            $"Shoulder clock   {clock:0.0}°\nSpeed   {-Session.Rig.ShoulderSpeed * 180 / Math.PI:0}°/s\n" +
            $"Wrist   {frame.WristAngle * 180 / Math.PI:0.0}°\nLimits   {min:0.0}° … {max:0.0}°\n" +
            $"Front foot   {frame.FrontFoot.Contact}\nRear foot   {frame.RearFoot.Contact}\n" +
            $"Head compensation   {frame.HeadAngle * 180 / Math.PI:0.0}°\n" +
            $"Snap   {(Session.Rig.IsSnapping ? "COMMITTED" : "ready at 12 → 8")}";
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
            _pause.EmitSignal(BaseButton.SignalName.Pressed);
            var frozen = Session.RenderFrame;
            Session.Advance(1);
            Require(Session.RenderFrame == frozen, "pause");
            _step.EmitSignal(BaseButton.SignalName.Pressed);
            Require(Session.TickCount == 13 && Session.Paused, "single tick");
            var rig = Session.Rig;
            Require(!ApplyJson("{\"Footwork\":{\"StepMetres\":20}}") && ReferenceEquals(rig, Session.Rig), "invalid reload");
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
            string directory = ProjectSettings.GlobalizePath("res://artifacts/batting-preview");
            System.IO.Directory.CreateDirectory(directory);
            foreach (double speed in new[] { 1.0, .25 })
            {
                Session.Select(BattingDemo.Fast);
                _demo.Select((int)BattingDemo.Fast);
                Session.PlaybackSpeed = speed;
                _speed.Select(speed == 1 ? 2 : 0);
                foreach (double target in new[] { 0, .4, .55, .7, .825, 1.6 })
                {
                    // Advance through render frames at the selected playback speed.
                    while (Session.Time + SimulationSettings.Tick / 2 < target) Session.Advance(1.0 / 120);
                    Refresh();
                    QueueRedraw();
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    string path = System.IO.Path.Combine(directory, $"fast-{speed * 100:0}pct-{target:0.000}.png");
                    Error result = GetViewport().GetTexture().GetImage().SavePng(path);
                    if (result != Error.Ok) throw new InvalidOperationException($"Capture failed: {result}");
                }
            }
            GD.Print("BATTING PREVIEW CAPTURE PASS: artifacts/batting-preview");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private static Vector2 W(Vec2 point) => new(535 - (float)(point.X - PitchDimensions.BatterReferenceX) * PreviewPixelsPerMetre, 760 - (float)point.Y * PreviewPixelsPerMetre);
    private void Text(Vector2 at, string text, int size, Color color) => DrawString(_font, at, text, HorizontalAlignment.Left, -1, size, color);
}
