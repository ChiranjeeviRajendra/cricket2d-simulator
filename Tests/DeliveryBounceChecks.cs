using System.Text.Json;
using Cricket.Physics;

internal static class DeliveryBounceChecks
{
    private readonly record struct Bounce(double ReleaseAngle, double ImpactDownSpeed, double Peak, double AtBatter);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void MatchReference()
    {
        var defaults = new SimulationSettings();
        var configured = JsonSerializer.Deserialize<SimulationSettings>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Config", "physics.json")))!;
        configured.Validate();
        Check(configured == defaults, "Shipped physics and code defaults disagree.");
        // James, Carre & Haake (2004), Table 2. Pace means TOTAL speed retained;
        // bounce means rebound/incident ANGLE, not normal restitution or height ratio.
        foreach (int direction in new[] { -1, 1 })
        {
            double angle = 12.5 * Math.PI / 180;
            var incoming = new BallState(new(10, defaults.BallRadius),
                new(direction * 36 * Math.Cos(angle), -36 * Math.Sin(angle)));
            var outgoing = Contacts.GroundImpulse(incoming, configured);
            double pace = outgoing.Velocity.Length / 36;
            double bounce = Math.Atan2(outgoing.Velocity.Y, Math.Abs(outgoing.Velocity.X)) / angle;
            Check(pace is >= .8710 and <= .9015, "Pitch pace is outside the match reference range.");
            Check(bounce is >= .524 and <= .727, "Pitch bounce angle is outside the match reference range.");
            Check(Math.Abs(bounce - .65) < .03, "Default pitch missed the representative mid-range bounce target.");
            Check(outgoing.KineticEnergy(defaults) < incoming.KineticEnergy(defaults), "Pitch impact created energy.");
            if (direction == 1) Console.WriteLine($"      36 m/s, 12.5 degree impact: pace {pace:P2}, angle ratio {bounce:P2}");
        }
    }

    public static void DeliveryTrends()
    {
        var settings = new SimulationSettings();
        foreach (double speed in new[] { 110.0, 130, 150 })
        {
            Bounce full = Measure(new(speed / 3.6, 18, 2.15), settings);
            Bounce good = Measure(new(speed / 3.6, 14, 2.15), settings);
            Bounce shortBall = Measure(new(speed / 3.6, 10, 2.15), settings);
            Check(shortBall.ReleaseAngle < good.ReleaseAngle && good.ReleaseAngle < full.ReleaseAngle,
                "Shorter pace deliveries must be directed more steeply downwards.");
            Check(shortBall.ImpactDownSpeed > good.ImpactDownSpeed && good.ImpactDownSpeed > full.ImpactDownSpeed &&
                shortBall.Peak > good.Peak && good.Peak > full.Peak &&
                shortBall.AtBatter > good.AtBatter && good.AtBatter > full.AtBatter,
                "Shortening a representative pace delivery did not increase bounce.");
        }
        foreach (double length in new[] { 10.0, 14, 18 })
        {
            Bounce low = Measure(new(140 / 3.6, length, 1.8), settings);
            Bounce tall = Measure(new(140 / 3.6, length, 2.38), settings);
            Bounce slow = Measure(new(110 / 3.6, length, 2.15), settings);
            Bounce fast = Measure(new(150 / 3.6, length, 2.15), settings);
            Check(tall.Peak > low.Peak && tall.AtBatter > low.AtBatter, "Higher release did not increase pace-delivery bounce.");
            Check(fast.Peak > slow.Peak, "Faster pace delivery did not increase peak bounce.");
            // Height at a fixed batter plane also depends on time since pitching. Close to
            // that plane a slower, fuller ball has more time to rise despite its lower apex.
            if (length == 10) Check(fast.AtBatter > slow.AtBatter, "Faster short ball did not climb higher at the batter.");
            if (length == 18) Check(fast.AtBatter < slow.AtBatter, "Full delivery lost its time-of-flight dependence.");
        }
        // Independent vacuum projectile solution: fixed release speed and landing point,
        // followed by a passive vertical impulse and ballistic ascent. No drag or Magnus.
        var vacuum = settings with { AirDensity = 0, GroundFriction = 0 };
        foreach (double length in new[] { 8.0, 14, 18 })
        {
            const double speed = 40, height = 2.15;
            double distance = length - PitchDimensions.ReleaseX, drop = height - vacuum.BallRadius;
            double speedSquared = speed * speed, g = vacuum.Gravity;
            double tangent = (speedSquared - Math.Sqrt(speedSquared * speedSquared -
                g * (g * distance * distance - 2 * drop * speedSquared))) / (g * distance);
            double initialVerticalSquared = speedSquared * tangent * tangent / (1 + tangent * tangent);
            double peak = vacuum.BallRadius + vacuum.GroundRestitution * vacuum.GroundRestitution *
                (initialVerticalSquared + 2 * g * drop) / (2 * g);
            Check(Math.Abs(Measure(new(speed, length, height), vacuum).Peak - peak) < .00001,
                "Delivery bounce disagrees with the independent vacuum solution.");
        }
        var previous = settings with { GroundRestitution = .43, GroundFriction = .22 };
        Console.WriteLine("      Simulated ball-centre heights (m), batter plane x=19.08 m; not measured match trajectories:");
        Console.WriteLine("      km/h  pitch-x  release  old-batter  new-batter  new-peak");
        foreach (var request in new DeliverySettings[]
        {
            new(140 / 3.6, 18, 2.15), new(140 / 3.6, 16, 2.15), new(140 / 3.6, 14, 2.15),
            new(140 / 3.6, 12, 2.15), new(140 / 3.6, 10, 2.15), new(140 / 3.6, 8, 2.15),
            new(110 / 3.6, 10, 2.15), new(150 / 3.6, 10, 2.15),
            new(140 / 3.6, 10, 1.8), new(140 / 3.6, 10, 2.38)
        })
        {
            Bounce old = Measure(request, previous), current = Measure(request, settings);
            Console.WriteLine($"      {request.Speed * 3.6,4:0}  {request.PitchingLength,7:0.00}  {request.ReleaseHeight,7:0.00}" +
                $"  {old.AtBatter,10:0.000}  {current.AtBatter,10:0.000}  {current.Peak,8:0.000}");
        }
    }

