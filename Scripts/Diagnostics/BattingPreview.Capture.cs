using System.Text.Json;
using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

public partial class BattingPreview
{
    /// <summary>Offline anatomical audit and editable Blender motion export. Only --capture-preview invokes it.</summary>
    private void CaptureRigMotion()
    {
        if (!_batter.Available) throw new InvalidOperationException("Motion capture requires a rendered Godot instance.");
        var rig = new BattingRig(Session.Physics, Session.Settings);
        for (int weight = -4; weight <= 4; weight++)
        {
            for (int tick = 0; tick < 360; tick++)
                rig.Step(new(0, 0, 0, 0, Math.Max(0, weight / 4.0), Math.Max(0, -weight / 4.0)), SimulationSettings.Tick, Session.Physics);
            for (int swing = 0; swing <= 40; swing++)
                foreach (ShotStyle shot in Enum.GetValues<ShotStyle>())
                {
                    var frame = rig.Frame with
                    {
                        SwingProgress = swing / 40.0,
                        Shot = shot
                    };
                    _batter.UpdatePose(BattingRig.BuildPose(frame, Session.Settings, Session.Physics), Session.Physics, 210, 1);
                    _batter.ValidateAnatomy();
                }
        }
        var clips = new Dictionary<string, List<float[][]>>();
        foreach (var demo in new[] { BattingDemo.Fast, BattingDemo.FrontFoot, BattingDemo.BackFoot, BattingDemo.Defence, BattingDemo.FullDrive, BattingDemo.Push, BattingDemo.HighFinish })
        {
            var session = new BattingPreviewSession(Session.Physics, Session.Settings) { Loop = false };
            session.Select(demo);
            var frames = new List<float[][]>();
            for (int i = 0; i <= (int)(session.Duration * 60); i++)
            {
                _batter.UpdatePose(session.Rig.Pose, session.Physics, 210, 1);
                _batter.ValidateAnatomy();
                frames.Add(_batter.CaptureBones());
                session.Advance(1.0 / 60);
            }
            clips.Add(demo.ToString(), frames);
        }
        string path = ProjectSettings.GlobalizePath("res://Art/Batsman/reference-motion.json");
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new { RigVersion = 2, Fps = 60, Clips = clips }));
        GD.Print("BATTING 3D ANATOMY PASS: 1,476 poses plus seven motion clips; no stretched limbs.");
    }
}
