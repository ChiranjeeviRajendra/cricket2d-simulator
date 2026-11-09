using Cricket.Physics;
using Godot;

namespace Cricket2D;

public partial class GameController
{
    private async Task<bool> WaitForDelivery()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (_practice!.IsPreparing && watch.Elapsed.TotalSeconds < 10)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return _hud.DeliveryPanel.CanBowl;
    }

    private async Task<bool> CheckDeliverySliders()
    {
        var panel = _hud.DeliveryPanel;
        T Find<T>(string name) where T : Node => (T)panel.FindChild(name, true, false);
        var bowler = Find<OptionButton>("BowlerSelector");
        var randomLength = Find<CheckBox>("RandomPitchLength");
        var randomSpeed = Find<CheckBox>("RandomReleaseSpeed");
        var length = Find<PitchLengthSlider>("PitchLengthSlider");
        var speed = Find<HSlider>("ReleaseSpeedSlider");
        var height = Find<HSlider>("ReleaseHeightSlider");
        var pitchGuide = Find<CheckBox>("ShowPitchPosition");
        var trajectoryGuide = Find<CheckBox>("ShowPredictedTrajectory");
        var scroll = Find<ScrollContainer>("SettingsScroll");
        var bowl = Find<Button>("Bowl");
        bool valid = panel.Settings == new PracticeBowlingSettings() && bowler.Selected == (int)BowlerType.Medium &&
            !length.IsVisibleInTree() && !speed.IsVisibleInTree() && height.IsVisibleInTree() && bowl.HasFocus();
        valid &= await WaitForDelivery();
        valid &= bowl.HasFocus() && bowl.GetGlobalRect().End.Y <= scroll.GlobalPosition.Y;
        valid &= SticksLeaveFocusUnchanged();
        SmokeButton(JoyButton.DpadDown); valid &= bowler.HasFocus();
        SmokeKey(Key.Up); valid &= bowl.HasFocus();
        var pending = _practice!.PendingPlan;
        int seed = _practice.Seed;

        // A/Enter edit controls; neither checkbox activation nor guides may bowl or reroll.
        pitchGuide.GrabFocus(); SmokeButton(JoyButton.A);
        trajectoryGuide.GrabFocus(); SmokeKey(Key.Enter);
        valid &= !_ground.Guides.ShowPitchPosition && !_ground.Guides.ShowPredictedTrajectory && _ground.ShowBallTrail &&
            ReferenceEquals(pending, _practice.PendingPlan) && _practice.Seed == seed && _simulation.Phase == DeliveryPhase.Setup;
        pitchGuide.ButtonPressed = true; trajectoryGuide.ButtonPressed = true;
        panel.Hide(); SmokeKey(Key.T);
        valid &= !_ground.ShowBallTrail && _ground.Guides.ShowPredictedTrajectory && _ground.Guides.ShowPitchPosition;
        SmokeKey(Key.T); panel.Open();
        bowler.GrabFocus(); SmokeButton(JoyButton.A);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        valid &= bowler.GetPopup().Visible && panel.Visible;
        SmokeKey(Key.Escape);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        valid &= !bowler.GetPopup().Visible && !_menu.Visible;
        bowler.GrabFocus(); SmokeKey(Key.Enter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SmokeKey(Key.Down); SmokeKey(Key.Enter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        valid &= panel.Settings.Bowler == BowlerType.Fast && !bowler.GetPopup().Visible;

        // The native popup must also select and dismiss through controller input.
        bowler.GrabFocus(); SmokeButton(JoyButton.A);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SmokeButton(JoyButton.DpadUp); SmokeButton(JoyButton.A);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (panel.Settings.Bowler != BowlerType.Medium || bowler.GetPopup().Visible)
        {
            GD.PushError($"Controller dropdown selection failed: bowler={panel.Settings.Bowler}, open={bowler.GetPopup().Visible}.");
            return false;
        }
        SmokeButton(JoyButton.A);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!bowler.GetPopup().Visible) { GD.PushError("Controller could not reopen the bowler dropdown."); return false; }
        foreach (float value in new[] { 1f, 0f })
        {
            Godot.Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = JoyAxis.LeftY, AxisValue = value });
            Godot.Input.FlushBufferedEvents();
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (bowler.GetPopup().GetFocusedItem() != (int)BowlerType.Medium)
        { GD.PushError("Left stick moved the bowler dropdown item."); return false; }
        foreach (var axis in new[] { JoyAxis.LeftX, JoyAxis.RightX, JoyAxis.RightY })
            SmokeAxis(axis);
        valid &= bowler.GetPopup().GetFocusedItem() == (int)BowlerType.Medium;
        SmokeButton(JoyButton.B);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (bowler.GetPopup().Visible || _menu.Visible || !panel.Visible)
        { GD.PushError("Controller B did not close only the bowler dropdown."); return false; }

        randomLength.GrabFocus(); SmokeButton(JoyButton.A);
        randomSpeed.GrabFocus(); SmokeKey(Key.Space);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        valid &= length.IsVisibleInTree() && speed.IsVisibleInTree() && !panel.Settings.RandomPitchLength && !panel.Settings.RandomSpeed;
        speed.GrabFocus();
        double originalSpeed = speed.Value;
        valid &= SticksLeaveFocusUnchanged() && speed.Value == originalSpeed;
        DeliverySettings? lastChange = null;
        void Changed(PracticeBowlingSettings values) => lastChange = values.ManualDelivery;
        panel.SettingsChanged += Changed;

        foreach (double value in new[] { 8.0, 20.12, 25.0 })
        {
            await Reveal(length);
            Drag(length, new(length.XForMetres(length.Value), 72), new(length.XForMetres(value), 72));
            valid &= Math.Abs(length.Value - value) < .011 && lastChange == panel.Values;
        }
        foreach (double value in new[] { 18.0, 49.9, 50, 80, 80.1, 110, 110.1, 130, 130.1, 180 })
        {
            speed.Value = value;
            valid &= panel.Settings.Bowler == BowlerProfiles.Classify(value) &&
                Math.Abs(panel.Values.Speed - SpeedUnits.ToMetresPerSecond(value)) < 1e-8 &&
                Find<Label>("ReleaseSpeedSliderValue").Text == $"{value:0.0} km/h";
        }
        await Reveal(speed);
        DragSlider(speed, 95);
        valid &= Math.Abs(speed.Value - 95) < .11 && panel.Settings.Bowler == BowlerType.Medium;
        bowler.GrabFocus(); SmokeButton(JoyButton.DpadRight);
        valid &= panel.Settings.Bowler == BowlerType.Fast && Math.Abs(speed.Value - 110.1) < 1e-8;
        foreach (double value in new[] { 1.53, 2.16 })
        {
            await Reveal(height); DragSlider(height, value);
            valid &= Math.Abs(height.Value - value) < .011 && panel.Values.ReleaseHeight == height.Value;
        }
        // Visible controls form one focus cycle; hidden sliders are removed immediately.
        randomLength.GrabFocus(); SmokeButton(JoyButton.DpadDown);
        valid &= length.HasFocus();
        length.Value = 16; SmokeKey(Key.Left);
        valid &= Math.Abs(length.Value - 16.1) < 1e-8;
        SmokeKey(Key.Right); SmokeKey(Key.Tab);
        valid &= randomSpeed.HasFocus();
        SmokeButton(JoyButton.DpadDown); valid &= speed.HasFocus();
        SmokeKey(Key.Down); valid &= height.HasFocus();
        randomLength.ButtonPressed = true;
        randomLength.GrabFocus(); SmokeKey(Key.Down); valid &= randomSpeed.HasFocus() && length.FocusMode == Control.FocusModeEnum.None;
        randomSpeed.ButtonPressed = true;
        randomSpeed.GrabFocus(); SmokeKey(Key.Tab); valid &= height.HasFocus() && speed.FocusMode == Control.FocusModeEnum.None;

        // Exercise an actual mouse checkbox click after scrolling it into view.
        await Reveal(randomSpeed); Click(randomSpeed);
        valid &= !randomSpeed.ButtonPressed && speed.IsVisibleInTree();
        panel.SetSettings(new PracticeBowlingSettings().WithManualSpeed(18) with { RandomPitchLength = false, RandomSpeed = false, ManualLength = 25 });
        valid &= bowl.Disabled; // Preparation immediately disables starting a stale ball.
        panel.Open();
        valid &= bowl.HasFocus();
        SmokeKey(Key.Enter);
        valid &= _simulation.Phase == DeliveryPhase.Setup && panel.Visible;
        await WaitForDelivery();
        valid &= bowl.Disabled && _ground.Preview == null && Find<Label>("DeliveryStatus").Text.Length > 0;
        panel.SetSettings(new PracticeBowlingSettings().WithManualSpeed(115.2) with { RandomPitchLength = false, RandomSpeed = false });
        valid &= await WaitForDelivery();
        valid &= bowl.HasFocus();
        await Reveal(height);
        valid &= scroll.ScrollVertical > 0 && panel.GetGlobalRect().Encloses(bowl.GetGlobalRect()) && !bowl.Disabled;
        panel.SettingsChanged -= Changed;
        GD.Print(valid ? "SMOKE PASS: bowler categories, random/manual boxes, dropdown, checkboxes, guides, scrolling and sliders." :
            "SMOKE FAIL: practice settings controls.");
        return valid;

        async Task Reveal(Control control)
        {
            control.GrabFocus(); scroll.EnsureControlVisible(control);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        void Click(Control control)
        {
            Vector2 position = control.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
            foreach (bool pressed in new[] { true, false })
                GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position,
                    ButtonIndex = MouseButton.Left, ButtonMask = pressed ? MouseButtonMask.Left : 0, Pressed = pressed }, true);
        }
        void DragSlider(HSlider slider, double value)
        {
            float thumb = slider.GetThemeIcon("grabber").GetWidth();
            Vector2 Point(double v) => new(thumb / 2 + (float)((v - slider.MinValue) / (slider.MaxValue - slider.MinValue)) *
                (slider.Size.X - thumb), slider.Size.Y / 2);
            Drag(slider, Point(slider.Value), Point(value));
        }
        void Drag(Control slider, Vector2 from, Vector2 to)
        {
            Vector2 start = slider.GlobalPosition + from, end = slider.GlobalPosition + to;
            GetViewport().PushInput(new InputEventMouseMotion { Position = start, GlobalPosition = start }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = start, GlobalPosition = start,
                ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseMotion { Position = end, GlobalPosition = end, ButtonMask = MouseButtonMask.Left }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = end, GlobalPosition = end,
                ButtonIndex = MouseButton.Left, Pressed = false }, true);
        }
    }

    private static void SmokeAxis(JoyAxis axis)
    {
        foreach (float value in new[] { -1f, 1f, 0f })
        {
            Godot.Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = axis, AxisValue = value });
            Godot.Input.FlushBufferedEvents();
        }
    }

    private async Task<bool> CheckDeliveryRequestCancellation()
    {
        _hud.DeliveryPanel.SetSettings(new PracticeBowlingSettings().WithManualSpeed(18) with
            { RandomPitchLength = false, RandomSpeed = false, ManualLength = 25 });
        SmokeButton(JoyButton.DpadUp);
        int seed = _practice!.Seed;
        SmokeButton(JoyButton.DpadUp);
        bool valid = _nextDeliveryRequested && _practice.Seed == seed;
        SmokeButton(JoyButton.Start);
        await WaitForDelivery();
        valid &= !_nextDeliveryRequested && _paused && _simulation.Phase == DeliveryPhase.Setup && _hud.PauseMenu.Visible;
        SmokeButton(JoyButton.Start);
        SmokeButton(JoyButton.DpadUp);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        valid &= !_nextDeliveryRequested && _hud.DeliveryPanel.Visible && !_hud.DeliveryPanel.CanBowl && _practice.Error.Length > 0;

        _hud.DeliveryPanel.SetSettings(new());
        valid &= await WaitForDelivery();
        _hud.DeliveryPanel.Hide(); RefreshPause();
        SmokeButton(JoyButton.DpadUp);
        valid &= _simulation.Phase == DeliveryPhase.Windup && !_hud.DeliveryPanel.Visible;
        SmokeButton(JoyButton.Start); SmokeButton(JoyButton.DpadUp); SmokeButton(JoyButton.A);
        valid &= _menu.Visible && !_nextDeliveryRequested && !_paused;
        SmokeButton(JoyButton.A); SmokeButton(JoyButton.A);
        valid &= _simulation.Phase == DeliveryPhase.Setup && !_hud.DeliveryPanel.Visible;
        GD.Print(valid ? "SMOKE PASS: first ball, repeated requests, pause cancels preparation, invalid settings stay editable." :
            "SMOKE FAIL: delivery request cancellation or invalid preparation.");
        return valid;
    }

    private bool SticksLeaveFocusUnchanged()
    {
        var focused = GetViewport().GuiGetFocusOwner();
        bool valid = true;
        foreach (var axis in new[] { JoyAxis.LeftX, JoyAxis.LeftY, JoyAxis.RightX, JoyAxis.RightY })
        {
            SmokeAxis(axis);
            valid &= focused == GetViewport().GuiGetFocusOwner();
        }
        return valid;
    }
}
