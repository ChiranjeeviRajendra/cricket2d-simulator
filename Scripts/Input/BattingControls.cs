using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Input bindings are named actions; the core never reads hardware directly.</summary>
public static class BattingControls
{
    public static void Register()
    {
        Axis("swing_back", Key.A, JoyAxis.LeftX, -1);
        Axis("swing_forward", Key.D, JoyAxis.LeftX, 1);
        Axis("stance_up", Key.W, JoyAxis.LeftY, -1);
        Axis("stance_down", Key.S, JoyAxis.LeftY, 1);
        // Reverse only the controller elbow axis; keyboard, swing and wrist stay unchanged.
        Axis("elbow_extend", Key.Left, JoyAxis.RightX, 1);
        Axis("elbow_flex", Key.Right, JoyAxis.RightX, -1);
        Axis("wrist_up", Key.Up, JoyAxis.RightY, -1);
        Axis("wrist_down", Key.Down, JoyAxis.RightY, 1);
    }

    public static BattingInput Read()
    {
        Vector2 left = Godot.Input.GetVector("swing_back", "swing_forward", "stance_up", "stance_down", 0.18f);
        Vector2 right = Godot.Input.GetVector("elbow_extend", "elbow_flex", "wrist_up", "wrist_down", 0.18f);
        return new(left.X, left.Y, right.X, right.Y);
    }

    public static string DeviceName()
    {
        var devices = Godot.Input.GetConnectedJoypads();
        return devices.Count > 0 ? Godot.Input.GetJoyName(devices[0]) : "Keyboard ready  ·  connect a controller anytime";
    }

    private static void Axis(string name, Key key, JoyAxis axis, float direction)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name, 0.18f);
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(name, new InputEventJoypadMotion { Axis = axis, AxisValue = direction, Device = -1 });
    }
}
