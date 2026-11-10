using System.Text.Json;
using Cricket.Physics;
internal static class BattingChecks
{
    private static readonly SimulationSettings Physics = new();
    private const double Dt = 1.0 / 960;
    public static BattingInput AtClock(double clock, double? faceClock = null, double front = 0, double back = 0)
    {
        double a=clock*Math.PI/180, b=(faceClock??0)*Math.PI/180;
        return new(Math.Sin(a),-Math.Cos(a),faceClock.HasValue?Math.Sin(b):0,faceClock.HasValue?-Math.Cos(b):0,front,back);
    }
    public static void Controls()
    {
        foreach(double clock in new[]{0.0,45,90,135,180,225,256,260,265,270,275,280,284,315})
        {
            var rig=new BattingRig(Physics);
            Settle(rig,AtClock(clock));
            Near(0,rig.Frame.SwingProgress,0,"a radial push must never swing");
            Settle(rig,default);
            Settle(rig,AtClock(clock+180));
            Near(0,rig.Frame.SwingProgress,0,"crossing the centre must not swing");
        }
        var swing=new BattingRig(Physics);
        Rotate(swing,1);
        Near(1,swing.Frame.SwingProgress,1e-6,"full deliberate rotation");
        var held=swing.Frame;
        Settle(swing,AtClock(0));
        Check(held==swing.Frame,"held follow-through must not loop");
        Settle(swing,default);
        Near(1,swing.Frame.SwingProgress,1e-6,"release holds the completed pose");
        Settle(swing,AtClock(0));
        Near(1,swing.Frame.SwingProgress,1e-6,"new radial push must re-arm");
        for(int i=0;i<2000;i++) swing.Step(AtClock(i%2==0?3:-3),Dt,Physics);
        Near(1,swing.Frame.SwingProgress,1e-6,"small angular jitter must not accumulate");
        swing.SuspendGesture();
        Settle(swing,AtClock(180));
        Near(1,swing.Frame.SwingProgress,1e-6,"pause/resume must discard the old angular anchor");
    }
    public static void RightStickDisabled()
    {
        var leftOnly=new BattingRig(Physics);
        var both=new BattingRig(Physics);
        for(int i=0;i<2000;i++)
        {
            double clock=255-Math.Min(1,i/900.0)*255;
            var input=AtClock(clock);
            leftOnly.Step(input,Dt,Physics);
            both.Step(input with { RightX=Math.Cos(i*.19),RightY=Math.Sin(i*.23) },Dt,Physics);
            Check(leftOnly.Pose==both.Pose,"right stick must have no batting effect");
        }
    }
    public static void Reengagement()
    {
        var rig=new BattingRig(Physics);
        foreach(double clock in new[]{315.0,300,285,270,255,243,240,180})
            rig.Step(AtClock(clock),SimulationSettings.Tick,Physics);
        Check(rig.GestureEngaged,"entering the start zone must engage");
        Settle(rig,AtClock(180));
        Near(180,rig.SwingClockDegrees,1e-5,"bounded swing reaches its requested angle");
        for(int stroke=0;stroke<12;stroke++)
        {
            Settle(rig,default);
            rig.Step(AtClock(255),SimulationSettings.Tick,Physics);
            var previous=rig.Frame;
            rig.Step(AtClock(243),SimulationSettings.Tick,Physics);
            Check(rig.BeganStroke && rig.MotionFromFrame==previous,"re-arming must retain the actual previous pose");
            Check(rig.BatTipSpeed<=136/3.6+1e-6,"re-arming created an excessive collision speed");
            Settle(rig,AtClock(243));
            Near(243,rig.SwingClockDegrees,1e-5,"repeated re-arm lost a stroke");
        }
        var idle=new Simulation(Physics);
        var ball=idle.Ball;
        foreach(double clock in new[]{255.0,243,210,180}) idle.Tick(AtClock(clock));
        for(int i=0;i<240;i++) idle.Tick(AtClock(180));
        Near(180,idle.Batter.SwingClockDegrees,1e-5,"idle rehearsal reaches the target");
        Check(idle.Phase==DeliveryPhase.Setup && idle.Ball==ball && idle.PhaseTime==0 && idle.TickCount==0,
            "idle rehearsal must not advance a delivery");
    }

