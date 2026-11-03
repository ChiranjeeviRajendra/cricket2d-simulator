using System.Text.Json;
using Cricket.Physics;

internal static class BattingChecks
{
    private const double Dt = 1.0 / 960;
    private static readonly SimulationSettings Physics = new();
    public static BattingInput AtClock(double left, double? right = null, double strength = 1)
    {
        double l = left * Math.PI / 180, r = (right ?? 0) * Math.PI / 180;
        return new(Math.Sin(l), -Math.Cos(l), right.HasValue ? Math.Sin(r) * strength : 0,
            right.HasValue ? -Math.Cos(r) * strength : 0);
    }

    public static void StraightSwing()
    {
        var rig = new BattingRig(Physics);
        for (double clock = 240; clock >= 30; clock -= 5)
        {
            Settle(rig, AtClock(clock));
            Near(0, ClockCurve.SignedDelta(rig.SwingClockDegrees - clock), .001, "left-stick clock direction");
            Near(0, rig.ElbowFlexion, 1e-8, "extended arms");
            var p = rig.Pose;
            Vec2 direction = (p.Hands - p.Shoulder).Normalized;
            Near(1, Vec2.Dot(direction, p.Bat.Direction), 1e-9, "straight blade");
            Near(0, Vec2.Cross(p.LeadElbow - p.Shoulder, direction), 2e-8, "straight lead elbow");
            Near(0, Vec2.Cross(p.RearElbow - p.Shoulder, direction), 2e-8, "straight rear elbow");
            // Projection flips physical X and Y; verify the player's actual screen direction.
            Near(Math.Sin(clock * Math.PI / 180), -direction.X, 2e-5, "screen X");
            Near(-Math.Cos(clock * Math.PI / 180), -direction.Y, 2e-5, "screen Y");
        }
        Settle(rig, AtClock(180));
        Near(180, rig.SwingClockDegrees, .001, "reversal before the snap is allowed");
    }

    public static void IndependentWrist()
    {
        var neutral = new BattingRig(Physics);
        var active = new BattingRig(Physics);
        bool batChanged = false;
        for (int i = 0; i < 4000; i++)
        {
            double clock = 240 - Math.Min(i * .15, 270);
            var input = AtClock(clock, clock + 70);
            neutral.Step(input with { RightX = 0, RightY = 0 }, Dt, Physics);
            active.Step(input, Dt, Physics);
            Near(neutral.ShoulderAngle, active.ShoulderAngle, 0, "wrist cannot steer shoulder");
            Near(neutral.ShoulderSpeed, active.ShoulderSpeed, 0, "wrist cannot change swing speed");
            Check(neutral.Pose.Hands == active.Pose.Hands && neutral.Pose.Shoulder == active.Pose.Shoulder &&
                neutral.Pose.LeadElbow == active.Pose.LeadElbow, "Wrist input changed the arm or stance.");
            Check(active.Pose.Bat.Grip == active.Pose.Hands, "The fist is not the bat pivot.");
            batChanged |= Math.Abs(active.Pose.Bat.Angle - neutral.Pose.Bat.Angle) > .2;
        }
        Check(batChanged, "Right stick never changed the bat orientation.");
    }

    public static void WristLimits()
    {
        var rig = new BattingRig(Physics);
        foreach (double clock in new[] { 240.0, 210, 180, 120, 60, 30, 5 })
        {
            Settle(rig, AtClock(clock, clock + 150));
            Near(rig.WristMaxDegrees, rig.WristAngleDegrees, .001, "upper wrist stop");
            Settle(rig, AtClock(clock, clock - 150));
            Near(rig.WristMinDegrees, rig.WristAngleDegrees, .001, "lower wrist stop");
        }
        var keys = rig.Settings.Wrist.AnglesBySwing;
        Near(-77.5, ClockCurve.Sample(keys, 345, k => k.MinDegrees), 1e-10, "12 o'clock interpolation");
        // Abruptly aim the left stick across the circle: constraints must use the moving arm, not its target.
        for (int i = 0; i < 1600; i++)
        {
            rig.Step(AtClock(180, 0), Dt, Physics);
            double min = ClockCurve.Sample(keys, rig.SwingClockDegrees, k => k.MinDegrees);
            double max = ClockCurve.Sample(keys, rig.SwingClockDegrees, k => k.MaxDegrees);
            Check(rig.WristAngleDegrees >= min - 1e-10 && rig.WristAngleDegrees <= max + 1e-10,
                "Wrist escaped its current shoulder-dependent limits.");
        }
    }

