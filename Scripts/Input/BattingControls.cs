using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Input bindings are named actions; the core never reads hardware directly.</summary>
public static class BattingControls
{
    public static void Register()
    {
        Axis("swing_left", Key.A, JoyAxis.LeftX, -1);
        Axis("swing_right", Key.D, JoyAxis.LeftX, 1);
        Axis("swing_up", Key.W, JoyAxis.LeftY, -1);
        Axis("swing_down", Key.S, JoyAxis.LeftY, 1);
        Axis("bat_left", Key.Left, JoyAxis.RightX, -1);
        Axis("bat_right", Key.Right, JoyAxis.RightX, 1);
        Axis("bat_up", Key.Up, JoyAxis.RightY, -1);
        Axis("bat_down", Key.Down, JoyAxis.RightY, 1);
    }

    public static BattingInput Read()
    {
        // The core applies the configurable radial dead zones exactly once.
        Vector2 left = Godot.Input.GetVector("swing_left", "swing_right", "swing_up", "swing_down", 0);
        Vector2 right = Godot.Input.GetVector("bat_left", "bat_right", "bat_up", "bat_down", 0);
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
        InputMap.AddAction(name, 0);
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(name, new InputEventJoypadMotion { Axis = axis, AxisValue = direction, Device = -1 });
    }
}
