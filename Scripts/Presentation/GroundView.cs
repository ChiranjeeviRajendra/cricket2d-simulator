using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>Pure presentation: a single metres-to-pixels transform, never used by physics.</summary>
public partial class GroundView : Node2D
{
    public Simulation Simulation { get; set; } = null!;
    public bool ShowTrajectory { get; set; } = true;
    public bool Diagnostics { get; set; }
    private double _pausedFraction;
    private long _resumeTick = -1;
    private bool _interpolationPaused;
    public bool InterpolationPaused
    {
        get => _interpolationPaused;
        set
        {
            if (value && !_interpolationPaused) _pausedFraction = RenderFraction;
            if (!value && _interpolationPaused) _resumeTick = Simulation.TickCount;
            _interpolationPaused = value;
        }
    }
    private double RenderFraction => Simulation.Phase == DeliveryPhase.Setup ? 1 :
        _interpolationPaused || _resumeTick == Simulation.TickCount ? _pausedFraction : Engine.GetPhysicsInterpolationFraction();
    public DeliveryPlan? Preview { get; set; }
    private const float Left = (float)FieldCamera.Left;
    private const float GroundY = (float)FieldCamera.GroundY;
    private readonly FieldCamera _camera = new();
    private float PixelsPerMetre => (float)_camera.PixelsPerMetre;
    private float Zoom => (float)_camera.Zoom;
    private Vec2? _captureBall;
    public Vec2? TrackedBallPosition => _captureBall ?? (Simulation.BallVisible ?
        Vec2.Lerp(Simulation.PreviousBall.Position, Simulation.Ball.Position, RenderFraction) : null);
    private static readonly Color Skin = new("bc8a68");
    private static readonly Color White = new("e2e9d9");
    private static readonly Color ShadowWhite = new("9caeaa");
    private static readonly Color BatWood = new("d4b781");
    private Font _font = null!;

    public override void _Ready()
    {
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        _font = new SystemFont { FontNames = ["Bahnschrift", "Segoe UI"] };
    }

    public override void _Process(double delta)
    {
        if (TrackedBallPosition is Vec2 ball) _camera.Fit(ball);
        else _camera.Reset();
        QueueRedraw();
    }