    public static void SwingSpeeds()
    {
        var rig = new BattingRig(Physics);
        Settle(rig, AtClock(240));
        var samples = new Dictionary<int, double>();
        for (int i = 0; i < 3000 && !rig.IsSnapping; i++)
        {
            rig.Step(AtClock(0), Dt, Physics);
            double progress = ClockCurve.Wrap(240 - rig.SwingClockDegrees);
            foreach (int mark in new[] { 5, 30, 90, 175, 200, 220, 235 })
                if (progress >= mark && !samples.ContainsKey(mark)) samples[mark] = Math.Abs(rig.ShoulderSpeed) * 180 / Math.PI;
        }
        Check(samples.Count == 7, "Did not traverse the requested speed phases.");
        Check(samples[30] > samples[5] * 1.5, "8 to 7 did not accelerate.");
        Near(1100, samples[90], 2, "peak through the hitting arc");
        Near(samples[90], samples[175], 2, "peak held to 2 o'clock");
        Check(samples[200] < samples[175] && samples[220] < samples[200] && samples[235] < samples[220],
            "Follow-through did not slow in the requested stages.");
    }

    public static void Snap()
    {
        var rig = new BattingRig(Physics);
        for (int i = 0; i < 3000 && !rig.IsSnapping; i++) rig.Step(AtClock(0), Dt, Physics);
        Check(rig.IsSnapping, "12 o'clock did not commit the snap.");
        Near(0, rig.SwingClockDegrees, 1e-8, "snap starts exactly at 12");
        double beforeWrist = rig.WristAngleDegrees;
        double previous = rig.ShoulderAngle;
        int steps = 0;
        while (rig.IsSnapping && steps++ < 200)
        {
            // The left stick tries to reverse while the right stick still steers the bat.
            rig.Step(AtClock(30, 90), Dt, Physics);
            Check(rig.ShoulderAngle < previous, "Committed snap reversed or stopped.");
            Check(Math.Abs(rig.ShoulderAngle - previous) <= 1440 * Math.PI / 180 * Dt + 1e-9, "Snap teleported.");
            Check(rig.WristAngleDegrees >= rig.WristMinDegrees && rig.WristAngleDegrees <= rig.WristMaxDegrees, "Snap broke wrist limits.");
            previous = rig.ShoulderAngle;
        }
        Check(!rig.IsSnapping && steps < 150, "Snap did not finish promptly.");
        Near(240, rig.SwingClockDegrees, 1e-8, "snap endpoint");
        Check(Math.Abs(beforeWrist - rig.WristAngleDegrees) > 5, "Wrist stopped responding during snap.");
        Settle(rig, AtClock(0));
        Near(240, rig.SwingClockDegrees, 1e-8, "held stick must not repeat the snap");
        Check(rig.AwaitingSnapRearm, "Expected a held-stick rearm guard.");
        Settle(rig, AtClock(240));
        Near(240, rig.SwingClockDegrees, .001, "next swing rearmed at 8");
        Check(!rig.AwaitingSnapRearm, "Rearm did not clear.");
    }

    public static void FullCircles()
    {
        var rig = new BattingRig(Physics);
        Settle(rig, AtClock(240));
        double startingAngle = rig.ShoulderAngle;
        for (int cycle = 0; cycle < 4; cycle++)
        {
            rig.Step(AtClock(240), Dt, Physics);
            bool snapped = false;
            for (int i = 0; i < 4000; i++)
            {
                var previous = rig.Pose.Bat;
                double shoulder = rig.ShoulderAngle;
                rig.Step(AtClock(0), Dt, Physics);
                Check(rig.ShoulderAngle <= shoulder + 1e-10, "Full swing reversed at the seam.");
                Check(Math.Abs(rig.Pose.Bat.Angle - previous.Angle) < .06, "Bat angle wrapped through a collision spike.");
                if (rig.AwaitingSnapRearm) { snapped = true; break; }
            }
            Check(snapped, "Full circle never completed.");
            Near(startingAngle - (cycle + 1) * Math.Tau, rig.ShoulderAngle, 1e-8, "continuous revolution");
        }
        // A real rotating stick can pass 8 before the slowed follow-through reaches 12.
        // That crossing must finish the circle instead of steering the lagging arm backwards.
        foreach (double degreesPerSecond in new[] { 180.0, 480, 720 })
        {
            var rotating = new BattingRig(Physics);
            Settle(rotating, AtClock(240));
            double start = rotating.ShoulderAngle;
            int steps = (int)Math.Ceiling(360 / degreesPerSecond / Dt);
            for (int i = 1; i <= steps; i++)
                rotating.Step(AtClock(240 - Math.Min(360, i * Dt * degreesPerSecond)), Dt, Physics);
            Settle(rotating, AtClock(240));
            Near(start - Math.Tau, rotating.ShoulderAngle, 1e-8, $"rotating stick at {degreesPerSecond} degrees/s");
        }
    }

