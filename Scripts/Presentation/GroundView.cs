using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>Pure presentation: a single metres-to-pixels transform, never used by physics.</summary>
public partial class GroundView : Node2D
{
    public Simulation Simulation { get; set; } = null!;
    private bool _showTrajectory = true, _diagnostics, _viewDirty = true;
    private BattingFrame _lastFrame;
    private DeliveryPhase _lastPhase;
    private DeliveryPlan? _preview;
    private Func<Vec2, Vector2> _worldToScreen = null!;
    public bool ShowTrajectory { get => _showTrajectory; set { _viewDirty |= _showTrajectory != value; _showTrajectory = value; } }
    public bool Diagnostics { get => _diagnostics; set { _viewDirty |= _diagnostics != value; _diagnostics = value; } }
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
            _viewDirty = true;
        }
    }
    private double RenderFraction => Simulation.Phase == DeliveryPhase.Setup ? 1 :
        _interpolationPaused || _resumeTick == Simulation.TickCount ? _pausedFraction : Engine.GetPhysicsInterpolationFraction();
    public DeliveryPlan? Preview { get => _preview; set { _viewDirty |= !ReferenceEquals(_preview, value); _preview = value; } }
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
    private FieldDrawing _fieldDrawing = null!;
    internal int SceneryRebuilds => _fieldDrawing.RebuildCount;

    public override void _Ready()
    {
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _fieldDrawing = new(Simulation.Field);
        _worldToScreen = W;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        if ((Simulation.Phase == DeliveryPhase.Setup || InterpolationPaused) && !_viewDirty &&
            _lastPhase == Simulation.Phase && _lastFrame == Simulation.Batter.Frame) return;
        _lastPhase = Simulation.Phase;
        _lastFrame = Simulation.Batter.Frame;
        _viewDirty = false;
        if (TrackedBallPosition is Vec2 ball) _camera.Fit(ball);
        else _camera.Reset();
        QueueRedraw();
    }

    internal void SetCaptureBall(Vec2? position)
    {
        _captureBall = position;
        _viewDirty = true;
        _camera.Reset();
        if (position is Vec2 ball) _camera.Fit(ball);
        QueueRedraw();
    }

    public override void _ExitTree() => _fieldDrawing?.Dispose();

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

    private void Environment() => _fieldDrawing.Background(this, _camera);

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
        BatterDrawing.Draw(this, BattingRig.BuildPose(frame, Simulation.Batting), Simulation.Settings, _worldToScreen, PixelsPerMetre, Diagnostics);
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
        DrawingPrimitives.Ellipse(this, W(new(hip.X, 0)), new Vector2(26, 4) * Zoom, new Color(0, 0, 0, 0.2f));
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
        DrawingPrimitives.Ellipse(this, ground, new(Math.Max(2, (4 + (float)position.Y) * Zoom), 1.5f), new Color(0, 0, 0, 0.15f));
        // Minimum visible radius and halo keep distant balls readable; physics size is unchanged.
        DrawCircle(screen, 5, new Color(1, 0.6f, 0.4f, 0.12f));
        DrawCircle(screen, Math.Max(2.2f, (float)Simulation.Settings.BallRadius * PixelsPerMetre), new("e8735c"));
        DrawLine(screen + new Vector2(-0.7f, -1.4f), screen + new Vector2(0.7f, 1.4f), new("ffe1c0"), 0.8f, true);
        if (Diagnostics)
            DrawLine(screen, W(position + Simulation.Ball.Velocity * 0.04), VisualTheme.Accent, 1, true);
    }

    private void Boundaries() => _fieldDrawing.Foreground();

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
}
