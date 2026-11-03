namespace Cricket.Physics;

/// <summary>Convert only at the UI boundary; simulation velocities remain in m/s.</summary>
public static class SpeedUnits
{
    public static double ToKilometresPerHour(double metresPerSecond) => metresPerSecond * 3.6;
    public static double ToMetresPerSecond(double kilometresPerHour) => kilometresPerHour / 3.6;
}
