using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cricket.Physics;

/// <summary>Designer-facing angles use the screen clock: 0 = 12, 90 = 3, 180 = 6, 270 = 9.</summary>
public sealed record BattingSettings
{
    public StickSettings Input { get; init; } = new();
    public SwingSettings Swing { get; init; } = new();
    public WristSettings Wrist { get; init; } = new();
    public ArmSettings Arms { get; init; } = new();
    public StanceSettings Stance { get; init; } = new();
    public BodyMotionSettings BodyMotion { get; init; } = new();
    public FootworkSettings Footwork { get; init; } = new();
    public HeadMotionSettings HeadMotion { get; init; } = new();

    public static BattingSettings FromJson(string json, SimulationSettings? physics = null)
    {
        var settings = JsonSerializer.Deserialize<BattingSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new ArgumentException("Config/batsman.json must contain an object.");
        settings.Validate(physics);
        return settings;
    }

    public void Validate(SimulationSettings? physics = null)
    {
        Require(Input is not null && Swing is not null && Wrist is not null && Arms is not null && Stance is not null,
            "Input, Swing, Wrist, Arms and Stance must be objects");
        Require(InRange(Input!.LeftDeadZone, 0, .9) && InRange(Input.RightDeadZone, 0, .9), "Input dead zones must be in [0, 0.9]");
        Require(Clock(Swing!.InitialClockDegrees), "Swing.InitialClockDegrees must be in [0, 360)");
        Require(Positive(Swing.TargetResponse) && Positive(Swing.BrakingDegreesPerSecondSquared) &&
            Positive(Swing.ReverseMaxSpeedDegreesPerSecond) && Positive(Swing.ReverseAccelerationDegreesPerSecondSquared) &&
            Swing.ReverseSpeedScale is > 0 and < 1, "Swing response/braking/reverse acceleration must be positive; reverse speed scale must be between 0 and 1");
        Require(Swing.Snap is not null, "Swing.Snap must be an object");
        var snap = Swing.Snap!;
        Require(Clock(snap.StartClockDegrees) && Clock(snap.EndClockDegrees) && snap.ArcDegrees is > 0 and < 180,
            "Swing.Snap must cover a counterclockwise arc smaller than 180 degrees");
        Require(Positive(snap.SpeedDegreesPerSecond) && Positive(snap.AccelerationDegreesPerSecondSquared) &&
            InRange(snap.RearmWindowDegrees, 1, 180), "Swing.Snap speed, acceleration or rearm window is invalid");
        Require(ClockCurve.Wrap(snap.EndClockDegrees - Swing.InitialClockDegrees) < 360 - snap.ArcDegrees,
            "Swing.InitialClockDegrees must be outside the committed snap arc");
        ValidateCurve(Swing.SpeedBySwing, "Swing.SpeedBySwing");
        foreach (var key in Swing.SpeedBySwing)
            Require(Positive(key.MaxSpeedDegreesPerSecond) && Positive(key.AccelerationDegreesPerSecondSquared),
                "Swing.SpeedBySwing speeds and accelerations must be positive");
        Require(Positive(Wrist!.Response) && Positive(Wrist.MaxSpeedDegreesPerSecond), "Wrist response and speed must be positive");
        ValidateCurve(Wrist.AnglesBySwing, "Wrist.AnglesBySwing");
        foreach (var key in Wrist.AnglesBySwing)
            Require(InRange(key.MinDegrees, -179, 179) && InRange(key.MaxDegrees, -179, 179) &&
                InRange(key.NeutralDegrees, key.MinDegrees, key.MaxDegrees),
                "Wrist.AnglesBySwing requires -179 <= MinDegrees <= NeutralDegrees <= MaxDegrees <= 179");
        ValidateCurve(Arms!.FlexionBySwing, "Arms.FlexionBySwing");
        foreach (var key in Arms.FlexionBySwing)
            Require(InRange(key.ElbowFlexionDegrees, 0, 135), "Arms flexion must be in [0, 135]");
        Require(Arms.LeadElbowBend is -1 or 1 && Arms.RearElbowBend is -1 or 1, "Arms bend directions must be -1 or 1");
        Require(Positive(Stance!.HipHeightMetres) && Positive(Stance.TorsoHeightMetres) &&
            InRange(Stance.MinimumBladeClearanceMetres, 0, .1), "Stance heights or blade clearance are invalid");
        ValidateCurve(Stance.PoseBySwing, "Stance.PoseBySwing");
        Require(BodyMotion is not null && Footwork is not null && HeadMotion is not null,
            "BodyMotion, Footwork and HeadMotion must be objects");
        Require(InRange(BodyMotion!.SettlingSeconds, .02, 1) && InRange(BodyMotion.RecoverySeconds, .05, 2) &&
            InRange(BodyMotion.HipTransferMetres, 0, .08) && InRange(BodyMotion.AdditionalCrouchMetres, 0, .05),
            "BodyMotion timing or movement limits are invalid");
        Require(InRange(Footwork!.StepMetres, 0, .15) && InRange(Footwork.LiftMetres, 0, .06) &&
            InRange(Footwork.StepSeconds, .04, 1) && InRange(Footwork.RearHeelDegrees, 0, 30) &&
            Positive(Footwork.TriggerDegreesPerSecond), "Footwork distances, timing, heel angle or trigger are invalid");
        Require(InRange(HeadMotion!.CompensationDegrees, 0, 15) && InRange(HeadMotion.SettlingSeconds, .02, 1),
            "HeadMotion compensation or settling time is invalid");
        physics ??= new();
        foreach (var key in Stance.PoseBySwing)
        {
            Require(InRange(key.HipShiftMetres, -.15, .15) && InRange(key.ShoulderLeanMetres, -.2, .2) &&
                InRange(key.CrouchMetres, 0, .15), "Stance offsets exceed supported anatomical bounds");
            // Guarantee clearance for every wrist angle without letting the wrist move the shoulder.
            Require(Stance.HipHeightMetres + Stance.TorsoHeightMetres - key.CrouchMetres - BodyMotion.AdditionalCrouchMetres >=
                BattingRig.UpperArm + BattingRig.Forearm + physics.BatLength + Stance.MinimumBladeClearanceMetres,
                "Stance shoulder height must clear the fully extended arm and bat above the pitch");
        }
        // Smoothing and foot trajectories remain inside these independent envelopes. Check all
        // corners, not only matching curve keys: motion can lag behind the shoulder angle.
        double[] hipX = [PitchDimensions.BatterReferenceX + Stance.PoseBySwing.Min(k => k.HipShiftMetres) - BodyMotion.HipTransferMetres,
            PitchDimensions.BatterReferenceX + Stance.PoseBySwing.Max(k => k.HipShiftMetres)];
        double[] hipY = [Stance.HipHeightMetres - Stance.PoseBySwing.Max(k => k.CrouchMetres) - BodyMotion.AdditionalCrouchMetres,
            Stance.HipHeightMetres - Stance.PoseBySwing.Min(k => k.CrouchMetres)];
        double heel = Footwork.RearHeelDegrees * Math.PI / 180;
        FootPose raisedFoot = FootPose.AtRest(BattingRig.BackFootX) with { Angle = heel };
        Vec2 raisedRear = raisedFoot.Ankle;
        double[] ankleX = [BattingRig.FrontFootX - Footwork.StepMetres, BattingRig.FrontFootX, raisedRear.X, BattingRig.BackFootX];
        double[] ankleY = [BattingRig.AnkleHeight, BattingRig.AnkleHeight + Footwork.LiftMetres, raisedRear.Y];
        foreach (double x in hipX)
        foreach (double y in hipY)
        foreach (double ax in ankleX)
        foreach (double ay in ankleY)
            Require((new Vec2(x, y) - new Vec2(ax, ay)).Length < BattingRig.Thigh + BattingRig.Shin &&
                y - ay > Math.Abs(BattingRig.Thigh - BattingRig.Shin),
                "Combined Stance/BodyMotion/Footwork envelope exceeds leg reach");
    }

