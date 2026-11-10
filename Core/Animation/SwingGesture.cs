namespace Cricket.Physics;

/// <summary>An absolute 8:30 -> 6 -> 3 -> 12 arc, gated against radial pushes and centre crossings.</summary>
public sealed class SwingGesture
{
    public const double StartClockDegrees = 255;
    private bool _tracking;
    private double _previousAngle, _clock, _anchorClock;
    public bool Engaged { get; private set; }
    public double Target { get; private set; }

    public void Step(double x, double y, StickSettings settings)
    {
        double radius = double.IsFinite(x) && double.IsFinite(y) ? Math.Sqrt(x*x+y*y) : 0;
        if (radius < settings.LeftDeadZone) { Reset(); return; }
        double angle = ClockCurve.Wrap(Math.Atan2(x,-y)*180/Math.PI);
        if (!_tracking)
        {
            if (radius < settings.SwingEngageRadius) return;
            // Keep looking for the start zone; an earlier off-angle push must not lock out rotation.
            if (Math.Abs(ClockCurve.SignedDelta(angle-StartClockDegrees)) > settings.StartWindowDegrees+1e-4)
                return;
            _tracking=true;
            _previousAngle=_clock=_anchorClock=angle;
            return;
        }
        double delta=ClockCurve.SignedDelta(angle-_previousAngle);
        _previousAngle=angle;
        if (Math.Abs(delta)>settings.MaxSampleArcDegrees)
        { Reset(); return; }
        _clock+=delta;
        // Leaving the legal arc completes this gesture. Keep seeking the start on the rim,
        // rather than accumulating another revolution and pinning every later stroke at 12.
        if(Engaged && (_clock<-.0001 || _clock>StartClockDegrees+.0001)) { Reset(); return; }
        if (!Engaged)
        {
            // Moving back toward the start establishes the newest anchor. Do not make the
            // player rotate past an older, lower clock position before the next swing can start.
            if (_clock>StartClockDegrees+settings.StartWindowDegrees+1e-4) { Reset(); return; }
            _anchorClock=Math.Max(_anchorClock,_clock);
            if (_clock>StartClockDegrees+.0001 || _anchorClock-_clock < settings.RotationThresholdDegrees) return;
            Engaged=true;
        }
        // Keep 6, 3 and 12 at their actual stick positions; do not subtract the arming arc.
        Target=Math.Clamp((StartClockDegrees-_clock)/StartClockDegrees,0,1);
    }

    public void Reset() { _tracking=Engaged=false; _previousAngle=_clock=_anchorClock=Target=0; }
}
