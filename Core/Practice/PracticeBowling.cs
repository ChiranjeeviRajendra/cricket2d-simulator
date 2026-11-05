namespace Cricket.Physics;

public enum BowlerType { SuperSlow, Slow, Medium, Fast, SuperFast }

public readonly record struct BowlerProfile(BowlerType Type, string Name, int MinSpeedTenths, int MaxSpeedTenths)
{
    public double MinSpeedKph => MinSpeedTenths / 10.0;
    public double MaxSpeedKph => MaxSpeedTenths / 10.0;
}

public static class BowlerProfiles
{
    private static readonly BowlerProfile[] Profiles =
    [
        new(BowlerType.SuperSlow, "Super Slow", 180, 499),
        new(BowlerType.Slow, "Slow", 500, 800),
        new(BowlerType.Medium, "Medium", 801, 1100),
        new(BowlerType.Fast, "Fast", 1101, 1300),
        new(BowlerType.SuperFast, "Super Fast", 1301, 1800)
    ];
    public static ReadOnlySpan<BowlerProfile> All => Profiles;
    public static BowlerProfile Get(BowlerType type) => (uint)type < Profiles.Length
        ? Profiles[(int)type] : throw new ArgumentOutOfRangeException(nameof(type));

    public static BowlerType Classify(double speedKph)
    {
        if (!double.IsFinite(speedKph) || speedKph < 18 || speedKph > 180)
            throw new ArgumentOutOfRangeException(nameof(speedKph), "Release speed must be between 18 and 180 km/h.");
        int tenths = (int)Math.Round(speedKph * 10, MidpointRounding.AwayFromZero);
        foreach (var profile in Profiles) if (tenths <= profile.MaxSpeedTenths) return profile.Type;
        return BowlerType.SuperFast;
    }
}

public sealed record PracticeBowlingSettings
{
    public BowlerType Bowler { get; init; } = BowlerType.Medium;
    public bool RandomPitchLength { get; init; } = true;
    public bool RandomSpeed { get; init; } = true;
    public double ManualLength { get; init; } = 16;
    public double ManualSpeedKph { get; init; } = 95;
    public double ReleaseHeight { get; init; } = 2.15;
    public DeliverySettings ManualDelivery => new(SpeedUnits.ToMetresPerSecond(ManualSpeedKph), ManualLength, ReleaseHeight);

    public PracticeBowlingSettings WithManualSpeed(double speedKph) =>
        this with { ManualSpeedKph = speedKph, Bowler = BowlerProfiles.Classify(speedKph) };

    public PracticeBowlingSettings WithBowler(BowlerType bowler)
    {
        var profile = BowlerProfiles.Get(bowler);
        return this with { Bowler = bowler, ManualSpeedKph = Math.Clamp(ManualSpeedKph, profile.MinSpeedKph, profile.MaxSpeedKph) };
    }
}

/// <summary>Bounded, cancellable generation. Randomness belongs to a pending ball, never a render or physics tick.</summary>
public static class PracticeDeliveryGenerator
{
    public const int MaxRandomAttempts = 64;

    public static DeliverySettings Sample(PracticeBowlingSettings settings, Random random)
    {
        var profile = BowlerProfiles.Get(settings.Bowler);
        double speed = settings.RandomSpeed ? random.Next(profile.MinSpeedTenths, profile.MaxSpeedTenths + 1) / 10.0 : settings.ManualSpeedKph;
        double length = settings.RandomPitchLength ? random.Next((int)(DeliverySettings.MinPitchingLength * 100),
            (int)(DeliverySettings.MaxPitchingLength * 100) + 1) / 100.0 : settings.ManualLength;
        return new(SpeedUnits.ToMetresPerSecond(speed), length, settings.ReleaseHeight);
    }

    public static bool TryCreatePlan(PracticeBowlingSettings settings, int seed, SimulationSettings physics,
        out DeliveryPlan? plan, out string error, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        plan = null;
        error = settings.ManualDelivery.ValidationError ?? "";
        if (!Enum.IsDefined(settings.Bowler)) error = "Choose a valid bowler type.";
        if (error.Length != 0) return false;
        if (!settings.RandomPitchLength && !settings.RandomSpeed)
            return DeliverySolver.TrySolve(settings.ManualDelivery, physics, out plan, out error, cancellation);

        var random = new Random(seed);
        for (int attempt = 0; attempt < MaxRandomAttempts; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (DeliverySolver.TrySolve(Sample(settings, random), physics, out plan, out error, cancellation)) return true;
        }
        // Try the most reachable permitted combination without changing any manual value.
        var fallback = new DeliverySettings(
            SpeedUnits.ToMetresPerSecond(settings.RandomSpeed ? BowlerProfiles.Get(settings.Bowler).MaxSpeedKph : settings.ManualSpeedKph),
            settings.RandomPitchLength ? DeliverySettings.MinPitchingLength : settings.ManualLength, settings.ReleaseHeight);
        if (DeliverySolver.TrySolve(fallback, physics, out plan, out error, cancellation)) return true;
        error = "No reachable delivery with these settings. Increase release speed, choose a faster bowler, or shorten the pitch length.";
        return false;
    }
}
