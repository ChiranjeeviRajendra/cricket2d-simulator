using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

internal sealed partial class FieldDrawing
{
    private static readonly Color[] SeatColours = [new("356d82"), new("568f99"), new("d1b369"), new("53788d")];
    private void Stands(bool front)
    {
        const int terraceSegments = 160;
        int rows = Settings.Seating.Rows;
        // The far half is seen from the pitch; the near half is seen from behind.
        for (int order = 0; order < rows; order++)
        {
            int row = front ? order : rows - 1 - order;
            double inner = _geometry.RowOffset(row), outer = inner + _geometry.RowDepth;
            double height = _geometry.RowHeight(row), lower = row == 0 ? height : _geometry.RowHeight(row - 1);
            for (int segment = 0; segment < terraceSegments; segment++)
            {
                double a = segment * Math.Tau / terraceSegments, b = (segment + 1) * Math.Tau / terraceSegments;
                if ((Math.Sin((a + b) / 2) < 0) != front) continue;
                Vec2 ia = _geometry.OvalPoint(a, inner), ib = _geometry.OvalPoint(b, inner);
                Vec2 oa = _geometry.OvalPoint(a, outer), ob = _geometry.OvalPoint(b, outer);
                Face(new("a5afad"), new(ia, height), new(ib, height), new(ob, height), new(oa, height));
                if (row > 0)
                    Face(new("778887"), new(ia, lower), new(ib, lower), new(ib, height), new(ia, height));
                // A narrow pale nosing reads as a step without hundreds of antialiased line commands.
                Vec2 na = _geometry.OvalPoint(a, inner + .045), nb = _geometry.OvalPoint(b, inner + .045);
                Face(new("c7cdc7"), new(ia, height), new(ib, height), new(nb, height), new(na, height));
            }
            _building.Add(new([], default, Seats: StadiumSeating.CreateRow(_geometry, row, front)));
        }
        Aisles(front);
    }

    private void Aisles(bool front)
    {
        double bottom = Settings.Barricade.HeightMetres, top = Settings.OuterBarricade.HeightMetres;
        int steps = (int)Math.Ceiling((top - bottom) / .18);
        for (int section = 0; section < Settings.Seating.Sections; section++)
        {
            double angle = StadiumSeating.AisleAngle(_geometry, section);
            if ((Math.Sin(angle) < 0) != front) continue;
            for (int order = 0; order < steps; order++)
            {
                int step = front ? order : steps - 1 - order;
                // Small access stairs occupy the reserved gaps between seating blocks.
                double start = _geometry.SeatingOffset + Settings.Seating.WidthMetres * step / steps;
                double end = _geometry.SeatingOffset + Settings.Seating.WidthMetres * (step + 1) / steps;
                double height = bottom + (top - bottom) * (step + 1) / steps;
                double previous = bottom + (top - bottom) * step / steps;
                double half = StadiumSeating.AisleHalfAngle(_geometry, angle, (start + end) / 2);
                Vec2 a = _geometry.OvalPoint(angle - half, start), b = _geometry.OvalPoint(angle + half, start);
                Vec2 c = _geometry.OvalPoint(angle + half, end), d = _geometry.OvalPoint(angle - half, end);
                Face(new("bcc0b5"), new(a, height), new(b, height), new(c, height), new(d, height));
                Face(new("8f998e"), new(a, previous), new(b, previous), new(b, height), new(a, height));
                if (step % 2 == 0) Line(a, b, new("d6c67b"), .6f, height);
            }
            for (int edge = -1; edge <= 1; edge += 2)
            {
                Vertex? last = null;
                for (int post = 0; post <= 7; post++)
                {
                    double fraction = post / 7.0, offset = _geometry.SeatingOffset + Settings.Seating.WidthMetres * fraction;
                    double half = StadiumSeating.AisleHalfAngle(_geometry, angle, offset);
                    Vec2 point = _geometry.OvalPoint(angle + edge * half, offset);
                    double height = bottom + (top - bottom) * fraction;
                    Vertex rail = new(point, height + .85);
                    _building.Add(new([new(point, height), rail], new("d4dad6"), .8f));
                    if (last.HasValue) _building.Add(new([last.Value, rail], new("d4dad6"), 1));
                    last = rail;
                }
            }
        }
    }

    private void DrawSeats(StadiumSeat[] seats, FieldMesh mesh)
    {
        foreach (ref readonly var seat in seats.AsSpan())
        {
            if (seat.Ground.Y <= _near) continue;
            float scale = (float)FieldLayout.Scale(_camera, seat.Ground.Y);
            Vector2 ground = P(seat.Ground, seat.Height);
            if (ground.X < -scale || ground.X > _viewSize.X + scale || ground.Y < 0 || ground.Y - scale * 1.4f > _viewSize.Y) continue;
            Color colour = SeatColours[(seat.Section / 2) % SeatColours.Length];
            Vector2 hip = ground + new Vector2(0, -.5f * scale);
            float width = .23f * scale;
            // Seat backs remain visible around the seated body and in empty places.
            mesh.Polygon(colour, [hip + new Vector2(-width, .1f * scale), hip + new Vector2(-width, -.36f * scale),
                hip + new Vector2(width, -.36f * scale), hip + new Vector2(width, .1f * scale)]);
            if (!seat.Occupied) continue;
            int direction = Math.Abs(seat.Facing.X) > .65 ? (seat.Facing.X > 0 ? 2 : 3) : seat.Facing.Y > 0 ? 1 : 0;
            int sprite = direction * 50 + (int)((seat.Style >> 16) % 50);
            float stature = .95f + ((seat.Style >> 8) & 7) * .015f;
            Vector2 size = new(.84f * scale, 1.3f * scale * stature);
            mesh.Sprite(new(ground - new Vector2(size.X / 2, size.Y * .92f), size),
                new(new Vector2(sprite % 16 * 64 / 1024f, sprite / 16 * 96 / 1280f), new Vector2(64 / 1024f, 96 / 1280f)));
        }
    }
}
