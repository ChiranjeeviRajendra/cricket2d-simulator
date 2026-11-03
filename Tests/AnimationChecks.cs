using System.Text.Json;
using Cricket.Physics;
using Cricket2D.Presentation;

internal static class AnimationChecks
{
    private static readonly SimulationSettings Physics = new();
    private const double Dt = 1.0 / 960;

    public static void Footwork()
    {
        var rig = new BattingRig(Physics);
        var phases = new HashSet<BattingMotionState>();
        bool stepped = false, planted = false, pivoted = false;
        FootPose original = rig.Frame.FrontFoot, rear = rig.Frame.RearFoot;
        for (int i = 0; i < 3000; i++)
        {
            double clock = i < 900 ? 70 : i < 1800 ? 240 : 0;
            BattingFrame previous = rig.Frame;
            rig.Step(BattingChecks.AtClock(clock), Dt, Physics);
            phases.Add(rig.Frame.State);
            CheckFoot(previous.FrontFoot, rig.Frame.FrontFoot);
            Check(rig.Frame.RearFoot.Toe == rear.Toe, "Rear toe slid during pivot.");
            stepped |= rig.Frame.FrontFoot.Contact == FootContact.Airborne;
            planted |= rig.Frame.FrontFoot.Contact == FootContact.Planted && rig.Frame.FrontFoot.Toe.X < original.Toe.X - .049;
            pivoted |= rig.Frame.RearFoot.Angle > .1;
            ValidatePose(rig.Pose);
        }
        for (int i = 0; i < 1500; i++) rig.Step(BattingChecks.AtClock(240), Dt, Physics);
        Check(stepped && planted && pivoted, "Step, plant and heel pivot must all occur.");
        Check(rig.Frame.FrontFoot == original && rig.Frame.RearFoot == rear, "Feet did not recover to guard.");
        Check(phases.Contains(BattingMotionState.Backlift) && phases.Contains(BattingMotionState.ForwardSwing) &&
            phases.Contains(BattingMotionState.FollowThrough) && phases.Contains(BattingMotionState.Recovery), "Missing movement phase.");
        Check(rig.Frame.State == BattingMotionState.Ready, "Recovery did not finish.");
    }

    public static void Interruptions()
    {
        var rig = new BattingRig(Physics);
        FootPose ready = rig.Frame.FrontFoot;
        for (int i = 0; i < 20; i++) rig.Step(BattingChecks.AtClock(90), Dt, Physics);
        Check(rig.Frame.FrontFoot.Contact == FootContact.Airborne, "Early forward step missing.");
        double maxTravel = 0;
        for (int i = 0; i < 900; i++)
        {
            FootPose previous = rig.Frame.FrontFoot;
            rig.Step(BattingChecks.AtClock(240), Dt, Physics);
            CheckFoot(previous, rig.Frame.FrontFoot);
            maxTravel = Math.Max(maxTravel, ready.Toe.X - rig.Frame.FrontFoot.Toe.X);
        }
        Check(maxTravel < rig.Settings.Footwork.StepMetres * .95, "Interrupted step planted the full stroke.");
        Check(rig.Frame.FrontFoot == ready, "Interrupted step failed to return.");
        // Reverse again while the foot is returning, then start a fresh forward stroke.
        for (int i = 0; i < 20; i++) rig.Step(BattingChecks.AtClock(90), Dt, Physics);
        for (int i = 0; i < 45; i++) rig.Step(BattingChecks.AtClock(240), Dt, Physics);
        Check(rig.Frame.FrontFoot.Contact == FootContact.Airborne, "Expected recovery in progress.");
        for (int i = 0; i < 250; i++)
        {
            FootPose previous = rig.Frame.FrontFoot;
            rig.Step(BattingChecks.AtClock(90), Dt, Physics);
            CheckFoot(previous, rig.Frame.FrontFoot);
            ValidatePose(rig.Pose);
        }
        Check(rig.Frame.FrontFoot.Contact == FootContact.Planted, "Consecutive stroke never planted.");
    }

    public static void Independence()
    {
        var neutral = new BattingRig(Physics);
        var wrist = new BattingRig(Physics);
        for (int i = 0; i < 8000; i++)
        {
            double left = ClockCurve.Wrap(240 - i * .35);
            neutral.Step(BattingChecks.AtClock(left), Dt, Physics);
            wrist.Step(BattingChecks.AtClock(left, i * 1.7), Dt, Physics);
            Check(neutral.Frame with { WristAngle = wrist.Frame.WristAngle } == wrist.Frame, "Wrist changed body, head, footwork or shoulder.");
            Check(neutral.ShoulderSpeed == wrist.ShoulderSpeed && neutral.IsSnapping == wrist.IsSnapping, "Wrist changed swing dynamics.");
        }
    }

