using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>All ovals use the same normal-offset geometry as the physical wedge/wall crossings.</summary>
internal sealed class FieldDrawing(FieldGeometry geometry)
{
    private readonly Texture2D _sky = GD.Load<Texture2D>("res://Assets/Field/sky.png");
    private const int Segments = 320;
    private static readonly Color[] CarColours = [new("d6d9d5"),new("466d81"),new("a35b47"),
        new("404a51"),new("b3b5a3"),new("6b7f63")];
    private FieldSettings Settings => geometry.Settings;
    private FieldCamera _camera = null!;
    private Node2D _canvas = null!;
    private double _near;
    private Vector2 P(Vec2 point, double height = 0)
    {
        Vec2 p = FieldLayout.Project(_camera, point, height);
        return new((float)p.X, (float)p.Y);
    }
    private bool Visible(params Vec2[] points) => points.All(p => p.Y > _near);
    private void Polygon(Color colour, params Vector2[] points)
    {
        if (points.All(p => p.X < -30) || points.All(p => p.X > 1470) ||
            points.All(p => p.Y > 960) || points.All(p => p.Y < 0)) return;
        // Curved 3D strips can project to a folded or edge-on quad. Explicit triangles
        // avoid ambiguous ear clipping and skip zero-area faces at the camera horizon.
        for (int i = 1; i < points.Length - 1; i++)
        {
            if (Math.Abs((points[i] - points[0]).Cross(points[i + 1] - points[0])) < .00001f) continue;
            _canvas.DrawPrimitive(new Vector2[] { points[0], points[i], points[i + 1] },
                new Color[] { colour, colour, colour }, Array.Empty<Vector2>());
        }
    }
    private void Line(Vec2 a, Vec2 b, Color colour, float width = 1, double height = 0)
    {
        if (a.Y <= _near && b.Y <= _near) return;
        if (a.Y <= _near) a = Vec2.Lerp(a, b, (_near - a.Y) / (b.Y - a.Y));
        if (b.Y <= _near) b = Vec2.Lerp(b, a, (_near - b.Y) / (a.Y - b.Y));
        _canvas.DrawLine(P(a, height), P(b, height), colour, width, true);
    }
    private void Face(Color colour, params (Vec2 Ground, double Height)[] vertices)
    {
        // Clip in world depth before projecting: a partially visible parking/wall section
        // must not disappear just because its outer edge passes behind the near plane.
        var clipped = new List<(Vec2 Ground, double Height)>();
        var previous = vertices[^1];
        foreach (var current in vertices)
        {
            bool before = previous.Ground.Y >= _near, inside = current.Ground.Y >= _near;
            if (before != inside)
            {
                double fraction = (_near - previous.Ground.Y) / (current.Ground.Y - previous.Ground.Y);
                clipped.Add((Vec2.Lerp(previous.Ground, current.Ground, fraction),
                    previous.Height + (current.Height - previous.Height) * fraction));
            }
            if (inside) clipped.Add(current);
            previous = current;
        }
        if (clipped.Count >= 3) Polygon(colour, clipped.Select(v => P(v.Ground, v.Height)).ToArray());
    }
    public void Background(Node2D canvas, FieldCamera camera)
    {
        _canvas = canvas;
        _camera = camera;
        _near = FieldLayout.DepthAtScreenY(camera, 980);
        canvas.DrawTextureRect(_sky, new(0, 0, 1440, 900), false);
        double far = Settings.Boundary.DepthRadiusMetres + geometry.ParkingOffset + Settings.Parking.WidthMetres + 35;
        float horizon = P(new(geometry.CentreX, far)).Y;
        // Distant vegetation anchors the ground without a cutaway below the pitch.
        for (int i = 0; i < 50; i++)
        {
            float x = i * 31 - 20, h = 10 + i * 19 % 17;
            canvas.DrawCircle(new(x, horizon - h / 3), h, new("738e82"));
            canvas.DrawCircle(new(x + 14, horizon), h * .9f, new("688477"));
        }
        for (float y = horizon; y < 900; y += 4)
            canvas.DrawRect(new(0, y, 1440, 4), new Color("789457").Lerp(new("496d3d"), (y - horizon) / (900 - horizon)));
        double half = 760 / FieldLayout.Scale(camera, far), middle = (camera.WorldMinX + camera.WorldMaxX) / 2;
        for (int i = (int)Math.Floor((middle - half) / 5); i < (middle + half) / 5; i++)
        {
            if ((i & 1) == 0) continue;
            Polygon(new(.75f, .84f, .53f, .10f), P(new(i * 5, far)), P(new((i + 1) * 5, far)),
                P(new((i + 1) * 5, _near)), P(new(i * 5, _near)));
        }
        Parking();
        Ring(false);
    }
    public void Foreground() => Ring(true);

