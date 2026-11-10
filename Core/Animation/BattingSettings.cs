using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cricket.Physics;

/// <summary>SI distances and seconds; angles in degrees. Limits are animation choices, not clinical measurements.</summary>
public sealed record BattingSettings
{
    public StickSettings Input { get; init; } = new();
    public SwingSettings Swing { get; init; } = new();
    public BodySettings Body { get; init; } = new();

    public static BattingSettings FromJson(string json, SimulationSettings? physics = null)
    {
        var result = JsonSerializer.Deserialize<BattingSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new ArgumentException("Config/batsman.json must contain an object.");
        result.Validate(physics);
        return result;
    }
    public void Validate(SimulationSettings? physics = null)
    {
        if (Input is null || Swing is null || Body is null)
            throw new ArgumentException("Input, Swing and Body must be objects.");
        Range(Input.LeftDeadZone, .2, .65, "Input.LeftDeadZone");
        Range(Input.SwingEngageRadius, Input.LeftDeadZone + .05, .95, "Input.SwingEngageRadius");
        Range(Input.RotationThresholdDegrees, 1, 30, "Input.RotationThresholdDegrees");
        Range(Input.StartWindowDegrees, 5, 30, "Input.StartWindowDegrees");
        Range(Input.MaxSampleArcDegrees, 45, 90, "Input.MaxSampleArcDegrees");
        Range(Input.TriggerDeadZone, 0, .3, "Input.TriggerDeadZone");
        Range(Swing.StartSpeedKph, 1, 136, "Swing.StartSpeedKph");
        Range(Swing.MaxSpeedKph, Swing.StartSpeedKph, 136, "Swing.MaxSpeedKph");
        Range(Swing.AccelerationDegrees, 5, 90, "Swing.AccelerationDegrees");
        Range(Swing.BrakingDegrees, 1, 25, "Swing.BrakingDegrees");
        Range(Body.GripSpacingMetres, .08, .12, "Body.GripSpacingMetres");
        Range(Body.TransferSeconds, .18, .65, "Body.TransferSeconds");
        Range(Body.StepSeconds, .18, .5, "Body.StepSeconds");
        Range(Body.FrontStrideMetres, .15, .65, "Body.FrontStrideMetres");
        Range(Body.DefaultForwardWeight, 0, .5, "Body.DefaultForwardWeight");
        Range(Body.BackStrideMetres, .05, .14, "Body.BackStrideMetres");
        Range(Body.StepLiftMetres, .025, .09, "Body.StepLiftMetres");
        Range(Body.RearHeelDegrees, 35, 65, "Body.RearHeelDegrees");
        if (physics is not null && (physics.BatLength > 1 || physics.HandleLength < Body.GripSpacingMetres + .06))
            throw new ArgumentException("The batting rig requires a bat at most 1 m long and room for both gloves on its handle.");
    }
    private static void Range(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max)
            throw new ArgumentException($"Invalid Config/batsman.json: {name} must be in [{min}, {max}].");
    }
}
public sealed record StickSettings
{
    public double LeftDeadZone { get; init; } = .45;
    public double SwingEngageRadius { get; init; } = .60;
    public double RotationThresholdDegrees { get; init; } = 6;
    public double StartWindowDegrees { get; init; } = 15;
    public double MaxSampleArcDegrees { get; init; } = 65;
    public double TriggerDeadZone { get; init; } = .04;
}
public sealed record SwingSettings
{
    public double StartSpeedKph { get; init; } = 50;
    public double MaxSpeedKph { get; init; } = 136;
    public double AccelerationDegrees { get; init; } = 25;
    public double BrakingDegrees { get; init; } = 10;
}
public sealed record BodySettings
{
    public double GripSpacingMetres { get; init; } = .105;
    public double TransferSeconds { get; init; } = .28;
    public double StepSeconds { get; init; } = .24;
    public double FrontStrideMetres { get; init; } = .60;
    public double DefaultForwardWeight { get; init; } = .35;
    public double BackStrideMetres { get; init; } = .14;
    public double StepLiftMetres { get; init; } = .055;
    public double RearHeelDegrees { get; init; } = 58;
}
public static class ClockCurve
{
    public static double Wrap(double degrees) => (degrees % 360 + 360) % 360;
    public static double SignedDelta(double degrees) => Wrap(degrees + 180) - 180;
}
