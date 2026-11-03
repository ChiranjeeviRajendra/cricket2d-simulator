using Cricket.Physics;

namespace Cricket2D.Presentation;

/// <summary>The full rail measures 25 m, from the bowler on the right to the left-hand limit.</summary>
public static class PitchLengthScale
{
    public static double FractionForMetres(double metres) => 1 - metres / DeliverySettings.MaxPitchingLength;
    public static double SelectMetres(double fraction) => Math.Round(Math.Clamp(
        (1 - fraction) * DeliverySettings.MaxPitchingLength,
        DeliverySettings.MinPitchingLength, DeliverySettings.MaxPitchingLength), 2, MidpointRounding.AwayFromZero);
}
