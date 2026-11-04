using Godot;
using System.Runtime.InteropServices;

namespace Cricket2D;

/// <summary>Retained triangles sharing one atlas for solid colours and spectator sprites.</summary>
internal sealed class FieldMesh : IDisposable
{
    private readonly List<Vector2> _vertices = new(32768);
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly record struct Attribute(uint Colour, Vector2 Uv);
    private readonly List<Attribute> _attributes = new(32768);
    private readonly List<int> _indices = new(65536);
    private readonly ArrayMesh _mesh = new();
    private int[] _indexBuffer = [];
    private int _vertexCapacity;
    private readonly Texture2D _texture = GD.Load<Texture2D>("res://Assets/Field/crowd.png");
    private static readonly Vector2 White = new(1016 / 1024f, 1272 / 1280f);
    private static readonly Vector2[] Circle = Enumerable.Range(0, 32)
        .Select(i => Vector2.FromAngle(i * MathF.Tau / 32)).ToArray();

    private static uint Packed(Color colour) => (uint)colour.R8 | (uint)colour.G8 << 8 | (uint)colour.B8 << 16 | (uint)colour.A8 << 24;

    public void Clear() { _vertices.Clear(); _attributes.Clear(); _indices.Clear(); }
    public void Polygon(Color colour, ReadOnlySpan<Vector2> points)
    {
        if (points.Length < 3) return;
        float minX = points[0].X, maxX = minX, minY = points[0].Y, maxY = minY;
        foreach (var point in points)
        {
            minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
        }
        if (maxX < -30 || minX > 1470 || maxY < 0 || minY > 960) return;
        int start = _vertices.Count;
        var attribute = new Attribute(Packed(colour), White);
        foreach (var point in points) { _vertices.Add(point); _attributes.Add(attribute); }
        for (int i = 1; i < points.Length - 1; i++)
        {
            if (Math.Abs((points[i] - points[0]).Cross(points[i + 1] - points[0])) < .00001f) continue;
            _indices.Add(start); _indices.Add(start + i); _indices.Add(start + i + 1);
        }
    }

    // Caller culls the complete seat before submitting its one atlas quad.
    public void Sprite(Rect2 rectangle, Rect2 uv)
    {
        int start = _vertices.Count;
        _vertices.Add(rectangle.Position); _vertices.Add(new(rectangle.End.X, rectangle.Position.Y));
        _vertices.Add(rectangle.End); _vertices.Add(new(rectangle.Position.X, rectangle.End.Y));
        _attributes.Add(new(uint.MaxValue, uv.Position)); _attributes.Add(new(uint.MaxValue, new(uv.End.X, uv.Position.Y)));
        _attributes.Add(new(uint.MaxValue, uv.End)); _attributes.Add(new(uint.MaxValue, new(uv.Position.X, uv.End.Y)));
        _indices.Add(start); _indices.Add(start + 1); _indices.Add(start + 2);
        _indices.Add(start); _indices.Add(start + 2); _indices.Add(start + 3);
    }

    public void Gradient(float top, Color topColour, Color bottomColour)
    {
        int start = _vertices.Count;
        _vertices.AddRange([new(0, top), new(1440, top), new(1440, 900), new(0, 900)]);
        var topAttribute = new Attribute(Packed(topColour), White);
        var bottomAttribute = new Attribute(Packed(bottomColour), White);
        _attributes.Add(topAttribute); _attributes.Add(topAttribute); _attributes.Add(bottomAttribute); _attributes.Add(bottomAttribute);
        _indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
    }

    public void Line(Vector2 a, Vector2 b, Color colour, float width = 1)
    {
        if (Math.Max(a.X, b.X) < -30 || Math.Min(a.X, b.X) > 1470 ||
            Math.Max(a.Y, b.Y) < 0 || Math.Min(a.Y, b.Y) > 960) return;
        Vector2 difference = b - a;
        if (difference.LengthSquared() < .000001f) return;
        Vector2 normal = new Vector2(-difference.Y, difference.X).Normalized();
        Vector2 half = normal * (width * .5f);
        Polygon(colour, [a - half, b - half, b + half, a + half]);
        Vector2 fringe = normal * .35f;
        FadeQuad(a - half, b - half, b - half - fringe, a - half - fringe, colour);
        FadeQuad(b + half, a + half, a + half + fringe, b + half + fringe, colour);
    }