    public static void LiveBounce()
    {
        var request = new DeliverySettings(140 / 3.6, 10, 2.15);
        double expected = Measure(request, new()).AtBatter;
        foreach (int substeps in new[] { 1, 8, 16 })
        {
            var sim = new Simulation(new() { Substeps = substeps });
            Check(sim.Begin(request, out string error), error);
            bool crossed = false;
            for (int i = 0; i < 600; i++)
            {
                BallState previous = sim.Ball;
                sim.Tick(default);
                Check(!sim.BatHit && !sim.WicketHit, "Bounce fixture was interrupted by another collision.");
                if (previous.Position.X < PitchDimensions.BatterReferenceX && sim.Ball.Position.X >= PitchDimensions.BatterReferenceX)
                {
                    double fraction = (PitchDimensions.BatterReferenceX - previous.Position.X) /
                        (sim.Ball.Position.X - previous.Position.X);
                    double actual = Vec2.Lerp(previous.Position, sim.Ball.Position, fraction).Y;
                    Check(sim.FirstBounceX.HasValue && Math.Abs(sim.FirstBounceX.Value - request.PitchingLength) < .003 &&
                        Math.Abs(actual - expected) < .001, "Live bounce disagrees with the fine-step reference.");
                    crossed = true;
                    break;
                }
            }
            Check(crossed, "Live bounce did not reach the batter plane.");
        }
    }

    private static Bounce Measure(DeliverySettings request, SimulationSettings settings)
    {
        Check(DeliverySolver.TrySolve(request, settings, out var plan, out string error), error);
        BallState ball = plan!.Release;
        const double dt = 1.0 / 3840;
        bool bounced = false, peaked = false;
        double peak = 0, atBatter = double.NaN, impactDownSpeed = 0;
        for (int i = 0; i < 3840 * 6; i++)
        {
            BallState next = BallFlight.Integrate(ball, dt, settings);
            if (next.Position.Y <= settings.BallRadius)
            {
                Check(!bounced, "Fixture bounced twice before completing the height measurements.");
                // Fine-step crossing interpolation independent of the live ground-time search.
                double fraction = (ball.Position.Y - settings.BallRadius) / (ball.Position.Y - next.Position.Y);
                var hit = new BallState(Vec2.Lerp(ball.Position, next.Position, fraction),
                    Vec2.Lerp(ball.Velocity, next.Velocity, fraction), ball.Spin + fraction * (next.Spin - ball.Spin));
                Check(Math.Abs(hit.Position.X - request.PitchingLength) < .002, "Wrong first pitching point.");
                impactDownSpeed = -hit.Velocity.Y;
                ball = Contacts.GroundImpulse(hit, settings);
                bounced = true;
                continue;
            }
            if (bounced)
            {
                peak = Math.Max(peak, next.Position.Y);
                peaked |= next.Velocity.Y <= 0;
                if (ball.Position.X < PitchDimensions.BatterReferenceX && next.Position.X >= PitchDimensions.BatterReferenceX)
                    atBatter = Vec2.Lerp(ball.Position, next.Position,
                        (PitchDimensions.BatterReferenceX - ball.Position.X) / (next.Position.X - ball.Position.X)).Y;
                if (peaked && double.IsFinite(atBatter)) return new(plan.Angle, impactDownSpeed, peak, atBatter);
            }
            ball = next;
        }
        throw new InvalidOperationException("Bounce measurement exceeded its bounded duration.");
    }
}
