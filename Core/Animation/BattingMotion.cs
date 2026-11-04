namespace Cricket.Physics;

public enum BattingMotionState { Ready, Backlift, ForwardSwing, FollowThrough, Recovery }
public enum FootContact { Planted, Airborne, ToePivot }

/// <summary>Toe is the shoe centreline, half a shoe thickness above the pitch.</summary>
public readonly record struct FootPose(Vec2 Toe, double Angle, FootContact Contact)
{
    public static readonly Vec2 ToeToAnkle = new(.10, .0375);
    public Vec2 Ankle => Toe + Rotate(ToeToAnkle, Angle);
    public Vec2 Heel => Toe + Vec2.FromAngle(Angle) * .15;
    public static FootPose AtRest(double ankleX) =>
        new(new(ankleX - ToeToAnkle.X, BattingRig.AnkleHeight - ToeToAnkle.Y), 0, FootContact.Planted);
    public static Vec2 Rotate(Vec2 vector, double angle) =>
        new(vector.X * Math.Cos(angle) - vector.Y * Math.Sin(angle), vector.X * Math.Sin(angle) + vector.Y * Math.Cos(angle));
    public static FootPose Interpolate(FootPose a, FootPose b, double t) => new(Vec2.Lerp(a.Toe, b.Toe, t),
        a.Angle + (b.Angle - a.Angle) * t, t >= 1 ? b.Contact : t <= 0 ? a.Contact :
        a.Contact == b.Contact ? a.Contact : a.Toe == b.Toe ? FootContact.ToePivot : FootContact.Airborne);
}

/// <summary>Interpolate rig parameters, then rebuild IK. Never interpolate individual bone endpoints.</summary>
public readonly record struct BattingFrame(Vec2 Hip, Vec2 Shoulder, double ShoulderAngle, double ElbowFlexion,
    double WristAngle, FootPose FrontFoot, FootPose RearFoot, double HeadAngle, BattingMotionState State)
{
    public static BattingFrame Interpolate(BattingFrame a, BattingFrame b, double fraction)
    {
        double t = Math.Clamp(fraction, 0, 1);
        return new(Vec2.Lerp(a.Hip, b.Hip, t), Vec2.Lerp(a.Shoulder, b.Shoulder, t),
            Lerp(a.ShoulderAngle, b.ShoulderAngle), Lerp(a.ElbowFlexion, b.ElbowFlexion), Lerp(a.WristAngle, b.WristAngle),
            FootPose.Interpolate(a.FrontFoot, b.FrontFoot, t), FootPose.Interpolate(a.RearFoot, b.RearFoot, t),
            Lerp(a.HeadAngle, b.HeadAngle), t < 1 ? a.State : b.State);
        double Lerp(double x, double y) => x + (y - x) * t;
    }
}

/// <summary>Supporting movement only. No input from the wrist and no feedback into swing control.</summary>
internal sealed class BattingMotion
{
    private readonly BattingSettings _settings;
    private readonly double _minimumShift, _maximumShift, _minimumCrouch, _maximumCrouch;
    private DampedValue _shift, _lean, _crouch, _head, _heel, _step;
    private bool _stroke, _returning, _wasForward, _wasSnapping;
    private double _stepTarget, _stepElapsed;
    public BattingMotionState State { get; private set; }
    public FootPose FrontFoot { get; private set; } = FootPose.AtRest(BattingRig.FrontFootX);
    public FootPose RearFoot { get; private set; } = FootPose.AtRest(BattingRig.BackFootX);

    public BattingMotion(BattingSettings settings, double clock)
    {
        _settings = settings;
        _minimumShift = settings.Stance.PoseBySwing.Min(k => k.HipShiftMetres) - settings.BodyMotion.HipTransferMetres;
        _maximumShift = settings.Stance.PoseBySwing.Max(k => k.HipShiftMetres);
        _minimumCrouch = settings.Stance.PoseBySwing.Min(k => k.CrouchMetres);
        _maximumCrouch = settings.Stance.PoseBySwing.Max(k => k.CrouchMetres) + settings.BodyMotion.AdditionalCrouchMetres;
        _shift.Value = ClockCurve.Sample(settings.Stance.PoseBySwing, clock, k => k.HipShiftMetres);
        _lean.Value = ClockCurve.Sample(settings.Stance.PoseBySwing, clock, k => k.ShoulderLeanMetres);
        _crouch.Value = ClockCurve.Sample(settings.Stance.PoseBySwing, clock, k => k.CrouchMetres);
        _head.Value = HeadTarget();
    }