    public static void Shots()
    {
        foreach(ShotStyle shot in Enum.GetValues<ShotStyle>())
        {
            var rig=new BattingRig(Physics);
            Settle(rig,new(0,0,0,0,Shot:shot));
            Near(0,rig.Frame.SwingProgress,0,"button alone must not swing");
            double stop=shot==ShotStyle.Defence?60:shot==ShotStyle.Push?45:0;
            rig.Step(AtClock(255) with { Shot=shot },SimulationSettings.Tick,Physics);
            for(int clock=249;clock>=0;clock-=3)
            {
                rig.Step(AtClock(clock) with { Shot=shot },SimulationSettings.Tick,Physics);
                Check(rig.BatTipSpeed<=136/3.6+1e-6,"shot exceeded the bat speed ceiling");
                var ray=rig.Pose.Anatomy.TopGrip-rig.Pose.Anatomy.TopShoulder;
                double armClock=ClockCurve.Wrap(Math.Atan2(ray.X,ray.Y)*180/Math.PI);
                Near(rig.SwingClockDegrees,armClock,1e-4,"arm must match the governed swing angle");
                Validate(rig.Pose,rig.Settings);
            }
            Settle(rig,AtClock(0) with { Shot=shot });
            Near(stop,rig.SwingClockDegrees,1e-5,"mode endpoint");
            if(shot==ShotStyle.Defence) Near(-Math.PI/2,rig.Pose.Bat.Angle,1e-8,"X vertical wall");
            if(shot==ShotStyle.Push) Near(-150*Math.PI/180,rig.Pose.Bat.Angle,1e-8,"B points to 4");
            if(shot==ShotStyle.HighFinish) Check(rig.Pose.Bat.FaceNormal.Y>.999,"Y face upward");
            for(int clock=6;clock<=180;clock+=6)
                rig.Step(AtClock(clock) with { Shot=shot },SimulationSettings.Tick,Physics);
            Settle(rig,AtClock(180) with { Shot=shot });
            Near(75.0/255,rig.Frame.SwingProgress,1e-6,"reverse eventually reaches the held target");
            Near(0,rig.SwingSpeed,1e-9,"held target settles");
        }
        var automatic=new BattingRig(Physics);
        var explicitA=new BattingRig(Physics);
        for(int clock=255;clock>=0;clock-=3)
        {
            automatic.Step(AtClock(clock),SimulationSettings.Tick,Physics);
            explicitA.Step(AtClock(clock) with { Shot=ShotStyle.FullDrive },SimulationSettings.Tick,Physics);
            Check(automatic.Pose==explicitA.Pose,"initial no-button stroke must be A");
        }
    }

    public static void LiveModes()
    {
        var rig=new BattingRig(Physics);
        Rotate(rig,.8);
        foreach(ShotStyle shot in Enum.GetValues<ShotStyle>())
        {
            var before=rig.Frame;
            var fromBat=rig.Pose.Bat;
            rig.Step(AtClock(51) with { Shot=shot },SimulationSettings.Tick,Physics);
            Check(rig.Frame.Shot==shot && rig.MotionFromFrame==before,"mode transition lost the previous pose");
            Check(rig.BatTipSpeed<=136/3.6+1e-6,"mode switch bypassed the speed ceiling");
            var previousBat=fromBat;
            for(int step=1;step<=Physics.Substeps;step++)
            {
                var bat=rig.SampleBatMotion(fromBat,step/(double)Physics.Substeps);
                Check(BattingRig.MaximumBatSpeed(previousBat,bat,Physics.BatLength,SimulationSettings.Tick/Physics.Substeps)
                    <=136/3.6+1e-6,"collision substep bypassed the speed ceiling");
                previousBat=bat;
            }
            Check(previousBat==rig.Pose.Bat,"collision sweep must finish at the rendered bat");
            Settle(rig,AtClock(51) with { Shot=shot });
            Validate(rig.Pose,rig.Settings);
        }
        Settle(rig,AtClock(51));
        var held=rig.Pose;
        Check(rig.Frame.Shot==ShotStyle.HighFinish,"button release must retain the selected mode");
        Settle(rig,default);
        Check(rig.Pose==held,"releasing controls must not restore guard or A");
    }

