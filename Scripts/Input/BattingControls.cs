using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Input bindings are named actions; the core never reads hardware directly.</summary>
public static class BattingControls
{
    private static readonly StringName SwingLeft = "swing_left", SwingRight = "swing_right",
        SwingUp = "swing_up", SwingDown = "swing_down", BatLeft = "bat_left", BatRight = "bat_right",
        BatUp = "bat_up", BatDown = "bat_down";
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
        Vector2 left = Godot.Input.GetVector(SwingLeft, SwingRight, SwingUp, SwingDown, 0);
        Vector2 right = Godot.Input.GetVector(BatLeft, BatRight, BatUp, BatDown, 0);
        return new(left.X, left.Y, right.X, right.Y);
    }

    private static void Axis(string name, Key key, JoyAxis axis, float direction)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name, 0);
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(name, new InputEventJoypadMotion { Axis = axis, AxisValue = direction, Device = -1 });
    }
}
