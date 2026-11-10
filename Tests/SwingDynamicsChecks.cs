using Cricket.Physics;

internal static class SwingDynamicsChecks
{
    private static readonly SimulationSettings Physics = new();
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    public static void SpeedAndBrake()
    {
        foreach (double dt in new[] { 1.0/960, SimulationSettings.Tick })
        foreach (bool releaseButton in new[] { false, true })
        {
            var rig = new BattingRig(Physics);
            double first = 0, peak = 0, speedAt25 = 0;
            for (int i=0; i<1000; i++)
            {
                double clock = Math.Max(0,255-i*40);
                rig.Step(BattingChecks.AtClock(clock) with { Shot = ShotStyle.FullDrive },dt,Physics);
                double speed = rig.BatTipSpeed*3.6, travel = 255-rig.SwingClockDegrees;
                if (first==0 && speed>0) first=speed;
                if (travel>=25 && speedAt25==0) speedAt25=speed;
                peak=Math.Max(peak,speed);
                Check(speed<=136.00001,"Bat exceeded 136 km/h during acceleration.");
                if (travel>=85) break;
            }
            Console.WriteLine($"      swing at {1/dt:0} Hz: first {first:0.00}, at 25 degrees {speedAt25:0.00}, peak {peak:0.00} km/h");
            // BatTipSpeed measures the whole tick's sweep. At 120 Hz the tick crossing 25°
            // includes earlier acceleration; its mean is lower than the endpoint speed.
            Check(first is >=49 and <85 && peak>133 && speedAt25>(dt<.002?133:120),
                "Swing did not progressively reach its speed ceiling near 25 degrees.");
            double release=255-rig.SwingClockDegrees;
            var input=releaseButton?BattingChecks.AtClock(0):new BattingInput(0,0,0,0,Shot:ShotStyle.FullDrive);
            double last=double.PositiveInfinity;
            for (int i=0;i<240;i++)
            {
                rig.Step(input,dt,Physics);
                Check(rig.BatTipSpeed<=136/3.6+1e-6,"Release exceeded the speed ceiling.");
                // Geometry may slightly change the tip metric; angular motion must keep braking.
                double speed=Math.Abs(rig.SwingSpeed);
                Check(speed<=last+.02,"Release accelerated instead of braking.");
                last=speed;
            }
            double stopping=255-rig.SwingClockDegrees-release;
            Console.WriteLine($"      {(releaseButton?"button":"stick")} release stopping arc: {stopping:0.000} degrees");
            Check(stopping is >0 and <=10.001 && rig.SwingSpeed==0,"Release did not stop within ten degrees.");
            var held=rig.Pose;
            for(int i=0;i<240;i++) rig.Step(input,dt,Physics);
            Check(rig.Pose==held && rig.Frame.Shot==ShotStyle.FullDrive,"Released pose drifted or changed mode.");
        }
    }

    public static void StanceAndGround()
    {
        var neutral=new BattingRig(Physics);
        Check(neutral.Frame.WeightShift==.35,"Default forward weight must be 35%.");
        var front=new BattingRig(Physics); BattingChecks.Settle(front,new(0,0,0,0,1,0));
        var back=new BattingRig(Physics); BattingChecks.Settle(back,new(0,0,0,0,0,1));
        Check(back.Pose.Head.Y>neutral.Pose.Head.Y && neutral.Pose.Head.Y>front.Pose.Head.Y,"Full L2 must be the tallest stance.");
        Check(front.Pose.BackAnkle.X-front.Pose.FrontAnkle.X>1.0,"Front-foot stance is too narrow.");
        Console.WriteLine($"      head heights neutral/front/back: {neutral.Pose.Head.Y:0.000}/{front.Pose.Head.Y:0.000}/{back.Pose.Head.Y:0.000} m; front-stance ankle spacing {front.Pose.BackAnkle.X-front.Pose.FrontAnkle.X:0.000} m");
        // Compare arm reach with an extended downward blade: the pitch must not retract the wrists.
        var frame=front.Frame with { SwingProgress=75.0/255, Shot=ShotStyle.FullDrive, BatAngle=double.NaN };
        var pose=BattingRig.BuildPose(frame,front.Settings);
        BattingChecks.Validate(pose,front.Settings);
        var blade=pose.Bat.Blade(Physics);
        Check(Math.Min(blade.A.Y,blade.B.Y)-blade.Radius<-.10,"The bat is still constrained above the pitch.");
    }
}