    private static void ValidateCurve<T>(T[]? keys, string name) where T : ClockKey
    {
        Require(keys is { Length: >= 2 }, $"{name} needs at least two keys");
        double previous = -1;
        foreach (var key in keys!)
        {
            Require(key is not null && Clock(key.ClockDegrees) && key.ClockDegrees > previous,
                $"{name} keys must have unique ClockDegrees in ascending order in [0, 360)");
            previous = key!.ClockDegrees;
        }
    }

    private static bool Clock(double value) => double.IsFinite(value) && value is >= 0 and < 360;
    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
    private static bool InRange(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
    private static void Require(bool valid, string message)
    {
        if (!valid) throw new ArgumentException($"Invalid Config/batsman.json: {message}.");
    }
}

public sealed record StickSettings
{
    public double LeftDeadZone { get; init; } = .18;
    public double RightDeadZone { get; init; } = .18;
}

public sealed record SwingSettings
{
    public double InitialClockDegrees { get; init; } = 240;
    public double TargetResponse { get; init; } = 24;
    public double BrakingDegreesPerSecondSquared { get; init; } = 9000;
    public double ReverseMaxSpeedDegreesPerSecond { get; init; } = 240;
    public double ReverseSpeedScale { get; init; } = .3;
    public double ReverseAccelerationDegreesPerSecondSquared { get; init; } = 1800;
    public SnapSettings Snap { get; init; } = new();
    public SwingKey[] SpeedBySwing { get; init; } =
    [
        new(0, 420, 5000), new(30, 760, 7000), new(60, 1100, 11000),
        new(210, 1100, 11000), new(240, 700, 10000)
    ];
}

public sealed record SnapSettings
{
    public double StartClockDegrees { get; init; } = 0;
    public double EndClockDegrees { get; init; } = 240;
    public double SpeedDegreesPerSecond { get; init; } = 1440;
    public double AccelerationDegreesPerSecondSquared { get; init; } = 36000;
    public double RearmWindowDegrees { get; init; } = 90;
    [JsonIgnore] public double ArcDegrees => ClockCurve.Wrap(StartClockDegrees - EndClockDegrees);
}

public sealed record WristSettings
{
    public double Response { get; init; } = 24;
    public double MaxSpeedDegreesPerSecond { get; init; } = 720;
    public WristKey[] AnglesBySwing { get; init; } =
    [
        new(0, -65, -15, 35), new(30, -60, 0, 45), new(60, -75, 0, 65),
        new(120, -65, 0, 65), new(180, -45, 0, 55), new(210, -55, 0, 70),
        new(240, -55, 0, 75), new(270, -55, 0, 55), new(300, -75, -15, 40),
        new(330, -90, -35, 20)
    ];
}

public sealed record ArmSettings
{
    public int LeadElbowBend { get; init; } = 1;
    public int RearElbowBend { get; init; } = -1;
    public ArmKey[] FlexionBySwing { get; init; } =
    [new(0, 30), new(30, 0), new(240, 0), new(270, 30), new(300, 75), new(330, 85)];
}

public sealed record StanceSettings
{
    public double HipHeightMetres { get; init; } = .82;
    public double TorsoHeightMetres { get; init; } = .69;
    public double MinimumBladeClearanceMetres { get; init; } = .005;
    public StanceKey[] PoseBySwing { get; init; } =
    [
        new(0, -.02, -.06, 0), new(30, -.04, -.09, 0), new(60, -.04, -.10, .01),
        new(90, -.03, -.10, .02), new(180, 0, -.07, .025), new(240, .03, -.03, .01),
        new(270, .03, -.02, 0), new(330, 0, -.04, 0)
    ];
}

public sealed record BodyMotionSettings
{
    public double SettlingSeconds { get; init; } = .10;
    public double RecoverySeconds { get; init; } = .22;
    public double HipTransferMetres { get; init; } = .025;
    public double AdditionalCrouchMetres { get; init; } = .005;
}

public sealed record FootworkSettings
{
    public double StepMetres { get; init; } = .05;
    public double LiftMetres { get; init; } = .015;
    public double StepSeconds { get; init; } = .12;
    public double RearHeelDegrees { get; init; } = 10;
    public double TriggerDegreesPerSecond { get; init; } = 60;
}

public sealed record HeadMotionSettings
{
    public double CompensationDegrees { get; init; } = 6;
    public double SettlingSeconds { get; init; } = .10;
}

public abstract record ClockKey(double ClockDegrees);
public sealed record SwingKey(double ClockDegrees, double MaxSpeedDegreesPerSecond,
    double AccelerationDegreesPerSecondSquared) : ClockKey(ClockDegrees);
public sealed record WristKey(double ClockDegrees, double MinDegrees, double NeutralDegrees, double MaxDegrees) : ClockKey(ClockDegrees);
public sealed record ArmKey(double ClockDegrees, double ElbowFlexionDegrees) : ClockKey(ClockDegrees);
public sealed record StanceKey(double ClockDegrees, double HipShiftMetres, double ShoulderLeanMetres, double CrouchMetres) : ClockKey(ClockDegrees);

public static class ClockCurve
{
    public static double Wrap(double degrees) => (degrees % 360 + 360) % 360;
    public static double SignedDelta(double degrees) => Wrap(degrees + 180) - 180;

    /// <summary>Periodic linear interpolation, including the last key through 12 o'clock to the first.</summary>
    public static double Sample<T>(T[] keys, double clockDegrees, Func<T, double> value) where T : ClockKey
    {
        double clock = Wrap(clockDegrees);
        for (int i = 0; i < keys.Length; i++)
        {
            T a = keys[i], b = keys[(i + 1) % keys.Length];
            double end = i == keys.Length - 1 ? b.ClockDegrees + 360 : b.ClockDegrees;
            double point = clock < a.ClockDegrees ? clock + 360 : clock;
            if (point <= end)
            {
                double t = (point - a.ClockDegrees) / (end - a.ClockDegrees);
                return value(a) + (value(b) - value(a)) * t;
            }
        }
        throw new InvalidOperationException("Clock curve has no interval; validate settings before use.");
    }
}
