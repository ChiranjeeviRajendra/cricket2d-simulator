using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Named input actions; right-stick axes are deliberately not registered for batting.</summary>
public static class BattingControls
{
    private static readonly StringName SwingLeft="swing_left", SwingRight="swing_right",
        SwingUp="swing_up", SwingDown="swing_down", FrontWeight="front_weight", BackWeight="back_weight",
        Defence="shot_defence", Push="shot_push", HighFinish="shot_high_finish", FullDrive="shot_full_drive";
    public static void Register()
    {
        // Godot's built-in UI actions include analog axes, including native popups.
        // Keep sticks available to batting while UI navigation uses the D-pad only.
        foreach (var action in InputMap.GetActions())
        {
            if (!action.ToString().StartsWith("ui_", StringComparison.Ordinal)) continue;
            foreach (var input in InputMap.ActionGetEvents(action))
                if (input is InputEventJoypadMotion) InputMap.ActionEraseEvent(action, input);
        }
        Axis("swing_left",Key.A,JoyAxis.LeftX,-1);
        Axis("swing_right",Key.D,JoyAxis.LeftX,1);
        Axis("swing_up",Key.W,JoyAxis.LeftY,-1);
        Axis("swing_down",Key.S,JoyAxis.LeftY,1);
        Axis("front_weight",Key.E,JoyAxis.TriggerRight,1);
        Axis("back_weight",Key.Q,JoyAxis.TriggerLeft,1);
        Button("shot_defence",Key.J,JoyButton.X);
        Button("shot_full_drive",Key.K,JoyButton.A);
        Button("shot_push",Key.L,JoyButton.B);
        Button("shot_high_finish",Key.I,JoyButton.Y);
    }

    public static BattingInput Read()
    {
        Vector2 left=Godot.Input.GetVector(SwingLeft,SwingRight,SwingUp,SwingDown,0);
        // A deterministic priority resolves accidental simultaneous button presses.
        ShotStyle? shot=Godot.Input.IsActionPressed(Defence)?ShotStyle.Defence:
            Godot.Input.IsActionPressed(Push)?ShotStyle.Push:
            Godot.Input.IsActionPressed(HighFinish)?ShotStyle.HighFinish:
            Godot.Input.IsActionPressed(FullDrive)?ShotStyle.FullDrive:null;
        return new(left.X,left.Y,0,0,Godot.Input.GetActionRawStrength(FrontWeight),
            Godot.Input.GetActionRawStrength(BackWeight),shot);
    }

    public static bool IsPause(InputEvent evt) =>
        evt is InputEventJoypadButton { Pressed:true, ButtonIndex:JoyButton.Start } ||
        evt is InputEventKey { Pressed:true, Echo:false, Keycode:Key.Space };

    public static bool IsNextDelivery(InputEvent evt) =>
        evt is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.DpadUp } ||
        evt is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Up };

    private static void Axis(string name,Key key,JoyAxis axis,float direction)
    {
        if(InputMap.HasAction(name))return;
        InputMap.AddAction(name,0);
        InputMap.ActionAddEvent(name,new InputEventKey { PhysicalKeycode=key });
        InputMap.ActionAddEvent(name,new InputEventJoypadMotion { Axis=axis,AxisValue=direction,Device=-1 });
    }
    private static void Button(string name,Key key,JoyButton button)
    {
        if(InputMap.HasAction(name))return;
        InputMap.AddAction(name);
        InputMap.ActionAddEvent(name,new InputEventKey { PhysicalKeycode=key });
        InputMap.ActionAddEvent(name,new InputEventJoypadButton { ButtonIndex=button,Device=-1 });
    }
}
