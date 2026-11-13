using System.Diagnostics;
using System.Text.Json;
using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Opt-in, real rendered benchmark; writes measurements and exits without altering settings.</summary>
internal partial class PerformanceRun(Simulation simulation, GroundView ground, GameHud hud, MainMenu menu) : Node
{
    private static readonly string[] Scenarios = ["ready", "live", "wide", "camera-motion", "settings-drag", "delivery-resets", "menu", "random-practice"];
    private readonly List<object> _results = [];
    private readonly List<double> _frames = new(16384), _process = new(16384), _physics = new(16384), _drawCalls = new(16384);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Process _processInfo = Process.GetCurrentProcess();
    private double _start, _previous, _sampleStart, _cpuStart;
    private long _allocated;
    private int _gen0, _gen1, _gen2, _scenario = -1;
    private bool _sampling;
    private double _duration = 6;
    private string _output = "res://artifacts/performance.json";
    private HSlider? _speedSlider;
    private int _resets;
    private int _randomRevision;
    private int _rebuildStart;
    private int _cycles = 1, _cycle = 1;
    public BattingInput Input
    {
        get
        {
            if(_scenario is not (1 or 2 or 3)) return default;
            double time=_clock.Elapsed.TotalSeconds;
            double phase=time%3.5;
            double pressure=Math.Sin(time);
            ShotStyle shot=(ShotStyle)((int)(time/3.5)%4);
            if(phase>2.2) return new(0,0,0,0,Math.Max(0,pressure),Math.Max(0,-pressure),shot);
            double angle=(SwingGesture.StartClockDegrees*(1-Math.Clamp((phase-.2)/.65,0,1)))*Math.PI/180;
            return new(Math.Sin(angle),-Math.Cos(angle),0,0,Math.Max(0,pressure),Math.Max(0,-pressure),shot);
        }
    }

