using Cricket.Physics;

namespace Cricket2D.Presentation;

public enum BattingDemo { Live, Slow, Fast, Reverse, FrontFoot, BackFoot, Defence, FullDrive, Push, HighFinish }

/// <summary>Isolated fixed-tick rehearsal. Never owns a delivery or writes configuration.</summary>
public sealed class BattingPreviewSession
{
    public SimulationSettings Physics { get; }
    public BattingSettings Settings { get; private set; }
    public BattingRig Rig { get; private set; }
    public BattingFrame PreviousFrame { get; private set; }
    public BattingInput LastInput { get; private set; }
    public BattingDemo Demo { get; private set; }
    public bool Paused { get; set; }
    public bool Loop { get; set; } = true;
    public long TickCount { get; private set; }
    public double Time => TickCount * SimulationSettings.Tick;
    public double Duration => Demo == BattingDemo.Slow ? 4 : Demo == BattingDemo.Reverse ? 6 : 3.5;
    private double _accumulator, _playbackSpeed = 1;
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (value is not (.25 or .5 or 1)) throw new ArgumentOutOfRangeException(nameof(value));
            _playbackSpeed = value;
        }
    }
    public BattingFrame RenderFrame => Rig.Frame;
    public HumanPose RenderPose => Rig.Pose;

    public BattingPreviewSession(SimulationSettings physics, BattingSettings settings)
    {
        Physics = physics;
        Settings = settings;
        Rig = new(physics, settings);
        PreviousFrame = Rig.Frame;
    }

    public void Select(BattingDemo demo) { Demo = demo; Reset(); }
    public void Reset()
    {
        Rig = new(Physics, Settings);
        PreviousFrame = Rig.Frame;
        TickCount = 0;
        LastInput=default;
        _accumulator = 0;
    }

    public void Advance(double elapsedSeconds, BattingInput liveInput = default)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (Paused) return;
        // Bound catch-up work after a long suspension; the normal view already clamps to 0.25 s.
        _accumulator += Math.Min(elapsedSeconds, .5) * PlaybackSpeed;
        while (_accumulator + 1e-12 >= SimulationSettings.Tick)
        {
            _accumulator = Math.Max(0, _accumulator - SimulationSettings.Tick);
            Tick(liveInput);
            if (Demo != BattingDemo.Live && Time >= Duration)
            {
                if (!Loop) { Paused = true; PreviousFrame = Rig.Frame; _accumulator = 0; break; }
                double remainder = _accumulator;
                Reset();
                _accumulator = remainder;
            }
        }
    }

    public void SingleStep(BattingInput liveInput = default)
    {
        Paused = true;
        Tick(liveInput);
        // A stepped frame is displayed immediately and remains frozen until the next action.
        PreviousFrame = Rig.Frame;
        _accumulator = 0;
    }

    public bool TryReload(string json, out string error)
    {
        try
        {
            BattingSettings replacement = BattingSettings.FromJson(json, Physics);
            var rig = new BattingRig(Physics, replacement);
            Settings = replacement;
            Rig = rig;
            PreviousFrame = rig.Frame;
            LastInput=default;
            TickCount = 0;
            _accumulator = 0;
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
        {
            error = ex.Message;
            return false;
        }
    }

    private void Tick(BattingInput liveInput)
    {
        PreviousFrame = Rig.Frame;
        LastInput=Demo == BattingDemo.Live ? liveInput : Demonstration(Time);
        Rig.Step(LastInput, SimulationSettings.Tick, Physics);
        TickCount++;
    }

    private BattingInput Demonstration(double time)
    {
        double t = Math.Max(0, time - .4);
        double progress = Math.Clamp(t * (Demo == BattingDemo.Slow ? .5 : 1.5), 0, 1);
        if (time > (Demo == BattingDemo.Slow ? 2.8 : 2.2)) progress = 0;
        if (Demo == BattingDemo.Reverse)
            progress = time < 1.1 ? Math.Clamp((time - .4) * 1.1, 0, .65) :
                time < 2 ? .65 - Math.Clamp((time - 1.1) * .7, 0, .6) :
                time < 3.1 ? Math.Clamp((time - 2) * 1.4, 0, 1) : 0;
        double left = SwingGesture.StartClockDegrees * (1 - progress) * Math.PI / 180;
        double pressure = time is >= .35 and < 2.3 ? 1 : 0;
        double front = Demo == BattingDemo.FrontFoot ? pressure : 0;
        double back = Demo == BattingDemo.BackFoot ? pressure : 0;
        ShotStyle shot=Demo switch { BattingDemo.Defence=>ShotStyle.Defence, BattingDemo.FullDrive=>ShotStyle.FullDrive,
            BattingDemo.Push=>ShotStyle.Push, BattingDemo.HighFinish=>ShotStyle.HighFinish, _=>ShotStyle.FullDrive };
        if(Demo!=BattingDemo.Reverse && time>(Demo==BattingDemo.Slow?2.8:2.2))
            return new(0,0,0,0,front,back,shot);
        return new(Math.Sin(left),-Math.Cos(left),0,0,front,back,shot);
    }
}
