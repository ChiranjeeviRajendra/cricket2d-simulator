using System.Numerics;

namespace Cricket.Physics;

/// <summary>Screen stick axes: right/down positive. Triggers are independent normalized [0,1] pressures.</summary>
public readonly record struct BattingInput(double LeftX, double LeftY, double RightX, double RightY,
    double FrontWeight = 0, double BackWeight = 0, ShotStyle? Shot = null);
public readonly record struct BatPose(Vec2 Grip, double Angle)
{
    public Vec2 Direction => Vec2.FromAngle(Angle);
    public Vec2 FaceNormal => -Direction.Perpendicular;
    public Capsule Blade(SimulationSettings s) =>
        new(Grip + Direction * s.HandleLength, Grip + Direction * (s.BatLength - s.BatHalfThickness), s.BatHalfThickness);
    public static BatPose Lerp(BatPose a, BatPose b, double t) => new(Vec2.Lerp(a.Grip, b.Grip, t), a.Angle + (b.Angle - a.Angle) * t);
}
public readonly record struct HumanPose(Vec2 Hip, Vec2 Shoulder, Vec2 Head,
    Vec2 LeadElbow, Vec2 RearShoulder, Vec2 RearElbow, Vec2 Hands,
    Vec2 FrontKnee, Vec2 FrontAnkle, Vec2 BackKnee, Vec2 BackAnkle, BatPose Bat,
    FootPose FrontFoot, FootPose RearFoot, double HeadAngle, BattingMotionState MotionState,
    Vec2 BottomHand, Vec2 Spine, Vec2 Chest, Vec2 Neck, double TorsoTwist, double PelvisTwist, AthletePose Anatomy);

/// <summary>Fixed-step input, anatomical pose synthesis and the single authoritative collision bat.</summary>
public sealed class BattingRig
{
    public const double UpperArm = .32, Forearm = .29, Thigh = .45, Shin = .43;
    public const double FrontFootX = PitchDimensions.BatterReferenceX - .26;
    public const double BackFootX = PitchDimensions.BatterReferenceX + .26;
    public const double AnkleHeight = .085;
    private readonly BattingMotion _motion;
    private readonly SwingMotion _swingMotion;
    private bool _shotHeld, _adjustingShot;
    private double _swing, _speed;
    private readonly SwingGesture _gesture = new();
    public bool GestureEngaged => _gesture.Engaged;
    public BattingSettings Settings { get; }
    public HumanPose Pose { get; private set; }
    public BattingFrame Frame { get; private set; }
    public BattingFrame MotionFromFrame { get; private set; }
    public bool BeganStroke { get; private set; }
    public double SwingClockDegrees => SwingGesture.StartClockDegrees * (1 - _swing);
    public double SwingSpeed => _speed;
    public double BatTipSpeed { get; private set; }

    public void SuspendGesture() { _gesture.Reset(); _swingMotion.Stop(); _speed=BatTipSpeed=0; _shotHeld=_adjustingShot=false; }

