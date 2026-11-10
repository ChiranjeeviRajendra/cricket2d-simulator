using System.Text.Json.Serialization;

namespace Cricket.Physics;

public static class PitchDimensions
{
    // Game specification: exactly 20.12 metres between the wicket lines.
    public const double WicketSeparation = 20.12;
    public const double StumpHeight = 0.7112;
    public const double StumpRadius = 0.019;
    public const double PoppingCrease = 1.2192;
    public const double ReleaseX = 1.2192;
    public const double BatterReferenceX = 19.08;
    public const double PreparedPitchMargin = 0.30;
}

/// <summary>Effective ball/surface coefficients; reference tests and limitations are in docs/PHYSICS.md.</summary>
public sealed record SimulationSettings
{
    public double BallMass { get; init; } = 0.156;
    public double BallRadius { get; init; } = 0.036;
    public double Gravity { get; init; } = 9.80665;
    public double AirDensity { get; init; } = 1.225;
    public double DragCoefficient { get; init; } = 0.47;
    public double MagnusCoefficient { get; init; } = 0.12;
    public double SpinDecay { get; init; } = 0.025;
    // Prepared turf strip: leather-ball drop tests, not the ball's steel-plate restitution.
    public double GroundRestitution { get; init; } = 0.43;
    public double GroundFriction { get; init; } = 0.22;
    public double RollingResistance { get; init; } = 0.025;
    // Grass outfield: effective values calibrated against the AFL/CA turf reference tests.
    public double OutfieldRestitution { get; init; } = 0.30;
    public double OutfieldFriction { get; init; } = 0.35;
    public double OutfieldRollingResistance { get; init; } = 0.075;
    public double OutfieldTangentialRetention { get; init; } = 0.65;
    // Curtis et al. (2021), table 1 mean mass/inertia, translated to a grip 0.10 m from the handle end.
    public double BatMass { get; init; } = 1.17;
    public double BatCentreOfMassFromGrip { get; init; } = 0.40;
    public double BatInertiaAboutGrip { get; init; } = 0.2459;
    [JsonIgnore] public double BatInertiaAboutCentreOfMass =>
        BatInertiaAboutGrip - BatMass * BatCentreOfMassFromGrip * BatCentreOfMassFromGrip;
    public double BatRestitution { get; init; } = 0.50;
    public double BatFriction { get; init; } = 0.28;
    public double BatHalfThickness { get; init; } = 0.032;
    public double BatLength { get; init; } = 0.85;
    public double HandleLength { get; init; } = 0.28;
    public int Substeps { get; init; } = 8;
    public const double Tick = 1.0 / 120.0;
    public const double WindupDuration = 1.25;

    public void Validate()
    {
        double[] positive = [BallMass, BallRadius, Gravity, BatMass, BatInertiaAboutGrip,
            BatHalfThickness, BatLength, HandleLength, BatInertiaAboutCentreOfMass];
        double[] nonNegative = [AirDensity, DragCoefficient, MagnusCoefficient, SpinDecay,
            GroundFriction, RollingResistance, OutfieldFriction, OutfieldRollingResistance, BatFriction, BatCentreOfMassFromGrip];
        if (positive.Any(x => !double.IsFinite(x) || x <= 0) ||
            nonNegative.Any(x => !double.IsFinite(x) || x < 0) ||
            !double.IsFinite(GroundRestitution) || GroundRestitution is < 0 or > 1 ||
            !double.IsFinite(OutfieldRestitution) || OutfieldRestitution is < 0 or > 1 ||
            !double.IsFinite(OutfieldTangentialRetention) || OutfieldTangentialRetention is < 0 or > 1 ||
            !double.IsFinite(BatRestitution) || BatRestitution is < 0 or > 1 ||
            HandleLength >= BatLength || BatCentreOfMassFromGrip >= BatLength || Substeps is < 1 or > 32)
            throw new ArgumentException("Invalid physics coefficients. Check Config/physics.json.");
    }
}
