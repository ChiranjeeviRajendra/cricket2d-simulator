using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Compact delivery telemetry. Setup is opened only through the A button.</summary>
public partial class GameHud : Control
{
    public DeliveryPanel DeliveryPanel { get; private set; } = null!;
    public event Action? SetupRequested;
    private Label _speed = null!;
    private Label _maxSpeed = null!;
    private Label _maxLength = null!;
    private Label _maxHeight = null!;

    public override void _Ready()
    {
        Theme = VisualTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _speed = Stat("LIVE BALL SPEED", "0.0 km/h", 38);
        _maxSpeed = Stat("MAX BALL SPEED", "0.0 km/h", 226);
        _maxLength = Stat("MAX LENGTH FROM BATTER", "0.00 m", 414);
        _maxHeight = Stat("MAX HEIGHT ABOVE GROUND", "0.00 m", 602);

        var setup = new Button
        {
            Text = "A", TooltipText = "Delivery settings", FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(42, 42)
        };
        setup.Pressed += () => SetupRequested?.Invoke();
        AddChild(setup);
        setup.Position = new(1358, 24);

        DeliveryPanel = new DeliveryPanel();
        AddChild(DeliveryPanel);
        DeliveryPanel.Hide();
    }

    public void Refresh(Simulation sim)
    {
        double liveSpeed = sim.Phase == DeliveryPhase.Live ? sim.Ball.Velocity.Length : 0;
        _speed.Text = $"{SpeedUnits.ToKilometresPerHour(liveSpeed):0.0} km/h";
        _maxSpeed.Text = $"{SpeedUnits.ToKilometresPerHour(sim.Statistics.MaxSpeed):0.0} km/h";
        _maxLength.Text = $"{sim.Statistics.MaxLengthFromBatter:0.00} m";
        _maxHeight.Text = $"{sim.Statistics.MaxHeight:0.00} m";
    }

    private Label Stat(string caption, string value, float x)
    {
        var card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", VisualTheme.Box(new("172c32"), VisualTheme.Border, 7, 10));
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
