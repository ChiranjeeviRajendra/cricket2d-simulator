namespace Cricket.Physics;

public readonly record struct BallState(Vec2 Position, Vec2 Velocity, double Spin = 0)
{
    public double KineticEnergy(SimulationSettings s) =>
        0.5 * s.BallMass * Velocity.LengthSquared + 0.2 * s.BallMass * s.BallRadius * s.BallRadius * Spin * Spin;
}

public static class BallFlight
{
    public static Vec2 Acceleration(Vec2 velocity, double spin, SimulationSettings s)
    {
        double speed = velocity.Length;
        double area = Math.PI * s.BallRadius * s.BallRadius;
        double pressurePerMass = 0.5 * s.AirDensity * area / s.BallMass;
        Vec2 drag = -pressurePerMass * s.DragCoefficient * speed * velocity;
        // Saturated 2D Magnus approximation. Seam/swing in the third axis is not modelled.
        double lift = Math.Clamp(s.MagnusCoefficient * spin * s.BallRadius / Math.Max(speed, 0.1), -0.3, 0.3);
        return new Vec2(0, -s.Gravity) + drag + pressurePerMass * lift * speed * velocity.Perpendicular;
    }

    /// <summary>RK4 integration shared by live simulation and the delivery solver.</summary>
    public static BallState Integrate(BallState b, double dt, SimulationSettings s)
    {
        Vec2 a1 = Acceleration(b.Velocity, b.Spin, s);
        Vec2 v2 = b.Velocity + a1 * (dt / 2);
        Vec2 a2 = Acceleration(v2, b.Spin, s);
        Vec2 v3 = b.Velocity + a2 * (dt / 2);
        Vec2 a3 = Acceleration(v3, b.Spin, s);
        Vec2 v4 = b.Velocity + a3 * dt;
        Vec2 a4 = Acceleration(v4, b.Spin, s);
        return new(b.Position + dt / 6 * (b.Velocity + 2 * v2 + 2 * v3 + v4),
            b.Velocity + dt / 6 * (a1 + 2 * a2 + 2 * a3 + a4), b.Spin * Math.Exp(-s.SpinDecay * dt));
    }

    public static double GroundTime(BallState b, double dt, SimulationSettings s)
    {
        double lo = 0, hi = dt;
        for (int i = 0; i < 26; i++)
        {
            double mid = (lo + hi) / 2;
            if (Integrate(b, mid, s).Position.Y > s.BallRadius) lo = mid;
            else hi = mid;
        }
        return hi;
    }
}