    private void Parking()
    {
        double start = geometry.ParkingOffset, end = start + Settings.Parking.WidthMetres;
        for (int i = 0; i < Segments; i++)
        {
            double a = i * Math.Tau / Segments, b = (i + 1) * Math.Tau / Segments;
            Vec2 ia = geometry.OvalPoint(a, start), ib = geometry.OvalPoint(b, start), oa = geometry.OvalPoint(a, end), ob = geometry.OvalPoint(b, end);
            Face(new("596263"), (ia, 0), (ib, 0), (ob, 0), (oa, 0));
            Line(oa, ob, new("b7b9a5"), 1.5f);
            Line(ia, ib, new("aeb6a2"), 1.2f);
            if (i % 4 < 2) Line(geometry.OvalPoint(a, (start + end) / 2), geometry.OvalPoint(b, (start + end) / 2), new("d1c99a"));
        }
        int bays = (int)(Math.Tau * Math.Sqrt((geometry.RadiusX * geometry.RadiusX +
            Settings.Boundary.DepthRadiusMetres * Settings.Boundary.DepthRadiusMetres) / 2) / 3.3);
        for (int row = 0; row < 2; row++)
        {
            double offset = row == 0 ? start + 3 : end - 3;
            // Draw the farthest cars first. Their simple 3D boxes stay correctly oriented around the oval.
            foreach (int i in Enumerable.Range(0, bays).OrderByDescending(i => Math.Sin(i * Math.Tau / bays)))
            {
                double angle = i * Math.Tau / bays;
                Line(geometry.OvalPoint(angle, offset - 2.4), geometry.OvalPoint(angle, offset + 2.4), new("c5c8b9"));
                if ((i + row * 3) % 7 is 0 or 1) continue;
                Car((i + .5) * Math.Tau / bays, offset, CarColours[(i * 7 + row) % CarColours.Length]);
            }
        }
    }
    private void Car(double angle, double offset, Color colour)
    {
        Vec2 centre = geometry.OvalPoint(angle, offset);
        Vec2 normal = new Vec2(Math.Cos(angle) / geometry.RadiusX, Math.Sin(angle) / Settings.Boundary.DepthRadiusMetres).Normalized;
        Vec2 side = normal.Perpendicular;
        Vec2[] corners = [centre-normal*2.05-side*.87,centre+normal*2.05-side*.87,
            centre+normal*2.05+side*.87,centre-normal*2.05+side*.87];
        if (!Visible(corners)) return;
        Vector2 cp = P(centre);
        if (cp.X < -100 || cp.X > 1540 || cp.Y > 1000) return;
        Polygon(new(0, 0, 0, .22f), corners.Select(p => P(p + new Vec2(.2, -.2))).ToArray());
        foreach (int j in Enumerable.Range(0, 4).OrderByDescending(j => (corners[j].Y + corners[(j + 1) % 4].Y) / 2))
            Polygon(colour.Darkened(.23f), P(corners[j], .18), P(corners[(j + 1) % 4], .18),
                P(corners[(j + 1) % 4], .85), P(corners[j], .85));
        Polygon(colour, corners.Select(p => P(p, .85)).ToArray());
        Vec2[] roof = [centre-normal*.95-side*.7,centre+normal*.8-side*.7,
            centre+normal*.8+side*.7,centre-normal*.95+side*.7];
        Vec2[] windowBase = [centre-normal*1.3-side*.84,centre+normal*1.15-side*.84,
            centre+normal*1.15+side*.84,centre-normal*1.3+side*.84];
        foreach (int j in Enumerable.Range(0, 4).OrderByDescending(j => (windowBase[j].Y + windowBase[(j + 1) % 4].Y) / 2))
            Polygon(new("293f49"), P(windowBase[j], .86), P(windowBase[(j + 1) % 4], .86),
                P(roof[(j + 1) % 4], 1.35), P(roof[j], 1.35));
        Polygon(colour.Lightened(.10f), roof.Select(p => P(p, 1.35)).ToArray());
        foreach (double axle in new[] { -1.25, 1.25 })
            foreach (double sign in new[] { -1.0, 1.0 })
            {
                Vec2 wheel = centre + normal * axle + side * (sign * .89);
                float radius = (float)(FieldLayout.Scale(_camera, wheel.Y) * .26);
                _canvas.DrawCircle(P(wheel, .27), Math.Max(.5f, radius), new("263231"));
            }
    }

