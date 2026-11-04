using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cricket.Physics;

/// <summary>One metre-based definition for the physical cross-sections and visible ovals.</summary>
public sealed record FieldSettings
{
    public BoundarySettings Boundary { get; init; } = new();
    public BarricadeSettings Barricade { get; init; } = new();
    public SeatingSettings Seating { get; init; } = new();
    public OuterBarricadeSettings OuterBarricade { get; init; } = new();
    public static FieldSettings FromJson(string json)
    {
        var value = JsonSerializer.Deserialize<FieldSettings>(json, new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new ArgumentException("Field configuration cannot be null.");
        value.Validate();
        return value;
    }
    public void Validate()
    {
        if (Boundary == null || Barricade == null || Seating == null || OuterBarricade == null)
            throw new ArgumentException("Field Boundary, Barricade, Seating and OuterBarricade objects cannot be null.");
        Range(Boundary.LeftDistanceMetres, 10, 150, "Boundary.LeftDistanceMetres");
        Range(Boundary.RightDistanceMetres, 25, 180, "Boundary.RightDistanceMetres");
        Range(Boundary.DepthRadiusMetres, 20, 120, "Boundary.DepthRadiusMetres");
        Range(Boundary.WidthMetres, .1, 1, "Boundary.WidthMetres");
        Range(Boundary.HeightMetres, .05, .6, "Boundary.HeightMetres");
        Range(Boundary.Restitution, 0, 1, "Boundary.Restitution");
        Range(Boundary.Friction, 0, 1, "Boundary.Friction");
        Range(Barricade.ClearGapMetres, 1, 20, "Barricade.ClearGapMetres");
        Range(Barricade.HeightMetres, .3, 4, "Barricade.HeightMetres");
        Range(Barricade.ThicknessMetres, .1, 1, "Barricade.ThicknessMetres");
        if (Barricade.Restitution.HasValue) Range(Barricade.Restitution.Value, 0, 1, "Barricade.Restitution");
        if (Barricade.Friction.HasValue) Range(Barricade.Friction.Value, 0, 1, "Barricade.Friction");
        Range(Seating.WidthMetres, 12, 60, "Seating.WidthMetres");
        Range(Seating.Rows, 8, 64, "Seating.Rows");
        Range(Seating.Sections, 8, 32, "Seating.Sections");
        if (Seating.Sections % 2 != 0)
            throw new ArgumentException("Seating.Sections must be even so access aisles stay clear of the ball's collision plane.");
        Range(Seating.AisleWidthMetres, 1, 3, "Seating.AisleWidthMetres");
        Range(Seating.SeatSpacingMetres, .5, 1, "Seating.SeatSpacingMetres");
        Range(Seating.Occupancy, 0, 1, "Seating.Occupancy");
        if (Seating.WidthMetres / Seating.Rows < .6)
            throw new ArgumentException("Seating rows must be at least 0.6 m deep. Increase WidthMetres or reduce Rows.");
        Range(OuterBarricade.HeightMetres, .3, 80, "OuterBarricade.HeightMetres");
        Range(OuterBarricade.ThicknessMetres, .1, 1, "OuterBarricade.ThicknessMetres");
        if (OuterBarricade.Restitution.HasValue) Range(OuterBarricade.Restitution.Value, 0, 1, "OuterBarricade.Restitution");
        if (OuterBarricade.Friction.HasValue) Range(OuterBarricade.Friction.Value, 0, 1, "OuterBarricade.Friction");
        if (OuterBarricade.HeightMetres <= Barricade.HeightMetres)
            throw new ArgumentException("The outer barricade must be taller than the inner barricade for descending seating.");
    }
    private static void Range(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max)
            throw new ArgumentException($"Field {name} must be between {min} and {max}.");
    }
}

public sealed record BoundarySettings
{
    public double LeftDistanceMetres { get; init; } = 40;
    public double RightDistanceMetres { get; init; } = 75;
    public double DepthRadiusMetres { get; init; } = 45;
    // Vinyl-covered PU cricket wedge profile from Promofoam. Restitution/friction are tuning assumptions.
    public double WidthMetres { get; init; } = .23;
    public double HeightMetres { get; init; } = .147;
    public double Restitution { get; init; } = .20;
    public double Friction { get; init; } = .40;
}
public sealed record BarricadeSettings
{
    public double ClearGapMetres { get; init; } = 5;
    public double HeightMetres { get; init; } = 1.5;
    public double ThicknessMetres { get; init; } = .25;
    // Null inherits the pitch coefficient, including later edits to physics.json.
    public double? Restitution { get; init; }
    public double? Friction { get; init; }
}
public sealed record SeatingSettings
{
    public double WidthMetres { get; init; } = 21.5;
    public int Rows { get; init; } = 28;
    public int Sections { get; init; } = 16;
    public double AisleWidthMetres { get; init; } = 1.8;
    public double SeatSpacingMetres { get; init; } = .62;
    public double Occupancy { get; init; } = .82;
    public int CrowdSeed { get; init; } = 4821;
}
public sealed record OuterBarricadeSettings
{
    public double HeightMetres { get; init; } = 25;
    public double ThicknessMetres { get; init; } = .25;
    // Null inherits the inner barricade's resolved coefficients.
    public double? Restitution { get; init; }
    public double? Friction { get; init; }
}
