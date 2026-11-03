namespace Cricket.Physics;

/// <summary>Per-delivery maxima in SI units, sampled at physics substeps and contacts.</summary>
public sealed class DeliveryStatistics
{
    public double MaxSpeed { get; private set; }
    public double MaxLengthFromBatter { get; private set; }
    public double MaxHeight { get; private set; }

    public void Reset() => MaxSpeed = MaxLengthFromBatter = MaxHeight = 0;

    public void Observe(BallState ball)
    {
        MaxSpeed = Math.Max(MaxSpeed, ball.Velocity.Length);
        MaxLengthFromBatter = Math.Max(MaxLengthFromBatter,
            Math.Abs(ball.Position.X - PitchDimensions.BatterReferenceX));
        MaxHeight = Math.Max(MaxHeight, ball.Position.Y);
    }
}