    public static void WeightAndHead()
    {
        var animated = new BattingRig(Physics);
        var comparison = new BattingRig(Physics, new()
        {
            BodyMotion = new() { HipTransferMetres = 0, AdditionalCrouchMetres = 0 },
            HeadMotion = new() { CompensationDegrees = 0 }
        });
        Vec2 initialHead = animated.Pose.Head, comparisonHead = comparison.Pose.Head;
        double extraSpeed = 0;
        for (int i = 0; i < 1600; i++)
        {
            HumanPose before = animated.Pose, beforeComparison = comparison.Pose;
            animated.Step(BattingChecks.AtClock(90), Dt, Physics);
            comparison.Step(BattingChecks.AtClock(90), Dt, Physics);
            Check(animated.ShoulderAngle == comparison.ShoulderAngle, "Supporting motion altered shoulder control.");
            Vec2 gripShift = animated.Pose.Bat.Grip - comparison.Pose.Bat.Grip;
            Vec2 shoulderShift = animated.Pose.Shoulder - comparison.Pose.Shoulder;
            Check((gripShift - shoulderShift).Length < 1e-10, "Physical grip did not inherit shoulder translation.");
            Vec2 extraVelocity = (animated.Pose.Bat.Grip - before.Bat.Grip -
                (comparison.Pose.Bat.Grip - beforeComparison.Bat.Grip)) / Dt;
            extraSpeed = Math.Max(extraSpeed, extraVelocity.Length);
        }
        Check(extraSpeed > .05, "Weight transfer did not contribute to physical bat velocity.");
        Check(Math.Abs(animated.Pose.Head.X - initialHead.X) < Math.Abs(comparison.Pose.Head.X - comparisonHead.X),
            "Head compensation amplified torso movement.");
        Check(animated.Pose.HeadAngle < 0, "Head should counter the forward lean.");
    }

    public static void Interpolation()
    {
        var rig = new BattingRig(Physics);
        bool snap = false;
        for (int tick = 0; tick < 1000; tick++)
        {
            BattingFrame previous = rig.Frame;
            for (int sub = 0; sub < 8; sub++)
                rig.Step(BattingChecks.AtClock(ClockCurve.Wrap(240 - (tick * 8 + sub) * .4), tick * 5), Dt, Physics);
            snap |= rig.IsSnapping;
            Check(Math.Abs(previous.ShoulderAngle - rig.Frame.ShoulderAngle) < .3, "Unwrapped shoulder jumped at snap boundary.");
            for (int sample = 0; sample <= 12; sample++)
            {
                BattingFrame frame = BattingFrame.Interpolate(previous, rig.Frame, sample / 12.0);
                ValidatePose(BattingRig.BuildPose(frame, rig.Settings));
            }
        }
        Check(snap, "Interpolation run did not exercise a snap.");
        var sim = new Simulation(Physics);
        Check(sim.Begin(new(32, 16, 2.15), out _), "Delivery did not begin.");
        for (int i = 0; i < 45; i++) sim.Tick(BattingChecks.AtClock(0));
        sim.ReturnToSetup();
        Check(sim.PreviousFrame == sim.Batter.Frame, "Setup kept stale render history.");
        sim.Begin(new(32, 16, 2.15), out _);
        Check(sim.PreviousFrame == sim.Batter.Frame && sim.PreviousBall == sim.Ball, "Delivery reset interpolates from the previous stroke.");
    }

