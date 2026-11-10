using Cricket.Physics;
using Cricket2D.Presentation;

// Dependency-free executable tests: failures return nonzero, suitable for CI and offline PCs.
var tests = new (string Name, Action Run)[]
{
    ("Vacuum flight matches analytical gravity", Vacuum),
    ("Quadratic drag slows horizontal flight", Drag),
    ("1,000 oblique bounces dissipate energy including spin", BounceEnergy),
    ("Sliding ball loses energy before transitioning to rolling", GroundMotion),
    ("Configured pitch drops and outfield bounce remain consistent", BallSurfaceChecks.TurfBounce),
    ("Prepared-pitch rebound matches measured seam-delivery pace and angle", DeliveryBounceChecks.MatchReference),
    ("Release height, speed and length produce physical bounce trends", DeliveryBounceChecks.DeliveryTrends),
    ("Live delivery bounce agrees across physics substeps", DeliveryBounceChecks.LiveBounce),
    ("Outfield rolling stops within the reference ramp-test range", BallSurfaceChecks.OutfieldRoll),
    ("Grass absorbs horizontal speed in an oblique impact", BallSurfaceChecks.AngledOutfieldBounce),
    ("Rolling across pitch edges agrees with independent work/energy predictions", BallSurfaceChecks.SurfaceTransitions),
    ("Pitch/outfield impacts and spinning contact cannot create energy", BallSurfaceChecks.PassiveContact),
    ("Live outfield balls settle and surface coefficients remain configurable", BallSurfaceChecks.LiveRollAndSettings),
    ("Stationary bat is passive; moving bat transfers momentum", BatImpulse),
    ("Swept collision catches a 50 m/s ball and a moving bat", SweptContact),
    ("Delivery solver hits requested first-pitch locations", Deliveries),
    ("Bad inputs and physically unreachable lengths are rejected", Validation),
    ("Exact wicket spacing and inclusive delivery limits", DeliveryBounds),
    ("Delivery waits indefinitely for a manual return to setup", Lifecycle),
    ("Per-delivery maxima retain peak speed, horizontal distance and height", DeliveryMaxima),
    ("Speed entry converts km/h to the unchanged SI simulation", SpeedConversion),
    ("Live simulation agrees with delivery solver", LiveLanding),
    ("Playable swing sequences produce bat contact", PlayableSwing),
    ("Ball passes through the visible batter; wicket stays collidable", InvulnerableBatter),
    ("Only deliberate rotation advances the swing; pushes and jitter do not", BattingChecks.Controls),
    ("Right stick is disabled for batting", BattingChecks.RightStickDisabled),
    ("Bat speed ramps to 136 km/h and releases brake within ten degrees", SwingDynamicsChecks.SpeedAndBrake),
    ("Default weight, tall back-foot stance and ground-transparent blade", SwingDynamicsChecks.StanceAndGround),
    ("New deliberate rotations re-engage without teleporting the held bat", BattingChecks.Reengagement),
    ("Four held shot modifiers produce distinct constrained finishes", BattingChecks.Shots),
    ("Shot modes change before, during and after a stroke without re-arming", BattingChecks.LiveModes),
    ("Y follows A to 2, then turns its blade to 3 as arms reach 12", BattingChecks.HighFinish),
    ("Progressive trigger transfer, rear-knee lowering and opposing inputs", BattingChecks.WeightTransfer),
    ("Anatomical reach and continuous randomized movement", BattingChecks.Anatomy),
    ("Large radial deadzone and rotation threshold reject slight movement", BattingChecks.DeadZones),
    ("Bat orientation changes launch through collision physics", BattingChecks.PhysicsResponse),
    ("Bat inertia, isolated momentum and impact energy remain physical", BattingChecks.CricketPhysics),
    ("Batsman configuration survives delivery resets", BattingChecks.Configuration),
    ("Supported tuning extremes preserve limbs", BattingChecks.TuningEnvelope),
    ("Invalid and obsolete batsman settings are rejected", BattingChecks.InvalidConfiguration),
    ("Repeated inputs reproduce the same state", Determinism),
    ("Foot contacts stay planted and one foot always supports the body", AnimationChecks.Footwork),
    ("Interpolation preserves anatomy through trigger reversals", AnimationChecks.Interpolation),
    ("Preview playback agrees at 30/60/144 FPS, pauses and steps", AnimationChecks.Playback),
    ("Preview reload is atomic", AnimationChecks.Reload),
    ("Pitch slider preserves stump spacing and the 25 m extension", PitchLengthScaling),
    ("Preview solver cancels stale work and publishes only the latest slider values", PerformanceChecks.PreviewWorker),
    ("Five bowler profiles classify every manual speed and include random endpoints", PracticeChecks.Profiles),
    ("Seeded practice deliveries respect profiles, manual values and solver accuracy", PracticeChecks.Generation),
    ("Practice generation bounds retries, cancels and reports unreachable settings", PracticeChecks.Failures),
    ("Pending practice balls survive guide/menu changes and start the exact preview", PracticeChecks.Session),
    ("Leaving practice restores defaults and cancels stale delivery preparation", PracticeChecks.Reset),
    ("Steady physics avoids per-tick heap allocation and bounds the trail", PerformanceChecks.SteadySimulationAllocations),
    ("Impact sounds classify bat contact, suppress chatter and rotate configured variants", AudioChecks.Selection),
    ("Audio configuration rejects invalid rules, volumes and sample paths", AudioChecks.Configuration),
    ("Live collision events expose bounded contact measurements for audio", AudioChecks.LiveContacts),
    ("Render rates of 30, 60 and 144 Hz yield identical simulation", RenderRates),
    ("Camera fits the ball and both wickets in either direction", CameraFraming),
    ("Camera retains zoom during flight and resets for the next ball", CameraReset),
    ("High shots fit vertically without losing either wicket", HighShotFraming),
    ("Boundary distances and live height are independent of camera zoom", FieldMeasurements),
    ("Configurable oval geometry shares physical crossings and a constant barricade gap", FieldChecks.Layout),
    ("Oblique field impacts dissipate energy, transfer spin and cannot tunnel", FieldChecks.Impacts),
    ("Live balls hit wedges and barricades, clear their tops and settle", FieldChecks.LiveContacts),
    ("Outer barricade matches inner-wall response and cannot tunnel", FieldChecks.OuterBarricadeContacts),
    ("Stepped seating matches both walls, keeps aisles clear and collides with lofted shots", FieldChecks.Seating),
    ("Slope support obeys gravity and field impacts are render-rate independent", FieldChecks.SupportedMotion)
};
int failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS  {test.Name}"); }
    catch (Exception e) { failures++; Console.Error.WriteLine($"FAIL  {test.Name}: {e.Message}"); }
}
Console.WriteLine($"\n{tests.Length - failures}/{tests.Length} checks passed.");
return failures == 0 ? 0 : 1;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void PitchLengthScaling()
{
    Near(1, PitchLengthScale.FractionForMetres(0), 1e-12, "Bowler at right edge");
    Near(0, PitchLengthScale.FractionForMetres(25), 1e-12, "25 m at left edge");
    Near(.1952, PitchLengthScale.FractionForMetres(PitchDimensions.WicketSeparation), 1e-12, "4.88 m extension beyond batter");
    foreach (double width in new[] { 320.0, 740, 1440 })
    {
        double bowler = width * PitchLengthScale.FractionForMetres(0);
        double batter = width * PitchLengthScale.FractionForMetres(PitchDimensions.WicketSeparation);
        Near(20.12, (bowler - batter) / (width / 25), 1e-12, "Stump separation at resized widths");
    }
    foreach (double metres in new[] { 8.0, 8.01, 16, 20.12, 24.99, 25 })
        Near(metres, PitchLengthScale.SelectMetres(PitchLengthScale.FractionForMetres(metres)), 1e-12, "Numeric/slider round trip");
    Near(8, PitchLengthScale.SelectMetres(1.2), 1e-12, "Minimum selection clamp");
    Near(25, PitchLengthScale.SelectMetres(-.2), 1e-12, "Maximum selection clamp");
}
static void Near(double expected, double actual, double tolerance, string description)
{
    Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance,
        $"{description}: expected {expected:G8}, got {actual:G8}, tolerance {tolerance}");
}
static void Vacuum()
{
    var s = new SimulationSettings { AirDensity = 0 };
    BallState ball = new(new(0, 15), new(32, 5));
    for (int i = 0; i < 960; i++) ball = BallFlight.Integrate(ball, 1.0 / 960, s);
    Near(32, ball.Position.X, 1e-8, "x after 1 s");
    Near(15 + 5 - s.Gravity / 2, ball.Position.Y, 1e-8, "height after 1 s");
    Near(32, ball.Velocity.X, 1e-8, "horizontal velocity");
    Near(5 - s.Gravity, ball.Velocity.Y, 1e-8, "vertical velocity");
}
static void Drag()
{
    var s = new SimulationSettings();
    BallState ball = new(new(0, 100), new(40, 0));
    double previous = 40;
    for (int i = 0; i < 960; i++)
    {
        ball = BallFlight.Integrate(ball, 1.0 / 960, s);
        Check(ball.Velocity.X < previous, "Horizontal speed must fall under drag.");
        previous = ball.Velocity.X;
    }
    Check(ball.Position.X < 40, "Drag must reduce range.");
    Near(0, Vec2.Dot(new(12, 8), BallFlight.Acceleration(new(12, 8), 150, s) -
        BallFlight.Acceleration(new(12, 8), 0, s)), 1e-9, "Magnus force does no work");
}
static void BounceEnergy()
{
    var rng = new Random(32);
    var s = new SimulationSettings();
    for (int i = 0; i < 1000; i++)
    {
        BallState before = new(new(0, s.BallRadius), new(rng.NextDouble() * 100 - 50, -rng.NextDouble() * 20 - .01),
            rng.NextDouble() * 1000 - 500);
        BallState after = Contacts.GroundImpulse(before, s);
        Near(-before.Velocity.Y * s.GroundRestitution, after.Velocity.Y, 1e-10, "normal restitution");
        Check(after.KineticEnergy(s) <= before.KineticEnergy(s) + 1e-8, "Ground created energy.");
    }
}
static void GroundMotion()
{
    var s = new SimulationSettings();
    foreach (double spin in new[] { -400.0, 0.0, 400.0 })
    {
        BallState ball = new(new(0, s.BallRadius), new(12, 0), spin);
        for (int i = 0; i < 12000; i++)
        {
            double energy = ball.KineticEnergy(s);
            ball = Contacts.GroundMotion(ball, 1.0 / 960, s);
            Check(ball.KineticEnergy(s) <= energy + 1e-9, "Ground contact created energy.");
        }
        Near(0, ball.Velocity.X + ball.Spin * s.BallRadius, 1e-8, "rolling without slip");
    }
}
static void BatImpulse()
{
    var s = new SimulationSettings();
    BallState b = new(new(0, 1), new(35, 4));
    BallState stationary = Contacts.BatImpulse(b, new(-1, 0), Vec2.Zero, new(0, -0.15), s, out double j);
    Check(j > 0 && stationary.Velocity.X < 0, "Stationary bat should deflect the ball back.");
    Check(stationary.KineticEnergy(s) < b.KineticEnergy(s), "Passive bat added energy.");
    BallState moving = Contacts.BatImpulse(b, new(-1, 0), new(-18, 0), new(0, -0.15), s, out _);
    Check(moving.Velocity.X < stationary.Velocity.X, "Contact point velocity must influence the shot.");
    Contacts.BatImpulse(b, new(-1, 0), Vec2.Zero, new(0, -0.4), s, out double toeImpulse);
    Check(toeImpulse < j, "A larger rotational lever should reduce effective mass.");
}
static void SweptContact()
{
    Capsule bat = new(new(0, 0.4), new(0, 1.2), 0.032);
    Check(Contacts.Sweep(new(-0.2, .8), new(.2167, .8), .036, bat, bat, out double fraction, out Vec2 normal),
        "Fast ball tunneled through stationary blade.");
    Near((.2 - .068) / .4167, fraction, 1e-5, "impact fraction");
    Check(normal.X < -.99, "Wrong incoming normal.");
    Capsule from = new(new(-.3, .4), new(-.3, 1.2), .032);
    Capsule to = new(new(.3, .4), new(.3, 1.2), .032);
    Check(Contacts.Sweep(new(0, .8), new(0, .8), .036, from, to, out _, out _), "Moving bat missed stationary ball.");
    Check(!Contacts.Sweep(new(-.2, 1.5), new(.2, 1.5), .036, bat, bat, out _, out _), "False impact above bat.");
    Check(Contacts.Sweep(new(-.2, 1.24), new(.2, 1.24), .036, bat, bat, out _, out _), "Rounded bat toe missed.");
}
static void Deliveries()
{
    var s = new SimulationSettings();
    foreach (double speed in new[] { 25.0, 32.0, 40.0, 50.0 })
    foreach (double length in new[] { 8.0, 10.0, 16.0, 25.0 })
    foreach (double height in new[] { 1.53, 2.0, 2.16, 2.38, 2.40 })
    {
        var request = new DeliverySettings(speed, length, height);
        Check(DeliverySolver.TrySolve(request, s, out var plan, out string error), error);
        Near(length, plan!.PredictedPitchX, 0.002, "predicted first pitch");
        Near(speed, plan.Release.Velocity.Length, 1e-10, "launch speed");
        Near(height, plan.Release.Position.Y, 1e-10, "release height");
        // Independently integrate at a finer step and interpolate the ground crossing.
        BallState b = plan.Release;
        const double dt = 1.0 / 3840;
        for (int i = 0; i < 30000; i++)
        {
            BallState next = BallFlight.Integrate(b, dt, s);
            if (next.Position.Y <= s.BallRadius)
            {
                double t = (b.Position.Y - s.BallRadius) / (b.Position.Y - next.Position.Y);
                Near(length, Vec2.Lerp(b.Position, next.Position, t).X, .002, "independent ground crossing");
                break;
            }
            Check(i < 29999, "No landing found.");
            b = next;
        }
    }
}
static void Validation()
{
    var s = new SimulationSettings();
    foreach (var invalid in new[] { new DeliverySettings(double.NaN, 16, 2), new(30, double.PositiveInfinity, 2),
        new(30, 16, -1), new(51, 16, 2), new(30, 0, 2), new(5, 8, 1.53) })
        Check(!DeliverySolver.TrySolve(invalid, s, out _, out var error) && error.Length > 0, "Invalid/unreachable delivery accepted.");
    try { (s with { Substeps = 0 }).Validate(); throw new Exception("Invalid config accepted."); }
    catch (ArgumentException) { }
}
static void DeliveryBounds()
{
    var s = new SimulationSettings();
    Near(20.12, PitchDimensions.WicketSeparation, 0, "wicket separation");
    foreach (double height in new[] { 1.53, 2.16, 2.40 })
    foreach (double length in new[] { 8.0, 20.12, 25.0 })
    {
        Check(DeliverySolver.TrySolve(new(32, length, height), s, out var plan, out string error), error);
        Near(height, plan!.Release.Position.Y, 0, "release endpoint");
        Near(length, plan.PredictedPitchX, .002, "pitching length");
    }
    foreach (double height in new[] { 1.52999, 2.40001, double.NaN, double.PositiveInfinity })
        Check(!DeliverySolver.TrySolve(new(32, 16, height), s, out _, out _), "Out-of-range release height accepted.");
    foreach (double length in new[] { 7.99999, 25.00001, double.NaN, double.PositiveInfinity })
        Check(!DeliverySolver.TrySolve(new(32, length, 2), s, out _, out _), "Out-of-range pitching length accepted.");
}
static void Lifecycle()
{
    var sim = new Simulation(new());
    Check(sim.Phase == DeliveryPhase.Setup, "Should open on setup.");
    Check(sim.Begin(new(32, 16, 2.15), out var error), error);
    for (int i = 0; i < 149; i++) sim.Tick(default);
    Check(sim.Phase == DeliveryPhase.Windup, "Released too early.");
    sim.Tick(default);
    Check(sim.Phase == DeliveryPhase.Live, "Must release at 1.25 s.");
    Near(0, sim.PhaseTime, 1e-10, "timer begins at release");
    for (int i = 0; i < 3600; i++) sim.Tick(default);
    Check(sim.Phase == DeliveryPhase.Live, "Delivery reset without a manual request.");
    Near(30, sim.PhaseTime, 1e-8, "uninterrupted live time");
    Check(sim.Trail.Count <= 320, "Long deliveries must retain a bounded trail.");
    double peakSpeed = sim.Statistics.MaxSpeed;
    sim.ReturnToSetup();
    Check(sim.Phase == DeliveryPhase.Setup, "Manual return must open setup.");
    BallState paused = sim.Ball;
    for (int i = 0; i < 120; i++) sim.Tick(default);
    Check(sim.Ball == paused && sim.Statistics.MaxSpeed == peakSpeed, "Setup must retain the previous delivery's measurements.");
    Check(sim.Begin(new(40, 14, 2.16), out error), error);
    Check(!sim.BatHit && !sim.WicketHit && sim.FirstBounceX == null && sim.Trail.Count == 0, "Old delivery state survived reset.");
    Check(sim.Statistics.MaxSpeed == 0 && sim.Statistics.MaxLengthFromBatter == 0 && sim.Statistics.MaxHeight == 0,
        "A new delivery must clear the previous maxima.");
}
static void DeliveryMaxima()
{
    var stats = new DeliveryStatistics();
    stats.Observe(new(new(PitchDimensions.BatterReferenceX + 3, 4), new(3, 4)));
    Near(5, stats.MaxSpeed, 0, "speed magnitude");
    Near(3, stats.MaxLengthFromBatter, 1e-12, "horizontal rather than diagonal distance");
    Near(4, stats.MaxHeight, 0, "height above ground");
    stats.Observe(new(new(PitchDimensions.BatterReferenceX - 20, 1), new(1, 0)));
    Near(5, stats.MaxSpeed, 0, "retained speed peak");
    Near(20, stats.MaxLengthFromBatter, 1e-12, "distance on opposite side of batter");
    Near(4, stats.MaxHeight, 0, "retained height after falling");
    stats.Reset();
    Check(stats.MaxSpeed == 0 && stats.MaxLengthFromBatter == 0 && stats.MaxHeight == 0, "Maxima reset failed.");

    var sim = new Simulation(new());
    sim.Begin(new(32, 16, 2.15), out _);
    double speed = 0, length = 0, height = 0;
    for (int i = 0; i < 1600; i++)
    {
        sim.Tick(new(i < 198 ? -.65 : .75, 0, 0, 0));
        if (sim.Phase != DeliveryPhase.Live) continue;
        speed = Math.Max(speed, sim.Ball.Velocity.Length);
        foreach (var contact in sim.Events)
            speed = Math.Max(speed, Math.Max(contact.SpeedBefore, contact.SpeedAfter));
        length = Math.Max(length, Math.Abs(sim.Ball.Position.X - PitchDimensions.BatterReferenceX));
        height = Math.Max(height, sim.Ball.Position.Y);
        Check(sim.Statistics.MaxSpeed + 1e-10 >= speed && sim.Statistics.MaxLengthFromBatter + 1e-10 >= length &&
            sim.Statistics.MaxHeight + 1e-10 >= height, "Statistics lost a sampled peak or collision impulse.");
    }
    Check(sim.Statistics.MaxSpeed >= 32 && sim.Statistics.MaxHeight >= 2.15, "Release state must count toward maxima.");
}
static void SpeedConversion()
{
    Near(115.2, SpeedUnits.ToKilometresPerHour(32), 1e-12, "default release display");
    Near(10, SpeedUnits.ToMetresPerSecond(36), 1e-12, "km/h to m/s");
    foreach (double kilometresPerHour in new[] { 18.0, 115.2, 180.0 })
    {
        double metresPerSecond = SpeedUnits.ToMetresPerSecond(kilometresPerHour);
        Check(new DeliverySettings(metresPerSecond, 8, 2.15).ValidationError == null, "Valid release speed rejected.");
        Near(kilometresPerHour, SpeedUnits.ToKilometresPerHour(metresPerSecond), 1e-9, "speed input round trip");
    }
    // The minimum allowed speed cannot physically reach even the shortest allowed length.
    foreach (double kilometresPerHour in new[] { 36.0, 115.2, 180.0 })
    {
        double metresPerSecond = SpeedUnits.ToMetresPerSecond(kilometresPerHour);
        Check(DeliverySolver.TrySolve(new(metresPerSecond, 8, 2.15), new(), out var plan, out string error), error);
        Near(kilometresPerHour, SpeedUnits.ToKilometresPerHour(plan!.Release.Velocity.Length), 1e-9, "launch speed round trip");
    }
}
static void LiveLanding()
{
    foreach (double speed in new[] { 20.0, 32.0, 50.0 })
    foreach (double length in new[] { 8.0, 12.0, 16.0 })
    {
        var sim = new Simulation(new());
        Check(sim.Begin(new(speed, length, 2.15), out string error), error);
        for (int i = 0; i < 740 && sim.FirstBounceX == null; i++) sim.Tick(default);
        Check(sim.FirstBounceX.HasValue, "No live bounce recorded.");
        Near(length, sim.FirstBounceX!.Value, .003, "live first pitch");
        Check(sim.Ball.Position.Y >= sim.Settings.BallRadius, "Ball beneath ground.");
    }
}
static void PlayableSwing()
{
    int hits = 0;
    double highestExitSpeed = 0;
    for (int timing = 150; timing <= 240; timing += 6)
    {
        var sim = new Simulation(new());
        sim.Begin(new(32, 16, 2.15), out _);
        for (int i = 0; i < 500; i++)
        {
            double clock = 255 - Math.Clamp((i - timing) * 8.0, 0, 255);
            sim.Tick(BattingChecks.AtClock(clock));
        }
        if (sim.BatHit) hits++;
        highestExitSpeed = Math.Max(highestExitSpeed, sim.HitExitSpeed ?? 0);
    }
    Console.WriteLine($"      swing sweep: {hits}/16 bat contacts, maximum exit {highestExitSpeed:0.00} m/s");
    Check(hits >= 2, "Bat cannot be reached with ordinary controller swings.");
    Check(highestExitSpeed > 10, "Timed swings cannot produce a useful shot.");
}
static void InvulnerableBatter()
{
    int overlappingTicks = 0;
    bool wicketHit = false;
    foreach (double length in new[] { 10.0, 16.0 })
    {
        var sim = new Simulation(new());
        Check(sim.Begin(new(32, length, 2.15), out var error), error);
        for (int i = 0; i < 300; i++)
        {
            BallState before = sim.Ball;
            sim.Tick(BattingChecks.AtClock(30)); // Hold the bat high, away from the incoming delivery.
            if (sim.Phase != DeliveryPhase.Live) continue;
            wicketHit |= sim.WicketHit;
            if (sim.Ball.Position.X < 18 || sim.Ball.Position.X > 19.5) continue;
            HumanPose p = sim.Batter.Pose;
            Capsule[] body = [new(p.Hip, p.Shoulder, .105), new(p.FrontKnee, p.FrontAnkle, .075),
                new(p.BackKnee, p.BackAnkle, .07), new(p.Head, p.Head, .115)];
            if (!body.Any(c => (sim.Ball.Position - c.Closest(sim.Ball.Position)).Length < c.Radius + sim.Settings.BallRadius)) continue;
            overlappingTicks++;
            BallState expected = before;
            for (int substep = 0; substep < sim.Settings.Substeps; substep++)
                expected = BallFlight.Integrate(expected, SimulationSettings.Tick / sim.Settings.Substeps, sim.Settings);
            Near(0, (expected.Position - sim.Ball.Position).Length, 1e-9, "unimpeded body crossing position");
            Near(0, (expected.Velocity - sim.Ball.Velocity).Length, 1e-9, "unimpeded body crossing velocity");
            Check(sim.Events.Count == 0, "Visible body produced a collision.");
        }
    }
    Check(overlappingTicks >= 2, "Regression deliveries did not cross the visible body.");
    Check(wicketHit, "The ball should still hit the wicket behind the invulnerable batter.");
}
static void Determinism()
{
    var a = new Simulation(new());
    var b = new Simulation(new());
    a.Begin(new(32, 16, 2.15), out _);
    b.Begin(new(32, 16, 2.15), out _);
    for (int i = 0; i < 740; i++)
    {
        var input = new BattingInput(Math.Sin(i * .04), 0.1, Math.Cos(i * .02), 0);
        a.Tick(input); b.Tick(input);
        Check(a.Ball == b.Ball && a.Batter.Pose == b.Batter.Pose, "Repeated state diverged.");
        Check(double.IsFinite(a.Ball.Velocity.Length), "Non-finite velocity.");
    }
}
static void RenderRates()
{
    BallState? reference = null;
    foreach (int fps in new[] { 30, 60, 144 })
    {
        var sim = new Simulation(new());
        sim.Begin(new(32, 16, 2.15), out _);
        double accumulator = 0;
        int ticks = 0;
        for (int frame = 0; frame < fps * 5; frame++)
        {
            accumulator += 1.0 / fps;
            while (accumulator + 1e-10 >= SimulationSettings.Tick)
            {
                sim.Tick(new(Math.Sin(ticks * .02), 0, 0, 0));
                accumulator -= SimulationSettings.Tick;
                ticks++;
            }
        }
        Check(ticks == 600, "Wrong physics tick count.");
        if (reference.HasValue) Check(reference.Value == sim.Ball, "Render frequency changed the result.");
        reference = sim.Ball;
    }
}

