using Cricket.Physics;

namespace Cricket2D.Presentation;

/// <summary>Fixed field marks measured from the batter's normal stance at ground level.</summary>
public static class FieldLayout
{
    public const double BatterReferenceX = PitchDimensions.BatterReferenceX;
    public const double LeftBoundaryDistance = 37;
    public const double RightBoundaryDistance = 75;
    public const double LeftBoundaryX = BatterReferenceX + LeftBoundaryDistance;
    public const double RightBoundaryX = BatterReferenceX - RightBoundaryDistance;

    public static double BallHeight(Vec2 position) => Math.Max(0, position.Y);
}
