using System.Numerics;

namespace Cricket.Physics;

internal readonly record struct StrokePose(double ArmRadians, double BatDegrees, double Depth, double Lean, double Twist);

/// <summary>Clock-space strokes from 8:30. Progress is angular travel, never elapsed animation time.</summary>
internal static class BattingStroke
{
    public static double MaximumProgress(ShotStyle style) => style switch
    { ShotStyle.Defence => (SwingGesture.StartClockDegrees-60)/SwingGesture.StartClockDegrees,
        ShotStyle.Push => (SwingGesture.StartClockDegrees-45)/SwingGesture.StartClockDegrees, _ => 1 };

    public static StrokePose Sample(double progress, ShotStyle style)
    {
        // The motor enforces each mode's endpoint. A live mode change must not teleport the arms.
        double travel=Math.Clamp(progress,0,1)*SwingGesture.StartClockDegrees;
        double armDegrees=90-SwingGesture.StartClockDegrees+travel;
        double faceTurn=Smooth(travel/(SwingGesture.StartClockDegrees-90));
        double bladeDegrees=style switch
        {
            ShotStyle.Defence => -180+90*faceTurn,
            ShotStyle.Push => -180+150*faceTurn,
            // Follow A through 2 o'clock (30 degrees above horizontal), then turn the
            // blade clockwise to 3 while the arm continues counterclockwise to 12.
            ShotStyle.HighFinish => armDegrees<=30?armDegrees:(90-armDegrees)*.5,
            _ => armDegrees
        };
        return new(armDegrees*Math.PI/180,-180-bladeDegrees,.10+.12*(1-faceTurn),0,-20+130*faceTurn);
    }

    private static double Smooth(double value) { double t=Math.Clamp(value,0,1); return t*t*(3-2*t); }

    public static Vector3 ElbowGuide(Vector3 shoulder, double progress, bool top)
    {
        float raised=(float)Smooth((progress-.65)/.35);
        return shoulder+new Vector3(top?.5f:-.5f,-.20f+.5f*raised,top?.55f:.70f);
    }
}
