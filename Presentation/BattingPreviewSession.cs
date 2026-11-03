using Cricket.Physics;

namespace Cricket2D.Presentation;

public enum BattingDemo { Live, Slow, Fast, Reverse, Snap, Wrist }

/// <summary>Isolated fixed-tick rehearsal. Never owns a delivery or writes configuration.</summary>
public sealed class BattingPreviewSession
{
    public SimulationSettings Physics { get; }
    public BattingSettings Settings { get; private set; }
    public BattingRig Rig { get; private set; }
    public BattingFrame PreviousFrame { get; private set; }
    public BattingDemo Demo { get; private set; }
    public bool Paused { get; set; }
    public bool Loop { get; set; } = true;
    public long TickCount { get; private set; }
    public double Time => TickCount * SimulationSettings.Tick;
    public double Duration => Demo is BattingDemo.Slow or BattingDemo.Wrist ? 4 : Demo == BattingDemo.Reverse ? 6 : 3;
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
    public BattingFrame RenderFrame => BattingFrame.Interpolate(PreviousFrame, Rig.Frame, _accumulator / SimulationSettings.Tick);
    public HumanPose RenderPose => BattingRig.BuildPose(RenderFrame, Settings);

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
        _accumulator = 0;
    }

    public void Advance(double elapsedSeconds, BattingInput liveInput = default)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (Paused) return;
        _accumulator += elapsedSeconds * PlaybackSpeed;
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
        double dt = SimulationSettings.Tick / Physics.Substeps;
        for (int i = 0; i < Physics.Substeps; i++)
            Rig.Step(Demo == BattingDemo.Live ? liveInput : Demonstration(Time + i * dt), dt, Physics);
        TickCount++;
    }

    private BattingInput Demonstration(double time)
    {
        double end = Settings.Swing.Snap.EndClockDegrees;
        double arc = 360 - Settings.Swing.Snap.ArcDegrees;
        double t = Math.Max(0, time - .35);
        double progress;
        double? wrist = null;
        switch (Demo)
        {
            case BattingDemo.Reverse:
                progress = t < .7 ? Math.Min(arc - 8, t * 450) : Math.Max(0, arc - 8 - (t - .7) * 180);
                break;
            case BattingDemo.Wrist:
                progress = t < 1.8 ? Math.Min(arc * .6, t * 450) : 0;
                if (t > .55 && t < 1.8) wrist = end - progress + Math.Sin((t - .55) * 10) * 110;
                break;
            default:
                progress = Math.Min(arc, t * (Demo == BattingDemo.Slow ? 180 : 720));
                // Hold the threshold through the snap, then explicitly rearm at guard.
                if (time > (Demo == BattingDemo.Slow ? 2.2 : 1.25)) progress = 0;
                if (Demo == BattingDemo.Snap && Rig.IsSnapping) progress = arc - 30;
                break;
        }
        double left = (end - progress) * Math.PI / 180;
        double right = (wrist ?? 0) * Math.PI / 180;
        return new(Math.Sin(left), -Math.Cos(left), wrist.HasValue ? Math.Sin(right) : 0, wrist.HasValue ? -Math.Cos(right) : 0);
    }
}
