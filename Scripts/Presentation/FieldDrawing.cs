using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>Immutable world geometry projected into retained meshes only when framing changes.</summary>
internal sealed partial class FieldDrawing : IDisposable
{
    private const int Segments = 320;
    private readonly record struct Vertex(Vec2 Ground, double Height = 0);
    private sealed record Primitive(Vertex[] Vertices, Color Colour, float LineWidth = 0, StadiumSeat[]? Seats = null);
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
        _building = _back; OuterWall(false); Stands(false); Ring(false);
        _building = _front; Ring(true); Stands(true); OuterWall(true);
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
        double far = Settings.Boundary.DepthRadiusMetres + _geometry.OuterBarricadeOffset + 35;
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
        if (primitive.Seats != null) { DrawSeats(primitive.Seats, mesh); return; }
        if (primitive.LineWidth > 0)
        {
            Vec2 a = vertices[0].Ground, b = vertices[1].Ground;
            double heightA = vertices[0].Height, heightB = vertices[1].Height;
            if (a.Y <= _near && b.Y <= _near) return;
            if (a.Y <= _near)
            {
                double fraction = (_near - a.Y) / (b.Y - a.Y);
                a = Vec2.Lerp(a, b, fraction); heightA += (heightB - heightA) * fraction;
            }
            if (b.Y <= _near)
            {
                double fraction = (_near - b.Y) / (a.Y - b.Y);
                b = Vec2.Lerp(b, a, fraction); heightB += (heightA - heightB) * fraction;
            }
            mesh.Line(P(a, heightA), P(b, heightB), primitive.Colour, primitive.LineWidth);
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
            WallSegment(a, b, i, _geometry.BarricadeOffset, wall.HeightMetres, wall.ThicknessMetres);
        }
    }

    private void OuterWall(bool front)
    {
        var wall = Settings.OuterBarricade;
        for (int i = 0; i < Segments; i++)
        {
            double a = i * Math.Tau / Segments, b = (i + 1) * Math.Tau / Segments;
            if ((Math.Sin((a + b) / 2) < 0) != front) continue;
            WallSegment(a, b, i, _geometry.OuterBarricadeOffset, wall.HeightMetres, wall.ThicknessMetres);
        }
    }

    private void WallSegment(double a, double b, int segment, double offset, double height, double thickness)
    {
        Vec2 wa = _geometry.OvalPoint(a, offset), wb = _geometry.OvalPoint(b, offset);
        Vec2 xa = _geometry.OvalPoint(a, offset + thickness), xb = _geometry.OvalPoint(b, offset + thickness);
        Face(new("66786e"), new(xa), new(xb), new(xb, height), new(xa, height));
        Face(new("d5d9ce"), new(wa), new(wb), new(wb, height), new(wa, height));
        Face(new("f0efdf"), new(wa, height), new(wb, height), new(xb, height), new(xa, height));
        if (segment % 6 == 0) _building.Add(new([new(wa), new(wa, height)], new("a2aca0"), 1));
    }
    // The meshes are owned here; the imported sky texture is shared through Godot's resource cache.
    public void Dispose() { _background.Dispose(); _foreground.Dispose(); }
}