static void CameraFraming()
{
    foreach (int direction in new[] { -1, 1 })
    {
        var camera = new FieldCamera();
        for (int metres = 0; metres <= 250; metres++)
        {
            Vec2 ball = new(FieldLayout.BatterReferenceX + direction * metres, 2);
            camera.Fit(ball);
            Check(camera.Contains(ball), $"Ball escaped the camera at {ball}.");
            Check(camera.Contains(Vec2.Zero), "Bowler wicket escaped the camera.");
            Check(camera.Contains(new(PitchDimensions.WicketSeparation, PitchDimensions.StumpHeight)), "Striker wicket escaped the camera.");
            if (ball.X > PitchDimensions.WicketSeparation)
            {
                Near(FieldCamera.Right, camera.Project(Vec2.Zero).X, 1e-8, "bowler wicket anchored right");
                Check(camera.Project(ball).X < camera.Project(new(PitchDimensions.WicketSeparation, 0)).X, "Ball must be left of both wickets.");
            }
            if (ball.X < 0)
            {
                Near(FieldCamera.Left, camera.Project(new(PitchDimensions.WicketSeparation, 0)).X, 1e-8, "striker wicket anchored left");
                Check(camera.Project(ball).X > camera.Project(Vec2.Zero).X, "Ball must be right of both wickets.");
            }
        }
    }
}

