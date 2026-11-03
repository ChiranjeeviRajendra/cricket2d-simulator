namespace Cricket.Physics;

/// <param name="Swing">Left stick X: -1 backlift, +1 forward swing.</param>
/// <param name="Crouch">Left stick Y: positive lowers the stance.</param>
/// <param name="Elbow">Right stick X: negative extends, positive flexes.</param>
/// <param name="Wrist">Right stick Y: small bat-face adjustment.</param>
public readonly record struct BattingInput(double Swing, double Crouch, double Elbow, double Wrist);

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
    Vec2 FrontKnee, Vec2 FrontAnkle, Vec2 BackKnee, Vec2 BackAnkle, BatPose Bat);

/// <summary>
/// Animation-driven human, not a ragdoll. Fixed bone lengths, constrained hinge joints,
/// damped acceleration, planted feet, and a second arm solved to the shared grip.
/// Positive rotation is counterclockwise in the physical world.
/// </summary>
public sealed class BattingRig
{
    public const double UpperArm = 0.32;
    public const double Forearm = 0.30;
    public const double Thigh = 0.45;
    public const double Shin = 0.43;
    public double ShoulderAngle { get; private set; } = Radians(-145);
    public double ElbowFlexion { get; private set; } = Radians(65);
    public double ShoulderSpeed { get; private set; }
    public HumanPose Pose { get; private set; }
    private double _elbowSpeed;
    private double _crouch;
    private double _wrist;

    public BattingRig(SimulationSettings s) => Pose = BuildPose(s);

    public void Step(BattingInput input, double dt, SimulationSettings s)
    {
        double shoulderTarget = Radians(-145 - Math.Clamp(input.Swing, -1, 1) * (input.Swing < 0 ? 105 : 65));
        (double angle, double speed) = Joint(ShoulderAngle, ShoulderSpeed, shoulderTarget, 14, 120, dt);
        ShoulderAngle = Math.Clamp(angle, Radians(-210), Radians(-40));
        ShoulderSpeed = speed;
        double elbowTarget = Radians(65 + Math.Clamp(input.Elbow, -1, 1) * (input.Elbow < 0 ? 45 : 70));
        (ElbowFlexion, _elbowSpeed) = Joint(ElbowFlexion, _elbowSpeed, elbowTarget, 10, 90, dt);
        ElbowFlexion = Math.Clamp(ElbowFlexion, Radians(20), Radians(135));
        _crouch += (Math.Clamp(input.Crouch, 0, 1) * 0.10 - _crouch) * Math.Min(1, dt * 10);
        _wrist += (Math.Clamp(input.Wrist, -1, 1) * Radians(25) - _wrist) * Math.Min(1, dt * 12);
        Pose = BuildPose(s);
    }

    private HumanPose BuildPose(SimulationSettings s)
    {
        double transfer = Math.Clamp((-ShoulderAngle - Radians(145)) /
            Radians(ShoulderAngle > Radians(-145) ? 105 : 65), -1, 1);
        Vec2 hip = new(PitchDimensions.BatterReferenceX - transfer * 0.07, 0.84 - _crouch);
        Vec2 shoulder = hip + new Vec2(-0.07 - transfer * 0.05, 0.58);
        Vec2 elbow = shoulder + Vec2.FromAngle(ShoulderAngle) * UpperArm;
        double forearmAngle = ShoulderAngle + ElbowFlexion;
        Vec2 hand = elbow + Vec2.FromAngle(forearmAngle) * Forearm;
        double batAngle = forearmAngle - Radians(35) + _wrist;
        // Keep the blade above the surface with a bounded wrist correction.
        double tipY = hand.Y + Math.Sin(batAngle) * (s.BatLength - s.BatHalfThickness);
        if (tipY < s.BatHalfThickness)
        {
            double limit = Math.Asin(Math.Clamp((s.BatHalfThickness - hand.Y) /
                (s.BatLength - s.BatHalfThickness), -1, 1));
            double normalized = Math.Atan2(Math.Sin(batAngle), Math.Cos(batAngle));
            batAngle += (Math.Cos(normalized) >= 0 ? limit : -Math.PI - limit) - normalized;
        }
        Vec2 rearShoulder = shoulder + new Vec2(0.025, 0);
        Vec2 rearElbow = SolveJoint(rearShoulder, hand, 0.34, 0.32, 1);
        Vec2 frontAnkle = new(18.73, 0.075);
        Vec2 backAnkle = new(19.42, 0.075);
        return new(hip, shoulder, shoulder + new Vec2(-0.06, 0.25), elbow,
            rearShoulder, rearElbow, hand,
            SolveJoint(hip, frontAnkle, Thigh, Shin, -1), frontAnkle,
            SolveJoint(hip, backAnkle, Thigh, Shin, -1), backAnkle,
            new(hand, batAngle));
    }

    public static Vec2 SolveJoint(Vec2 root, Vec2 target, double a, double b, double bend)
    {
        Vec2 direction = (target - root).Normalized;
        double d = Math.Clamp((target - root).Length, Math.Abs(a - b) + 1e-6, a + b - 1e-6);
        double along = (a * a - b * b + d * d) / (2 * d);
        double perpendicular = Math.Sqrt(Math.Max(0, a * a - along * along));
        return root + direction * along + direction.Perpendicular * (bend * perpendicular);
    }

    private static (double, double) Joint(double value, double velocity, double target,
        double maxSpeed, double maxAcceleration, double dt)
    {
        double acceleration = Math.Clamp(190 * (target - value) - 25 * velocity, -maxAcceleration, maxAcceleration);
        velocity = Math.Clamp(velocity + acceleration * dt, -maxSpeed, maxSpeed);
        return (value + velocity * dt, velocity);
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
