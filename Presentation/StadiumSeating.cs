using Cricket.Physics;

namespace Cricket2D.Presentation;

public readonly record struct StadiumSeat(Vec2 Ground, Vec2 Facing, double Height, int Section, uint Style, bool Occupied);

/// <summary>Deterministic seating layout, built once. Spectators have no nodes or physics bodies.</summary>
public static class StadiumSeating
{
    public static double AisleAngle(FieldGeometry field, int section) =>
        (section + .5) * Math.Tau / field.Settings.Seating.Sections;

    public static double AisleHalfAngle(FieldGeometry field, double angle, double offset)
    {
        // Arc length per radian on the normal-offset ellipse.
        double a = field.RadiusX, b = field.Settings.Boundary.DepthRadiusMetres;
        double speed = Math.Sqrt(a * a * Math.Sin(angle) * Math.Sin(angle) + b * b * Math.Cos(angle) * Math.Cos(angle));
        return field.Settings.Seating.AisleWidthMetres / (2 * (speed + offset * a * b / (speed * speed)));
    }

    public static StadiumSeat[] CreateRow(FieldGeometry field, int row, bool front)
    {
        var settings = field.Settings.Seating;
        double offset = field.RowOffset(row) + field.RowDepth * .55;
        double a = field.RadiusX, b = field.Settings.Boundary.DepthRadiusMetres;
        double circumference = Math.PI * (3 * (a + b) - Math.Sqrt((3 * a + b) * (a + 3 * b))) + Math.Tau * offset;
        int count = (int)(circumference / settings.SeatSpacingMetres);
        var seats = new List<StadiumSeat>(count / 2);
        // Arc-length sampling avoids crowded seats at the ends of a stretched oval.
        double angle = 0, sectionAngle = Math.Tau / settings.Sections;
        for (int index = 0; angle < Math.Tau; index++)
        {
            double speed = Math.Sqrt(a * a * Math.Sin(angle) * Math.Sin(angle) + b * b * Math.Cos(angle) * Math.Cos(angle));
            double increment = settings.SeatSpacingMetres / (speed + offset * a * b / (speed * speed));
            double seatAngle = angle + increment / 2;
            if (seatAngle >= Math.Tau) break;
            angle += increment;
            if ((Math.Sin(seatAngle) < 0) != front) continue;
            int section = (int)Math.Floor(seatAngle / sectionAngle);
            double aisle = AisleAngle(field, section);
            if (Math.Abs(seatAngle - aisle) < AisleHalfAngle(field, aisle, offset) + increment * .45) continue;
            uint style = Hash(unchecked((uint)settings.CrowdSeed + (uint)row * 65537 + (uint)index));
            Vec2 facing = -new Vec2(Math.Cos(seatAngle) / a, Math.Sin(seatAngle) / b).Normalized;
            seats.Add(new(field.OvalPoint(seatAngle, offset), facing, field.RowHeight(row),
                section, style, (style & 0xffff) / 65536.0 < settings.Occupancy));
        }
        seats.Sort(static (a, b) => b.Ground.Y.CompareTo(a.Ground.Y));
        return seats.ToArray();
    }

    private static uint Hash(uint value)
    {
        unchecked
        {
            value ^= value >> 16; value *= 0x7feb352d;
            value ^= value >> 15; value *= 0x846ca68b;
            return value ^ (value >> 16);
        }
    }
}
