using Cricket.Physics;

internal static class DeliveryTimingChecks
{
    public static void ReleaseAndAnimation()
    {
        foreach (double speed in new[] { .5, .75, 1 }) CheckReleaseAndAnimation(speed);
    }

    private static void CheckReleaseAndAnimation(double speed)
    {
        var settings = new SimulationSettings { AirDensity = 0, SpinDecay = 0 };
        var sim = new Simulation(settings, gameplay: new() { BowlingSpeed = speed });
        Require(sim.Begin(new(32, 16, 2.15), out var error), error);
        var rig = new BattingRig(settings, sim.Batting);
        BallState release = sim.Plan!.Release;
        for (int tick = 1; tick <= 150; tick++)
        {
            var input = BattingChecks.AtClock(255 - Math.Clamp((tick - 100) * 3.0, 0, 255), front: .5);
            sim.Tick(input);
            rig.Step(input, SimulationSettings.Tick, settings);
            Require(sim.Batter.Pose == rig.Pose, "Windup slowed the batsman.");
            Require(sim.Ball == release, "Ball moved before release.");
            Require(sim.Phase == (tick == 150 ? DeliveryPhase.Live : DeliveryPhase.Windup), "Windup duration changed.");
        }
        for (int tick = 1; tick <= 30; tick++)
        {
            var input = BattingChecks.AtClock(105 - tick, back: .4);
            sim.Tick(input);
            rig.Step(input, SimulationSettings.Tick, settings);
            Require(sim.Batter.Pose == rig.Pose, "Incoming ball slow motion changed batting speed.");
            // Independent analytical flight at the selected rate, without affecting the rig or gameplay clock.
            Near(Vacuum(release, tick * SimulationSettings.Tick * speed, settings), sim.Ball, "Selected incoming flight speed");
            Require(Math.Abs(sim.PhaseTime - tick * SimulationSettings.Tick) < 1e-10, "Live clock was slowed.");
        }
        sim.ReturnToSetup();
        BallState stopped = sim.Ball;
        for (int tick = 0; tick < 30; tick++) sim.Tick(default);
        Require(sim.Ball == stopped && sim.PhaseTime == 0, "Setup advanced the ball.");
    }

    public static void NonBatContacts()
    {
        var settings = new SimulationSettings { AirDensity = 0, SpinDecay = 0 };
        var sim = Released(settings);
        int ticks = 0;
        while (!sim.FirstBounceX.HasValue && ticks < 600) { sim.Tick(default); ticks++; }
        Require(sim.FirstBounceX.HasValue && !sim.BatHit, "Pitch fixture missed its first bounce.");
        Require(Math.Abs(ticks * SimulationSettings.Tick - 2 * sim.Plan!.FlightTime) <= SimulationSettings.Tick + 1e-6,
            "Pitch contact did not take twice the physical flight time.");
        BallState before = sim.Ball;
        sim.Tick(default);
        Near(Vacuum(before, SimulationSettings.Tick * .5, settings), sim.Ball, "Slow motion after pitch bounce");

        sim = Released(settings);
        SetBall(sim, new(new(PitchDimensions.WicketSeparation - .15, .4), new(32, 0)));
        for (int tick = 0; tick < 5 && !sim.WicketHit; tick++) sim.Tick(default);
        Require(sim.WicketHit && !sim.BatHit, "Wicket fixture did not hit the stumps without the bat.");
        before = sim.Ball;
        sim.Tick(default);
        Near(Vacuum(before, SimulationSettings.Tick * .5, settings), sim.Ball, "Slow motion after wicket contact");
    }

    public static void BatImpactAndReset()
    {
        foreach (double speed in new[] { .5, .75, 1 })
        foreach (int substeps in new[] { 1, 8, 16 })
        {
            // Negligible gravity isolates the exact contact time and outgoing travel distance.
            var settings = new SimulationSettings { AirDensity = 0, Gravity = 1e-8, SpinDecay = 0, Substeps = substeps };
            var sim = Released(settings, speed);
            BatPose bat = sim.Batter.Pose.Bat;
            Capsule blade = bat.Blade(settings);
            Vec2 middle = (blade.A + blade.B) * .5;
            Vec2 normal = bat.Direction.Perpendicular;
            if (normal.X > 0) normal = -normal;
            double impactTime = SimulationSettings.Tick / substeps * .25;
            double gap = 32 * speed * impactTime;
            SetBall(sim, new(middle + normal * (settings.BallRadius + blade.Radius + gap), -normal * 32));
            sim.Tick(default);
            Require(sim.BatHit && sim.Batter.Pose.Bat == bat, "Stationary bat fixture did not register an impact.");
            Require(sim.Events.Count == 1 && sim.Events[0].Kind == ContactKind.Bat, "Unexpected contact in bat timing fixture.");
            ContactEvent hit = sim.Events[0];
            Require(Math.Abs(hit.SpeedBefore - 32) < 1e-7, "Slow motion reduced physical impact speed.");
            Vec2 expectedPosition = hit.Position + sim.Ball.Velocity * (SimulationSettings.Tick - impactTime);
            Require((sim.Ball.Position - expectedPosition).Length < 1e-7,
                "Ball did not return to full speed during the remainder of the impact tick.");
            BallState before = sim.Ball;
            sim.Tick(default);
            Near(Vacuum(before, SimulationSettings.Tick, settings), sim.Ball, "Normal speed after bat impact");

            sim.ReturnToSetup();
            Require(sim.BeginPrepared(sim.Plan!, out var error), error);
            for (int tick = 0; tick < 150; tick++) sim.Tick(default);
            Require(!sim.BatHit && sim.Phase == DeliveryPhase.Live, "Next delivery retained the previous hit.");
            before = sim.Ball;
            sim.Tick(default);
            Near(Vacuum(before, SimulationSettings.Tick * speed, settings), sim.Ball, "Selected speed on the next delivery");
        }
    }

    private static Simulation Released(SimulationSettings settings, double speed = .5)
    {
        var sim = new Simulation(settings, gameplay: new() { BowlingSpeed = speed });
        Require(sim.Begin(new(32, 16, 2.15), out var error), error);
        for (int tick = 0; tick < 150; tick++) sim.Tick(default);
        Require(sim.Phase == DeliveryPhase.Live, "Delivery did not release.");
        return sim;
    }

    private static BallState Vacuum(BallState ball, double time, SimulationSettings settings) =>
        new(ball.Position + ball.Velocity * time + new Vec2(0, -.5 * settings.Gravity * time * time),
            ball.Velocity + new Vec2(0, -settings.Gravity * time), ball.Spin);

    private static void SetBall(Simulation sim, BallState ball)
    {
        typeof(Simulation).GetProperty(nameof(Simulation.Ball))!.SetValue(sim, ball);
        typeof(Simulation).GetProperty(nameof(Simulation.PreviousBall))!.SetValue(sim, ball);
    }

    private static void Near(BallState expected, BallState actual, string message) =>
        Require((expected.Position - actual.Position).Length < 1e-7 &&
            (expected.Velocity - actual.Velocity).Length < 1e-7 && Math.Abs(expected.Spin - actual.Spin) < 1e-7, message);

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
