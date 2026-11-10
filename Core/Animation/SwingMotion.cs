namespace Cricket.Physics;

/// <summary>Distance-based acceleration and braking; release never schedules a return to guard.</summary>
internal sealed class SwingMotion(SwingSettings settings)
{
    public double Degrees { get; private set; }
    public double Speed { get; private set; }
    private int _direction;
    private double _travel;

    public void Stop() => Speed = 0;

    public void Step(double target, bool driven, double metresPerDegree, double dt, double maximum)
    {
        double start = settings.StartSpeedKph / 3.6, limit = settings.MaxSpeedKph / 3.6;
        double distance = target - Degrees;
        int direction = Math.Sign(distance);
        double braking = limit * limit / (2 * metresPerDegree * settings.BrakingDegrees);
        // Reverse through zero; changing the target cannot instantaneously reverse momentum.
        bool brake = !driven || (Speed > 0 && direction != _direction);
        double before = Degrees;
        if (brake)
        {
            double time = Math.Min(dt, Speed / braking);
            Degrees += _direction * (Speed * time - .5 * braking * time * time) / metresPerDegree;
            Speed = Math.Max(0, Speed - braking * dt);
        }
        else if (direction != 0)
        {
            if (Speed == 0) { _direction = direction; _travel = 0; Speed = start; }
            // v² = v0² + 2as: the speed envelope reaches its maximum after 25 actual degrees.
            double acceleration = (limit * limit - start * start) /
                (2 * metresPerDegree * settings.AccelerationDegrees);
            double envelope = Math.Sqrt(start * start + (limit * limit - start * start) *
                Math.Min(1, _travel / settings.AccelerationDegrees));
            double next = Math.Min(limit, Math.Min(Speed, envelope) + acceleration * dt);
            if (Speed * Speed / (2 * braking) >= Math.Abs(distance) * metresPerDegree)
                next = Math.Max(0, Speed - braking * dt);
            double advance = (Speed + next) * .5 * dt / metresPerDegree;
            Degrees += direction * Math.Min(Math.Abs(distance), advance);
            Speed = advance >= Math.Abs(distance) ? 0 : next;
        }
        Degrees = Math.Clamp(Degrees, 0, maximum);
        if (Degrees == before || Degrees is 0 || Degrees == maximum) Speed = 0;
        _travel += Math.Abs(Degrees - before);
    }

    public void Limit(double previous, double accepted)
    {
        double proposed = Math.Abs(Degrees - previous), actual = Math.Abs(accepted - previous);
        if (proposed > 1e-10) Speed *= Math.Min(1, actual / proposed);
        _travel = Math.Max(0, _travel - Math.Max(0, proposed - actual));
        Degrees = accepted;
    }
}