    public BattingRig(SimulationSettings physics, BattingSettings? settings = null)
    {
        Settings = settings ?? new();
        Settings.Validate(physics);
        _motion = new(Settings.Body);
        _swingMotion = new(Settings.Swing);
        Frame = _motion.Step(Settings.Body.DefaultForwardWeight, 0, 0, ShotStyle.FullDrive, 0);
        Pose = BuildPose(Frame, Settings);
        Frame = Frame with { BatAngle = Pose.Bat.Angle };
        MotionFromFrame=Frame;
    }
    public void Step(BattingInput input, double dt, SimulationSettings physics)
    {
        if (!double.IsFinite(dt) || dt <= 0 || dt > .05) throw new ArgumentOutOfRangeException(nameof(dt));
        bool wasEngaged=_gesture.Engaged;
        _gesture.Step(input.LeftX,input.LeftY,Settings.Input);
        bool releasedShot = _shotHeld && !input.Shot.HasValue;
        _shotHeld = input.Shot.HasValue;
        if (releasedShot) _gesture.Reset();
        BeganStroke=_gesture.Engaged && !wasEngaged;
        ShotStyle shot=input.Shot is { } selected && Enum.IsDefined(selected)?selected:Frame.Shot;
        if (releasedShot || (wasEngaged && !_gesture.Engaged)) _adjustingShot=false;
        if (input.Shot.HasValue && shot!=Frame.Shot) _adjustingShot=true;
        MotionFromFrame=Frame;
        double from = _swingMotion.Degrees;
        // Measure the local tip path, including hand translation, rather than confusing arm
        // angular speed with blade-tip linear speed. Value types keep this allocation-free.
        double sampleDegrees = from < 254.75 ? .25 : -.25;
        double sampleProgress = (from + sampleDegrees) / SwingGesture.StartClockDegrees;
        double bladeDelta = (BattingStroke.Sample(sampleProgress, Frame.Shot).BatDegrees -
            BattingStroke.Sample(_swing, Frame.Shot).BatDegrees) * Math.PI / 180;
        var sample = BuildPose(Frame with { SwingProgress = sampleProgress, BatAngle = Frame.BatAngle + bladeDelta }, Settings);
        double metresPerDegree = Math.Max(.002, MaximumBatSpeed(Pose.Bat, sample.Bat, physics.BatLength, 1) / Math.Abs(sampleDegrees));
        double previousSpeed = _swingMotion.Speed;
        _swingMotion.Step(Math.Min(_gesture.Target, BattingStroke.MaximumProgress(shot)) * SwingGesture.StartClockDegrees,
            _gesture.Engaged, metresPerDegree, dt, SwingGesture.StartClockDegrees);
        double next = _swingMotion.Degrees / SwingGesture.StartClockDegrees;
        double weightInput = DeadZone(input.FrontWeight, Settings.Input.TriggerDeadZone) - DeadZone(input.BackWeight, Settings.Input.TriggerDeadZone);
        double baseline = Settings.Body.DefaultForwardWeight;
        double weight = baseline + weightInput * (weightInput >= 0 ? 1-baseline : 1+baseline);
        var body = _motion.Step(weight, next, (next-_swing)/dt, shot, dt);
        double angle = BattingStroke.Sample(next,shot).BatDegrees * Math.PI / 180;
        if (!_gesture.Engaged && !_adjustingShot)
        {
            // Release retains the current blade adjustment while the arm completes braking.
            angle = Frame.BatAngle + (BattingStroke.Sample(next, Frame.Shot).BatDegrees -
                BattingStroke.Sample(_swing, Frame.Shot).BatDegrees) * Math.PI / 180;
        }
        var candidate = body with { BatAngle = angle };
        double speedLimit = Math.Min(Settings.Swing.MaxSpeedKph / 3.6,
            Math.Max(Settings.Swing.StartSpeedKph / 3.6, Math.Max(previousSpeed, _swingMotion.Speed)));
        // Bound the actual swept bat, including mode changes. The conservative maximum is
        // evaluated analytically along the same grip/angle interpolation used for collisions.
        HumanPose pose = BuildPose(candidate, Settings);
        for (int i=0; i<6; i++)
        {
            double speed = MaximumBatSpeed(Pose.Bat, pose.Bat, physics.BatLength, dt);
            if (speed <= speedLimit + 1e-8) break;
            double fraction = speedLimit / speed * .9999;
            candidate = candidate with
            {
                SwingProgress = _swing + (candidate.SwingProgress-_swing)*fraction,
                BatAngle = Frame.BatAngle + (candidate.BatAngle-Frame.BatAngle)*fraction
            };
            pose = BuildPose(candidate, Settings);
        }
        _swingMotion.Limit(from, candidate.SwingProgress * SwingGesture.StartClockDegrees);
        _speed = (candidate.SwingProgress-_swing)/dt;
        _swing = candidate.SwingProgress;
        BatTipSpeed = MaximumBatSpeed(Pose.Bat, pose.Bat, physics.BatLength, dt);
        // A pathological pose request must never bypass the hard ceiling after bounded work.
        if (BatTipSpeed > Settings.Swing.MaxSpeedKph/3.6 + 1e-8)
        {
            _swingMotion.Limit(_swing*SwingGesture.StartClockDegrees,from);
            _swingMotion.Stop(); _swing=Frame.SwingProgress; _speed=BatTipSpeed=0;
            return;
        }
        if (Math.Abs(candidate.BatAngle-BattingStroke.Sample(_swing,shot).BatDegrees*Math.PI/180)<1e-8)
            _adjustingShot=false;
        Frame = candidate;
        Pose = pose;
    }
    /// <summary>Sample the collision bat across one tick, including a live change of shot mode.</summary>
    public BatPose SampleBatMotion(BatPose from, double fraction)
    {
        double t=Math.Clamp(fraction,0,1);
        if(t==0) return from;
        if(t==1) return Pose.Bat;
        return BatPose.Lerp(from,Pose.Bat,t);
    }

