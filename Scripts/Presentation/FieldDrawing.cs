using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>Immutable world geometry projected into retained meshes only when framing changes.</summary>
internal sealed class FieldDrawing : IDisposable
{
    private const int Segments = 320;
    private readonly record struct Vertex(Vec2 Ground, double Height = 0);
    private sealed record Primitive(Vertex[] Vertices, Color Colour, float LineWidth = 0, double Radius = 0);
    private static readonly Color[] CarColours = [new("d6d9d5"), new("466d81"), new("a35b47"),
        new("404a51"), new("b3b5a3"), new("6b7f63")];
    private readonly FieldGeometry _geometry;
    private readonly Texture2D _sky = GD.Load<Texture2D>("res://Assets/Field/sky.png");
    private readonly FieldMesh _background = new(), _foreground = new();
    private readonly List<Primitive> _back = [], _front = [];
    private List<Primitive> _building;
    private FieldSettings Settings => _geometry.Settings;
    private FieldCamera _camera = null!;
    private double _near, _scale = double.NaN, _origin = double.NaN;
    private Node2D _canvas = null!;
    public int RebuildCount { get; private set; }

    public FieldDrawing(FieldGeometry geometry)
    {
        _geometry = geometry;
        _building = _back; Parking(); Ring(false);
        _building = _front; Ring(true);
        foreach (int side in new[] { -1, 1 })
        {
            double x = side > 0 ? geometry.LeftX : geometry.RightX;
            Face(new("fffef7"), new(new(x, 0)),
                new(new(x + side * Settings.Boundary.WidthMetres / 2, 0), Settings.Boundary.HeightMetres),
                new(new(x + side * Settings.Boundary.WidthMetres, 0)));
        }
    }
    private Vector2 P(Vec2 point, double height = 0)
    {
        Vec2 p = FieldLayout.Project(_camera, point, height);
        return new((float)p.X, (float)p.Y);
    }
    private void Face(Color colour, params Vertex[] vertices) => _building.Add(new(vertices, colour));
    private void Line(Vec2 a, Vec2 b, Color colour, float width = 1, double height = 0) =>
        _building.Add(new([new(a, height), new(b, height)], colour, width));
    private void Disc(Vec2 point, double height, double radius, Color colour) =>
        _building.Add(new([new(point, height)], colour, Radius: radius));

    public void Background(Node2D canvas, FieldCamera camera)
    {
        _canvas = canvas;
        _camera = camera;
        if (_scale != camera.PixelsPerMetre || _origin != camera.WorldMinX)
        {
            _scale = camera.PixelsPerMetre; _origin = camera.WorldMinX;
            _near = FieldLayout.DepthAtScreenY(camera, 980);
            _background.Clear(); _foreground.Clear();
            Ground();
            foreach (var primitive in _back) Project(primitive, _background);
            foreach (var primitive in _front) Project(primitive, _foreground);
            _background.Upload(); _foreground.Upload();
            RebuildCount++;
        }
        canvas.DrawTextureRect(_sky, new(0, 0, 1440, 900), false);
        _background.Draw(canvas);
    }
    public void Foreground() => _foreground.Draw(_canvas);

    private void Ground()
    {
        double far = Settings.Boundary.DepthRadiusMetres + _geometry.ParkingOffset + Settings.Parking.WidthMetres + 35;
        float horizon = P(new(_geometry.CentreX, far)).Y;
        for (int i = 0; i < 50; i++)
        {
            float x = i * 31 - 20, h = 10 + i * 19 % 17;
            _background.Disc(new(x, horizon - h / 3), h, new("738e82"));
            _background.Disc(new(x + 14, horizon), h * .9f, new("688477"));
        }
        _background.Gradient(horizon, new("789457"), new("496d3d"));
        double half = 760 / FieldLayout.Scale(_camera, far), middle = (_camera.WorldMinX + _camera.WorldMaxX) / 2;
        for (int i = (int)Math.Floor((middle - half) / 5); i < (middle + half) / 5; i++)
        {
            if ((i & 1) == 0) continue;
            _background.Polygon(new(.75f, .84f, .53f, .10f),
                [P(new(i * 5, far)), P(new((i + 1) * 5, far)), P(new((i + 1) * 5, _near)), P(new(i * 5, _near))]);
        }
    }

