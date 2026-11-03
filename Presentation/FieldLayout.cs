using Cricket.Physics;

namespace Cricket2D.Presentation;

/// <summary>Scenery projection; at zero depth it matches the physical ball/actor plane exactly.</summary>
public static class FieldLayout
{
    public const double BatterReferenceX = PitchDimensions.BatterReferenceX;
    public const double HorizonY = 500;
    public const double FocalPixels = 800;
    public static double BallHeight(Vec2 position) => Math.Max(0, position.Y);
    public static double Scale(FieldCamera camera, double depth) =>
        FocalPixels / (FocalPixels / camera.PixelsPerMetre + depth);
    public static Vec2 Project(FieldCamera camera, Vec2 ground, double height = 0)
    {
        double scale = Scale(camera, ground.Y), fraction = scale / camera.PixelsPerMetre;
        return new(720 + (camera.Project(new(ground.X, 0)).X - 720) * fraction,
            HorizonY + (FieldCamera.GroundY - HorizonY) * fraction - height * scale);
    }
    public static double DepthAtScreenY(FieldCamera camera, double y) =>
        FocalPixels / camera.PixelsPerMetre * ((FieldCamera.GroundY - HorizonY) / (y - HorizonY) - 1);
}