    public static void DeadZones()
    {
        var settings = new BattingSettings { Input = new() { LeftDeadZone = .4, RightDeadZone = .5 } };
        var rig = new BattingRig(Physics, settings);
        HumanPose initial = rig.Pose;
        Settle(rig, new(.25, .25, .3, .3));
        Check(rig.Pose == initial, "Radial dead zone allowed stick drift.");
        Settle(rig, AtClock(180, 220));
        Check(rig.WristAngleDegrees > 20, "Active right stick had no effect.");
        double angle = rig.ShoulderAngle;
        Settle(rig, default);
        Near(angle, rig.ShoulderAngle, 0, "centred left stick retains the swing position");
        Near(0, rig.WristAngleDegrees, .001, "centred right stick restores neutral bat angle");
        Settle(rig, new(double.NaN, 0, double.PositiveInfinity, 0));
        Check(double.IsFinite(rig.Pose.Bat.Angle), "Non-finite hardware input poisoned the pose.");
    }

    public static void Configuration()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Config", "batsman.json"));
        var settings = BattingSettings.FromJson(json);
        Check(JsonSerializer.Serialize(settings) == JsonSerializer.Serialize(new BattingSettings()), "JSON and code defaults differ.");
        // Editing only JSON must alter the runtime rig and survive delivery resets.
        string tuned = json.Replace("\"InitialClockDegrees\": 240", "\"InitialClockDegrees\": 180")
            .Replace("\"ClockDegrees\": 180, \"MinDegrees\": -45, \"NeutralDegrees\": 0, \"MaxDegrees\": 55",
                "\"ClockDegrees\": 180, \"MinDegrees\": -10, \"NeutralDegrees\": 5, \"MaxDegrees\": 12");
        settings = BattingSettings.FromJson(tuned);
        var sim = new Simulation(Physics, settings);
        Near(180, sim.Batter.SwingClockDegrees, 0, "loaded initial angle");
        Near(5, sim.Batter.WristAngleDegrees, 0, "loaded neutral angle");
        Settle(sim.Batter, AtClock(180, 270));
        Near(12, sim.Batter.WristAngleDegrees, .001, "loaded wrist limit");
        Check(sim.Begin(new(32, 16, 2.15), out _), "Delivery failed.");
        Near(180, sim.Batter.SwingClockDegrees, 0, "tuning retained on delivery reset");
        Near(5, sim.Batter.WristAngleDegrees, 0, "neutral retained on delivery reset");
        var slow = settings with { Swing = settings.Swing with { SpeedBySwing = [new(0, 80, 200), new(180, 80, 200)] } };
        var normalRig = new BattingRig(Physics, settings);
        var slowRig = new BattingRig(Physics, slow);
        for (int i = 0; i < 120; i++)
        {
            normalRig.Step(AtClock(90), Dt, Physics);
            slowRig.Step(AtClock(90), Dt, Physics);
        }
        Check(normalRig.SwingClockDegrees < slowRig.SwingClockDegrees - 10, "Custom speed curve was ignored.");
        // Clearance must be validated against the loaded equipment, not a hard-coded default bat.
        var shorterBat = Physics with { BatLength = .6 };
        var lowerStance = BattingSettings.FromJson(json.Replace("\"TorsoHeightMetres\": 0.69", "\"TorsoHeightMetres\": 0.5"), shorterBat);
        var shorterRig = new BattingRig(shorterBat, lowerStance);
        Check(shorterRig.Pose.Shoulder.Y < 1.4, "Compatible custom bat and stance settings were not loaded.");
    }

    public static void InvalidConfiguration()
    {
        string[] invalid =
        [
            "null", "{\"Input\":null}", "{\"Swing\":{\"TargetResponse\":0}}",
            "{\"Input\":{\"RightDeadZone\":1}}", "{\"Swing\":{\"Snap\":null}}",
            "{\"Swing\":{\"Snap\":{\"StartClockDegrees\":240}}}",
            "{\"Swing\":{\"ReverseSpeedScale\":1}}", "{\"Swing\":{\"ReverseAccelerationDegreesPerSecondSquared\":0}}",
            "{\"Swing\":{\"SpeedBySwing\":[]}}", "{\"Arms\":{\"FlexionBySwing\":[null,null]}}",
            "{\"Arms\":{\"FlexionBySwing\":[{\"ClockDegrees\":90,\"ElbowFlexionDegrees\":0},{\"ClockDegrees\":0,\"ElbowFlexionDegrees\":0}]}}",
            "{\"Wrist\":{\"AnglesBySwing\":[{\"ClockDegrees\":0,\"MinDegrees\":10,\"MaxDegrees\":-10},{\"ClockDegrees\":90}]}}",
            "{\"Stance\":{\"TorsoHeightMetres\":0.1}}", "{\"Swign\":{}}"
        ];
        foreach (string json in invalid)
        {
            bool rejected = false;
            try { BattingSettings.FromJson(json); }
            catch (ArgumentException ex) { rejected = ex.Message.Contains("Config/batsman.json"); }
            catch (JsonException) { rejected = true; }
            Check(rejected, $"Malformed configuration was accepted or failed unclearly: {json}");
        }
    }

    public static void ReverseLimits()
    {
        var rig = new BattingRig(Physics);
        Settle(rig, AtClock(1)); // Just before 12, still under manual control.
        Near(1, rig.SwingClockDegrees, .001, "manual control up to the snap threshold");
        double peakReverse = 0;
        for (int i = 0; i < 2400; i++)
        {
            rig.Step(AtClock(240), Dt, Physics);
            Check(!rig.IsSnapping && rig.SwingClockDegrees <= 240 + 1e-8, "Reverse crossed the 8 o'clock stop.");
            double speed = rig.ShoulderSpeed * 180 / Math.PI;
            double forwardCap = ClockCurve.Sample(rig.Settings.Swing.SpeedBySwing, rig.SwingClockDegrees, k => k.MaxSpeedDegreesPerSecond);
            Check(speed <= Math.Min(240, forwardCap * .3) + 2, "Reverse exceeded its local slower speed envelope.");
            peakReverse = Math.Max(peakReverse, speed);
        }
        Near(240, rig.SwingClockDegrees, .001, "reverse stops at 8");
        Check(peakReverse > 200 && peakReverse <= 240 + 1e-8, "Backlift speed cap was not used.");
        double stopAngle = rig.ShoulderAngle;
        // Continuous reversal into 9/10/11 must clamp, never launch a forward swing.
        for (double clock = 240; clock <= 359; clock += .5)
        {
            rig.Step(AtClock(clock), Dt, Physics);
            Near(stopAngle, rig.ShoulderAngle, 1e-8, "blocked reverse in the snap-only sector");
            Check(!rig.IsSnapping, "Blocked reverse started a snap.");
        }
        for (double clock = 359.5; clock <= 600; clock += .5)
        {
            rig.Step(AtClock(clock), Dt, Physics);
            Near(stopAngle, rig.ShoulderAngle, 1e-8, "a full reverse stick circle cannot cross the stop");
        }
        // Direct acquisition at 9 from a centred stick must have the same hard-stop behavior.
        var fresh = new BattingRig(Physics);
        Settle(fresh, AtClock(270));
        Near(240, fresh.SwingClockDegrees, 1e-8, "initial backward input stops at 8");
        Settle(fresh, AtClock(30));
        Near(30, fresh.SwingClockDegrees, .001, "blocked backlift does not poison the next forward target");
        double before = fresh.ShoulderSpeed;
        fresh.Step(AtClock(240), Dt, Physics);
        Check((fresh.ShoulderSpeed - before) * 180 / Math.PI <= 1800 * Dt + 1e-6,
            "Backlift acceleration uses the faster forward acceleration.");
    }

    public static void CricketPhysics()
    {
        var s = Physics with { BatFriction = 0 };
        Near(1.17, s.BatMass, 1e-12, "published sample mean bat mass");
        Near(.3512 - 1.17 * .5 * .5, s.BatInertiaAboutCentreOfMass, 1e-12, "parallel-axis conversion from handle end");
        Vec2 normal = new(-1, 0), lever = new(0, -.15), batVelocity = new(-20, 0);
        BallState incoming = new(new(0, 1), new(32, 0));
        BallState outgoing = Contacts.BatImpulse(incoming, normal, batVelocity, lever, s, out double j);
        Vec2 impulse = normal * j;
        Vec2 batAfter = batVelocity - impulse / s.BatMass;
        double angularAfter = -Vec2.Cross(lever, impulse) / s.BatInertiaAboutCentreOfMass;
        Vec2 pBefore = incoming.Velocity * s.BallMass + batVelocity * s.BatMass;
        Vec2 pAfter = outgoing.Velocity * s.BallMass + batAfter * s.BatMass;
        Near(0, (pBefore - pAfter).Length, 1e-10, "linear momentum across an isolated impact");
        Near(Vec2.Cross(lever, incoming.Velocity * s.BallMass),
            Vec2.Cross(lever, outgoing.Velocity * s.BallMass) + s.BatInertiaAboutCentreOfMass * angularAfter,
            1e-10, "angular momentum across an isolated impact");
        double relativeAfter = Vec2.Dot(outgoing.Velocity - batAfter - angularAfter * lever.Perpendicular, normal);
        Near(-s.BatRestitution * Vec2.Dot(incoming.Velocity - batVelocity, normal), relativeAfter, 1e-10, "relative normal restitution");
        double totalBefore = incoming.KineticEnergy(s) + .5 * s.BatMass * batVelocity.LengthSquared;
        double totalAfter = outgoing.KineticEnergy(s) + .5 * s.BatMass * batAfter.LengthSquared +
            .5 * s.BatInertiaAboutCentreOfMass * angularAfter * angularAfter;
        Check(totalAfter < totalBefore, "Finite-mass bat impact created energy.");
        foreach (var (offset, expected) in new[] { (.15, .26), (.30, .09) })
        {
            var blocked = Contacts.BatImpulse(incoming, normal, Vec2.Zero, new(0, -offset), s, out _);
            Near(expected, -blocked.Velocity.X / incoming.Velocity.X, .02, "stationary-bat rebound ratio reference");
        }
        var rng = new Random(211);
        for (int i = 0; i < 1000; i++)
        {
            Vec2 n = Vec2.FromAngle(rng.NextDouble() * Math.Tau);
            Vec2 r = new(0, rng.NextDouble() * .7 - .4);
            BallState before = new(new(0, 1), -n * (1 + rng.NextDouble() * 49) +
                n.Perpendicular * (rng.NextDouble() * 40 - 20), rng.NextDouble() * 800 - 400);
            BallState after = Contacts.BatImpulse(before, n, Vec2.Zero, r, Physics, out _);
            Vec2 transferred = (after.Velocity - before.Velocity) * Physics.BallMass;
            double batSpin = -Vec2.Cross(r, transferred) / Physics.BatInertiaAboutCentreOfMass;
            double energyAfter = after.KineticEnergy(Physics) + .5 * transferred.LengthSquared / Physics.BatMass +
                .5 * Physics.BatInertiaAboutCentreOfMass * batSpin * batSpin;
            Check(energyAfter <= before.KineticEnergy(Physics) + 1e-8,
                "Oblique frictional impact created energy in the isolated ball/bat pair.");
        }
        var rig = new BattingRig(Physics);
        double peakTipSpeed = 0;
        for (int i = 0; i < 3000 && !rig.IsSnapping; i++)
        {
            Vec2 previous = rig.Pose.Bat.Grip + rig.Pose.Bat.Direction * s.BatLength;
            rig.Step(AtClock(0), Dt, Physics);
            if (rig.SwingClockDegrees is >= 60 and <= 210)
            {
                Vec2 tip = rig.Pose.Bat.Grip + rig.Pose.Bat.Direction * s.BatLength;
                peakTipSpeed = Math.Max(peakTipSpeed, (tip - previous).Length / Dt);
            }
        }
        Check(peakTipSpeed is >= 25.9 and <= 30.9, "Default peak tip speed is outside the published men's power-hitting mean +/- SD band.");
        Console.WriteLine($"      cricket reference: peak bat tip {peakTipSpeed:0.00} m/s; COM inertia {s.BatInertiaAboutCentreOfMass:0.0000} kg m^2");
        try { (s with { BatInertiaAboutGrip = .01 }).Validate(); throw new Exception("Impossible inertia accepted."); }
        catch (ArgumentException) { }
    }

    private static void Settle(BattingRig rig, BattingInput input)
    {
        for (int i = 0; i < 1600; i++) rig.Step(input, Dt, Physics);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Near(double expected, double actual, double tolerance, string message) =>
        Check(double.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance,
            $"{message}: expected {expected:G8}, got {actual:G8}");
}
