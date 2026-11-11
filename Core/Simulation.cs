namespace Cricket.Physics;

public enum DeliveryPhase { Setup, Windup, Live }
public enum ContactKind { Pitch, Bat, Wicket, Boundary, Barricade, Terrace }
/// <summary>Impact measurements for presentation; blade fraction runs from the handle end of the blade to its toe.</summary>
public readonly record struct ContactEvent(ContactKind Kind, Vec2 Position, double SpeedBefore, double SpeedAfter,
    double ImpactSpeed = 0, double? BatBladeFraction = null, double? BatFaceAlignment = null);

/// <summary>One Tick advances 1/120 s of gameplay; the released ball uses the selected speed until its first bat hit.</summary>
public sealed class Simulation
{
    public SimulationSettings Settings { get; }
    public GameplaySettings Gameplay { get; }
    public BattingSettings Batting { get; }
    public FieldGeometry Field { get; }
    public BattingRig Batter { get; private set; }
    public DeliveryPhase Phase { get; private set; } = DeliveryPhase.Setup;
    public DeliveryPlan? Plan { get; private set; }
    public BallState Ball { get; private set; }
    public BallState PreviousBall { get; private set; }
    public HumanPose PreviousPose { get; private set; }
    public BattingFrame PreviousFrame { get; private set; }
    public bool BallVisible => Phase == DeliveryPhase.Live;
    public bool WicketHit { get; private set; }
    public bool BatHit { get; private set; }
    public double? FirstBounceX { get; private set; }
    public double? HitExitSpeed { get; private set; }
    public double PhaseTime { get; private set; }
    public DeliveryStatistics Statistics { get; } = new();
    public long TickCount { get; private set; }
    public List<Vec2> Trail { get; } = [];
    public List<ContactEvent> Events { get; } = [];
    public string Outcome => WicketHit ? "BOWLED" : BatHit ? "BAT CONTACT" :
        Phase == DeliveryPhase.Live && Ball.Position.X > PitchDimensions.WicketSeparation + 0.2 ? "THROUGH TO KEEPER" : "BALL IN PLAY";

    public Simulation(SimulationSettings settings, BattingSettings? batting = null, FieldSettings? field = null,
        GameplaySettings? gameplay = null)
    {
        settings.Validate();
        Settings = settings;
        Gameplay = gameplay ?? new();
        Field = new(field ?? new(), settings);
        Batting = batting ?? new();
        Batter = new(settings, Batting);
        PreviousPose = Batter.Pose;
        PreviousFrame = Batter.Frame;
    }

    public bool Begin(DeliverySettings request, out string error)
    {
        if (!DeliverySolver.TrySolve(request, Settings, out var plan, out error)) return false;
        return BeginPrepared(plan!, out error);
    }

    /// <summary>Starts a solver-produced plan for these physics settings without solving on the game thread.</summary>
    public bool BeginPrepared(DeliveryPlan plan, out string error)
    {
        error = plan.Settings.ValidationError ?? "";
        if (plan.Physics != Settings) error = "Prepare this delivery again using the current physics settings.";
        if (error.Length != 0) return false;
        Plan = plan;
        Batter = new(Settings, Batting);
        PreviousPose = Batter.Pose;
        PreviousFrame = Batter.Frame;
        Phase = DeliveryPhase.Windup;
        PhaseTime = 0;
        WicketHit = BatHit = false;
        FirstBounceX = HitExitSpeed = null;
        Statistics.Reset();
        Trail.Clear();
        Events.Clear();
        Ball = PreviousBall = plan!.Release;
        return true;
    }

    public void ReturnToSetup()
    {
        Phase = DeliveryPhase.Setup;
        PhaseTime = 0;
        PreviousFrame = Batter.Frame;
        PreviousPose = Batter.Pose;
        PreviousBall = Ball;
    }

    public void Tick(BattingInput input)
    {
        Events.Clear();
        PreviousBall = Ball;
        PreviousPose = Batter.Pose;
        PreviousFrame = Batter.Frame;
        // Input is sampled once per physics tick, not once per collision substep.
        // Spread its pose change across those substeps so velocity is delta / Tick, not delta / (Tick / 8).
        Batter.Step(input, SimulationSettings.Tick, Settings);
        PreviousFrame=Batter.MotionFromFrame;
        // Free rehearsal before bowling still accepts batting input; no ball/time/statistics advance.
        if (Phase == DeliveryPhase.Setup) return;
        double dt = SimulationSettings.Tick / Settings.Substeps;
        BatPose from = PreviousPose.Bat;
        for (int i = 0; i < Settings.Substeps; i++)
        {
            if (Phase == DeliveryPhase.Live)
            {
                BatPose to=Batter.SampleBatMotion(PreviousPose.Bat,(i+1.0)/Settings.Substeps);
                AdvanceBall(dt, from, to);
                from=to;
                Statistics.Observe(Ball);
            }
        }
        TickCount++;
        PhaseTime += SimulationSettings.Tick;
        if (Phase == DeliveryPhase.Windup && PhaseTime + 1e-9 >= SimulationSettings.WindupDuration)
        {
            Ball = PreviousBall = Plan!.Release;
            Phase = DeliveryPhase.Live;
            PhaseTime = 0;
            Trail.Add(Ball.Position);
            Statistics.Observe(Ball);
        }
        else if (Phase == DeliveryPhase.Live)
        {
            if (TickCount % 2 == 0) Trail.Add(Ball.Position);
            if (Trail.Count > 320) Trail.RemoveAt(0);
        }
    }