    public static void HighFinish()
    {
        var a=new BattingRig(Physics);
        var y=new BattingRig(Physics);
        for(int clock=255;clock>=0;clock-=3)
        {
            a.Step(AtClock(clock),SimulationSettings.Tick,Physics);
            y.Step(AtClock(clock) with { Shot=ShotStyle.HighFinish },SimulationSettings.Tick,Physics);
            if(clock>=60) Check(a.Pose==y.Pose,"Y must match A until the arms reach 2 o'clock");
            else
            {
                // Linear clock mapping: arms 2 / 1 / 12 correspond to blade 2 / 2:30 / 3.
                double expectedBladeClock=90-clock/2.0;
                double bladeClock=ClockCurve.Wrap(Math.Atan2(-y.Pose.Bat.Direction.X,y.Pose.Bat.Direction.Y)*180/Math.PI);
                Near(expectedBladeClock,bladeClock,1e-6,"Y blade must turn toward 3 throughout the last arm arc");
                Near(clock,y.SwingClockDegrees,1e-6,"Y blade turn must not delay the arms");
                Validate(y.Pose,y.Settings);
            }
        }
        Check(y.Pose.Bat.FaceNormal.Y>.999,"Y ends with its face toward the sky");
    }
    internal static void Rotate(BattingRig rig,double progress,ShotStyle shot=ShotStyle.FullDrive)
    {
        Settle(rig,default);
        double travel=255*progress;
        for(int i=0;i<=360;i++) rig.Step(AtClock(255-travel*i/360) with { Shot=shot },1.0/360,Physics);
        Settle(rig,AtClock(255-travel) with { Shot=shot });
    }
    public static void WeightTransfer()
    {
        double previousHip=double.PositiveInfinity, previousKnee=double.PositiveInfinity;
        foreach(double amount in new[]{0.0,.25,.5,.75,1})
        {
            var rig=new BattingRig(Physics);Settle(rig,AtClock(120,null,amount));
            Check(rig.Pose.Hip.X<=previousHip && rig.Pose.BackKnee.Y<=previousKnee,"R2 must progressively transfer forward and lower rear knee");
            previousHip=rig.Pose.Hip.X;previousKnee=rig.Pose.BackKnee.Y;
            Validate(rig.Pose,rig.Settings);
        }
        Check(previousKnee<.22 && previousKnee>.05,"full R2 rear knee must approach, not enter, ground");
        previousHip=double.NegativeInfinity;
        foreach(double amount in new[]{0.0,.25,.5,.75,1})
        {
            var rig=new BattingRig(Physics);Settle(rig,AtClock(120,null,0,amount));
            Check(rig.Pose.Hip.X>=previousHip,"L2 must progressively transfer back");
            previousHip=rig.Pose.Hip.X;Validate(rig.Pose,rig.Settings);
        }
        var neutral=new BattingRig(Physics);var both=new BattingRig(Physics);
        var loaded=new BattingRig(Physics);Settle(loaded,new(0,0,0,0,0,1));
        var stance=loaded.Pose.Anatomy;
        Check(Math.Abs(stance.Hip.X-stance.RearAnkle.X)<.10,"back-foot pelvis must sit over the supporting rear ankle");
        Check(Math.Abs(stance.Neck.X-stance.RearAnkle.X)<.11,"back-foot head must follow rear-leg support");
        Check(stance.RearAnkle.X<neutral.Pose.Anatomy.RearAnkle.X-.10,"rear foot must step back before accepting weight");
        Settle(both,new(0,0,0,0,1,1));
        Check(neutral.Frame==both.Frame,"equal triggers must cancel");
        Settle(both,new(0,0,0,0,1,0));Settle(both,default);
        Near(.35,both.Frame.WeightShift,1e-8,"trigger release returns to the 35% baseline");
        Near(neutral.Pose.Hip.X,both.Pose.Hip.X,1e-8,"default stance return");
    }
    public static void Anatomy()
    {
        var rig=new BattingRig(Physics);
        rig.Step(new(double.NaN,0,double.PositiveInfinity,0,double.NaN,double.NegativeInfinity),Dt,Physics);
        Validate(rig.Pose,rig.Settings);
        foreach(ShotStyle shot in Enum.GetValues<ShotStyle>())
        foreach(double weight in new[]{-1.0,-.5,0,.5,1})
        {
            Settle(rig,new(0,0,0,0,Math.Max(0,weight),Math.Max(0,-weight)));
            HumanPose? previous=null;
            for(int i=0;i<=960;i++)
            {
                var pose=BattingRig.BuildPose(rig.Frame with { SwingProgress=i/960.0,Shot=shot,BatAngle=double.NaN },rig.Settings);
                Validate(pose,rig.Settings);
                if(previous.HasValue)
                {
                    // Adjacent authored angles must not flip an IK pole.
                    double movement=Math.Max((pose.Anatomy.TopElbow-previous.Value.Anatomy.TopElbow).Length(),
                        (pose.Anatomy.BottomElbow-previous.Value.Anatomy.BottomElbow).Length());
                    Check(movement<.035,$"{shot} elbow discontinuity {movement:0.000} m at {i/960.0:0.000}, weight {weight}");
                }
                previous=pose;
            }
        }
        // Random pushes/centre crossings cannot bypass the start gate or break joint lengths.
        var rng=new Random(93);
        for(int i=0;i<4000;i++)
        {
            var input=i%3==0?default:new BattingInput(rng.NextDouble()*2-1,rng.NextDouble()*2-1,0,0,
                rng.NextDouble(),rng.NextDouble());
            rig.Step(input,SimulationSettings.Tick,Physics);
            Validate(rig.Pose,rig.Settings);
            Check(rig.BatTipSpeed<=136/3.6+1e-6,"random inputs exceeded maximum bat speed");
        }
    }
    public static void Validate(HumanPose p,BattingSettings settings,SimulationSettings? physics = null)
    {
        physics ??= Physics;
        var a=p.Anatomy;
        Near(BattingRig.UpperArm,(a.TopElbow-a.TopShoulder).Length(),1e-5,"top upper arm");
        Near(BattingRig.Forearm,(a.TopGrip-a.TopElbow).Length(),1e-5,"top forearm");
        Near(BattingRig.UpperArm,(a.BottomElbow-a.BottomShoulder).Length(),1e-5,"bottom upper arm");
        Near(BattingRig.Forearm,(a.BottomGrip-a.BottomElbow).Length(),1e-5,"bottom forearm");
        Near(BattingRig.Thigh,(a.FrontKnee-a.FrontHip).Length(),1e-5,"front thigh");
        Near(BattingRig.Shin,(a.FrontAnkle-a.FrontKnee).Length(),1e-5,"front shin");
        Near(BattingRig.Thigh,(a.RearKnee-a.RearHip).Length(),1e-5,"rear thigh");
        Near(BattingRig.Shin,(a.RearAnkle-a.RearKnee).Length(),1e-5,"rear shin");
        Near(settings.Body.GripSpacingMetres,(p.BottomHand-p.Hands).Length,1e-9,"two distinct grips");
        Check(p.Bat.Grip==p.Hands,"physical bat detached");
        Check(p.FrontKnee.Y>.05 && p.BackKnee.Y>.05,"knee below pitch");
        Check(p.FrontFoot.Toe.Y>=.035-1e-9 && p.RearFoot.Toe.Y>=.035-1e-9,"toe below pitch");
        Check(double.IsFinite(p.Head.X+p.Head.Y+p.Bat.Angle),"nonfinite pose");
    }
    public static void Configuration()
    {
        var changed=BattingSettings.FromJson("{\"Input\":{\"RotationThresholdDegrees\":20}}");
        var rig=new BattingRig(Physics,changed);
        for(int i=0;i<=15;i++)rig.Step(AtClock(255-i),Dt,Physics);
        Near(0,rig.Frame.SwingProgress,0,"configured rotation threshold");
        var sim=new Simulation(Physics,changed);sim.Begin(new(32,16,2.15),out _);
        Check(ReferenceEquals(changed,sim.Batter.Settings),"settings lost on reset");
    }
    public static void TuningEnvelope()
    {
        for(int corner=0;corner<8;corner++)
        {
            var settings=new BattingSettings
            {
                Body=new BodySettings
                {
                    FrontStrideMetres=(corner&1)==0?.15:.65, BackStrideMetres=(corner&1)==0?.05:.14,
                    StepLiftMetres=(corner&2)==0?.025:.09, RearHeelDegrees=(corner&2)==0?35:65,
                    StepSeconds=.18, TransferSeconds=.18, GripSpacingMetres=(corner&4)==0?.08:.12
                }
            };
            var physics=Physics with { BatLength=(corner&4)==0?.85:1 };
            var rig=new BattingRig(physics,settings);
            for(int i=0;i<720;i++)
            {
                var input=AtClock(120+120*Math.Cos(i*.03),i*7,i%240<80?1:0,i%240 is >=80 and <160?1:0) with { Shot=(ShotStyle)(corner%4) };
                rig.Step(input,SimulationSettings.Tick,physics);
                Validate(rig.Pose,settings,physics);
            }
        }
    }
    public static void InvalidConfiguration()
    {
        foreach(string json in new[]{"null","{\"Wrist\":{}}","{\"Body\":null}","{\"Body\":{\"FrontStrideMetres\":2}}",
            "{\"Input\":{\"TriggerDeadZone\":1}}","{\"BottomHand\":{\"MaxAdjustmentDegrees\":180}}",
            "{\"Swing\":{\"MaxSpeedKph\":137}}","{\"Swing\":{\"StartSpeedKph\":80,\"MaxSpeedKph\":70}}",
            "{\"Swing\":{\"AccelerationDegrees\":0}}","{\"Swing\":{\"BrakingDegrees\":0}}",
            "{\"Swing\":{\"RecoveryDegreesPerSecond\":240}}","{\"Body\":{\"DefaultForwardWeight\":1}}"})
        {
            bool rejected=false;
            try{BattingSettings.FromJson(json);}catch(Exception e)when(e is ArgumentException or JsonException){rejected=true;}
            Check(rejected,"invalid or obsolete config accepted");
        }
    }
    public static void DeadZones()
    {
        var rig=new BattingRig(Physics);var initial=rig.Frame;
        for(int i=0;i<2400;i++)
            rig.Step(new(.44*Math.Cos(i*.03),.44*Math.Sin(i*.03),1,1,.01,.01,ShotStyle.FullDrive),Dt,Physics);
        Check(initial==rig.Frame,"slight stick movement or a shot button triggered movement");
        var gesture=new SwingGesture();
        for(int i=0;i<360;i++)gesture.Step(.59*Math.Sin(i*.04),-.59*Math.Cos(i*.04),rig.Settings.Input);
        Near(0,gesture.Target,0,"engagement must require the outer radius");
        gesture.Step(0,-1,rig.Settings.Input);
        gesture.Step(0,1,rig.Settings.Input);
        Near(0,gesture.Target,0,"an angular teleport must not swing");
    }
    public static void PhysicsResponse()
    {
        // Face orientation changes the collision normal; no launch-angle override or artificial impulse.
        double exitY(double angle)
        {
            var direction=Vec2.FromAngle(angle);
            var normal=direction.Perpendicular;
            if(normal.X>0)normal=-normal;
            return Contacts.BatImpulse(new(new(0,1),new(25,0)),normal,Vec2.Zero,
                direction*.1,Physics,out _).Velocity.Y;
        }
        Check(exitY(-Math.PI/2+.3)<exitY(-Math.PI/2-.3),"bat orientation must change launch angle");
    }
    public static void CricketPhysics()
    {
        var s = Physics with { BatFriction = 0 };
        Near(1.17, s.BatMass, 1e-12, "published sample mean bat mass");
        Near(.3512 - 1.17 * .5 * .5, s.BatInertiaAboutCentreOfMass, 1e-12, "parallel-axis conversion from handle end");
        Vec2 normal = new(-1, 0), lever = new(0, -.15), batVelocity = new(-20, 0);
        BallState incoming = new(new(0, 1), new(32, 0));
        BallState outgoing = Contacts.BatImpulse(incoming, normal, batVelocity, lever, s, out double j);
        Vec2 impulse = normal * j;
        Vec2 batAfter = batVelocity - impulse / s.BatMass;
        double angularAfter = -Vec2.Cross(lever, impulse) / s.BatInertiaAboutCentreOfMass;
        Vec2 pBefore = incoming.Velocity * s.BallMass + batVelocity * s.BatMass;
        Vec2 pAfter = outgoing.Velocity * s.BallMass + batAfter * s.BatMass;
        Near(0, (pBefore - pAfter).Length, 1e-10, "linear momentum across an isolated impact");
        Near(Vec2.Cross(lever, incoming.Velocity * s.BallMass),
            Vec2.Cross(lever, outgoing.Velocity * s.BallMass) + s.BatInertiaAboutCentreOfMass * angularAfter,
            1e-10, "angular momentum across an isolated impact");
        double relativeAfter = Vec2.Dot(outgoing.Velocity - batAfter - angularAfter * lever.Perpendicular, normal);
        Near(-s.BatRestitution * Vec2.Dot(incoming.Velocity - batVelocity, normal), relativeAfter, 1e-10, "relative normal restitution");
        double totalBefore = incoming.KineticEnergy(s) + .5 * s.BatMass * batVelocity.LengthSquared;
        double totalAfter = outgoing.KineticEnergy(s) + .5 * s.BatMass * batAfter.LengthSquared +
            .5 * s.BatInertiaAboutCentreOfMass * angularAfter * angularAfter;
        Check(totalAfter < totalBefore, "Finite-mass bat impact created energy.");
        foreach (var (offset, expected) in new[] { (.15, .26), (.30, .09) })
        {
            var blocked = Contacts.BatImpulse(incoming, normal, Vec2.Zero, new(0, -offset), s, out _);
            Near(expected, -blocked.Velocity.X / incoming.Velocity.X, .02, "stationary-bat rebound ratio reference");
        }
        var rng = new Random(211);
        for (int i = 0; i < 1000; i++)
        {
            Vec2 n = Vec2.FromAngle(rng.NextDouble() * Math.Tau);
            Vec2 r = new(0, rng.NextDouble() * .7 - .4);
            BallState before = new(new(0, 1), -n * (1 + rng.NextDouble() * 49) +
                n.Perpendicular * (rng.NextDouble() * 40 - 20), rng.NextDouble() * 800 - 400);
            BallState after = Contacts.BatImpulse(before, n, Vec2.Zero, r, Physics, out _);
            Vec2 transferred = (after.Velocity - before.Velocity) * Physics.BallMass;
            double batSpin = -Vec2.Cross(r, transferred) / Physics.BatInertiaAboutCentreOfMass;
            double energyAfter = after.KineticEnergy(Physics) + .5 * transferred.LengthSquared / Physics.BatMass +
                .5 * Physics.BatInertiaAboutCentreOfMass * batSpin * batSpin;
            Check(energyAfter <= before.KineticEnergy(Physics) + 1e-8,
                "Oblique frictional impact created energy in the isolated ball/bat pair.");
        }
        try { (s with { BatInertiaAboutGrip = .01 }).Validate(); throw new Exception("Impossible inertia accepted."); }
        catch (ArgumentException) { }
    }
    internal static void Settle(BattingRig rig,BattingInput input)
    {for(int i=0;i<2400;i++)rig.Step(input,Dt,Physics);}
    internal static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Near(double expected,double actual,double tolerance,string message)=>
        Check(double.IsFinite(actual)&&Math.Abs(expected-actual)<=tolerance,$"{message}: expected {expected:G8}, got {actual:G8}");
}