static void CameraReset()
{
    var camera = new FieldCamera();
    camera.Fit(new(100, 20));
    double wideScale = camera.PixelsPerMetre;
    for (int x = 100; x >= -100; x--)
    {
        Vec2 ball = new(x, 2);
        camera.Fit(ball);
        Check(camera.PixelsPerMetre <= wideScale, "View zoomed back in during the delivery.");
        Check(camera.Contains(ball) && camera.Contains(Vec2.Zero) &&
            camera.Contains(new(PitchDimensions.WicketSeparation, 0)), "Direction reversal lost an object.");
        wideScale = camera.PixelsPerMetre;
    }
    camera.Reset();
    Near(FieldCamera.BasePixelsPerMetre, camera.PixelsPerMetre, 0, "reset scale");
    Near(FieldCamera.Left, camera.Project(new(PitchDimensions.WicketSeparation, 0)).X, 1e-8, "initial striker position");
    Near(FieldCamera.Right, camera.Project(Vec2.Zero).X, 1e-8, "initial bowler position");
}

static void HighShotFraming()
{
    foreach (double x in new[] { -100.0, 0, 10, 20.12, 100 })
    {
        var camera = new FieldCamera();
        for (int height = 0; height <= 100; height++)
        {
            Vec2 ball = new(x, height);
            camera.Fit(ball);
            Check(camera.Contains(ball) && camera.Contains(Vec2.Zero) &&
                camera.Contains(new(PitchDimensions.WicketSeparation, PitchDimensions.StumpHeight)), "High shot escaped the frame.");
            Near(PitchDimensions.WicketSeparation * camera.PixelsPerMetre,
                camera.Project(Vec2.Zero).X - camera.Project(new(PitchDimensions.WicketSeparation, 0)).X,
                1e-8, "wicket separation at zoom");
        }
    }
}

static void FieldMeasurements()
{
    var field = new FieldGeometry(new(), new());
    Near(40, field.LeftX - FieldLayout.BatterReferenceX, 1e-10, "left boundary distance");
    Near(75, FieldLayout.BatterReferenceX - field.RightX, 1e-10, "right boundary distance");
    var camera = new FieldCamera();
    foreach (Vec2 ball in new[] { new Vec2(15, .036), new Vec2(80, 12.34), new Vec2(-120, 50) })
    {
        camera.Fit(ball);
        Near(ball.Y, FieldLayout.BallHeight(ball), 0, "height above ground");
        Check(camera.Project(new(field.LeftX, 0)).X < camera.Project(new(FieldLayout.BatterReferenceX, 0)).X,
            "Left boundary is on the wrong side.");
        Check(camera.Project(new(field.RightX, 0)).X > camera.Project(new(FieldLayout.BatterReferenceX, 0)).X,
            "Right boundary is on the wrong side.");
    }
}