    private void Ring(bool front)
    {
        var boundary = Settings.Boundary; var wall = Settings.Barricade;
        for (int i = 0; i < Segments; i++)
        {
            double a = i * Math.Tau / Segments, b = (i + 1) * Math.Tau / Segments;
            if ((Math.Sin((a + b) / 2) < 0) != front) continue;
            Vec2 innerA = geometry.OvalPoint(a), innerB = geometry.OvalPoint(b);
            Vec2 outerA = geometry.OvalPoint(a, boundary.WidthMetres), outerB = geometry.OvalPoint(b, boundary.WidthMetres);
            Vec2 ridgeA = geometry.OvalPoint(a, boundary.WidthMetres / 2), ridgeB = geometry.OvalPoint(b, boundary.WidthMetres / 2);
            Face(new("fffef7"), (innerA, 0), (innerB, 0), (ridgeB, boundary.HeightMetres), (ridgeA, boundary.HeightMetres));
            Face(new("d9e0d4"), (ridgeA, boundary.HeightMetres), (ridgeB, boundary.HeightMetres), (outerB, 0), (outerA, 0));
            Vec2 wa = geometry.OvalPoint(a, geometry.BarricadeOffset), wb = geometry.OvalPoint(b, geometry.BarricadeOffset);
            Vec2 xa = geometry.OvalPoint(a, geometry.BarricadeOffset + wall.ThicknessMetres), xb = geometry.OvalPoint(b, geometry.BarricadeOffset + wall.ThicknessMetres);
            Face(new("66786e"), (xa, 0), (xb, 0), (xb, wall.HeightMetres), (xa, wall.HeightMetres));
            Face(new("d5d9ce"), (wa, 0), (wb, 0), (wb, wall.HeightMetres), (wa, wall.HeightMetres));
            Face(new("f0efdf"), (wa, wall.HeightMetres), (wb, wall.HeightMetres), (xb, wall.HeightMetres), (xa, wall.HeightMetres));
            if (i % 6 == 0 && Visible(wa)) _canvas.DrawLine(P(wa), P(wa, wall.HeightMetres), new("a2aca0"), 1, true);
        }
        if (front) CrossSections();
    }
    private void CrossSections()
    {
        // Exactly the same physical triangle vertices used by FieldGeometry, with no distance signs.
        foreach (int side in new[] { -1, 1 })
        {
            double x = side > 0 ? geometry.LeftX : geometry.RightX;
            Polygon(new("fffef7"), P(new(x, 0)), P(new(x + side * Settings.Boundary.WidthMetres / 2, 0), Settings.Boundary.HeightMetres),
                P(new(x + side * Settings.Boundary.WidthMetres, 0)));
        }
    }
}
