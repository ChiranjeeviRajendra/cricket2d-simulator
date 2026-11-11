using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

public partial class GameController
{
    private async Task<bool> CheckMainSettings()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var slider = (HSlider)_menu.SettingsPanel.FindChild("BowlingSpeedSlider", true, false);
        bool valid = _menu.ShowingSettings && !_menu.ShowingPractice && _simulation == null &&
            slider.HasFocus() && slider.MinValue == .5 && slider.MaxValue == 1 && slider.Step == .01;
        SmokeButton(JoyButton.DpadRight);
        valid &= Math.Abs(_gameplay.BowlingSpeed - .51) < 1e-9;
        SmokeKey(Key.Right);
        valid &= Math.Abs(_gameplay.BowlingSpeed - .52) < 1e-9;
        slider.Value = .99;
        SmokeKey(Key.Right); SmokeKey(Key.Right);
        valid &= _gameplay.BowlingSpeed == 1;
        slider.Value = .51;
        SmokeButton(JoyButton.DpadLeft); SmokeButton(JoyButton.DpadLeft);
        valid &= _gameplay.BowlingSpeed == .5 && SticksLeaveFocusUnchanged();
        SmokeNextDelivery();
        valid &= _simulation == null && _menu.ShowingSettings;

        float thumb = slider.GetThemeIcon("grabber").GetWidth();
        Vector2 start = slider.GlobalPosition + new Vector2(thumb / 2, slider.Size.Y / 2);
        Vector2 end = slider.GlobalPosition + slider.Size / 2;
        GetViewport().PushInput(new InputEventMouseMotion { Position = start, GlobalPosition = start }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = start, GlobalPosition = start,
            ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseMotion { Position = end, GlobalPosition = end, ButtonMask = MouseButtonMask.Left }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = end, GlobalPosition = end,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
        valid &= Math.Abs(_gameplay.BowlingSpeed - .75) < 1e-9;
        SmokeKey(Key.Escape);
        valid &= !_menu.ShowingSettings && GetViewport().GuiGetFocusOwner()?.Name == "Settings" &&
            UserSettingsStore.Load(_userSettingsPath).BowlingSpeed == .75;
        SmokeButton(JoyButton.A);
        valid &= _menu.ShowingSettings && slider.Value == .75;
        SmokeButton(JoyButton.B);
        SmokeKey(Key.Enter);
        SmokeKey(Key.Tab);
        valid &= GetViewport().GuiGetFocusOwner()?.Name == "SettingsBack";
        SmokeButton(JoyButton.A);
        valid &= !_menu.ShowingSettings && _gameplay.BowlingSpeed == .75;
        GD.Print(valid ? "SMOKE PASS: global settings slider, mouse/keyboard/controller, limits, saved reload and Back." :
            "SMOKE FAIL: global settings or persistence.");
        return valid;
    }
}
