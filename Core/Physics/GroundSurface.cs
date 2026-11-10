namespace Cricket.Physics;

/// <summary>Surface under the ball in the 2D contact plane; no per-tick material allocations.</summary>
public readonly record struct GroundSurface(double Restitution, double Friction,
    double RollingResistance, double TangentialRetention)
{
    public static GroundSurface At(double x, SimulationSettings s) =>
        x >= -PitchDimensions.PreparedPitchMargin && x <= PitchDimensions.WicketSeparation + PitchDimensions.PreparedPitchMargin
            ? new(s.GroundRestitution, s.GroundFriction, s.RollingResistance, 1)
            : new(s.OutfieldRestitution, s.OutfieldFriction, s.OutfieldRollingResistance, s.OutfieldTangentialRetention);
}
