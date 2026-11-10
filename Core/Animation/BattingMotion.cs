namespace Cricket.Physics;

public enum BattingMotionState { Ready, Backlift, ForwardSwing, FollowThrough, Recovery }
public enum FootContact { Planted, Airborne, ToePivot }
public readonly record struct FootPose(Vec2 Toe, double Angle, FootContact Contact, double Yaw = 0, double Depth = .14)
{
    public static readonly Vec2 ToeToAnkle = new(.14, .05);
    public Vec2 Ankle { get { var offset=Rotate(ToeToAnkle,Angle); return Toe+new Vec2(offset.X*Math.Sin(Yaw),offset.Y); } }
    public double AnkleDepth => Depth-Rotate(ToeToAnkle,Angle).X*Math.Cos(Yaw);
    public Vec2 Heel => Toe + new Vec2(Math.Cos(Angle)*Math.Sin(Yaw),Math.Sin(Angle))*.24;
    public static FootPose AtRest(double ankleX) => new(new(ankleX, .035), 0, FootContact.Planted);
    public static Vec2 Rotate(Vec2 v, double a) => new(v.X * Math.Cos(a) - v.Y * Math.Sin(a), v.X * Math.Sin(a) + v.Y * Math.Cos(a));
    public static FootPose Interpolate(FootPose a, FootPose b, double t) => new(Vec2.Lerp(a.Toe, b.Toe, t),
        a.Angle + (b.Angle - a.Angle) * t, t == 0 ? a.Contact : t == 1 ? b.Contact :
        a.Contact == b.Contact ? a.Contact : a.Toe == b.Toe ? FootContact.ToePivot : FootContact.Airborne,
        a.Yaw+(b.Yaw-a.Yaw)*t,a.Depth+(b.Depth-a.Depth)*t);
}

/// <summary>Compact replay state. Reconstruct constrained joints after interpolating controls.</summary>
public readonly record struct BattingFrame(double SwingProgress, double WeightShift, ShotStyle Shot,
    FootPose FrontFoot, FootPose RearFoot, BattingMotionState State, double BatAngle = double.NaN)
{
    public static BattingFrame Interpolate(BattingFrame a, BattingFrame b, double fraction)
    {
        double t = Math.Clamp(fraction, 0, 1);
        return new(a.SwingProgress + (b.SwingProgress - a.SwingProgress) * t,
            a.WeightShift + (b.WeightShift - a.WeightShift) * t,
            t < 1 ? a.Shot : b.Shot,
            FootPose.Interpolate(a.FrontFoot, b.FrontFoot, t), FootPose.Interpolate(a.RearFoot, b.RearFoot, t),
            t < 1 ? a.State : b.State,
            double.IsFinite(a.BatAngle) && double.IsFinite(b.BatAngle) ? a.BatAngle + (b.BatAngle-a.BatAngle)*t : double.NaN);
    }
}
/// <summary>Bounded damped weight shift and lift/plant steps. A planted foot never translates.</summary>
internal sealed class BattingMotion
{
    private readonly BodySettings _settings;
    private double _weight, _velocity;
    private StepFoot _front = new(BattingRig.FrontFootX), _rear = new(BattingRig.BackFootX);
    public BattingMotion(BodySettings settings)
    {
        _settings = settings;
        _weight = settings.DefaultForwardWeight;
        // Start already planted in the default stance; no unsolicited opening step.
        _front = new(BattingRig.FrontFootX - settings.FrontStrideMetres * _weight);
    }
    public BattingFrame Step(double requestedWeight, double swing, double swingSpeed, ShotStyle shot, double dt)
    {
        double omega = 6 / _settings.TransferSeconds;
        double error = _weight - requestedWeight, decay = Math.Exp(-omega * dt);
        double temp = (_velocity + omega * error) * dt;
        double next = Math.Clamp(requestedWeight + (error + temp) * decay, -1, 1);
        _weight += Math.Clamp(next - _weight, -2.5 * dt, 2.5 * dt);
        _velocity = Math.Clamp((_velocity - omega * temp) * decay, -2.5, 2.5);
        if (Math.Abs(_weight - requestedWeight) < 1e-6 && Math.Abs(_velocity) < 1e-5)
        { _weight = requestedWeight; _velocity = 0; }
        double front = Math.Max(0, _weight), back = Math.Max(0,-_weight);
        double forwardRequest=Math.Max(0,requestedWeight), backwardRequest=Math.Max(0,-requestedWeight);
        // Move the receiving foot first. The lightly loaded front foot may then gather toward the rear leg.
        if (backwardRequest>0)
        {
            _rear.Advance(BattingRig.BackFootX+_settings.BackStrideMetres*backwardRequest,front*_settings.RearHeelDegrees*Math.PI/180,front*1.2+back*.12,dt,_settings,!_front.Moving);
            _front.Advance(BattingRig.FrontFootX+.08*backwardRequest,back*.16,front+back*.2,dt,_settings,!_rear.Moving);
        }
        else
        {
            _front.Advance(BattingRig.FrontFootX-_settings.FrontStrideMetres*forwardRequest,back*.16,front+back*.2,dt,_settings,!_rear.Moving);
            _rear.Advance(BattingRig.BackFootX,front*_settings.RearHeelDegrees*Math.PI/180,front*1.2+back*.12,dt,_settings,!_front.Moving);
        }
        var state = swingSpeed < -.002 ? (swing < .2 ? BattingMotionState.Recovery : BattingMotionState.Backlift) : swingSpeed > .002 ?
            swing > .7 ? BattingMotionState.FollowThrough : BattingMotionState.ForwardSwing :
            swing < .005 ? BattingMotionState.Ready : BattingMotionState.FollowThrough;
        return new(swing, _weight, shot, _front.Pose, _rear.Pose, state);
    }
    private struct StepFoot(double ankleX)
    {
        private Vec2 _from, _to;
        private double _time, _duration;
        public bool Moving { get; private set; }
        public FootPose Pose { get; private set; } = FootPose.AtRest(ankleX);
        public void Advance(double targetAnkleX, double angle, double yaw, double dt, BodySettings settings, bool canLift)
        {
            Vec2 target = FootPose.AtRest(targetAnkleX).Toe;
            if (!Moving && canLift && Math.Abs(target.X - Pose.Toe.X) > (targetAnkleX == ankleX ? .00001 : .018))
            { Moving = true; _from = Pose.Toe; _to = target; _time = 0;
                _duration=Math.Max(settings.StepSeconds,Math.Abs(_to.X-_from.X)*1.875/3.2); }
            if (Moving)
            {
                _time = Math.Min(1, _time + dt / _duration);
                double ease = _time * _time * _time * (10 + _time * (-15 + 6 * _time));
                Vec2 toe = Vec2.Lerp(_from, _to, ease) + new Vec2(0, settings.StepLiftMetres * Math.Pow(Math.Sin(Math.PI * _time), 2));
                Moving = _time < 1;
                Pose = new(toe, angle, Moving ? FootContact.Airborne : angle > .001 ? FootContact.ToePivot : FootContact.Planted,yaw);
            }
            else Pose = Pose with { Angle = angle, Yaw = yaw, Contact = angle > .001 ? FootContact.ToePivot : FootContact.Planted };
        }
    }
}
