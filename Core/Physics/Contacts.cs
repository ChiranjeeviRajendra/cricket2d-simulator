namespace Cricket.Physics;

public readonly record struct Capsule(Vec2 A, Vec2 B, double Radius)
{
    public static Capsule Lerp(Capsule a, Capsule b, double t) =>
        new(Vec2.Lerp(a.A, b.A, t), Vec2.Lerp(a.B, b.B, t), a.Radius);
    public Vec2 Closest(Vec2 p)
    {
        Vec2 axis = B - A;
        double t = Math.Clamp(Vec2.Dot(p - A, axis) / Math.Max(axis.LengthSquared, 1e-16), 0, 1);
        return A + t * axis;
    }
}

public static class Contacts
{
    /// <summary>
    /// Conservative advancement against a translating/rotating capsule. A Lipschitz bound
    /// prevents stepping across thin objects, even when neither endpoint overlaps.
    /// Endpoints interpolate over one 1/960 s substep, bounding rotational approximation.
    /// </summary>
    public static bool Sweep(Vec2 start, Vec2 end, double radius, Capsule from, Capsule to,
        out double fraction, out Vec2 normal)
    {
        double travel = (end - start).Length + Math.Max((to.A - from.A).Length, (to.B - from.B).Length);
        fraction = 0;
        normal = new(0, 1);
        for (int i = 0; i < 96; i++)
        {
            Capsule shape = Capsule.Lerp(from, to, fraction);
            Vec2 p = Vec2.Lerp(start, end, fraction);
            Vec2 offset = p - shape.Closest(p);
            double gap = offset.Length - radius - shape.Radius;
            if (gap <= 1e-7) { normal = offset.Normalized; return true; }
            if (travel < 1e-12) return false;
            fraction += Math.Max(gap / travel * 0.95, 1e-9);
            if (fraction > 1) return false;
        }
        return false;
    }

    public static BallState GroundImpulse(BallState ball, SimulationSettings s)
    {
        if (ball.Velocity.Y >= 0) return ball;
        double normalImpulse = -(1 + s.GroundRestitution) * s.BallMass * ball.Velocity.Y;
        // Surface slip at the bottom of a solid sphere: vx + omega * radius.
        double slip = ball.Velocity.X + ball.Spin * s.BallRadius;
        double inertia = 0.4 * s.BallMass * s.BallRadius * s.BallRadius;
        double tangentImpulse = Math.Clamp(-slip / (1 / s.BallMass + s.BallRadius * s.BallRadius / inertia),
            -s.GroundFriction * normalImpulse, s.GroundFriction * normalImpulse);
        return new(new(ball.Position.X, s.BallRadius),
            new(ball.Velocity.X + tangentImpulse / s.BallMass, -s.GroundRestitution * ball.Velocity.Y),
            ball.Spin + s.BallRadius * tangentImpulse / inertia);
    }

    /// <summary>Supported contact: friction first removes slip, then rolling resistance slows the ball.</summary>
    public static BallState GroundMotion(BallState ball, double dt, SimulationSettings s)
    {
        double vx = ball.Velocity.X + BallFlight.Acceleration(new(ball.Velocity.X, 0), 0, s).X * dt;
        double inertia = 0.4 * s.BallMass * s.BallRadius * s.BallRadius;
        double slip = vx + ball.Spin * s.BallRadius;
        double requiredImpulse = -slip / (1 / s.BallMass + s.BallRadius * s.BallRadius / inertia);
        double limit = s.GroundFriction * s.BallMass * s.Gravity * dt;
        double impulse = Math.Clamp(requiredImpulse, -limit, limit);
        vx += impulse / s.BallMass;
        double spin = ball.Spin + s.BallRadius * impulse / inertia;
        if (Math.Abs(requiredImpulse) <= limit)
        {
            vx = Math.Sign(vx) * Math.Max(0, Math.Abs(vx) - s.RollingResistance * s.Gravity * dt);
            spin = -vx / s.BallRadius;
        }
        return new(new(ball.Position.X + (ball.Velocity.X + vx) * dt / 2, s.BallRadius), new(vx, 0), spin);
    }

    public static BallState BatImpulse(BallState ball, Vec2 normal, Vec2 contactVelocity,
        Vec2 leverFromGrip, SimulationSettings s, out double normalImpulse)
    {
        Vec2 relative = ball.Velocity - contactVelocity;
        double closing = Vec2.Dot(relative, normal);
        normalImpulse = 0;
        if (closing >= 0) return ball;
        double lever = Vec2.Cross(leverFromGrip, normal);
        double inverseMass = 1 / s.BallMass + 1 / s.BatMass + lever * lever / s.BatInertiaAboutGrip;
        normalImpulse = -(1 + s.BatRestitution) * closing / inverseMass;
        Vec2 tangent = normal.Perpendicular;
        double inertia = 0.4 * s.BallMass * s.BallRadius * s.BallRadius;
        double tangentLever = Vec2.Cross(leverFromGrip, tangent);
        double slip = Vec2.Dot(relative, tangent) - ball.Spin * s.BallRadius;
        double tangentInverseMass = 1 / s.BallMass + 1 / s.BatMass +
            s.BallRadius * s.BallRadius / inertia + tangentLever * tangentLever / s.BatInertiaAboutGrip;
        double jt = Math.Clamp(-slip / tangentInverseMass, -s.BatFriction * normalImpulse, s.BatFriction * normalImpulse);
        return ball with
        {
            Velocity = ball.Velocity + (normalImpulse * normal + jt * tangent) / s.BallMass,
            Spin = ball.Spin - s.BallRadius * jt / inertia
        };
    }
}
