namespace Cricket.Physics;

public readonly record struct DeliverySettings(double Speed, double PitchingLength, double ReleaseHeight)
{
    public const double MinReleaseHeight = 1.53;
    public const double MaxReleaseHeight = 2.16;
    public const double MinSpeed = 5;
    public const double MaxSpeed = 50;
    public const double MinPitchingLength = 8;
    public const double MaxPitchingLength = 25;

    public string? ValidationError =>
        !double.IsFinite(Speed) || Speed is < MinSpeed or > MaxSpeed ? "Release speed must be between 18 and 180 km/h." :
        !double.IsFinite(PitchingLength) || PitchingLength is < MinPitchingLength or > MaxPitchingLength ?
            $"Pitching length must be between {MinPitchingLength:0} and {MaxPitchingLength:0} m from the bowler-end wicket." :
        !double.IsFinite(ReleaseHeight) || ReleaseHeight is < MinReleaseHeight or > MaxReleaseHeight ?
            $"Ball release height must be between {MinReleaseHeight:0.00} and {MaxReleaseHeight:0.00} m." : null;
}

public sealed record DeliveryPlan(DeliverySettings Settings, BallState Release, double Angle,
    double FlightTime, double PredictedPitchX, IReadOnlyList<Vec2> Preview);

public static class DeliverySolver
{
    /// <summary>Finds the lower, bowling-like arc using the same drag model as the live ball.</summary>
    public static bool TrySolve(DeliverySettings request, SimulationSettings s, out DeliveryPlan? plan, out string error)
    {
        plan = null;
        error = request.ValidationError ?? "";
        if (error.Length != 0) return false;
        double lowAngle = -85 * Math.PI / 180;
        double lowRange = FirstPitch(request, lowAngle, s).X;
        double highAngle = lowAngle;
        bool bracketed = false;
        // Scan upwards: the first root is always the low trajectory, never the lob branch.
        for (int degrees = -84; degrees <= 65; degrees++)
        {
            highAngle = degrees * Math.PI / 180;
            double range = FirstPitch(request, highAngle, s).X;
            if (lowRange <= request.PitchingLength && range >= request.PitchingLength)
            { bracketed = true; break; }
            lowAngle = highAngle;
            lowRange = range;
        }
        if (!bracketed)
        {
            error = "That length cannot be reached at this speed and release height. Increase speed or shorten the length.";
            return false;
        }
        for (int i = 0; i < 32; i++)
        {
            double middle = (lowAngle + highAngle) / 2;
            if (FirstPitch(request, middle, s).X < request.PitchingLength) lowAngle = middle;
            else highAngle = middle;
        }
        double angle = (lowAngle + highAngle) / 2;
        var preview = new List<Vec2>();
        var landing = FirstPitch(request, angle, s, preview);
        plan = new(request, Release(request, angle), angle, landing.Time, landing.X, preview);
        return true;
    }

    private static BallState Release(DeliverySettings d, double angle) =>
        new(new(PitchDimensions.ReleaseX, d.ReleaseHeight), Vec2.FromAngle(angle) * d.Speed);

    private static (double X, double Time) FirstPitch(DeliverySettings d, double angle,
        SimulationSettings s, List<Vec2>? preview = null)
    {
        BallState ball = Release(d, angle);
        double dt = SimulationSettings.Tick / s.Substeps;
        preview?.Add(ball.Position);
        for (int i = 0; i < 20000; i++)
        {
            BallState next = BallFlight.Integrate(ball, dt, s);
            if (next.Position.Y <= s.BallRadius)
            {
                double hitTime = BallFlight.GroundTime(ball, dt, s);
                var hit = BallFlight.Integrate(ball, hitTime, s);
                preview?.Add(hit.Position);
                return (hit.Position.X, i * dt + hitTime);
            }
            ball = next;
            if (i % 16 == 0) preview?.Add(ball.Position);
        }
        return (double.NaN, double.NaN);
    }
}
