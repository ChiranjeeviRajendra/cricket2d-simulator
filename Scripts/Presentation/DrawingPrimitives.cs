using Godot;

namespace Cricket2D;

/// <summary>Main-thread drawing helpers; Godot copies submitted vertices into its draw commands.</summary>
internal static class DrawingPrimitives
{
    private static readonly Vector2[] Circle = Enumerable.Range(0, 24)
        .Select(i => Vector2.FromAngle(i * MathF.Tau / 24)).ToArray();
    private static readonly Vector2[] Points = new Vector2[24];

    public static void Ellipse(CanvasItem canvas, Vector2 centre, Vector2 radius, Color colour)
    {
        for (int i = 0; i < Points.Length; i++) Points[i] = centre + Circle[i] * radius;
        canvas.DrawColoredPolygon(Points, colour);
    }
}