    private void Project(Primitive primitive, FieldMesh mesh)
    {
        var vertices = primitive.Vertices;
        if (primitive.Radius > 0)
        {
            var centre = vertices[0];
            if (centre.Ground.Y <= _near) return;
            mesh.Disc(P(centre.Ground, centre.Height),
                Math.Max(.5f, (float)(primitive.Radius * FieldLayout.Scale(_camera, centre.Ground.Y))), primitive.Colour);
            return;
        }
        if (primitive.LineWidth > 0)
        {
            Vec2 a = vertices[0].Ground, b = vertices[1].Ground;
            if (a.Y <= _near && b.Y <= _near) return;
            if (a.Y <= _near) a = Vec2.Lerp(a, b, (_near - a.Y) / (b.Y - a.Y));
            if (b.Y <= _near) b = Vec2.Lerp(b, a, (_near - b.Y) / (a.Y - b.Y));
            mesh.Line(P(a, vertices[0].Height), P(b, vertices[1].Height), primitive.Colour, primitive.LineWidth);
            return;
        }
        Span<Vector2> clipped = stackalloc Vector2[6];
        int count = 0;
        Vertex previous = vertices[^1];
        foreach (Vertex current in vertices)
        {
            bool before = previous.Ground.Y >= _near, inside = current.Ground.Y >= _near;
            if (before != inside)
            {
                double fraction = (_near - previous.Ground.Y) / (current.Ground.Y - previous.Ground.Y);
                clipped[count++] = P(Vec2.Lerp(previous.Ground, current.Ground, fraction),
                    previous.Height + (current.Height - previous.Height) * fraction);
            }
            if (inside) clipped[count++] = P(current.Ground, current.Height);
            previous = current;
        }
        mesh.Polygon(primitive.Colour, clipped[..count]);
    }

    private void Parking()
    {
        double start = _geometry.ParkingOffset, end = start + Settings.Parking.WidthMetres;
        for (int i = 0; i < Segments; i++)
        {
            double a = i * Math.Tau / Segments, b = (i + 1) * Math.Tau / Segments;
            Vec2 ia = _geometry.OvalPoint(a, start), ib = _geometry.OvalPoint(b, start), oa = _geometry.OvalPoint(a, end), ob = _geometry.OvalPoint(b, end);
            Face(new("596263"), new(ia), new(ib), new(ob), new(oa));
            Line(oa, ob, new("b7b9a5"), 1.5f); Line(ia, ib, new("aeb6a2"), 1.2f);
            if (i % 4 < 2) Line(_geometry.OvalPoint(a, (start + end) / 2), _geometry.OvalPoint(b, (start + end) / 2), new("d1c99a"));
        }
        int bays = (int)(Math.Tau * Math.Sqrt((_geometry.RadiusX * _geometry.RadiusX +
            Settings.Boundary.DepthRadiusMetres * Settings.Boundary.DepthRadiusMetres) / 2) / 3.3);
        int[] order = Enumerable.Range(0, bays).OrderByDescending(i => Math.Sin(i * Math.Tau / bays)).ToArray();
        for (int row = 0; row < 2; row++)
        {
            double offset = row == 0 ? start + 3 : end - 3;
            foreach (int i in order)
            {
                double angle = i * Math.Tau / bays;
                Line(_geometry.OvalPoint(angle, offset - 2.4), _geometry.OvalPoint(angle, offset + 2.4), new("c5c8b9"));
                if ((i + row * 3) % 7 is 0 or 1) continue;
                Car((i + .5) * Math.Tau / bays, offset, CarColours[(i * 7 + row) % CarColours.Length]);
            }
        }
    }

