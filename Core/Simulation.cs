namespace Cricket.Physics;

public enum DeliveryPhase { Setup, Windup, Live }
public enum ContactKind { Pitch, Bat, Wicket }
public readonly record struct ContactEvent(ContactKind Kind, Vec2 Position, double SpeedBefore, double SpeedAfter);

/// <summary>Single owner of time/state. One Tick call always advances exactly 1/120 s.</summary>
public sealed class Simulation
{
    public SimulationSettings Settings { get; }
    public BattingSettings Batting { get; }
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

    public Simulation(SimulationSettings settings, BattingSettings? batting = null)
    {
        settings.Validate();
        Settings = settings;
        Batting = batting ?? new();
        Batter = new(settings, Batting);
        PreviousPose = Batter.Pose;
        PreviousFrame = Batter.Frame;
    }

    public bool Begin(DeliverySettings request, out string error)
    {
        if (!DeliverySolver.TrySolve(request, Settings, out var plan, out error)) return false;
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
        if (Phase == DeliveryPhase.Setup) return;
        double dt = SimulationSettings.Tick / Settings.Substeps;
        for (int i = 0; i < Settings.Substeps; i++)
        {
            BatPose from = Batter.Pose.Bat;
            Batter.Step(input, dt, Settings);
            if (Phase == DeliveryPhase.Live)
            {
                AdvanceBall(dt, from, Batter.Pose.Bat);
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
        // Far beyond the fixed view, continue free flight and ground bounces.
        double elapsed = 0;
        for (int iteration = 0; iteration < 6 && elapsed < dt - 1e-10; iteration++)
        {
            double remaining = dt - elapsed;
            bool rolling = Ball.Position.Y <= Settings.BallRadius + 1e-5 && Math.Abs(Ball.Velocity.Y) < 0.08;
            BallState next = rolling ? Roll(Ball, remaining) : BallFlight.Integrate(Ball, remaining, Settings);
            double earliest = remaining;
            ContactKind? kind = null;
            Vec2 normal = new(0, 1);
            Capsule collider = default;
            BatPose startBat = BatPose.Lerp(batFrom, batTo, elapsed / dt);
            if (!rolling && next.Position.Y <= Settings.BallRadius && Ball.Velocity.Y < 0)
            {
                earliest = BallFlight.GroundTime(Ball, remaining, Settings);
                kind = ContactKind.Pitch;
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
            Ball = rolling ? Roll(Ball, earliest) : BallFlight.Integrate(Ball, earliest, Settings);
            Statistics.Observe(Ball);
            double before = Ball.Velocity.Length;
            if (kind == ContactKind.Pitch)
            {
                if (!BatHit && FirstBounceX == null) FirstBounceX = Ball.Position.X;
                Ball = Contacts.GroundImpulse(Ball, Settings);
                if (Ball.Velocity.Y < 0.08) Ball = Ball with { Velocity = new(Ball.Velocity.X, 0) };
            }
            else if (kind == ContactKind.Bat)
            {
                BatPose impactPose = BatPose.Lerp(batFrom, batTo, (elapsed + earliest) / dt);
                Vec2 contact = impactPose.Blade(Settings).Closest(Ball.Position);
                Vec2 lever = contact - impactPose.Grip;
                Vec2 batVelocity = (batTo.Grip - batFrom.Grip) / dt +
                    (batTo.Angle - batFrom.Angle) / dt * lever.Perpendicular;
                Vec2 leverFromCentreOfMass = lever - impactPose.Direction * Settings.BatCentreOfMassFromGrip;
                Ball = Contacts.BatImpulse(Ball, normal, batVelocity, leverFromCentreOfMass, Settings, out double impulse);
                Ball = Ball with { Position = contact + normal * (Settings.BatHalfThickness + Settings.BallRadius + 1e-5) };
                if (impulse > 0) { BatHit = true; HitExitSpeed = Ball.Velocity.Length; }
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
            Events.Add(new(kind.Value, Ball.Position, before, Ball.Velocity.Length));
            Statistics.Observe(Ball);
            elapsed += Math.Max(earliest, 1e-8);

            void Consider(Capsule from, Capsule? to, ContactKind candidateKind)
            {
                if (!Contacts.Sweep(Ball.Position, next.Position, Settings.BallRadius, from, to ?? from, out double fraction, out Vec2 n)) return;
                double time = remaining * fraction;
                if (time > earliest) return;
                Vec2 surfaceVelocity = Vec2.Zero;
                if (candidateKind == ContactKind.Bat)
                {
                    BatPose pose = BatPose.Lerp(startBat, batTo, fraction);
                    Vec2 lever = pose.Blade(Settings).Closest(Vec2.Lerp(Ball.Position, next.Position, fraction)) - pose.Grip;
                    surfaceVelocity = (batTo.Grip - batFrom.Grip) / dt + (batTo.Angle - batFrom.Angle) / dt * lever.Perpendicular;
                }
                if (Vec2.Dot(Ball.Velocity - surfaceVelocity, n) >= -1e-6) return;
                earliest = time;
                kind = candidateKind;
                normal = n;
                collider = Capsule.Lerp(from, to ?? from, fraction);
            }
        }
        if (Ball.Position.Y < Settings.BallRadius)
            Ball = Ball with { Position = new(Ball.Position.X, Settings.BallRadius) };
    }

    private BallState Roll(BallState ball, double dt)
        => Contacts.GroundMotion(ball, dt, Settings);
}
