using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Delivery telemetry and pause status. Face buttons are reserved for shots during play.</summary>
public partial class GameHud : Control
{
    public DeliveryPanel DeliveryPanel { get; private set; } = null!;
    public PauseMenu PauseMenu { get; private set; } = null!;
    private Label _speed = null!;
    private Label _maxSpeed = null!;
    private Label _maxLength = null!;
    private Label _maxHeight = null!;
    private Label _fps = null!;
    private double _lastSpeed = double.NaN, _lastMaxSpeed = double.NaN,
        _lastLength = double.NaN, _lastHeight = double.NaN, _lastFps = double.NaN;

    public override void _Ready()
    {
        Theme = VisualTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _speed = Stat("LIVE BALL SPEED", "0.0 km/h", 38);
        _maxSpeed = Stat("MAX BALL SPEED", "0.0 km/h", 226);
        _maxLength = Stat("MAX LENGTH FROM BATTER", "0.00 m", 414);
        _maxHeight = Stat("MAX HEIGHT ABOVE GROUND", "0.00 m", 602);
        _fps = Stat("FPS", "—", 790);
        var controls=VisualTheme.Label("LS: 8:30 → 6 → 3 → 12 • A/default: straight  X: wall at 2  B: 1:30 / blade at 4  Y: sky • L1/LB + R1/RB: next ball • Start: pause",15,VisualTheme.OnDark);
        controls.Position=new(38,852);
        controls.MouseFilter=MouseFilterEnum.Ignore;
        AddChild(controls);

        DeliveryPanel = new DeliveryPanel();
        AddChild(DeliveryPanel);
        DeliveryPanel.Hide();
        PauseMenu = new PauseMenu();
        AddChild(PauseMenu);
    }

    public void Refresh(Simulation sim)
    {
        double liveSpeed = sim.Phase == DeliveryPhase.Live ? sim.Ball.Velocity.Length : 0;
        SetValue(_speed, SpeedUnits.ToKilometresPerHour(liveSpeed), 1, " km/h", ref _lastSpeed);
        SetValue(_maxSpeed, SpeedUnits.ToKilometresPerHour(sim.Statistics.MaxSpeed), 1, " km/h", ref _lastMaxSpeed);
        SetValue(_maxLength, sim.Statistics.MaxLengthFromBatter, 2, " m", ref _lastLength);
        SetValue(_maxHeight, sim.Statistics.MaxHeight, 2, " m", ref _lastHeight);
        SetValue(_fps, Engine.GetFramesPerSecond(), 0, "", ref _lastFps);
    }

    private static void SetValue(Label label, double value, int digits, string unit, ref double previous)
    {
        double rounded = Math.Round(value, digits);
        if (rounded == previous) return;
        previous = rounded;
        label.Text = rounded.ToString(digits == 0 ? "0" : digits == 1 ? "0.0" : "0.00") + unit;
    }

    private Label Stat(string caption, string value, float x)
    {
        var card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", VisualTheme.Box(VisualTheme.Panel, VisualTheme.Border, 7, 10));
        AddChild(card);
        card.Position = new(x, 22);
        card.Size = new(176, 58);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 3);
        card.AddChild(col);
        col.AddChild(VisualTheme.Label(caption, 9, VisualTheme.Muted));
        var label = VisualTheme.Label(value, 18);
        col.AddChild(label);
        return label;
    }
}