    internal void SetCaptureBall(Vec2? position)
    {
        _captureBall = position;
        _camera.Reset();
        if (position is Vec2 ball) _camera.Fit(ball);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Simulation == null) return;
        Environment();
        Pitch();
        Wicket(0, false);
        Wicket(PitchDimensions.WicketSeparation, Simulation.WicketHit);
        Target();
        Bowler();
        Batter();
        Boundaries();
        Ball();
    }

    private void Environment()
    {
        DrawRect(new(0, 0, 1440, 900), VisualTheme.Background);
        float sceneryOffset = GroundY - 627;
        // A restrained evening practice ground, all scenery is behind the physical plane.
        for (int y = 96; y < 500 + sceneryOffset; y += 4)
        {
            float t = (y - 96) / (404 + sceneryOffset);
            DrawRect(new(0, y, 1440, 4), new Color("101f26").Lerp(new("36565a"), t));
        }
        DrawSetTransform(new(0, sceneryOffset));
        DrawCircle(new(1140, 333), 36, new Color(0.77f, 0.86f, 0.68f, 0.08f));
        DrawCircle(new(1140, 333), 24, new Color("a4b8a0"));
        DrawColoredPolygon([new(0, 471), new(120, 434), new(256, 466), new(410, 440),
            new(570, 477), new(760, 445), new(940, 473), new(1110, 438), new(1280, 462), new(1440, 432),
            new(1440, 600), new(0, 600)], new("294347"));
        for (int i = 0; i < 32; i++)
        {
            float x = i * 49;
            float top = 474 - (float)(Math.Sin(i * 8.2) * 9);
            DrawCircle(new(x, top), 23 + i % 3 * 6, new("223c3e"));
            DrawLine(new(x, top), new(x, 512), new("223c3e"), 7);
        }
        DrawRect(new(0, 504, 1440, 123), new Color("304b42"));
        for (int i = 0; i < 7; i++)
            DrawRect(new(0, 510 + i * 19, 1440, 9), new Color(0.3f, 0.42f, 0.32f, 0.12f));
        DrawLine(new(0, 523), new(1440, 523), new("739081"), 1);
        for (int x = 15; x < 1440; x += 47) DrawLine(new(x, 516), new(x, 540), new("5e7c70"), 1);
        // Sparse grass blades, deterministic to avoid flicker.
        for (int i = 0; i < 175; i++)
        {
            float x = i * 8.27f;
            float y = 565 + (i * 37 % 58);
            DrawLine(new(x, y), new(x + 2, y - 3), new Color(0.56f, 0.65f, 0.41f, 0.16f), 1);
        }
        DrawSetTransform(Vector2.Zero);
        DrawRect(new(0, GroundY, 1440, 33), new("6c7053"));
        DrawRect(new(0, GroundY + 33, 1440, 900 - GroundY - 33), new("14282c"));
    }

    private void Pitch()
    {
        float strikerX = W(new(PitchDimensions.WicketSeparation, 0)).X;
        float bowlerX = W(Vec2.Zero).X;
        float margin = .30f * PixelsPerMetre;
        float depth = Math.Max(3, .33f * PixelsPerMetre);
        DrawRect(new(strikerX - margin, GroundY, bowlerX - strikerX + margin * 2, depth), new("9e9471"));
        DrawLine(new(strikerX - margin, GroundY), new(bowlerX + margin, GroundY), new("c7bb91"), Math.Max(1, 2 * Zoom));
        for (int i = 0; i < 80 && Zoom > .3; i++)
        {
            float x = W(new(i * .25, 0)).X;
            float y = GroundY + (4 + i % 9) * Zoom;
            DrawLine(new(x, y), new(x + 4 * Zoom, y), new("8e8768"), 1);
        }
        foreach (double x in new[] { PitchDimensions.PoppingCrease, PitchDimensions.WicketSeparation - PitchDimensions.PoppingCrease })
            DrawRect(new(W(new(x, 0)).X - Zoom * 2, GroundY, Math.Max(1, 4 * Zoom), depth), new("e3dfc4"));
    }

    private void Wicket(double x, bool hit)
    {
        // In true side projection the three stumps overlap. Small depth offsets make them legible.
        Vector2 root = W(new(x, 0));
        for (int i = -1; i <= 1; i++)
        {
            Vector2 bottom = root + new Vector2(i * 3.5f, -i * 0.9f) * Zoom;
            Vector2 top = bottom + new Vector2(hit ? (-13 - i * 6) * Zoom : 0, -(float)PitchDimensions.StumpHeight * PixelsPerMetre);
            DrawLine(bottom, top, i == 0 ? new("e5cf9f") : new("b09b77"), Math.Max(.7f, 2.4f * Zoom), true);
        }
        if (!hit) DrawLine(root + new Vector2(-5, -46) * Zoom, root + new Vector2(5, -46) * Zoom, new("f0dbb0"), Math.Max(.7f, 2 * Zoom), true);
        else
        {
            DrawLine(root + new Vector2(-17, -14) * Zoom, root + new Vector2(-22, -19) * Zoom, BatWood, Math.Max(.7f, 2 * Zoom), true);
            DrawLine(root + new Vector2(14, -4) * Zoom, root + new Vector2(9, -2) * Zoom, BatWood, Math.Max(.7f, 2 * Zoom), true);
        }
    }

    private void Target()
    {
        var plan = Simulation.Phase == DeliveryPhase.Setup ? Preview : Simulation.Plan;
        if (plan == null) return;
        Vector2 target = W(new(plan.Settings.PitchingLength, 0));
        DrawLine(target + new Vector2(-8, 0), target + new Vector2(8, 0), VisualTheme.OnDark, 3, true);
        DrawLine(target + new Vector2(0, 4), target + new Vector2(0, 22), VisualTheme.OnDark, 1, true);
        if (Simulation.Phase != DeliveryPhase.Live && ShowTrajectory)
        {
            for (int i = 1; i < plan.Preview.Count; i += 2)
                DrawLine(W(plan.Preview[i - 1]), W(plan.Preview[i]), new Color(0.78f, 0.93f, 0.58f, 0.24f), 1, true);
        }
    }

    private void Batter()
    {
        BattingFrame frame = BattingFrame.Interpolate(Simulation.PreviousFrame, Simulation.Batter.Frame, RenderFraction);
        BatterDrawing.Draw(this, BattingRig.BuildPose(frame, Simulation.Batting), Simulation.Settings, W, PixelsPerMetre, Diagnostics);
    }
    private void Bowler()
    {
        double height = (Simulation.Phase == DeliveryPhase.Setup ? Preview : Simulation.Plan)?.Settings.ReleaseHeight ?? 2.15;
        double scale = height / 2.15;
        double progress = Simulation.Phase == DeliveryPhase.Windup ? Simulation.PhaseTime / SimulationSettings.WindupDuration : 1;
        double sway = Simulation.Phase == DeliveryPhase.Windup ? Math.Sin(progress * Math.PI) * 0.16 : 0;
        Vec2 hip = new(PitchDimensions.ReleaseX - 0.16 - sway, 0.85 * scale);
        Vec2 shoulder = new(PitchDimensions.ReleaseX, height - 0.65 * scale);
        Vec2 head = shoulder + new Vec2(0.015, 0.24 * scale);
        Vec2 rearAnkle = new(hip.X - 0.30, 0.06);
        Vec2 frontAnkle = new(hip.X + 0.29, 0.06);
        Vec2 rearKnee = BattingRig.SolveJoint(hip, rearAnkle, 0.46 * scale, 0.43 * scale, 1);
        Vec2 frontKnee = BattingRig.SolveJoint(hip, frontAnkle, 0.46 * scale, 0.43 * scale, 1);
        DrawEllipse(W(new(hip.X, 0)), new Vector2(26, 4) * Zoom, new Color(0, 0, 0, 0.2f));
        Limb(hip, rearKnee, new("658d8c"), 0.12);
        Limb(rearKnee, rearAnkle, new("658d8c"), 0.10);
        Shoe(rearAnkle, ShadowWhite);
        Limb(hip, frontKnee, new("90b4a8"), 0.13);
        Limb(frontKnee, frontAnkle, new("90b4a8"), 0.10);
        Shoe(frontAnkle, White);
        Limb(hip, shoulder, new("90b4a8"), 0.23);
        Limb(shoulder, head, Skin, 0.07);
        DrawCircle(W(head), (float)(0.105 * scale * PixelsPerMetre), Skin);
        DrawArc(W(head + new Vec2(0, 0.03)), (float)(0.10 * scale * PixelsPerMetre), MathF.PI, MathF.Tau, 12, new("263a3e"), Math.Max(.6f, 4 * Zoom), true);
        double armAngle = Simulation.Phase == DeliveryPhase.Windup ? (-150 + progress * 240) * Math.PI / 180 :
            Simulation.Phase == DeliveryPhase.Live ? (90 - Math.Min(Simulation.PhaseTime / 0.4, 1) * 190) * Math.PI / 180 : Math.PI / 2;
        Vec2 hand = shoulder + Vec2.FromAngle(armAngle) * (0.65 * scale);
        Vec2 elbow = shoulder + Vec2.FromAngle(armAngle) * (0.33 * scale);
        Limb(shoulder, elbow, new("90b4a8"), 0.09);
        Limb(elbow, hand, Skin, 0.06);
        Vec2 otherElbow = shoulder + new Vec2(0.19, -0.22) * scale;
        Limb(shoulder, otherElbow, new("6f9991"), 0.09);
        Limb(otherElbow, hip + new Vec2(0.13, 0.07), Skin, 0.06);
        if (Simulation.Phase != DeliveryPhase.Live) DrawCircle(W(hand), (float)Simulation.Settings.BallRadius * PixelsPerMetre, new("e07462"));
        if (Diagnostics)
        {
            Vector2 release = W(new(PitchDimensions.ReleaseX, height));
            DrawLine(release + new Vector2(-6, 0), release + new Vector2(6, 0), VisualTheme.Accent, 1);
            DrawLine(release + new Vector2(0, -6), release + new Vector2(0, 6), VisualTheme.Accent, 1);
        }
    }

    private void Ball()
    {
        if (ShowTrajectory)
        {
            var trail = Simulation.Trail;
            for (int i = 1; i < trail.Count; i++)
            {
                if (!_camera.Contains(trail[i - 1]) || !_camera.Contains(trail[i])) continue;
                DrawLine(W(trail[i - 1]), W(trail[i]), new Color(0.95f, 0.75f, 0.55f, 0.08f + 0.55f * i / trail.Count), 1.4f, true);
            }
        }
        if (TrackedBallPosition is not Vec2 position) return;
        Vector2 screen = W(position);
        Vector2 ground = W(new(position.X, 0));
        DrawEllipse(ground, new(Math.Max(2, (4 + (float)position.Y) * Zoom), 1.5f), new Color(0, 0, 0, 0.15f));
        // Minimum visible radius and halo keep distant balls readable; physics size is unchanged.
        DrawCircle(screen, 5, new Color(1, 0.6f, 0.4f, 0.12f));
        DrawCircle(screen, Math.Max(2.2f, (float)Simulation.Settings.BallRadius * PixelsPerMetre), new("e8735c"));
        DrawLine(screen + new Vector2(-0.7f, -1.4f), screen + new Vector2(0.7f, 1.4f), new("ffe1c0"), 0.8f, true);
        if (Diagnostics)
            DrawLine(screen, W(position + Simulation.Ball.Velocity * 0.04), VisualTheme.Accent, 1, true);
    }

    private void Boundaries()
    {
        Boundary(FieldLayout.LeftBoundaryX, FieldLayout.LeftBoundaryDistance);
        Boundary(FieldLayout.RightBoundaryX, FieldLayout.RightBoundaryDistance);
    }

    private void Boundary(double worldX, double distance)
    {
        Vector2 root = W(new(worldX, 0));
        // Markers belong to the field. Never substitute an edge-pinned label when off-screen.
        if (root.X < FieldCamera.Left || root.X > FieldCamera.Right) return;
        float top = GroundY - Math.Max(32, PixelsPerMetre);
        DrawLine(root, new(root.X, top), VisualTheme.Accent, 2, true);
        DrawCircle(root, 3, VisualTheme.Accent);
        float boardX = root.X - 59;
        DrawRect(new(boardX, top - 28, 118, 25), VisualTheme.Panel);
        DrawRect(new(boardX, top - 28, 118, 25), VisualTheme.Accent, false, 1);
        Text(new(boardX + 8, top - 11), $"{distance:0} m BOUNDARY", 12, VisualTheme.Accent);
    }

    private Vector2 W(Vec2 p)
    {
        Vec2 screen = _camera.Project(p);
        return new((float)screen.X, (float)screen.Y);
    }
    private void Limb(Vec2 a, Vec2 b, Color color, double width)
    {
        float pixels = (float)width * PixelsPerMetre;
        DrawLine(W(a), W(b), color, pixels, true);
        DrawCircle(W(a), pixels / 2, color);
        DrawCircle(W(b), pixels / 2, color);
    }
    private void Shoe(Vec2 ankle, Color color) => Limb(ankle + new Vec2(-0.10, -0.04), ankle + new Vec2(0.05, -0.04), color, 0.075);
    private void DrawEllipse(Vector2 center, Vector2 radius, Color color)
    {
        var points = new Vector2[24];
        for (int i = 0; i < points.Length; i++) points[i] = center + Vector2.FromAngle(i * MathF.Tau / points.Length) * radius;
        DrawColoredPolygon(points, color);
    }
    private void Text(Vector2 at, string value, int size, Color color) => DrawString(_font, at, value, HorizontalAlignment.Left, -1, size, color);
}
