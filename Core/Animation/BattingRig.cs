namespace Cricket.Physics;

/// <summary>Raw stick vectors in screen coordinates: right is +X, down is +Y.</summary>
public readonly record struct BattingInput(double LeftX, double LeftY, double RightX, double RightY);

public readonly record struct BatPose(Vec2 Grip, double Angle)
{
    public Vec2 Direction => Vec2.FromAngle(Angle);
    public Capsule Blade(SimulationSettings s) =>
        new(Grip + Direction * s.HandleLength, Grip + Direction * (s.BatLength - s.BatHalfThickness), s.BatHalfThickness);
    public static BatPose Lerp(BatPose a, BatPose b, double t) =>
        new(Vec2.Lerp(a.Grip, b.Grip, t), a.Angle + (b.Angle - a.Angle) * t);
}

public readonly record struct HumanPose(Vec2 Hip, Vec2 Shoulder, Vec2 Head,
    Vec2 LeadElbow, Vec2 RearShoulder, Vec2 RearElbow, Vec2 Hands,
    Vec2 FrontKnee, Vec2 FrontAnkle, Vec2 BackKnee, Vec2 BackAnkle, BatPose Bat,
    FootPose FrontFoot, FootPose RearFoot, double HeadAngle, BattingMotionState MotionState);

/// <summary>Left stick drives the shoulder-to-fist ray; right stick only rotates the bat about the fist.</summary>
public sealed class BattingRig
{
    public const double UpperArm = .32, Forearm = .30, Thigh = .45, Shin = .43;
    public const double FrontFootX = 18.73, BackFootX = 19.42, AnkleHeight = .075;
    public BattingSettings Settings { get; }
    // World angles stay unwrapped so collision velocities never spike at the clock seam.
    public double ShoulderAngle => Radians(90 + Settings.Swing.Snap.EndClockDegrees - _swing);
    public double ShoulderSpeed => -Radians(_speed);
    public double SwingClockDegrees => ClockCurve.Wrap(Settings.Swing.Snap.EndClockDegrees - _swing);
    public double ElbowFlexion { get; private set; }
    public double WristAngleDegrees { get; private set; }
    public double WristMinDegrees { get; private set; }
    public double WristMaxDegrees { get; private set; }
    public bool IsSnapping { get; private set; }
    public bool AwaitingSnapRearm { get; private set; }
    public HumanPose Pose { get; private set; }
    public BattingFrame Frame { get; private set; }
    private readonly BattingMotion _motion;
    private double _swing, _target, _speed, _cycleStart, _wristOffset;
    private double _inputCycle, _previousInputPhase;
    private bool _trackingLeft;

    public BattingRig(SimulationSettings physics, BattingSettings? settings = null)
    {
        Settings = settings ?? new();
        Settings.Validate(physics);
        _swing = _target = ClockCurve.Wrap(Settings.Swing.Snap.EndClockDegrees - Settings.Swing.InitialClockDegrees);
        UpdateWrist(default, 0, initialize: true);
        _motion = new(Settings, SwingClockDegrees);
        UpdatePose();
    }