    private void AdvanceBall(double dt, BatPose batFrom, BatPose batTo)
    {
        // Ground, bat, wickets and field obstacles compete for the earliest swept contact.
        double elapsed = 0;
        for (int iteration = 0; iteration < 6 && elapsed < dt - 1e-10; iteration++)
        {
            double remaining = dt - elapsed;
            // Only the released ball uses slow time. Keep bat motion and collision
            // impulses in real time/SI units, and restore full time at the first hit,
            // including any time left in this substep after that contact.
            double ballTimeScale = BatHit ? 1 : Gameplay.BowlingSpeed;
            bool rolling = Ball.Position.Y <= Settings.BallRadius + 1e-5 && Math.Abs(Ball.Velocity.Y) < 0.08;
            bool supported = Field.TrySupport(Ball, Settings.BallRadius, out var support);
            if (supported) rolling = false;
            BallState Travel(double time) => supported ? Contacts.SurfaceMotion(Ball, support, time * ballTimeScale, Settings) :
                rolling ? Roll(Ball, time * ballTimeScale) : BallFlight.Integrate(Ball, time * ballTimeScale, Settings);
            BallState next = Travel(remaining);
            double earliest = remaining;
            ContactKind? kind = null;
            Vec2 normal = new(0, 1);
            Capsule collider = default;
            FieldSurface fieldSurface = default;
            BatPose startBat = BatPose.Lerp(batFrom, batTo, elapsed / dt);
            if (!rolling && next.Position.Y <= Settings.BallRadius && (Ball.Velocity.Y < 0 || next.Velocity.Y < 0))
            {
                if (supported)
                {
                    double lo = 0, hi = remaining;
                    for (int i = 0; i < 26; i++)
                    {
                        double mid = (lo + hi) / 2;
                        if (Travel(mid).Position.Y > Settings.BallRadius) lo = mid; else hi = mid;
                    }
                    earliest = hi;
                }
                else earliest = BallFlight.GroundTime(Ball, remaining * ballTimeScale, Settings) / ballTimeScale;
                kind = ContactKind.Pitch;
            }
            double sweepMinX = Math.Min(Ball.Position.X, next.Position.X) - Settings.BallRadius;
            double sweepMaxX = Math.Max(Ball.Position.X, next.Position.X) + Settings.BallRadius;
            double sweepMinY = Math.Min(Ball.Position.Y, next.Position.Y) - Settings.BallRadius;
            double sweepMaxY = Math.Max(Ball.Position.Y, next.Position.Y) + Settings.BallRadius;
            int surfaceCount = sweepMaxX < Field.LeftX && sweepMinX > Field.RightX ? 0 : Field.Surfaces.Count;
            for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
            {
                var surface = Field.Surfaces[surfaceIndex];
                // Reject distant terraces before the iterative swept-capsule test.
                if (Math.Max(surface.Edge.A.X, surface.Edge.B.X) < sweepMinX ||
                    Math.Min(surface.Edge.A.X, surface.Edge.B.X) > sweepMaxX ||
                    Math.Max(surface.Edge.A.Y, surface.Edge.B.Y) < sweepMinY ||
                    Math.Min(surface.Edge.A.Y, surface.Edge.B.Y) > sweepMaxY) continue;
                // Supports are integrated along their plane, so do not collide with themselves.
                if (supported && surface == support) continue;
                if (Consider(surface.Edge, null, surface.Kind)) fieldSurface = surface;
            }
            if (Ball.Position.X > 16.5 && Ball.Position.X < 22)
            {
                Consider(startBat.Blade(Settings), batTo.Blade(Settings), ContactKind.Bat);
                Consider(new(new(PitchDimensions.WicketSeparation, 0), new(PitchDimensions.WicketSeparation,
                    PitchDimensions.StumpHeight), PitchDimensions.StumpRadius), null, ContactKind.Wicket);
                // The batter is permanently invulnerable: the visible body, pads and
                // helmet never obstruct the ball. Bat and wicket contacts stay active.
            }
            if (kind == null) { Ball = next; break; }
            Ball = Travel(earliest);
            Statistics.Observe(Ball);
            double before = Ball.Velocity.Length;
            double impactSpeed = Math.Max(0, -Vec2.Dot(Ball.Velocity, normal));
            double? bladeFraction = null, faceAlignment = null;
            if (kind == ContactKind.Pitch)
            {
                if (!BatHit && FirstBounceX == null) FirstBounceX = Ball.Position.X;
                Ball = Contacts.GroundImpulse(Ball, Settings);
                if (Ball.Velocity.Y < 0.08) Ball = Ball with { Velocity = new(Ball.Velocity.X, 0) };
            }
            else if (kind == ContactKind.Bat)
            {
                BatPose impactPose = BatPose.Lerp(batFrom, batTo, (elapsed + earliest) / dt);
                Capsule blade = impactPose.Blade(Settings);
                Vec2 contact = blade.Closest(Ball.Position);
                Vec2 lever = contact - impactPose.Grip;
                Vec2 batVelocity = (batTo.Grip - batFrom.Grip) / dt +
                    (batTo.Angle - batFrom.Angle) / dt * lever.Perpendicular;
                bladeFraction = Math.Clamp(Vec2.Dot(contact - blade.A, blade.B - blade.A) /
                    (blade.B - blade.A).LengthSquared, 0, 1);
                faceAlignment = Math.Clamp(Math.Abs(Vec2.Dot(normal, impactPose.Direction.Perpendicular)), 0, 1);
                impactSpeed = Math.Max(0, -Vec2.Dot(Ball.Velocity - batVelocity, normal));
                Vec2 leverFromCentreOfMass = lever - impactPose.Direction * Settings.BatCentreOfMassFromGrip;
                Ball = Contacts.BatImpulse(Ball, normal, batVelocity, leverFromCentreOfMass, Settings, out double impulse);
                Ball = Ball with { Position = contact + normal * (Settings.BatHalfThickness + Settings.BallRadius + 1e-5) };
                if (impulse > 0) { BatHit = true; HitExitSpeed = Ball.Velocity.Length; }
                else impactSpeed = 0;
            }
            else if (kind is ContactKind.Boundary or ContactKind.Barricade or ContactKind.Terrace)
            {
                Ball = Contacts.SurfaceImpulse(Ball, normal, fieldSurface.Restitution, fieldSurface.Friction, Settings);
                Ball = Ball with { Position = collider.Closest(Ball.Position) + normal * (Settings.BallRadius + 1e-5) };
            }
            else
            {
                double vn = Vec2.Dot(Ball.Velocity, normal);
                const double restitution = 0.30;
                Ball = Ball with
                {
                    Velocity = Ball.Velocity - (1 + restitution) * Math.Min(0, vn) * normal,
                    Position = collider.Closest(Ball.Position) + normal * (collider.Radius + Settings.BallRadius + 1e-5)
                };
                WicketHit = true;
            }
            Events.Add(new(kind.Value, Ball.Position, before, Ball.Velocity.Length, impactSpeed, bladeFraction, faceAlignment));
            Statistics.Observe(Ball);
            elapsed += Math.Max(earliest, 1e-8);

            bool Consider(Capsule from, Capsule? to, ContactKind candidateKind)
            {
                if (!Contacts.Sweep(Ball.Position, next.Position, Settings.BallRadius, from, to ?? from, out double fraction, out Vec2 n)) return false;
                double time = remaining * fraction;
                if (time > earliest) return false;
                Vec2 surfaceVelocity = Vec2.Zero;
                if (candidateKind == ContactKind.Bat)
                {
                    BatPose pose = BatPose.Lerp(startBat, batTo, fraction);
                    Vec2 lever = pose.Blade(Settings).Closest(Vec2.Lerp(Ball.Position, next.Position, fraction)) - pose.Grip;
                    surfaceVelocity = (batTo.Grip - batFrom.Grip) / dt + (batTo.Angle - batFrom.Angle) / dt * lever.Perpendicular;
                }
                if (Vec2.Dot(Ball.Velocity - surfaceVelocity, n) >= -1e-6) return false;
                earliest = time;
                kind = candidateKind;
                normal = n;
                collider = Capsule.Lerp(from, to ?? from, fraction);
                return true;
            }
        }
        if (Ball.Position.Y < Settings.BallRadius)
            Ball = Ball with { Position = new(Ball.Position.X, Settings.BallRadius) };
    }

    private BallState Roll(BallState ball, double dt)
        => Contacts.GroundMotion(ball, dt, Settings);
}
