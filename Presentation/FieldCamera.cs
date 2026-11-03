using Cricket.Physics;

namespace Cricket2D.Presentation;

/// <summary>
/// Presentation-only framing in logical canvas pixels. Physical x increases to the left.
/// Both wickets and the ball always fit; the view only expands during a delivery.
/// </summary>
public sealed class FieldCamera
{
    public const double Left = 82;
    public const double Right = 1354;
    public const double Top = 106;
    public const double GroundY = 760;
    public const double BallPadding = 18;
    public const double BasePixelsPerMetre = (Right - Left) / PitchDimensions.WicketSeparation;

    public double PixelsPerMetre { get; private set; } = BasePixelsPerMetre;
    public double WorldMinX { get; private set; }
    public double WorldMaxX => WorldMinX + (Right - Left) / PixelsPerMetre;
    public double Zoom => PixelsPerMetre / BasePixelsPerMetre;

    public void Reset()
    {
        PixelsPerMetre = BasePixelsPerMetre;
        WorldMinX = 0;
    }

    public void Fit(Vec2 ball)
    {
        double pitch = PitchDimensions.WicketSeparation;
        double width = Right - Left;
        double requiredScale = BasePixelsPerMetre;
        // The farther wicket anchors the opposite screen edge. Include pixel padding
        // around the ball, even at large distances and when it is above the pitch.
        if (ball.X > pitch / 2)
            requiredScale = Math.Min(requiredScale, (width - BallPadding) / ball.X);
        else
            requiredScale = Math.Min(requiredScale, (width - BallPadding) / (pitch - ball.X));
        if (ball.Y > 0)
            requiredScale = Math.Min(requiredScale, (GroundY - Top - BallPadding) / ball.Y);

        PixelsPerMetre = Math.Min(PixelsPerMetre, requiredScale);
        double visibleWidth = width / PixelsPerMetre;
        // Transition continuously while the ball crosses the pitch. Outside it,
        // the wickets stay pinned toward the edge opposite the ball.
        double anchor = Math.Clamp(ball.X / pitch, 0, 1);
        WorldMinX = (pitch - visibleWidth) * (1 - anchor);
    }

    public Vec2 Project(Vec2 world) => new(
        Right - (world.X - WorldMinX) * PixelsPerMetre,
        GroundY - world.Y * PixelsPerMetre);

    public bool Contains(Vec2 world)
    {
        Vec2 screen = Project(world);
        return screen.X >= Left - 1e-8 && screen.X <= Right + 1e-8 &&
            screen.Y >= Top - 1e-8 && screen.Y <= GroundY + 1e-8;
    }
}
