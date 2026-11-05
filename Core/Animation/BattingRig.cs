using System.Numerics;

namespace Cricket.Physics;

/// <summary>Screen stick axes: right/down positive. Triggers are independent normalized [0,1] pressures.</summary>
public readonly record struct BattingInput(double LeftX, double LeftY, double RightX, double RightY,
    double FrontWeight = 0, double BackWeight = 0, ShotStyle Shot = ShotStyle.FullDrive);
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
    private static readonly SimulationSettings DefaultPhysics = new();
    private readonly SimulationSettings _physics;
    private readonly BattingMotion _motion;
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

    public void SuspendGesture() { _gesture.Reset(); _speed=0; }

    public BattingRig(SimulationSettings physics, BattingSettings? settings = null)
    {
        Settings = settings ?? new();
        Settings.Validate(physics);
        _physics = physics;
        _motion = new(Settings.Body);
        Frame = _motion.Step(0, 0, 0, ShotStyle.FullDrive, 0);
        MotionFromFrame=Frame;
        Pose = BuildPose(Frame, Settings, physics);
    }
    public void Step(BattingInput input, double dt, SimulationSettings physics)
    {
        if (!double.IsFinite(dt) || dt <= 0 || dt > .05) throw new ArgumentOutOfRangeException(nameof(dt));
        bool wasEngaged=_gesture.Engaged;
        _gesture.Step(input.LeftX,input.LeftY,Settings.Input);
        BeganStroke=_gesture.Engaged && !wasEngaged;
        ShotStyle shot=Enum.IsDefined(input.Shot)?input.Shot:ShotStyle.FullDrive;
        double maximum=BattingStroke.MaximumProgress(shot);
        // A valid new rotation interrupts recovery. Its velocity comes from adjacent stick samples,
        // not from the unrelated recovery pose; resetting to the new stroke must not create a false hit.
        double from=BeganStroke?Math.Min(_gesture.PreviousTarget,maximum):_swing;
        MotionFromFrame=BeganStroke?Frame with { SwingProgress=from,Shot=shot }:Frame;
        // No animation speed, acceleration, easing or catch-up on an active stroke.
        double next = Math.Min(maximum,_gesture.Engaged?_gesture.Target:
            Math.Max(0,_swing-Settings.Swing.RecoveryDegreesPerSecond/SwingGesture.StartClockDegrees*dt));
        _speed = (next-from)/dt;
        _swing = next;
        double weight = DeadZone(input.FrontWeight, Settings.Input.TriggerDeadZone) - DeadZone(input.BackWeight, Settings.Input.TriggerDeadZone);
        Frame = _motion.Step(weight, _swing, _speed, shot, dt);
        Pose = BuildPose(Frame, Settings, _physics);
    }
    /// <summary>Sample the collision bat across one tick, including a live change of shot mode.</summary>
    public BatPose SampleBatMotion(BatPose from, double fraction)
    {
        double t=Math.Clamp(fraction,0,1);
        if(t==0) return from;
        if(t==1) return Pose.Bat;
        // A mode switch changes blade orientation at the same stick angle. Spread that change
        // across the full tick rather than applying it all in the first collision substep.
        if(MotionFromFrame.Shot!=Frame.Shot) return BatPose.Lerp(from,Pose.Bat,t);
        return BuildPose(BattingFrame.Interpolate(MotionFromFrame,Frame,t),Settings,_physics).Bat;
    }
    public static HumanPose BuildPose(BattingFrame f, BattingSettings settings, SimulationSettings? physics = null)
    {
        physics ??= DefaultPhysics;
        double front = Math.Max(0,f.WeightShift), back = Math.Max(0,-f.WeightShift);
        var key = BattingStroke.Sample(f.SwingProgress,f.Shot);
        double torsoYaw = key.Twist*Math.PI/180;
        double pelvisYaw = key.Twist*Math.PI/180*.55 + front*.60;
        Vector3 frontAnkle = Point(f.FrontFoot.Ankle,f.FrontFoot.AnkleDepth);
        Vector3 rearAnkle = Point(f.RearFoot.Ankle,f.RearFoot.AnkleDepth);
        Vector3 hipOffset = AthleteMath.Turn(new(.105f,0,0),pelvisYaw);
        float hipX=(float)(.17*front-.33*back), hipY=(float)(.93-.39*front-.02*back);
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

        double batAngle=key.BatDegrees*Math.PI/180;
        // Constrain reach along the requested arm ray. Moving the wrist off that ray would
        // silently change its clock angle. Elbows bend where two-hand reach or pitch clearance requires it.
        Vector3 armRay=new((float)Math.Cos(key.ArmRadians),(float)Math.Sin(key.ArmRadians),0);
        Vector3 direction=new(-(float)Math.Cos(batAngle),(float)Math.Sin(batAngle),0);
        Vector3 rayOrigin=new(topShoulder.X,topShoulder.Y,centre.Z+(float)key.Depth);
        Vector3 spacing=direction*(float)settings.Body.GripSpacingMetres;
        float extension=Math.Min(RayReach(rayOrigin,armRay,topShoulder),RayReach(rayOrigin,armRay,bottomShoulder-spacing));
        double minimumY=.035+physics.BatLength*Math.Max(0,-Math.Sin(batAngle));
        if(armRay.Y<-.0001f) extension=Math.Min(extension,(float)((rayOrigin.Y-minimumY)/-armRay.Y));
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