    public void Step(BattingInput input, double dt, SimulationSettings physics)
    {
        if (!double.IsFinite(dt) || dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt));
        UpdateSwing(input, dt);
        UpdateWrist(input, dt);
        _motion.Step(SwingClockDegrees, _speed, IsSnapping, dt);
        UpdatePose();
    }

    private void UpdateSwing(BattingInput input, double dt)
    {
        var swing = Settings.Swing;
        var snap = swing.Snap;
        double snapStart = _cycleStart + 360 - snap.ArcDegrees;
        bool active = StickMagnitude(input.LeftX, input.LeftY) > Settings.Input.LeftDeadZone;
        double phase = active ? ClockCurve.Wrap(snap.EndClockDegrees - StickClock(input.LeftX, input.LeftY)) : 0;
        if (active)
        {
            double manualArc = 360 - snap.ArcDegrees;
            if (!_trackingLeft)
            {
                _inputCycle = _cycleStart;
                // An initial aim into the return arc chooses the nearer manual boundary.
                // In particular, moving backwards from 8 towards 9 stays at 8.
                if (phase > manualArc && Math.Abs(phase - 360 - (_swing - _cycleStart)) <
                    Math.Abs(phase - (_swing - _cycleStart))) _inputCycle -= 360;
            }
            else
            {
                double delta = phase - _previousInputPhase;
                // Wrap only through the snap-only arc. Large direct aims within the playable
                // arc remain usable with keyboard directions and a repositioned stick.
                if (delta < -180 && _previousInputPhase > manualArc) _inputCycle += 360;
                else if (delta > 180 && phase > manualArc) _inputCycle -= 360;
                _inputCycle = Math.Clamp(_inputCycle, _cycleStart - 360, _cycleStart + 360);
                // A fresh direct aim (including a keyboard direction jump) can recover from
                // the backstop; a continuous reverse circle remains stopped at the boundary.
                if (!IsSnapping && !AwaitingSnapRearm && _inputCycle < _cycleStart &&
                    phase <= manualArc && Math.Abs(delta) >= 90) _inputCycle = _cycleStart;
            }
            _previousInputPhase = phase;
        }
        _trackingLeft = active;
        if (IsSnapping)
        {
            // A rapid, swept return, never a teleport. Input cannot reverse or cancel it.
            _speed = MoveTowards(Math.Max(0, _speed), snap.SpeedDegreesPerSecond, snap.AccelerationDegreesPerSecondSquared * dt);
            _swing = Math.Min(_cycleStart + 360, _swing + _speed * dt);
            if (_swing >= _cycleStart + 360)
            {
                _cycleStart += 360;
                _target = _swing = _cycleStart;
                _speed = 0;
                IsSnapping = false;
                AwaitingSnapRearm = true;
            }
            return;
        }

        if (AwaitingSnapRearm)
        {
            // A held follow-through position must not trigger endless extra revolutions.
            if (!active || phase <= snap.RearmWindowDegrees || _inputCycle >= _cycleStart) AwaitingSnapRearm = false;
            else return;
            _inputCycle = Math.Max(_inputCycle, _cycleStart);
        }
        // Retain a forward clock crossing while the speed-limited arm catches up with the stick.
        if (active) _target = Math.Clamp(_inputCycle + phase, _cycleStart, snapStart);
        // A centred left stick retains the last target; it does not restore a guard pose.
        double error = _target - _swing;
        double forwardCap = ClockCurve.Sample(swing.SpeedBySwing, SwingClockDegrees, k => k.MaxSpeedDegreesPerSecond);
        double reverseCap = Math.Min(forwardCap * swing.ReverseSpeedScale, swing.ReverseMaxSpeedDegreesPerSecond);
        double cap = error < 0 ? reverseCap : forwardCap;
        double acceleration = ClockCurve.Sample(swing.SpeedBySwing, SwingClockDegrees, k => k.AccelerationDegreesPerSecondSquared);
        double desiredSpeed = Math.Sign(error) * Math.Min(cap,
            Math.Min(Math.Abs(error) * swing.TargetResponse, Math.Sqrt(2 * swing.BrakingDegreesPerSecondSquared * Math.Abs(error))));
        if (_target >= snapStart && error > 0) desiredSpeed = cap;
        bool braking = _speed * desiredSpeed < 0 || Math.Abs(desiredSpeed) < Math.Abs(_speed);
        double rate = braking ? swing.BrakingDegreesPerSecondSquared :
            desiredSpeed < 0 ? swing.ReverseAccelerationDegreesPerSecondSquared : acceleration;
        _speed = MoveTowards(_speed, desiredSpeed, rate * dt);
        _speed = Math.Clamp(_speed, -reverseCap, forwardCap);
        double next = _swing + _speed * dt;
        if (Math.Abs(error) < .0001 || (error != 0 && Math.Sign(_target - next) != Math.Sign(error)))
        {
            next = _target;
            _speed = 0;
        }
        _swing = Math.Clamp(next, _cycleStart, snapStart);
        if (_swing <= _cycleStart && _speed < 0) _speed = 0;
        if (_swing >= snapStart - .0001)
        {
            _swing = snapStart;
            IsSnapping = true;
        }
    }

    private void UpdateWrist(BattingInput input, double dt, bool initialize = false)
    {
        var wrist = Settings.Wrist;
        WristMinDegrees = ClockCurve.Sample(wrist.AnglesBySwing, SwingClockDegrees, k => k.MinDegrees);
        WristMaxDegrees = ClockCurve.Sample(wrist.AnglesBySwing, SwingClockDegrees, k => k.MaxDegrees);
        double neutral = ClockCurve.Sample(wrist.AnglesBySwing, SwingClockDegrees, k => k.NeutralDegrees);
        double magnitude = StickMagnitude(input.RightX, input.RightY);
        double target = neutral;
        if (magnitude > Settings.Input.RightDeadZone)
        {
            // Right stick points the blade in screen space, constrained relative to the current shoulder ray.
            double requested = ClockCurve.SignedDelta(StickClock(input.RightX, input.RightY) - SwingClockDegrees);
            double strength = (magnitude - Settings.Input.RightDeadZone) / (1 - Settings.Input.RightDeadZone);
            target += (Math.Clamp(requested, WristMinDegrees, WristMaxDegrees) - neutral) * strength;
        }
        double change = (target - neutral - _wristOffset) * (1 - Math.Exp(-wrist.Response * dt));
        _wristOffset = initialize ? 0 : _wristOffset +
            Math.Clamp(change, -wrist.MaxSpeedDegreesPerSecond * dt, wrist.MaxSpeedDegreesPerSecond * dt);
        WristAngleDegrees = Math.Clamp(neutral + _wristOffset, WristMinDegrees, WristMaxDegrees);
        _wristOffset = WristAngleDegrees - neutral;
    }

    private void UpdatePose()
    {
        ElbowFlexion = Radians(ClockCurve.Sample(Settings.Arms.FlexionBySwing, SwingClockDegrees, k => k.ElbowFlexionDegrees));
        Frame = _motion.Frame(ShoulderAngle, ElbowFlexion, Radians(WristAngleDegrees));
        Pose = BuildPose(Frame, Settings);
    }

    public static HumanPose BuildPose(BattingFrame frame, BattingSettings settings)
    {
        Vec2 hip = frame.Hip, shoulder = frame.Shoulder;
        double reach = Math.Sqrt(UpperArm * UpperArm + Forearm * Forearm + 2 * UpperArm * Forearm * Math.Cos(frame.ElbowFlexion));
        Vec2 hands = shoulder + Vec2.FromAngle(frame.ShoulderAngle) * reach;
        // Both shoulders coincide in the side projection; straight arms share the shoulder/fist ray.
        Vec2 elbow = SolveJoint(shoulder, hands, UpperArm, Forearm, settings.Arms.LeadElbowBend);
        Vec2 rearElbow = SolveJoint(shoulder, hands, UpperArm, Forearm, settings.Arms.RearElbowBend);
        Vec2 frontAnkle = frame.FrontFoot.Ankle, backAnkle = frame.RearFoot.Ankle;
        return new(hip, shoulder, shoulder + FootPose.Rotate(new(-.06, .25), frame.HeadAngle), elbow, shoulder, rearElbow, hands,
            SolveJoint(hip, frontAnkle, Thigh, Shin, -1), frontAnkle,
            SolveJoint(hip, backAnkle, Thigh, Shin, -1), backAnkle,
            new(hands, frame.ShoulderAngle + frame.WristAngle), frame.FrontFoot, frame.RearFoot, frame.HeadAngle, frame.State);
    }

    public static Vec2 SolveJoint(Vec2 root, Vec2 target, double a, double b, double bend)
    {
        Vec2 direction = (target - root).Normalized;
        double d = Math.Clamp((target - root).Length, Math.Abs(a - b) + 1e-6, a + b);
        double along = (a * a - b * b + d * d) / (2 * d);
        double perpendicular = Math.Sqrt(Math.Max(0, a * a - along * along));
        return root + direction * along + direction.Perpendicular * (bend * perpendicular);
    }

    private static double StickMagnitude(double x, double y) => double.IsFinite(x) && double.IsFinite(y) ?
        Math.Min(1, Math.Sqrt(x * x + y * y)) : 0;
    private static double StickClock(double x, double y) => ClockCurve.Wrap(Math.Atan2(x, -y) * 180 / Math.PI);
    private static double MoveTowards(double value, double target, double amount) => value + Math.Clamp(target - value, -amount, amount);
    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