    public void Step(double clock, double forwardSpeed, bool snapping, double dt)
    {
        var s = _settings;
        bool forward = !snapping && forwardSpeed > s.Footwork.TriggerDegreesPerSecond;
        bool reverse = !snapping && forwardSpeed < -3;
        double phase = ClockCurve.Wrap(s.Swing.Snap.EndClockDegrees - clock);
        bool atReady = phase < .05 && Math.Abs(forwardSpeed) < 3;
        if (_wasSnapping && !snapping || atReady && _stroke || reverse && FrontFoot.Contact == FootContact.Airborne && _stroke)
        {
            _stroke = false;
            _returning = true;
            SetStepTarget(0);
        }
        if (forward && !_wasForward)
        {
            _stroke = true;
            _returning = false;
            SetStepTarget(1);
        }
        _wasForward = forward;
        _wasSnapping = snapping;

        double duration = _stepTarget == 1 ? s.Footwork.StepSeconds : s.BodyMotion.RecoverySeconds;
        _stepElapsed += dt;
        // An exact critically damped update retains velocity when a step is interrupted.
        _step.Advance(_stepTarget, duration, dt, 12);
        _step.Constrain(0, 1);
        if (_stepElapsed >= duration && Math.Abs(_step.Value - _stepTarget) < .0002)
            _step = new() { Value = _stepTarget };
        double progress = _step.Value;
        Vec2 frontToe = FootPose.AtRest(BattingRig.FrontFootX).Toe +
            new Vec2(-s.Footwork.StepMetres * progress, s.Footwork.LiftMetres * Math.Sin(Math.PI * progress));
        bool planted = progress == _stepTarget && _step.Velocity == 0;
        FrontFoot = new(frontToe, 0, planted ? FootContact.Planted : FootContact.Airborne);
        if (_returning && planted && progress == 0) _returning = false;

        State = snapping ? BattingMotionState.Recovery : reverse ? BattingMotionState.Backlift :
            _returning ? BattingMotionState.Recovery :
            !snapping && forwardSpeed > 3 ? (phase >= 360 - s.Swing.Snap.ArcDegrees - 60 ?
                BattingMotionState.FollowThrough : BattingMotionState.ForwardSwing) :
            _stroke ? BattingMotionState.FollowThrough : BattingMotionState.Ready;
        double cap = ClockCurve.Sample(s.Swing.SpeedBySwing, clock, k => k.MaxSpeedDegreesPerSecond);
        double intensity = snapping ? 0 : Math.Clamp(forwardSpeed / cap, 0, 1);
        double settling = State == BattingMotionState.Recovery ? s.BodyMotion.RecoverySeconds : s.BodyMotion.SettlingSeconds;
        _shift.Advance(ClockCurve.Sample(s.Stance.PoseBySwing, clock, k => k.HipShiftMetres) -
            s.BodyMotion.HipTransferMetres * intensity, settling, dt);
        _lean.Advance(ClockCurve.Sample(s.Stance.PoseBySwing, clock, k => k.ShoulderLeanMetres), settling, dt);
        _crouch.Advance(ClockCurve.Sample(s.Stance.PoseBySwing, clock, k => k.CrouchMetres) +
            s.BodyMotion.AdditionalCrouchMetres * intensity, settling, dt);
        // Changing the recovery damping must never carry momentum outside the validated envelope.
        _shift.Constrain(_minimumShift, _maximumShift);
        _crouch.Constrain(_minimumCrouch, _maximumCrouch);
        _heel.Advance(_stroke && planted ? s.Footwork.RearHeelDegrees * Math.PI / 180 : 0, settling, dt);
        _heel.Constrain(0, s.Footwork.RearHeelDegrees * Math.PI / 180);
        if (Math.Abs(_heel.Value) < 1e-8 && Math.Abs(_heel.Velocity) < 1e-7) _heel = default;
        RearFoot = FootPose.AtRest(BattingRig.BackFootX) with
        {
            Angle = _heel.Value,
            Contact = _heel.Value > 0 ? FootContact.ToePivot : FootContact.Planted
        };
        _head.Advance(HeadTarget(), s.HeadMotion.SettlingSeconds, dt);
    }

    public BattingFrame Frame(double shoulderAngle, double flexion, double wristAngle)
    {
        Vec2 hip = new(PitchDimensions.BatterReferenceX + _shift.Value, _settings.Stance.HipHeightMetres - _crouch.Value);
        return new(hip, hip + new Vec2(_lean.Value, _settings.Stance.TorsoHeightMetres), shoulderAngle, flexion,
            wristAngle, FrontFoot, RearFoot, _head.Value, State);
    }

    private double HeadTarget()
    {
        double limit = _settings.HeadMotion.CompensationDegrees * Math.PI / 180;
        return Math.Clamp(Math.Atan2(_lean.Value, _settings.Stance.TorsoHeightMetres), -limit, limit);
    }
    private void SetStepTarget(double target)
    {
        if (_stepTarget == target) return;
        _stepTarget = target;
        _stepElapsed = 0;
    }

    private struct DampedValue
    {
        public double Value, Velocity;
        public void Constrain(double min, double max)
        {
            if (Value < min || Value > max) { Value = Math.Clamp(Value, min, max); Velocity = 0; }
        }
        public void Advance(double target, double seconds, double dt, double decay = 6)
        {
            double w = decay / seconds, error = Value - target, c = Velocity + w * error, e = Math.Exp(-w * dt);
            Value = target + (error + c * dt) * e;
            Velocity = (Velocity - w * c * dt) * e;
        }
    }
}