    private void Car(double angle, double offset, Color colour)
    {
        Vec2 centre = _geometry.OvalPoint(angle, offset);
        Vec2 normal = new Vec2(Math.Cos(angle) / _geometry.RadiusX, Math.Sin(angle) / Settings.Boundary.DepthRadiusMetres).Normalized;
        Vec2 side = normal.Perpendicular;
        Vec2[] corners = [centre-normal*2.05-side*.87,centre+normal*2.05-side*.87,
            centre+normal*2.05+side*.87,centre-normal*2.05+side*.87];
        Face(new(0, 0, 0, .22f), corners.Select(p => new Vertex(p + new Vec2(.2, -.2))).ToArray());
        foreach (int j in Enumerable.Range(0, 4).OrderByDescending(j => (corners[j].Y + corners[(j + 1) % 4].Y) / 2))
            Face(colour.Darkened(.23f), new(corners[j], .18), new(corners[(j + 1) % 4], .18),
                new(corners[(j + 1) % 4], .85), new(corners[j], .85));
        Face(colour, corners.Select(p => new Vertex(p, .85)).ToArray());
        Vec2[] roof = [centre-normal*.95-side*.7,centre+normal*.8-side*.7,
            centre+normal*.8+side*.7,centre-normal*.95+side*.7];
        Vec2[] windowBase = [centre-normal*1.3-side*.84,centre+normal*1.15-side*.84,
            centre+normal*1.15+side*.84,centre-normal*1.3+side*.84];
        foreach (int j in Enumerable.Range(0, 4).OrderByDescending(j => (windowBase[j].Y + windowBase[(j + 1) % 4].Y) / 2))
            Face(new("293f49"), new(windowBase[j], .86), new(windowBase[(j + 1) % 4], .86),
                new(roof[(j + 1) % 4], 1.35), new(roof[j], 1.35));
        Face(colour.Lightened(.10f), roof.Select(p => new Vertex(p, 1.35)).ToArray());
        foreach (double axle in new[] { -1.25, 1.25 })
        foreach (double sign in new[] { -1.0, 1.0 })
            Disc(centre + normal * axle + side * (sign * .89), .27, .26, new("263231"));
    }

    private void Ring(bool front)
    {
        var boundary = Settings.Boundary; var wall = Settings.Barricade;
        for (int i = 0; i < Segments; i++)
        {
            double a = i * Math.Tau / Segments, b = (i + 1) * Math.Tau / Segments;
            if ((Math.Sin((a + b) / 2) < 0) != front) continue;
            Vec2 innerA = _geometry.OvalPoint(a), innerB = _geometry.OvalPoint(b);
            Vec2 outerA = _geometry.OvalPoint(a, boundary.WidthMetres), outerB = _geometry.OvalPoint(b, boundary.WidthMetres);
            Vec2 ridgeA = _geometry.OvalPoint(a, boundary.WidthMetres / 2), ridgeB = _geometry.OvalPoint(b, boundary.WidthMetres / 2);
            Face(new("fffef7"), new(innerA), new(innerB), new(ridgeB, boundary.HeightMetres), new(ridgeA, boundary.HeightMetres));
            Face(new("d9e0d4"), new(ridgeA, boundary.HeightMetres), new(ridgeB, boundary.HeightMetres), new(outerB), new(outerA));
            Vec2 wa = _geometry.OvalPoint(a, _geometry.BarricadeOffset), wb = _geometry.OvalPoint(b, _geometry.BarricadeOffset);
            Vec2 xa = _geometry.OvalPoint(a, _geometry.BarricadeOffset + wall.ThicknessMetres), xb = _geometry.OvalPoint(b, _geometry.BarricadeOffset + wall.ThicknessMetres);
            Face(new("66786e"), new(xa), new(xb), new(xb, wall.HeightMetres), new(xa, wall.HeightMetres));
            Face(new("d5d9ce"), new(wa), new(wb), new(wb, wall.HeightMetres), new(wa, wall.HeightMetres));
            Face(new("f0efdf"), new(wa, wall.HeightMetres), new(wb, wall.HeightMetres), new(xb, wall.HeightMetres), new(xa, wall.HeightMetres));
            if (i % 6 == 0) _building.Add(new([new(wa), new(wa, wall.HeightMetres)], new("a2aca0"), 1));
        }
    }
    // The meshes are owned here; the imported sky texture is shared through Godot's resource cache.
    public void Dispose() { _background.Dispose(); _foreground.Dispose(); }
}