    public override void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--performance-output=")) _output = "res://" + arg.Split('=', 2)[1];
            if (arg.StartsWith("--performance-seconds=") && double.TryParse(arg.Split('=', 2)[1], out double seconds))
                _duration = Math.Clamp(seconds, 2, 120);
            if (arg.StartsWith("--performance-cycles=") && int.TryParse(arg.Split('=', 2)[1], out int cycles))
                _cycles = Math.Clamp(cycles, 1, 10);
            if (arg == "--performance-uncapped") { Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); }
        }
        Next();
    }

    public override void _Process(double delta)
    {
        double now = _clock.Elapsed.TotalSeconds, elapsed = now - _start;
        if (_scenario >= Scenarios.Length) return;
        if (_scenario == 3)
            ground.SetCaptureBall(new(-45 - 70 * Math.Sin(elapsed * .8), 15 + 25 * Math.Sin(elapsed * .5)));
        if (_scenario == 4) _speedSlider!.Value = 99 + 81 * Math.Sin(elapsed * 2);
        if (_scenario == 5 && elapsed >= _resets)
        {
            _resets++; simulation.ReturnToSetup(); simulation.Begin(new(32, 16, 2.15), out _);
        }
        if (_scenario == 7 && elapsed >= _randomRevision * .25)
        {
            hud.DeliveryPanel.SetSettings(hud.DeliveryPanel.Settings.WithBowler((BowlerType)(_randomRevision % 5)) with
                { ReleaseHeight = 1.53 + _randomRevision % 64 / 100.0 });
            _randomRevision++;
        }
        if (!_sampling && elapsed >= 2)
        {
            _sampling = true; _sampleStart = now;
            _allocated = GC.GetTotalAllocatedBytes();
            _gen0 = GC.CollectionCount(0); _gen1 = GC.CollectionCount(1); _gen2 = GC.CollectionCount(2);
            _cpuStart = _processInfo.TotalProcessorTime.TotalSeconds;
            _rebuildStart = ground.SceneryRebuilds;
        }
        else if (_sampling)
        {
            _frames.Add((now - _previous) * 1000);
            _process.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000);
            _physics.Add(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000);
            _drawCalls.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
        }
        _previous = now;
        if (_sampling && now - _sampleStart >= _duration)
        {
            _processInfo.Refresh();
            var result = new
            {
                Cycle = _cycle, Scenario = Scenarios[_scenario], Frames = _frames.Count,
                Fps = _frames.Count / (now - _sampleStart),
                FrameMsMedian = Percentile(_frames, .5), FrameMsP95 = Percentile(_frames, .95), FrameMsP99 = Percentile(_frames, .99),
                FpsOnePercentLow = OnePercentLow(_frames),
                ProcessMsP95 = Percentile(_process, .95), PhysicsMsP95 = Percentile(_physics, .95),
                DrawCallsMedian = Percentile(_drawCalls, .5),
                SceneryRebuilds = ground.SceneryRebuilds - _rebuildStart,
                AllocatedBytesPerFrame = (GC.GetTotalAllocatedBytes() - _allocated) / Math.Max(1, _frames.Count),
                Gen0 = GC.CollectionCount(0) - _gen0, Gen1 = GC.CollectionCount(1) - _gen1, Gen2 = GC.CollectionCount(2) - _gen2,
                CpuCorePercent = (_processInfo.TotalProcessorTime.TotalSeconds - _cpuStart) / (now - _sampleStart) * 100,
                WorkingSetMiB = _processInfo.WorkingSet64 / 1048576.0, PrivateMiB = _processInfo.PrivateMemorySize64 / 1048576.0,
                GodotObjects = Performance.GetMonitor(Performance.Monitor.ObjectCount),
                GodotResources = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount)
            };
            _results.Add(result); GD.Print(JsonSerializer.Serialize(result));
            Next();
        }
    }

    private void Next()
    {
        _scenario++; _sampling = false;
        if (_scenario == Scenarios.Length)
        {
            if (_cycle < _cycles) { _cycle++; _scenario = -1; Next(); return; }
            string path = ProjectSettings.GlobalizePath(_output);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Read back once after all samples. ViewportTexture.GetSize() includes the
            // canvas stretch transform on some Godot versions, so use the real image.
            using var image = GetViewport().GetTexture().GetImage();
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                Utc = DateTime.UtcNow, Resolution = DisplayServer.WindowGetSize().ToString(),
                Viewport = GetViewport().GetVisibleRect().Size.ToString(),
                RenderTarget = image.GetSize().ToString(),
                Gpu = RenderingServer.GetVideoAdapterName(), Renderer = RenderingServer.GetCurrentRenderingMethod(),
                MaxFps = Engine.MaxFps, Vsync = DisplayServer.WindowGetVsyncMode().ToString(), Results = _results
            }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"PERFORMANCE COMPLETE: {path}"); GetTree().Quit(); return;
        }
        ground.SetCaptureBall(null);
        if (_scenario == 0) { menu.Hide(); ground.Show(); hud.Show(); hud.DeliveryPanel.Hide(); simulation.ReturnToSetup(); }
        if (_scenario == 1) simulation.Begin(new(32, 16, 2.15), out _);
        if (_scenario == 2) ground.SetCaptureBall(new(-90, 20));
        if (_scenario == 4)
        {
            simulation.ReturnToSetup(); hud.DeliveryPanel.Open();
            hud.DeliveryPanel.SetSettings(hud.DeliveryPanel.Settings with { RandomPitchLength = false, RandomSpeed = false });
            _speedSlider = (HSlider)hud.DeliveryPanel.FindChild("ReleaseSpeedSlider", true, false);
        }
        if (_scenario == 5) { hud.DeliveryPanel.Hide(); _resets = 0; }
        if (_scenario == 6) { ground.Hide(); hud.Hide(); menu.ShowMain(); }
        if (_scenario == 7)
        {
            menu.Hide(); ground.Show(); hud.Show(); simulation.ReturnToSetup();
            hud.DeliveryPanel.Open(); hud.DeliveryPanel.SetSettings(new()); _randomRevision = 0;
        }
        _frames.Clear(); _process.Clear(); _physics.Clear(); _drawCalls.Clear();
        _start = _previous = _clock.Elapsed.TotalSeconds;
        GD.Print($"PERFORMANCE START: {Scenarios[_scenario]}");
    }

    private static double Percentile(List<double> data, double fraction)
    {
        data.Sort();
        return data.Count == 0 ? 0 : data[(int)((data.Count - 1) * fraction)];
    }
    private static double OnePercentLow(List<double> data)
    {
        // Percentile has already sorted the frame samples. Average the slowest 1%.
        int count = Math.Max(1,(int)Math.Ceiling(data.Count*.01));
        double total = 0;
        for (int i = data.Count-count; i < data.Count; i++) total += data[i];
        return total > 0 ? count*1000/total : 0;
    }
    public override void _ExitTree() => _processInfo.Dispose();
}