    private void FadeQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colour)
    {
        int start = _vertices.Count;
        _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
        var solid = new Attribute(Packed(colour), White);
        var transparent = new Attribute(solid.Colour & 0x00ffffff, White);
        _attributes.Add(solid); _attributes.Add(solid); _attributes.Add(transparent); _attributes.Add(transparent);
        _indices.Add(start); _indices.Add(start + 1); _indices.Add(start + 2);
        _indices.Add(start); _indices.Add(start + 2); _indices.Add(start + 3);
    }

    public void Disc(Vector2 centre, float radius, Color colour)
    {
        if (centre.X + radius < -30 || centre.X - radius > 1470 || centre.Y - radius > 960) return;
        Span<Vector2> points = stackalloc Vector2[32];
        for (int i = 0; i < points.Length; i++) points[i] = centre + Circle[i] * radius;
        Polygon(colour, points);
    }

    public void Upload()
    {
        if (_vertices.Count > _vertexCapacity || _indices.Count > _indexBuffer.Length)
        {
            // Keep a fixed topology budget. Unused index slots form zero-area triangles.
            // More than 65,536 vertices selects 32-bit indices on every backend.
            _vertexCapacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(65537, _vertices.Count));
            int triangles = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(32768, (_indices.Count + 2) / 3));
            _indexBuffer = new int[triangles * 3];
            _mesh.ClearSurfaces();
            using var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = new Vector2[_vertexCapacity];
            arrays[(int)Mesh.ArrayType.Color] = new Color[_vertexCapacity];
            arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[_vertexCapacity];
            arrays[(int)Mesh.ArrayType.Index] = _indexBuffer;
            _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays,
                flags: Mesh.ArrayFormat.FlagUseDynamicUpdate);
            _mesh.CustomAabb = new(new(-32, -32, -1), new(1504, 1024, 2));
            var format = (RenderingServer.ArrayFormat)_mesh.SurfaceGetFormat(0);
            if (RenderingServer.MeshSurfaceGetFormatVertexStride(format, _vertexCapacity) != 8 ||
                RenderingServer.MeshSurfaceGetFormatAttributeStride(format, _vertexCapacity) != 12 ||
                RenderingServer.MeshSurfaceGetFormatIndexStride(format, _vertexCapacity) != 4)
                throw new InvalidOperationException($"Unexpected 2D field mesh buffer layout: vertex={RenderingServer.MeshSurfaceGetFormatVertexStride(format, _vertexCapacity)}, attributes={RenderingServer.MeshSurfaceGetFormatAttributeStride(format, _vertexCapacity)}, index={RenderingServer.MeshSurfaceGetFormatIndexStride(format, _vertexCapacity)}.");
        }
        if (_vertexCapacity == 0) return;
        _indices.CopyTo(_indexBuffer);
        Array.Clear(_indexBuffer, _indices.Count, _indexBuffer.Length - _indices.Count);
        if (_vertices.Count > 0)
        {
            _mesh.SurfaceUpdateVertexRegion(0, 0, MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(_vertices)));
            _mesh.SurfaceUpdateAttributeRegion(0, 0, MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(_attributes)));
        }
        RenderingServer.MeshSurfaceUpdateIndexRegion(_mesh.GetRid(), 0, 0, MemoryMarshal.AsBytes(_indexBuffer.AsSpan()));
    }
    public void Draw(CanvasItem canvas)
    {
        if (_mesh.GetSurfaceCount() != 0) canvas.DrawMesh(_mesh, _texture);
    }
    public void Dispose() => _mesh.Dispose();
}
