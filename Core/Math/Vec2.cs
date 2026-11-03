namespace Cricket.Physics;

/// <summary>Double precision, right/up coordinates. No rendering units enter the core.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);
    public double LengthSquared => X * X + Y * Y;
    public double Length => Math.Sqrt(LengthSquared);
    public Vec2 Normalized => Length > 1e-12 ? this / Length : new(0, 1);
    public Vec2 Perpendicular => new(-Y, X);
    // Record-generated formatting would recursively print the Vec2-valued properties.
    public override string ToString() => FormattableString.Invariant($"({X:G6}, {Y:G6})");
    public static Vec2 FromAngle(double radians) => new(Math.Cos(radians), Math.Sin(radians));
    public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
    public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => a + (b - a) * t;
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double b) => new(a.X * b, a.Y * b);
    public static Vec2 operator *(double a, Vec2 b) => b * a;
    public static Vec2 operator /(Vec2 a, double b) => new(a.X / b, a.Y / b);
}
