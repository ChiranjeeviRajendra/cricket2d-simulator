using System.Diagnostics;
using Cricket.Physics;
using Cricket2D.Presentation;

internal static class PracticeChecks
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Near(double expected, double actual, string message) =>
        Check(Math.Abs(expected - actual) < 1e-9, $"{message}: {expected} vs {actual}");

    private sealed class EndpointRandom(bool upper) : Random
    {
        public override int Next(int minValue, int maxValue) => upper ? maxValue - 1 : minValue;
    }

    public static void Profiles()
    {
        var defaults = new PracticeBowlingSettings();
        Check(defaults.Bowler == BowlerType.Medium && defaults.RandomPitchLength && defaults.RandomSpeed,
            "Practice defaults changed");
        Near(95, defaults.ManualSpeedKph, "Manual default speed");
        foreach (var profile in BowlerProfiles.All)
        {
            Check(BowlerProfiles.Classify(profile.MinSpeedKph) == profile.Type &&
                BowlerProfiles.Classify(profile.MaxSpeedKph) == profile.Type, "Category endpoint misclassified");
            foreach (bool upper in new[] { false, true })
            {
                var sample = PracticeDeliveryGenerator.Sample(defaults.WithBowler(profile.Type), new EndpointRandom(upper));
                Near(upper ? profile.MaxSpeedKph : profile.MinSpeedKph, SpeedUnits.ToKilometresPerHour(sample.Speed), "Inclusive speed endpoint");
                Near(upper ? 25 : 8, sample.PitchingLength, "Inclusive length endpoint");
            }
            for (int tenths = profile.MinSpeedTenths; tenths <= profile.MaxSpeedTenths; tenths++)
                Check(defaults.WithManualSpeed(tenths / 10.0).Bowler == profile.Type, "Manual slider category mismatch");
        }
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, 17.9, 180.1 })
        {
            bool rejected = false;
            try { BowlerProfiles.Classify(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid speed classified");
        }
        Near(80, defaults.WithBowler(BowlerType.Slow).ManualSpeedKph, "Choosing slower bowler clamps manual speed");
        Near(130.1, defaults.WithBowler(BowlerType.SuperFast).ManualSpeedKph, "Choosing faster bowler clamps manual speed");
        Near(95, defaults.WithBowler(BowlerType.Medium).ManualSpeedKph, "Fitting manual speed retained");
    }

    public static void Generation()
    {
        var physics = new SimulationSettings();
        foreach (var profile in BowlerProfiles.All)
        foreach (bool length in new[] { false, true })
        foreach (bool speed in new[] { false, true })
        {
            var options = new PracticeBowlingSettings { Bowler = profile.Type, RandomPitchLength = length, RandomSpeed = speed,
                ManualSpeedKph = profile.MaxSpeedKph, ManualLength = 8, ReleaseHeight = 1.53 };
            Check(PracticeDeliveryGenerator.TryCreatePlan(options, 773, physics, out var plan, out string error), error);
            Check(PracticeDeliveryGenerator.TryCreatePlan(options, 773, physics, out var repeated, out error), error);
            Check(plan!.Settings == repeated!.Settings && plan.Release == repeated.Release && plan.Preview.SequenceEqual(repeated.Preview),
                "Same seed/options produced a different delivery");
            double kph = SpeedUnits.ToKilometresPerHour(plan.Settings.Speed);
            Check(kph >= profile.MinSpeedKph - 1e-8 && kph <= profile.MaxSpeedKph + 1e-8, "Speed escaped profile");
            Check(plan.Settings.PitchingLength is >= 8 and <= 25, "Length escaped range");
            Near(Math.Round(kph * 10), kph * 10, "Speed sampling resolution");
            Near(Math.Round(plan.Settings.PitchingLength * 100), plan.Settings.PitchingLength * 100, "Length sampling resolution");
            if (!speed) Near(options.ManualDelivery.Speed, plan.Settings.Speed, "Manual speed changed");
            if (!length) Near(options.ManualLength, plan.Settings.PitchingLength, "Manual length changed");
            Near(1.53, plan.Settings.ReleaseHeight, "Height randomized");
            Near(plan.Settings.PitchingLength, Math.Round(plan.PredictedPitchX, 7), "Solved pitch differs from request");
        }
        var exact = new PracticeBowlingSettings { RandomSpeed = false, RandomPitchLength = false,
            ManualLength = 15.1234, ManualSpeedKph = 94.5678, ReleaseHeight = 1.8765 };
        Check(PracticeDeliveryGenerator.TryCreatePlan(exact, 1, physics, out var manual, out _), "Manual fixture rejected");
        Check(manual!.Settings == exact.ManualDelivery, "Generator rounded manual values");
        var random = new Random(17);
        var distinct = new HashSet<DeliverySettings>();
        for (int i = 0; i < 1000; i++) distinct.Add(PracticeDeliveryGenerator.Sample(new(), random));
        Check(distinct.Count > 990, "Random generator repeats the same delivery");
    }

    public static void Failures()
    {
        var impossible = new PracticeBowlingSettings { RandomSpeed = false, ManualSpeedKph = 18, ReleaseHeight = 1.53 };
        Check(!PracticeDeliveryGenerator.TryCreatePlan(impossible, 1, new(), out var plan, out string error) && plan == null && error.Length > 0,
            "Random length changed an unreachable manual speed");
        Check(!PracticeDeliveryGenerator.TryCreatePlan(new(), 1, new() { Gravity = 1000 }, out plan, out error) && plan == null && error.Length > 0,
            "Bounded generation failed to report impossible physics");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        bool cancelled = false;
        try { PracticeDeliveryGenerator.TryCreatePlan(new(), 1, new(), out _, out _, cancellation.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Random delivery generation ignored cancellation");

        // A narrow feasible speed interval exercises the fallback without changing the requested length.
        double lo = 8, hi = 25;
        for (int i = 0; i < 16; i++)
        {
            double mid = (lo + hi) / 2;
            if (DeliverySolver.TrySolve(new(SpeedUnits.ToMetresPerSecond(49.9), mid, 1.53), new(), out _, out _)) lo = mid;
            else hi = mid;
        }
        var narrow = new PracticeBowlingSettings { Bowler = BowlerType.SuperSlow, RandomPitchLength = false,
            ManualLength = lo, ReleaseHeight = 1.53 };
        Check(PracticeDeliveryGenerator.TryCreatePlan(narrow, 5, new(), out plan, out error), error);
        Near(lo, plan!.Settings.PitchingLength, "Fallback changed manual length");
        Near(49.9, SpeedUnits.ToKilometresPerHour(plan.Settings.Speed), "Fallback did not use the permitted upper speed");
    }

    public static void Session()
    {
        using var session = new PracticeSession(new(), 8128);
        Check(!session.CanBowl && session.IsPreparing, "Pending work can bowl");
        Check(!session.TryBegin(new(new()), out _), "Started before preparation");
        Wait(session);
        var first = session.PendingPlan!;
        int seed = session.Seed;
        session.Guides = new(false, false);
        for (int i = 0; i < 100; i++) { session.OpenNextDelivery(); session.Poll(); }
        Check(ReferenceEquals(first, session.PendingPlan) && seed == session.Seed, "Guides/menu reopen rerolled pending ball");
        var wrongPhysics = new Simulation(new() { AirDensity = 0 });
        Check(!session.TryBegin(wrongPhysics, out _) && wrongPhysics.Phase == DeliveryPhase.Setup && session.CanBowl,
            "Plan for different physics was accepted or consumed");
        var sim = new Simulation(new());
        Check(session.TryBegin(sim, out string error), error);
        Check(ReferenceEquals(first, sim.Plan) && sim.Ball == first.Release && !session.CanBowl, "Bowl did not consume exact preview");
        Check(!session.TryBegin(sim, out _), "Same pending ball started twice");
        var direct = new Simulation(new()); direct.Begin(first.Settings, out _);
        for (int i = 0; i < 360; i++) { sim.Tick(default); direct.Tick(default); }
        Check(sim.Ball == direct.Ball && sim.Phase == direct.Phase, "Prepared start changes physics");
        session.OpenNextDelivery(); Check(session.Seed != seed && !session.CanBowl, "Next delivery did not get a new seed");
        Wait(session);
        session.Configure(new() { RandomSpeed = false, RandomPitchLength = false, ManualSpeedKph = 18, ManualLength = 25 });
        session.Poll();
        for (int i = 0; i < 60; i++) session.Configure(new() { ManualLength = 8 + i % 17, ReleaseHeight = 1.53 + i % 60 / 100.0 });
        var latest = new PracticeBowlingSettings { RandomSpeed = false, RandomPitchLength = false, ManualSpeedKph = 95, ManualLength = 16 };
        session.Configure(latest); Wait(session);
        Check(session.PendingPlan!.Settings == latest.ManualDelivery, "Stale randomized work replaced final manual settings");
        session.Configure(latest with { ManualSpeedKph = 18 }); Wait(session);
        Check(!session.CanBowl && session.PendingPlan == null && session.Error.Length > 0, "Invalid result kept stale preview");
        session.Configure(new()); session.Poll(); session.Dispose();
        Check(!session.Poll() && !session.CanBowl, "Disposed session published work");
    }

    public static void Reset()
    {
        using var session = new PracticeSession(new(), 8128);
        var sim = new Simulation(new());
        for (int i = 0; i < 6; i++)
        {
            session.OpenNextDelivery();
            session.Configure(new() { RandomSpeed = false, RandomPitchLength = false, ManualSpeedKph = 140,
                ManualLength = 18, ReleaseHeight = 1.8 });
            session.Guides = new(false, false);
            // Alternate resetting consumed balls and cancelling a preview that is still preparing.
            if (i % 2 == 0) { Wait(session); Check(session.TryBegin(sim, out _), "Fixture did not bowl"); }
            else session.Poll();
            int previousSeed = session.Seed;
            session.Reset();
            Check(session.Settings == new PracticeBowlingSettings() && session.Guides == new PracticeGuides(true, true),
                "Leaving practice retained settings or guides");
            Check(session.IsPreparing && !session.CanBowl && session.PendingPlan == null && session.Error.Length == 0 &&
                session.Seed != previousSeed, "Leaving practice retained a stale plan or error");
            Wait(session);
            Check(session.CanBowl && session.PendingPlan!.Settings.ReleaseHeight == 2.15 &&
                SpeedUnits.ToKilometresPerHour(session.PendingPlan.Settings.Speed) is >= 80.1 and <= 110,
                "A stale settings job replaced the default delivery");
            Check(session.TryBegin(sim, out _), "Fresh practice remained consumed after reset");
        }
    }

    private static void Wait(PracticeSession session)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < 10)
        {
            if (session.Poll()) return;
            Thread.Sleep(1);
        }
        throw new InvalidOperationException("Practice preparation timed out");
    }
}
