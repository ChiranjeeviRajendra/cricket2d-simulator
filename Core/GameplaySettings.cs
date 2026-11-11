namespace Cricket.Physics;

/// <summary>Shared gameplay preferences, independent of delivery preparation and physics coefficients.</summary>
public sealed class GameplaySettings
{
    public const double MinBowlingSpeed = .5, MaxBowlingSpeed = 1, BowlingSpeedStep = .01;
    private double _bowlingSpeed = MinBowlingSpeed;

    public double BowlingSpeed
    {
        get => _bowlingSpeed;
        set
        {
            if (!double.IsFinite(value) || value < MinBowlingSpeed || value > MaxBowlingSpeed)
                throw new ArgumentOutOfRangeException(nameof(value), "Bowling speed must be between 0.5 and 1.0.");
            _bowlingSpeed = value;
        }
    }
}
