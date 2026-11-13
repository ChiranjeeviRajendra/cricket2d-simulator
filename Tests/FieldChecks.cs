using System.Reflection;
using Cricket.Physics;
using Cricket2D.Presentation;

internal static class FieldChecks
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Near(double a, double b, double tolerance, string message) =>
        Check(double.IsFinite(b) && Math.Abs(a - b) <= tolerance, $"{message}: {a:G9} vs {b:G9}");
    private static double Energy(BallState b, SimulationSettings s) =>
        .5 * s.BallMass * b.Velocity.LengthSquared + .2 * s.BallMass * s.BallRadius * s.BallRadius * b.Spin * b.Spin;

    public static void ViewportSizing()
    {
        // Logical canvas sizes for 16:10, 16:9, ultrawide and 4:3 displays.
        Vec2[] sizes = [new(1440, 900), new(1600, 900), new(2400, 900), new(1440, 1080)];
        var camera = new FieldCamera();
        foreach (Vec2 size in sizes)
        {
            camera.SetViewportSize(size.X, size.Y);
            camera.Reset();
            Near(24, camera.Project(new(PitchDimensions.WicketSeparation, 0)).X, 1e-8, "Left wicket inset");
            Near(size.X - 24, camera.Project(Vec2.Zero).X, 1e-8, "Right wicket follows viewport edge");
            Near(size.Y - 140, camera.Project(Vec2.Zero).Y, 1e-8, "Ground follows viewport bottom");
            Vec2 origin = camera.Project(Vec2.Zero);
            Near(origin.X - camera.Project(new(1, 0)).X, origin.Y - camera.Project(new(0, 1)).Y,
                1e-8, "Equal horizontal and vertical metre scale preserves anatomy");
            foreach (Vec2 ball in new Vec2[] { new(-120, 40), new(150, 100), new(10, 180) })
            {
                camera.Reset(); camera.Fit(ball);
                foreach (Vec2 resized in sizes)
                {
                    double zoom = camera.Zoom;
                    camera.SetViewportSize(resized.X, resized.Y);
                    Near(zoom, camera.Zoom, 1e-12, "Resize retains delivery zoom");
                    camera.Fit(ball);
                    Check(camera.Contains(ball) && camera.Contains(Vec2.Zero) &&
                        camera.Contains(new(PitchDimensions.WicketSeparation, PitchDimensions.StumpHeight)),
                        "Resizing a tracked shot must retain the ball and both wickets");
                    Vec2 physical = camera.Project(ball);
                    Vec2 scenery = FieldLayout.Project(camera, new(ball.X, 0), ball.Y);
                    Near(physical.X, scenery.X, 1e-8, "Scenery and physics align horizontally after resize");
                    Near(physical.Y, scenery.Y, 1e-8, "Scenery and physics align vertically after resize");
                    double depth = FieldLayout.DepthAtScreenY(camera, resized.Y + 80);
                    Near(resized.Y + 80, FieldLayout.Project(camera, new(0, depth)).Y,
                        1e-8, "Foreground extends below the resized viewport");
                }
            }
        }
    }

    public static void Layout()
    {
        var settings = FieldSettings.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Config", "field.json")));
        var g = new FieldGeometry(settings, new());
        Near(40, g.LeftX - PitchDimensions.BatterReferenceX, 1e-12, "Left distance");
        Near(75, PitchDimensions.BatterReferenceX - g.RightX, 1e-12, "Right distance");
        Near(1.5, settings.Barricade.HeightMetres, 0, "Inner barricade height");
        Near(settings.OuterBarricade.HeightMetres, g.Surfaces.Max(s => Math.Max(s.Edge.A.Y, s.Edge.B.Y)),
            1e-12, "Configured outer barricade height reaches collision geometry");
        for (int i = 0; i < 180; i++)
        {
            double a = i * Math.Tau / 180;
            Near(5, (g.OvalPoint(a, g.BarricadeOffset) - g.OvalPoint(a, settings.Boundary.WidthMetres)).Length, 1e-10, "Clear gap around oval");
            Near(settings.Seating.WidthMetres, (g.OvalPoint(a, g.OuterBarricadeOffset) - g.OvalPoint(a, g.SeatingOffset)).Length,
                1e-10, "Outer barricade follows the back of the seating around the oval");
        }
        var custom = settings with { Boundary = settings.Boundary with { LeftDistanceMetres = 52, RightDistanceMetres = 91 } };
        var changed = new FieldGeometry(custom, new());
        Near(52, changed.LeftX - PitchDimensions.BatterReferenceX, 1e-12, "Edited left distance");
        Near(91, PitchDimensions.BatterReferenceX - changed.RightX, 1e-12, "Edited right distance");
        var camera = new FieldCamera();
        foreach (var shot in new Vec2[] { new(5, 2), new(-100, 40), new(110, 160) })
        {
            camera.Reset(); camera.Fit(shot);
            foreach (var geometry in new[] { g, changed })
                foreach (int side in new[] { -1, 1 })
                {
                    Vec2 point = geometry.OvalPoint(side == 1 ? 0 : Math.PI);
                    Near(side == 1 ? geometry.LeftX : geometry.RightX, point.X, 1e-12, "Oval physical crossing");
                    Vec2 screen = FieldLayout.Project(camera, point);
                    Near(camera.Project(new(point.X, 0)).X, screen.X, 1e-9, "Visible/physical X");
                    Near(camera.GroundY, screen.Y, 1e-9, "Ground contact Y");
                    Vec2 outer = geometry.OvalPoint(side == 1 ? 0 : Math.PI, geometry.OuterBarricadeOffset);
                    double wallX = (side == 1 ? geometry.LeftX : geometry.RightX) + side * geometry.OuterBarricadeOffset;
                    Vec2 top = FieldLayout.Project(camera, outer, geometry.Settings.OuterBarricade.HeightMetres);
                    Near(camera.Project(new(wallX, geometry.Settings.OuterBarricade.HeightMetres)).X, top.X, 1e-9, "Outer wall collision/render X");
                    Near(camera.Project(new(wallX, geometry.Settings.OuterBarricade.HeightMetres)).Y, top.Y, 1e-9, "Outer wall collision/render height");
                }
        }
        var differentPitch = new SimulationSettings { GroundRestitution = .67, GroundFriction = .31 };
        var inherited = new FieldGeometry(settings, differentPitch);
        Check(inherited.Surfaces.Where(s => s.Kind == ContactKind.Barricade).All(s => s.Restitution == .67 && s.Friction == .31), "Barricade must inherit edited pitch coefficients");
        var tuned = settings with { Barricade = settings.Barricade with { Restitution = .42, Friction = .35 } };
        Check(new FieldGeometry(tuned, differentPitch).Surfaces.Where(s => s.Kind == ContactKind.Barricade)
            .All(s => s.Restitution == .42 && s.Friction == .35), "Outer barricade must inherit inner barricade overrides");
        var widened = new FieldGeometry(settings with { Seating = settings.Seating with { WidthMetres = 33.5 } }, new());
        Near(12, widened.OuterBarricadeOffset - g.OuterBarricadeOffset, 1e-12, "Edited seating width moves outer wall");
        foreach (string json in new[]{"null","{\"Boundary\":null}","{\"Boundary\":{\"LeftDistanceMetres\":0}}",
            "{\"Boundary\":{\"Restitution\":1.1}}","{\"Barricade\":{\"ClearGapMetres\":-5}}","{\"Boundary\":{\"LeftDistanceMeters\":40}}",
            "{\"OuterBarricade\":null}","{\"OuterBarricade\":{\"HeightMetres\":81}}",
            "{\"OuterBarricade\":{\"ThicknessMetres\":0}}","{\"OuterBarricade\":{\"Friction\":-1}}",
            "{\"Seating\":null}","{\"Seating\":{\"Rows\":0}}","{\"Seating\":{\"Occupancy\":1.1}}",
            "{\"Seating\":{\"Sections\":9}}",
            "{\"Seating\":{\"WidthMetres\":12,\"Rows\":64}}","{\"OuterBarricade\":{\"HeightMetres\":1}}"})
        {
            bool rejected = false;
            try { FieldSettings.FromJson(json); } catch (ArgumentException) { rejected = true; } catch (System.Text.Json.JsonException) { rejected = true; }
            Check(rejected, $"Bad field settings accepted: {json}");
        }
        Near(40, FieldSettings.FromJson("{}").Boundary.LeftDistanceMetres, 0, "Defaults");
        Near(25, FieldSettings.FromJson("{\"OuterBarricade\":{\"ThicknessMetres\":0.4}}").OuterBarricade.HeightMetres, 0, "Partial outer settings retain 25 m default");
    }

    public static void Impacts()
    {
        var s = new SimulationSettings(); var random = new Random(4821);
        for (int i = 0; i < 3000; i++)
        {
            Vec2 n = Vec2.FromAngle(random.NextDouble() * Math.Tau), t = n.Perpendicular;
            double closing = .3 + random.NextDouble() * 90, e = random.NextDouble(), mu = random.NextDouble();
            var before = new BallState(new(0, 1), -n * closing + t * (random.NextDouble() * 100 - 50), random.NextDouble() * 1600 - 800);
            var after = Contacts.SurfaceImpulse(before, n, e, mu, s);
            Near(closing * e, Vec2.Dot(after.Velocity, n), 1e-9, "Normal restitution");
            Check(Energy(after, s) <= Energy(before, s) + 1e-8, "Static boundary added kinetic energy");
        }
        var ball = new BallState(new(0, s.BallRadius), new(15, -8), 120);
        var ground = Contacts.GroundImpulse(ball, s);
        var wallModel = Contacts.SurfaceImpulse(ball, new(0, 1), s.GroundRestitution, s.GroundFriction, s);
        Near(ground.Velocity.X, wallModel.Velocity.X, 1e-12, "Pitch tangent equivalence");
        Near(ground.Velocity.Y, wallModel.Velocity.Y, 1e-12, "Pitch normal equivalence");
        Near(ground.Spin, wallModel.Spin, 1e-9, "Pitch spin equivalence");
        var g = new FieldGeometry(new(), s);
        foreach (int side in new[] { -1, 1 })
        {
            double x = side == 1 ? g.LeftX : g.RightX;
            var hits = g.Surfaces.Where(surface => Contacts.Sweep(new(x - side * 2, s.BallRadius), new(x + side * 2, s.BallRadius),
                s.BallRadius, surface.Edge, surface.Edge, out _, out _)).ToArray();
            Check(hits.Any(h => h.Kind == ContactKind.Boundary), "A sweep tunneled through the thin wedge");
            double clearHeight = g.Settings.Barricade.HeightMetres + .8;
            Check(!g.Surfaces.Any(surface => Contacts.Sweep(new(x - side * 2, clearHeight), new(x + side * (g.SeatingOffset + .1), clearHeight), s.BallRadius,
                surface.Edge, surface.Edge, out _, out _)), "A clear aerial ball hit an invisible boundary wall");
        }
    }

    // Test-only state injection keeps the real live collision loop under test without adding a cheat API.
    private static Simulation Start(BallState ball, SimulationSettings? settings = null, FieldSettings? field = null)
    {
        var sim = new Simulation(settings ?? new(), field: field);
        Check(sim.Begin(new(32, 16, 2.15), out _), "Setup failed");
        while (sim.Phase != DeliveryPhase.Live) sim.Tick(default);
        typeof(Simulation).GetProperty(nameof(Simulation.Ball), BindingFlags.Public | BindingFlags.Instance)!.SetValue(sim, ball);
        typeof(Simulation).GetProperty(nameof(Simulation.PreviousBall))!.SetValue(sim, ball);
        // These fixtures inject outgoing shots, after incoming-ball slow motion ends.
        typeof(Simulation).GetProperty(nameof(Simulation.BatHit))!.SetValue(sim, true);
        return sim;
    }
    private static List<ContactEvent> Run(Simulation sim, int ticks)
    {
        var events = new List<ContactEvent>();
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick(default); events.AddRange(sim.Events);
            Check(double.IsFinite(sim.Ball.Position.X) && double.IsFinite(sim.Ball.Spin), "Non-finite field contact");
            Check(sim.Ball.Position.Y >= sim.Settings.BallRadius - 1e-8, "Ball fell below ground");
            ClearOfSolids(sim);
        }
        return events;
    }
    private static void ClearOfSolids(Simulation sim)
    {
        var g = sim.Field;
        foreach (int side in new[] { -1, 1 })
        {
            double x = side > 0 ? g.LeftX : g.RightX, w = g.Settings.Boundary.WidthMetres, h = g.Settings.Boundary.HeightMetres;
            double offset = (sim.Ball.Position.X - x) * side;
            if (offset >= g.SeatingOffset && offset < g.OuterBarricadeOffset)
            {
                int row = Math.Min(g.Settings.Seating.Rows - 1, (int)((offset - g.SeatingOffset) / g.RowDepth));
                Check(sim.Ball.Position.Y >= g.RowHeight(row) + sim.Settings.BallRadius - 1e-4, "Ball entered solid seating");
            }
            Vec2[] triangle = [new(x, 0), new(x + side * w / 2, h), new(x + side * w, 0)];
            double inner = x + side * g.BarricadeOffset, outer = inner + side * g.Settings.Barricade.ThicknessMetres;
            Vec2[] rectangle = [new(inner,0),new(inner,g.Settings.Barricade.HeightMetres),
                new(outer,g.Settings.Barricade.HeightMetres),new(outer,0)];
            double perimeterInner = x + side * g.OuterBarricadeOffset;
            double perimeterOuter = perimeterInner + side * g.Settings.OuterBarricade.ThicknessMetres;
            Vec2[] perimeter = [new(perimeterInner,0),new(perimeterInner,g.Settings.OuterBarricade.HeightMetres),
                new(perimeterOuter,g.Settings.OuterBarricade.HeightMetres),new(perimeterOuter,0)];
            foreach (var shape in new[] { triangle, rectangle, perimeter })
            {
                double distance = double.PositiveInfinity;
                bool positive = false, negative = false;
                for (int i = 0; i < shape.Length; i++)
                {
                    Vec2 a = shape[i], b = shape[(i + 1) % shape.Length];
                    double cross = Vec2.Cross(b - a, sim.Ball.Position - a);
                    positive |= cross > 0; negative |= cross < 0;
                    distance = Math.Min(distance, (sim.Ball.Position - new Capsule(a, b, 0).Closest(sim.Ball.Position)).Length);
                }
                Check(positive && negative, "Ball centre entered a solid field obstacle");
                Check(distance >= sim.Settings.BallRadius - 1e-4, $"Ball penetrated field obstacle by {sim.Settings.BallRadius - distance:G6} m");
            }
        }
    }
    public static void LiveContacts()
    {
        var s = new SimulationSettings { AirDensity = 0 }; var g = new FieldGeometry(new(), s);
        foreach (int side in new[] { -1, 1 })
        {
            double x = side == 1 ? g.LeftX : g.RightX;
            var incoming = new BallState(new(x - side * .6, s.BallRadius), new(side * 8, 0), -side * 8 / s.BallRadius);
            var sim = Start(incoming, s);
            var events = Run(sim, 60);
            Check(events.Any(e => e.Kind == ContactKind.Boundary), "Live rolling ball missed wedge");
            Check(!sim.WicketHit, "Field contact falsely broke wicket");
            double wall = x + side * g.BarricadeOffset;
            sim = Start(new(new(wall - side * .5, .65), new(side * 15, 0), 0), s);
            events = Run(sim, 12);
            Check(events.Any(e => e.Kind == ContactKind.Barricade), "Live ball missed barricade");
            Check(sim.Ball.Velocity.X * side < 0, "Barricade did not return ball inward");
            sim = Start(new(new(x - side * 1, 2.4), new(side * 45, 0), 0), s);
            events = Run(sim, 24);
            Check(!events.Any(e => e.Kind is ContactKind.Boundary or ContactKind.Barricade), "High ball cannot clear obstacles");
            // Very fast balls traverse the entire wedge in less than a single 120 Hz tick.
            sim = Start(new(new(x - side * .8, s.BallRadius), new(side * 180, 0), 0), s with { Substeps = 1 });
            events = Run(sim, 1);
            Check(events.Any(e => e.Kind == ContactKind.Boundary), "Fast ball tunneled in live loop");
            // Drop onto the outside face and test slow approaches to both sides over many ticks.
            foreach (double speed in new[] { -.4, .05, .4 })
            {
                double origin = speed < 0 ? x + side * (g.Settings.Boundary.WidthMetres + .12) : x - side * .12;
                sim = Start(new(new(origin, s.BallRadius), new(side * speed, 0), -side * speed / s.BallRadius), s);
                Run(sim, 960);
                Check(Energy(sim.Ball, s) < .001, "Slow contact failed to settle");
            }
        }
        var drop = Start(new(new(g.LeftX + g.Settings.Boundary.WidthMetres / 2, .8), new(0, -2), 0), s);
        Check(Run(drop, 100).Any(e => e.Kind == ContactKind.Boundary), "A falling ball missed the wedge apex");
    }
    public static void SupportedMotion()
    {
        var s = new SimulationSettings { AirDensity = 0, SpinDecay = 0 };
        var surface = new FieldGeometry(new(), s).Surfaces.First(x => x.Kind == ContactKind.Boundary);
        Vec2 p = Vec2.Lerp(surface.Edge.A, surface.Edge.B, .5) + surface.Normal * (s.BallRadius + 1e-5);
        BallState ball = new(p, Vec2.Zero);
        double initial = Energy(ball, s) + s.BallMass * s.Gravity * ball.Position.Y;
        for (int i = 0; i < 40; i++) ball = Contacts.SurfaceMotion(ball, surface, 1.0 / 960, s);
        Check(ball.Position.Y < p.Y, "Gravity failed to move a resting ball down the slope");
        Check(Energy(ball, s) + s.BallMass * s.Gravity * ball.Position.Y <= initial + 1e-6, "Supported rolling gained energy");
        var lifting = new BallState(p, -surface.Normal.Perpendicular * 100, 1600);
        var aero = new SimulationSettings();
        Check(Vec2.Dot(BallFlight.Acceleration(lifting.Velocity, lifting.Spin, aero), surface.Normal) > 0, "Lift fixture must pull away from the slope");
        var detached = Contacts.SurfaceMotion(lifting, surface, .001, aero);
        Check(Vec2.Dot(detached.Position - p, surface.Normal) > 0, "Outward lift must release a supported ball");
        // Same input through different render schedules must produce exactly the same final state.
        var g = new FieldGeometry(new(), s);
        BallState? expected = null;
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var sim = Start(new(new(g.LeftX - .4, s.BallRadius), new(7, 0), -7 / s.BallRadius), s);
            double debt = 0;
            for (int frame = 0; frame < fps * 3; frame++)
            {
                debt += 1.0 / fps;
                while (debt + 1e-12 >= SimulationSettings.Tick) { sim.Tick(default); debt -= SimulationSettings.Tick; }
            }
            if (expected.HasValue) Check(sim.Ball == expected.Value, "Field contacts depend on rendering rate");
            expected = sim.Ball;
        }
    }

    public static void OuterBarricadeContacts()
    {
        var physics = new SimulationSettings { AirDensity = 0 };
        var geometry = new FieldGeometry(new(), physics);
        foreach (int side in new[] { -1, 1 })
        {
            double x = side > 0 ? geometry.LeftX : geometry.RightX;
            double inner = x + side * geometry.BarricadeOffset;
            double outer = x + side * geometry.OuterBarricadeOffset;
            double thickness = geometry.Settings.OuterBarricade.ThicknessMetres;
            double height = geometry.Settings.OuterBarricade.HeightMetres;

            var lowWall = Start(new(new(inner - side * .5, .65), new(side * 15, 0), 120), physics);
            var tallWall = Start(new(new(outer + side * (thickness + .5), .65), new(-side * 15, 0), -120), physics);
            Run(lowWall, 12);
            var hits = Run(tallWall, 12);
            Check(hits.Any(e => e.Kind == ContactKind.Barricade), "Outer wall did not collide");
            Near(-lowWall.Ball.Velocity.X, tallWall.Ball.Velocity.X, 1e-7, "Both walls have the same normal rebound");
            Near(lowWall.Ball.Velocity.Y, tallWall.Ball.Velocity.Y, 1e-7, "Both walls have the same friction response");
            Near(-lowWall.Ball.Spin, tallWall.Ball.Spin, 1e-7, "Both walls have the same spin transfer");

            var outside = Start(new(new(outer + side * (thickness + .5), 10), new(-side * 35, 0)), physics);
            Check(Run(outside, 12).Any(e => e.Kind == ContactKind.Barricade) && outside.Ball.Velocity.X * side > 0,
                "Outer face failed to return an incoming outside ball");

            var fast = Start(new(new(outer + side * (thickness + .8), 10), new(-side * 180, 0)), physics with { Substeps = 1 });
            Check(Run(fast, 1).Any(e => e.Kind == ContactKind.Barricade) && fast.Ball.Velocity.X * side > 0,
                "Fast ball tunneled through outer barricade");

            var clear = Start(new(new(outer - side * .5, height + .7), new(side * 50, 0)), physics);
            Check(!Run(clear, 6).Any(e => e.Kind == ContactKind.Barricade), "Ball above outer wall hit invisible geometry");
            var top = Start(new(new(outer + side * thickness / 2, height + .35), new(0, -3)), physics);
            Check(Run(top, 15).Any(e => e.Kind == ContactKind.Barricade) && top.Ball.Velocity.Y > 0, "Outer barricade top failed to bounce ball");
        }
    }

    public static void Seating()
    {
        var physics = new SimulationSettings { AirDensity = 0 };
        var geometry = new FieldGeometry(new(), physics);
        Near(26.98, geometry.OuterBarricadeOffset, 1e-10, "Seating preserves the existing outer perimeter");
        Near(1.5, geometry.RowHeight(0), 1e-12, "Front row meets inner wall top");
        Near(25, geometry.RowHeight(geometry.Settings.Seating.Rows - 1), 1e-12, "Back row meets outer wall top");
        int total = 0, occupied = 0;
        for (int row = 0; row < geometry.Settings.Seating.Rows; row++)
        {
            if (row > 0) Check(geometry.RowHeight(row) > geometry.RowHeight(row - 1), "Seating is not stepped upwards");
            foreach (bool front in new[] { false, true })
            {
                var seats = StadiumSeating.CreateRow(geometry, row, front);
                Check(seats.SequenceEqual(StadiumSeating.CreateRow(geometry, row, front)), "Crowd must be deterministic");
                foreach (var seat in seats)
                {
                    total++; if (seat.Occupied) occupied++;
                    Near(geometry.RowHeight(row), seat.Height, 0, "Seat is not on its terrace");
                    Check(front == (seat.Ground.Y < 0), "Spectator on wrong side of field");
                    Check(Vec2.Dot(seat.Facing, new Vec2(geometry.CentreX, 0) - seat.Ground) > 0, "Spectator faces away from pitch");
                    for (int section = 0; section < geometry.Settings.Seating.Sections; section++)
                    {
                        double offset = geometry.RowOffset(row) + geometry.RowDepth * .55;
                        Vec2 aisle = geometry.OvalPoint(StadiumSeating.AisleAngle(geometry, section), offset);
                        Check((seat.Ground - aisle).Length > geometry.Settings.Seating.AisleWidthMetres / 2, "A seat obstructs the aisle");
                    }
                }
            }
        }
        Check(total > 10000 && Math.Abs((double)occupied / total - .82) < .02, "Unexpected crowd density");
        var empty = new FieldGeometry(new() { Seating = new() { Occupancy = 0 } }, physics);
        Check(StadiumSeating.CreateRow(empty, 0, false).All(s => !s.Occupied), "Empty crowd setting ignored");
        var full = new FieldGeometry(new() { Seating = new() { Occupancy = 1, CrowdSeed = 7 } }, physics);
        Check(StadiumSeating.CreateRow(full, 0, false).All(s => s.Occupied), "Full crowd setting ignored");

        foreach (int side in new[] { -1, 1 })
        {
            double x = side > 0 ? geometry.LeftX : geometry.RightX;
            var lofted = Start(new(new(x + side * (geometry.BarricadeOffset - .5), 12), new(side * 80, 0)), physics);
            Check(Run(lofted, 60).Any(e => e.Kind == ContactKind.Terrace), "Lofted shot missed the stands");
            Check(lofted.Ball.Velocity.X * side < 0, "Terrace riser did not return ball toward pitch");
            int row = 12;
            double inner = x + side * geometry.RowOffset(row), height = geometry.RowHeight(row);
            var drop = Start(new(new(inner + side * geometry.RowDepth / 2, height + .35), new(0, -3)), physics);
            Check(Run(drop, 15).Any(e => e.Kind == ContactKind.Terrace) && drop.Ball.Velocity.Y > 0, "Terrace tread did not bounce ball");
            var settled = Start(new(new(inner + side * geometry.RowDepth / 2, height + physics.BallRadius + 1e-5), Vec2.Zero), physics);
            Run(settled, 360);
            Near(height + physics.BallRadius + 1e-5, settled.Ball.Position.Y, 1e-8, "Resting terrace support drifted");
            var fast = Start(new(new(inner - side * .2, height - .35), new(side * 180, 0)), physics with { Substeps = 1 });
            Check(Run(fast, 1).Any(e => e.Kind == ContactKind.Terrace) && fast.Ball.Velocity.X * side < 0,
                "Fast ball tunneled through terrace riser");
            var clear = Start(new(new(x + side * geometry.SeatingOffset, 30), new(side * 100, 0)), physics);
            Check(!Run(clear, 28).Any(e => e.Kind is ContactKind.Terrace or ContactKind.Barricade), "High ball cannot clear seating");
        }
    }
}