    public static void Playback()
    {
        foreach (double speed in new[] { .25, .5, 1 })
        {
            List<HumanPose>? expected = null;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                var session = new BattingPreviewSession(Physics, new()) { PlaybackSpeed = speed };
                session.Select(BattingDemo.Fast);
                var samples = new List<HumanPose>();
                for (int frame = 1; frame <= fps * 12; frame++)
                {
                    session.Advance(1.0 / fps);
                    ValidatePose(session.RenderPose);
                    if (frame % (fps / 6) == 0) samples.Add(session.RenderPose);
                }
                if (expected != null)
                    for (int i = 0; i < samples.Count; i++)
                    {
                        Check((expected[i].Hands - samples[i].Hands).Length < 1e-8 &&
                            Math.Abs(expected[i].Bat.Angle - samples[i].Bat.Angle) < 1e-8, "Rendering differs across 30/60/144 FPS.");
                    }
                expected = samples;
                session.Paused = true;
                var frozen = session.RenderFrame;
                long ticks = session.TickCount;
                session.Advance(1);
                Check(session.RenderFrame == frozen && session.TickCount == ticks, "Paused preview moved.");
                session.SingleStep();
                Check(session.TickCount == ticks + 1 && session.RenderFrame == session.Rig.Frame, "Single step did not show exactly one tick.");
                session.Paused = false;
                session.Advance(SimulationSettings.Tick / speed);
                Check(session.TickCount == ticks + 2, "Resume retained stale time.");
                session.Reset();
                Check(session.RenderFrame == session.Rig.Frame && session.Time == 0, "Reset kept stale snapshots.");
            }
        }
        foreach (BattingDemo demo in Enum.GetValues<BattingDemo>().Where(d => d != BattingDemo.Live))
        {
            var session = new BattingPreviewSession(Physics, new()) { Loop = false };
            session.Select(demo);
            session.Advance(session.Duration);
            Check(session.Paused && Math.Abs(session.Time - session.Duration) < 1e-9, $"{demo} did not finish.");
            Check(session.Rig.Frame.State == BattingMotionState.Ready && session.Rig.Frame.FrontFoot.Contact == FootContact.Planted,
                $"{demo} did not recover.");
        }
    }

    public static void Reload()
    {
        var session = new BattingPreviewSession(Physics, new());
        session.Advance(.2, BattingChecks.AtClock(90));
        var rig = session.Rig;
        var frame = session.RenderFrame;
        foreach (string json in new[] { "{", "{\"Footwork\":null}", "{\"Footwork\":{\"StepMetres\":0.15}}",
            "{\"BodyMotion\":{\"AdditionalCrouchMetres\":0.05}}", "{\"HeadMotion\":{\"SettlingSeconds\":0}}" })
        {
            Check(!session.TryReload(json, out string error) && error.Length > 0, "Unsafe animation configuration accepted.");
            Check(ReferenceEquals(session.Rig, rig) && session.RenderFrame == frame, "Failed reload changed the working preview.");
        }
        var changed = new BattingSettings { Footwork = new() { StepMetres = .04 } };
        Check(session.TryReload(JsonSerializer.Serialize(changed), out _) && session.Time == 0 && session.Settings.Footwork.StepMetres == .04,
            "Valid reload did not reset with the new configuration.");
        Check(session.TryReload("{}", out _) && session.Settings.Footwork.StepMetres == .05, "Omitted objects did not receive defaults.");
    }

    private static void CheckFoot(FootPose previous, FootPose current)
    {
        if (previous.Contact == FootContact.Planted && current.Contact == FootContact.Planted)
            Check(previous == current, "Planted foot slid.");
        Check((previous.Toe - current.Toe).Length < .003, "Foot teleported during a transition.");
    }

    private static void ValidatePose(HumanPose p)
    {
        Bone(p.Shoulder, p.LeadElbow, BattingRig.UpperArm);
        Bone(p.LeadElbow, p.Hands, BattingRig.Forearm);
        Bone(p.RearShoulder, p.RearElbow, BattingRig.UpperArm);
        Bone(p.RearElbow, p.Hands, BattingRig.Forearm);
        Bone(p.Hip, p.FrontKnee, BattingRig.Thigh);
        Bone(p.FrontKnee, p.FrontAnkle, BattingRig.Shin);
        Bone(p.Hip, p.BackKnee, BattingRig.Thigh);
        Bone(p.BackKnee, p.BackAnkle, BattingRig.Shin);
        Check(p.Bat.Grip == p.Hands && p.FrontAnkle == p.FrontFoot.Ankle && p.BackAnkle == p.RearFoot.Ankle, "Shared pivot detached.");
        Capsule blade = p.Bat.Blade(Physics);
        Check(Math.Min(blade.A.Y, blade.B.Y) - Physics.BatHalfThickness >= .005 - 1e-9, "Bat clipped the pitch.");
        Check(p.FrontFoot.Toe.Y >= .0375 - 1e-9 && p.RearFoot.Toe.Y >= .0375 - 1e-9 &&
            p.FrontFoot.Heel.Y >= .0375 - 1e-9 && p.RearFoot.Heel.Y >= .0375 - 1e-9, "Shoe clipped the pitch.");
        Check(Math.Abs(p.HeadAngle) <= 6 * Math.PI / 180 + 1e-9, "Head exceeded compensation bound.");
        static void Bone(Vec2 a, Vec2 b, double length) => Check(Math.Abs((a - b).Length - length) < 1e-8, "Bone length changed.");
    }

    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
