namespace Cricket.Physics;

public readonly record struct FieldSurface(Capsule Edge, Vec2 Normal, ContactKind Kind,
    double Restitution, double Friction);

/// <summary>Convex wedge/wall profiles in the ball's vertical plane. No display units.</summary>
public sealed class FieldGeometry
{
    public FieldSettings Settings { get; }
    public double LeftX => PitchDimensions.BatterReferenceX + Settings.Boundary.LeftDistanceMetres;
    public double RightX => PitchDimensions.BatterReferenceX - Settings.Boundary.RightDistanceMetres;
    public double CentreX => (LeftX + RightX) / 2;
    public double RadiusX => (LeftX - RightX) / 2;
    public double BarricadeOffset => Settings.Boundary.WidthMetres + Settings.Barricade.ClearGapMetres;
    public double ParkingOffset => BarricadeOffset + Settings.Barricade.ThicknessMetres + Settings.Parking.VergeMetres;
    public IReadOnlyList<FieldSurface> Surfaces { get; }

    public FieldGeometry(FieldSettings settings, SimulationSettings physics)
    {
        settings.Validate();
        Settings = settings;
        var surfaces = new List<FieldSurface>();
        foreach (int side in new[] { -1, 1 })
        {
            double x = side > 0 ? LeftX : RightX;
            var b = settings.Boundary;
            Vec2 toe = new(x, 0), peak = new(x + side * b.WidthMetres / 2, b.HeightMetres),
                heel = new(x + side * b.WidthMetres, 0);
            Add(toe, peak, new(-side * b.HeightMetres, b.WidthMetres / 2), ContactKind.Boundary, b.Restitution, b.Friction);
            Add(peak, heel, new(side * b.HeightMetres, b.WidthMetres / 2), ContactKind.Boundary, b.Restitution, b.Friction);
            var wall = settings.Barricade;
            double inner = x + side * BarricadeOffset, outer = inner + side * wall.ThicknessMetres;
            double e = wall.Restitution ?? physics.GroundRestitution, mu = wall.Friction ?? physics.GroundFriction;
            Add(new(inner, 0), new(inner, wall.HeightMetres), new(-side, 0), ContactKind.Barricade, e, mu);
            Add(new(inner, wall.HeightMetres), new(outer, wall.HeightMetres), new(0, 1), ContactKind.Barricade, e, mu);
            Add(new(outer, wall.HeightMetres), new(outer, 0), new(side, 0), ContactKind.Barricade, e, mu);
        }
        Surfaces = surfaces.AsReadOnly();
        void Add(Vec2 a, Vec2 b, Vec2 n, ContactKind kind, double e, double mu) =>
            surfaces.Add(new(new(a, b, 0), n.Normalized, kind, e, mu));
    }

    /// <summary>Ground-plane (x, depth) point at an exact outward normal offset from the ellipse.</summary>
    public Vec2 OvalPoint(double angle, double offset = 0)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle), depth = Settings.Boundary.DepthRadiusMetres;
        Vec2 normal = new Vec2(c / RadiusX, s / depth).Normalized;
        return new Vec2(CentreX + RadiusX * c, depth * s) + offset * normal;
    }

    public bool TrySupport(BallState ball, double radius, out FieldSurface surface)
    {
        for (int i = 0; i < Surfaces.Count; i++)
        {
            var candidate = Surfaces[i];
            if (candidate.Normal.Y <= .05) continue;
            Vec2 edge = candidate.Edge.B - candidate.Edge.A;
            double along = Vec2.Dot(ball.Position - candidate.Edge.A, edge) / edge.LengthSquared;
            if (along <= 1e-7 || along >= 1 - 1e-7) continue;
            double distance = Vec2.Dot(ball.Position - candidate.Edge.A, candidate.Normal);
            if (Math.Abs(distance - radius) <= 3e-5 && Math.Abs(Vec2.Dot(ball.Velocity, candidate.Normal)) < .08)
            { surface = candidate; return true; }
        }
        surface = default;
        return false;
    }
}