    /// <summary>Maximum speed anywhere on a rigid bat along the tick's interpolated motion.</summary>
    public static double MaximumBatSpeed(BatPose from, BatPose to, double length, double dt)
    {
        Vec2 translation = to.Grip-from.Grip;
        double turn = to.Angle-from.Angle, arc = length*turn;
        double low = Math.Min(from.Angle,to.Angle), high = Math.Max(from.Angle,to.Angle);
        double best = Math.Max(Squared(from.Angle),Squared(to.Angle));
        if (Math.Abs(arc)>1e-12 && translation.LengthSquared>1e-20)
        {
            double peak = Math.Atan2(-arc*translation.X,arc*translation.Y);
            peak += Math.Ceiling((low-peak)/Math.Tau)*Math.Tau;
            if (peak<=high) best = Math.Max(best,Squared(peak));
        }
        return Math.Sqrt(Math.Max(translation.LengthSquared,best))/dt;
        double Squared(double angle) => (translation + Vec2.FromAngle(angle).Perpendicular*arc).LengthSquared;
    }
    public static HumanPose BuildPose(BattingFrame f, BattingSettings settings)
    {
        double front = Math.Max(0,f.WeightShift), back = Math.Max(0,-f.WeightShift);
        var key = BattingStroke.Sample(f.SwingProgress,f.Shot);
        double torsoYaw = key.Twist*Math.PI/180;
        double pelvisYaw = key.Twist*Math.PI/180*.55 + front*.60;
        Vector3 frontAnkle = Point(f.FrontFoot.Ankle,f.FrontFoot.AnkleDepth);
        Vector3 rearAnkle = Point(f.RearFoot.Ankle,f.RearFoot.AnkleDepth);
        Vector3 hipOffset = AthleteMath.Turn(new(.105f,0,0),pelvisYaw);
        float hipX=(float)(.24*front-.33*back), hipY=(float)(.93-.45*front+.06*back);
        const float reach = (float)(Thigh+Shin-.008);
        // Keep both leg reach spheres intersecting during step recovery and trigger reversals.
        hipX=Math.Clamp(hipX,Math.Max(frontAnkle.X-hipOffset.X-.77f,rearAnkle.X+hipOffset.X-.77f),
            Math.Min(frontAnkle.X-hipOffset.X+.77f,rearAnkle.X+hipOffset.X+.77f));
        float hipDepth=-.04f;
        hipY=Math.Min(hipY,MaximumHipHeight(frontAnkle,hipX+hipOffset.X,hipDepth+hipOffset.Z));
        hipY=Math.Min(hipY,MaximumHipHeight(rearAnkle,hipX-hipOffset.X,hipDepth-hipOffset.Z));
        Vector3 hip=new(hipX,hipY,hipDepth), frontHip=hip+hipOffset, rearHip=hip-hipOffset;
        float lean=(float)(.035+.13*front-.045*back+key.Lean);
        Vector3 spine=hip+new Vector3(lean*.25f,.17f,0);
        Vector3 chest=spine+new Vector3(lean*.5f,.20f,.025f);
        Vector3 centre=chest+new Vector3(lean*.25f,.12f,.015f);
        Vector3 neck=centre+new Vector3(.01f,.11f,.015f);
        Vector3 shoulderOffset=AthleteMath.Turn(new(.19f,0,0),torsoYaw);
        Vector3 topShoulder=centre+shoulderOffset;
        Vector3 bottomShoulder=centre-shoulderOffset-new Vector3(0,.012f,0);

        double batAngle=double.IsFinite(f.BatAngle)?f.BatAngle:key.BatDegrees*Math.PI/180;
        // Constrain reach along the requested arm ray. Moving the wrist off that ray would
        // silently change its clock angle. Only anatomical two-hand reach constrains extension.
        Vector3 armRay=new((float)Math.Cos(key.ArmRadians),(float)Math.Sin(key.ArmRadians),0);
        Vector3 direction=new(-(float)Math.Cos(batAngle),(float)Math.Sin(batAngle),0);
        Vector3 rayOrigin=new(topShoulder.X,topShoulder.Y,centre.Z+(float)key.Depth);
        Vector3 spacing=direction*(float)settings.Body.GripSpacingMetres;
        float extension=Math.Min(RayReach(rayOrigin,armRay,topShoulder),RayReach(rayOrigin,armRay,bottomShoulder-spacing));
        Vector3 grip=rayOrigin+armRay*Math.Max(.001f,extension);
        Vec2 hands=Project(grip);
        var bat=new BatPose(hands,batAngle);
        Vec2 bottom=hands+bat.Direction*settings.Body.GripSpacingMetres;
        Vector3 topGrip=grip, bottomGrip=grip+spacing;
        Vector3 topGuide=BattingStroke.ElbowGuide(topShoulder,f.SwingProgress,true);
        Vector3 bottomGuide=BattingStroke.ElbowGuide(bottomShoulder,f.SwingProgress,false);
        Vector3 topElbow=AthleteMath.Joint(topShoulder,topGrip,(float)UpperArm,(float)Forearm,topGuide);
        Vector3 bottomElbow=AthleteMath.Joint(bottomShoulder,bottomGrip,(float)UpperArm,(float)Forearm,bottomGuide);
        Vector3 frontPole=frontHip+AthleteMath.Turn(new(0,-.25f,.6f),f.FrontFoot.Yaw);
        Vector3 rearPole=rearHip+AthleteMath.Turn(new(0,-.25f,.6f),f.RearFoot.Yaw);
        Vector3 frontKnee=AthleteMath.Joint(frontHip,frontAnkle,(float)Thigh,(float)Shin,frontPole);
        Vector3 rearKnee=AthleteMath.Joint(rearHip,rearAnkle,(float)Thigh,(float)Shin,rearPole);
        var anatomy=new AthletePose(hip,spine,chest,centre,neck,
            topShoulder,topElbow,topGrip,bottomShoulder,bottomElbow,bottomGrip,
            frontHip,frontKnee,frontAnkle,Point(f.FrontFoot.Toe,f.FrontFoot.Depth),
            rearHip,rearKnee,rearAnkle,Point(f.RearFoot.Toe,f.RearFoot.Depth));
        double headAngle=.03+front*.04;
        return new(Project(hip),Project(topShoulder),Project(neck+new Vector3(.035f,.15f,.015f)),
            Project(topElbow),Project(bottomShoulder),Project(bottomElbow),hands,
            Project(frontKnee),Project(frontAnkle),Project(rearKnee),Project(rearAnkle),bat,
            f.FrontFoot,f.RearFoot,headAngle,f.State,bottom,Project(spine),Project(chest),Project(neck),
            torsoYaw,pelvisYaw,anatomy);

        float MaximumHipHeight(Vector3 ankle,float x,float z) =>
            ankle.Y+MathF.Sqrt(Math.Max(.001f,reach*reach-MathF.Pow(x-ankle.X,2)-MathF.Pow(z-ankle.Z,2)));

    }

