using System.Reflection;
using Cricket.Physics;

internal static class BallSurfaceChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void TurfBounce()
    {
        double pitch = DropHeight(10), outfield = DropHeight(30);
        Console.WriteLine($"      2 m drop: pitch rebound {pitch:0.000} m; outfield rebound {outfield:0.000} m");
        // SG leather/turf drop measurements (Mondal et al., 2025) give e around .40-.44.
        // The outfield envelope is the AFL/CA 2018 synthetic-turf reference, not a universal grass law.
        Check(pitch is >= .30 and <= .41, "Default pitch bounce is outside the measured leather/turf reference envelope.");
        Check(outfield is >= .10 and <= .40 && outfield < pitch, "Outfield did not absorb more vertical impact energy than the strip.");
    }

    public static void OutfieldRoll()
    {
        var settings = new SimulationSettings();
        // Ideal solid-sphere ramp exit from 1 m: mgh = (1/2 + 1/5) m v^2.
        // An upper-bound entrance-speed approximation; actual ramps also dissipate energy.
        double speed = Math.Sqrt(10 * settings.Gravity / 7);
        foreach (int direction in new[] { -1, 1 })
        {
            double start = direction > 0 ? 30 : -10;
            var ball = new BallState(new(start, settings.BallRadius), new(direction * speed, 0), -direction * speed / settings.BallRadius);
            int ticks = 0;
            for (; ticks < 60000 && ball.Velocity.LengthSquared > 0; ticks++)
            {
                double energy = ball.KineticEnergy(settings);
                ball = Contacts.GroundMotion(ball, 1.0 / 960, settings);
                Check(ball.KineticEnergy(settings) <= energy + 1e-10, "Rolling gained energy.");
                Check(ball.Velocity.X * direction >= 0, "Rolling resistance reversed the ball.");
                Check(Math.Abs(ball.Velocity.X + settings.BallRadius * ball.Spin) < 1e-9, "Rolling contact slipped.");
            }
            double distance = Math.Abs(ball.Position.X - start);
            Console.WriteLine($"      outfield ramp-equivalent roll: {distance:0.00} m in {ticks / 960.0:0.00} s");
            Check(ticks < 60000 && distance is >= 3.5 and <= 15, "Outfield roll exceeds the AFL/CA reference range.");
            var rest = ball;
            for (int i = 0; i < 960; i++) ball = Contacts.GroundMotion(ball, 1.0 / 960, settings);
            Check(ball == rest && ball.Spin == 0, "A settled ball drifted or kept spinning.");
        }
    }

    public static void AngledOutfieldBounce()
    {
        var settings = new SimulationSettings();
        double speed = SpeedUnits.ToMetresPerSecond(75), angle = 15 * Math.PI / 180;
        var ball = new BallState(new(30, settings.BallRadius), new(speed * Math.Cos(angle), -speed * Math.Sin(angle)));
        var rebound = Contacts.GroundImpulse(ball, settings);
        double retention = rebound.Velocity.X / ball.Velocity.X;
        Console.WriteLine($"      outfield 75 km/h, 15 degree impact: horizontal speed retained {retention:P1}");
        Check(retention is >= .35 and <= .60, "Outfield rebound retains too much horizontal speed for the reference turf test.");
        Check(rebound.KineticEnergy(settings) < ball.KineticEnergy(settings), "Outfield impact added energy.");
    }

    public static void SurfaceTransitions()
    {
        var s = new SimulationSettings { AirDensity = 0 };
        double rightEdge = PitchDimensions.WicketSeparation + PitchDimensions.PreparedPitchMargin;
        Check(GroundSurface.At(-PitchDimensions.PreparedPitchMargin, s).Restitution == s.GroundRestitution &&
            GroundSurface.At(rightEdge, s).Restitution == s.GroundRestitution &&
            GroundSurface.At(-PitchDimensions.PreparedPitchMargin - .0001, s).Restitution == s.OutfieldRestitution &&
            GroundSurface.At(rightEdge + .0001, s).Restitution == s.OutfieldRestitution,
            "Ground materials do not match the visible pitch edges.");
        foreach (int direction in new[] { -1, 1 })
        foreach (int frequency in new[] { 120, 960 })
        {
            double start = direction > 0 ? rightEdge - .12 : -PitchDimensions.PreparedPitchMargin + .12;
            var ball = new BallState(new(start, s.BallRadius), new(direction * 3, 0), -direction * 3 / s.BallRadius);
            // Independent work/energy solution on two flat materials, without air drag.
            double crossingSpeedSquared = 9 - 2 * s.RollingResistance * s.Gravity * .12;
            double expected = .12 + crossingSpeedSquared / (2 * s.OutfieldRollingResistance * s.Gravity);
            for (int i = 0; i < frequency * 30 && ball.Velocity.LengthSquared > 0; i++)
                ball = Contacts.GroundMotion(ball, 1.0 / frequency, s);
            Check(ball.Velocity == Vec2.Zero && Math.Abs(Math.Abs(ball.Position.X - start) - expected) < .02,
                "Pitch/outfield transition does not agree with the work/energy stopping distance.");
        }
    }

    public static void PassiveContact()
    {
        var random = new Random(907);
        var s = new SimulationSettings();
        foreach (double x in new[] { -10.0, 10, 30 })
        for (int i = 0; i < 1000; i++)
        {
            var ball = new BallState(new(x, s.BallRadius),
                new(random.NextDouble() * 100 - 50, -.001 - random.NextDouble() * 40), random.NextDouble() * 2000 - 1000);
            var after = Contacts.GroundImpulse(ball, s);
            Check(after.Velocity.Y >= 0 && after.KineticEnergy(s) <= ball.KineticEnergy(s) + 1e-9,
                "A ground impact injected energy.");
            // Include arbitrary initial spin/slip; dry friction may transfer spin energy into translation.
            ball = after with { Velocity = new(after.Velocity.X, 0) };
            for (int tick = 0; tick < 120; tick++)
            {
                var next = Contacts.GroundMotion(ball, 1.0 / 960, s);
                Check(next.KineticEnergy(s) <= ball.KineticEnergy(s) + 1e-9 && double.IsFinite(next.Spin),
                    "Sliding/spinning contact created energy or became non-finite.");
                ball = next;
            }
        }
        // Grass deformation losses must vanish at grazing incidence instead of abruptly slowing a tiny hop.
        var grazing = new BallState(new(30, s.BallRadius), new(8, -1e-6), -8 / s.BallRadius);
        var gentle = Contacts.GroundImpulse(grazing, s);
        Check(Math.Abs(gentle.Velocity.X - grazing.Velocity.X) < 1e-5, "A grazing impact caused a discontinuous speed drop.");
    }

    public static void LiveRollAndSettings()
    {
        var s = new SimulationSettings();
        var ball = new BallState(new(30, s.BallRadius), new(3, 0), -3 / s.BallRadius);
        var sim = Start(ball, s);
        for (int i = 0; i < 1200; i++) sim.Tick(default);
        Check(sim.Ball.Velocity == Vec2.Zero && sim.Ball.Spin == 0 && sim.Ball.Position.X is > 33 and < 38,
            "The live simulation did not use outfield rolling resistance.");
        BallState settled = sim.Ball;
        for (int i = 0; i < 1200; i++) sim.Tick(default);
        Check(sim.Ball == settled, "A stopped live ball resumed moving.");
        foreach (var invalid in new[]
        {
            s with { OutfieldRestitution = double.NaN }, s with { OutfieldRestitution = 1.01 },
            s with { OutfieldFriction = -.01 }, s with { OutfieldRollingResistance = double.PositiveInfinity },
            s with { OutfieldTangentialRetention = -.01 }, s with { OutfieldTangentialRetention = 1.01 }
        })
        {
            bool rejected = false;
            try { invalid.Validate(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid outfield coefficients were accepted.");
        }
        var configured = s with { OutfieldRestitution = .5, OutfieldFriction = 0, OutfieldTangentialRetention = 1 };
        var rebound = Contacts.GroundImpulse(new(new(30, s.BallRadius), new(10, -4)), configured);
        Check(rebound.Velocity == new Vec2(10, 2), "Custom outfield coefficients were ignored.");
    }

    private static double DropHeight(double x)
    {
        var settings = new SimulationSettings();
        var sim = Start(new(new(x, 2 + settings.BallRadius), Vec2.Zero), settings);
        bool bounced = false;
        double peak = 0;
        for (int i = 0; i < 1200; i++)
        {
            sim.Tick(default);
            bool impact = sim.Events.Any(e => e.Kind == ContactKind.Pitch);
            if (impact && bounced) return peak;
            bounced |= impact;
            if (bounced) peak = Math.Max(peak, sim.Ball.Position.Y - settings.BallRadius);
        }
        throw new InvalidOperationException("Drop test did not finish two bounces.");
    }

    private static Simulation Start(BallState ball, SimulationSettings settings)
    {
        var sim = new Simulation(settings);
        Check(sim.Begin(new(32, 16, 2.15), out _), "Could not prepare surface test.");
        for (int i = 0; i < 200 && sim.Phase != DeliveryPhase.Live; i++) sim.Tick(default);
        typeof(Simulation).GetProperty(nameof(Simulation.Ball), BindingFlags.Public | BindingFlags.Instance)!.SetValue(sim, ball);
        typeof(Simulation).GetProperty(nameof(Simulation.PreviousBall))!.SetValue(sim, ball);
        return sim;
    }
}
