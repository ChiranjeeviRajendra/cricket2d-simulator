using Cricket.Physics;
using Cricket2D.Presentation;
internal static class AnimationChecks
{
    public static void Footwork()
    {
        var physics=new SimulationSettings();var rig=new BattingRig(physics);
        for(int i=0;i<7200;i++)
        {
            var before=rig.Frame;
            var input=BattingChecks.AtClock(120,null,i<2400?1:0,i is >=2400 and <4800?1:0);
            rig.Step(input,1.0/960,physics);
            var after=rig.Frame;
            CheckFoot(before.FrontFoot,after.FrontFoot);CheckFoot(before.RearFoot,after.RearFoot);
            BattingChecks.Check(!(after.FrontFoot.Contact==FootContact.Airborne&&after.RearFoot.Contact==FootContact.Airborne),"both feet airborne");
            BattingChecks.Validate(rig.Pose,rig.Settings);
        }
        static void CheckFoot(FootPose a,FootPose b)
        {
            if(a.Contact!=FootContact.Airborne&&b.Contact!=FootContact.Airborne)
                BattingChecks.Check(a.Toe==b.Toe,"planted toe slid");
            BattingChecks.Check((a.Toe-b.Toe).Length<.004,"foot teleported");
        }
    }
    public static void Interpolation()
    {
        var physics=new SimulationSettings();var rig=new BattingRig(physics);
        for(int tick=0;tick<1200;tick++)
        {
            var previous=rig.Frame;
            for(int j=0;j<8;j++)rig.Step(BattingChecks.AtClock(120+120*Math.Cos(tick*.014),tick*3,
                (Math.Sin(tick*.02)+1)/2,(Math.Cos(tick*.017)+1)/2),1.0/960,physics);
            for(int n=0;n<=4;n++)
                BattingChecks.Validate(BattingRig.BuildPose(BattingFrame.Interpolate(previous,rig.Frame,n/4.0),rig.Settings),rig.Settings);
        }
    }
    public static void Playback()
    {
        foreach(double speed in new[]{.25,.5,1})
        {
            HumanPose? expected=null;
            foreach(int fps in new[]{30,60,144})
            {
                var session=new BattingPreviewSession(new(),new()){PlaybackSpeed=speed,Loop=false};
                session.Select(BattingDemo.FrontFoot);
                for(int i=0;i<fps;i++)session.Advance(1.0/fps);
                if(expected.HasValue)BattingChecks.Check((expected.Value.Hands-session.RenderPose.Hands).Length<1e-8,"render-rate-dependent preview");
                expected=session.RenderPose;
                session.Paused=true;var frozen=session.RenderFrame;session.Advance(1);
                BattingChecks.Check(frozen==session.RenderFrame,"paused preview moved");
                long ticks=session.TickCount;session.SingleStep();
                BattingChecks.Check(session.TickCount==ticks+1,"single step");
            }
        }
    }
    public static void Reload()
    {
        var session=new BattingPreviewSession(new(),new());session.Advance(.2,BattingChecks.AtClock(90));
        var original=session.Rig;
        BattingChecks.Check(!session.TryReload("{\"Body\":null}",out _)&&ReferenceEquals(original,session.Rig),"failed reload changed state");
        BattingChecks.Check(session.TryReload("{}",out _)&&!ReferenceEquals(original,session.Rig)&&session.Time==0,"valid reload failed");
    }
}