    // The simple 2D bowler still uses this planar solver; the batsman uses AthleteMath's 3D solver.
    public static Vec2 SolveJoint(Vec2 root,Vec2 target,double upper,double lower,int bend)
    {
        Vec2 axis=(target-root).Normalized;
        double d=Math.Clamp((target-root).Length,Math.Abs(upper-lower)+1e-6,upper+lower-1e-6);
        double along=(upper*upper-lower*lower+d*d)/(2*d);
        return root+axis*along+axis.Perpendicular*(bend*Math.Sqrt(Math.Max(0,upper*upper-along*along)));
    }

    private static Vector3 Point(Vec2 p,double depth) =>
        new((float)(PitchDimensions.BatterReferenceX-p.X),(float)p.Y,(float)depth);
    private static Vec2 Project(Vector3 p) => new(PitchDimensions.BatterReferenceX-p.X,p.Y);

    private static float RayReach(Vector3 origin,Vector3 direction,Vector3 shoulder)
    {
        Vector3 offset=origin-shoulder;
        float along=Vector3.Dot(offset,direction);
        return -along+MathF.Sqrt(Math.Max(0,along*along+.603f*.603f-offset.LengthSquared()));
    }
    private static double DeadZone(double value, double zone) => double.IsFinite(value) ? Math.Clamp((value-zone)/(1-zone),0,1) : 0;
}
