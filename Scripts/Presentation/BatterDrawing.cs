using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>The existing character artwork, shared by the match and close-up preview.</summary>
public static class BatterDrawing
{
    private static readonly Color Skin = new("bc8a68"), White = new("e2e9d9"),
        ShadowWhite = new("9caeaa"), BatWood = new("d4b781");
    public static void Draw(CanvasItem canvas, HumanPose p, SimulationSettings settings, Func<Vec2, Vector2> W, float PixelsPerMetre, bool Diagnostics)
    {
        float Zoom = PixelsPerMetre / (float)FieldCamera.BasePixelsPerMetre;
        // Render the same bones that position the physical bat, without stretching either arm.
        DrawingPrimitives.Ellipse(canvas, W(new(FieldLayout.BatterReferenceX, 0.015)), new Vector2(29, 4) * Zoom, new Color(0, 0, 0, 0.20f));
        Limb(p.Hip, p.BackKnee, ShadowWhite, 0.135);
        Limb(p.BackKnee, p.BackAnkle, ShadowWhite, 0.105);
        Shoe(p.RearFoot, new("aebeb5"));
        Limb(p.RearShoulder, p.RearElbow, ShadowWhite, 0.095);
        Limb(p.RearElbow, p.Hands, Skin.Darkened(0.15f), 0.065);
        Limb(p.Hip, p.FrontKnee, White, 0.145);
        Limb(p.FrontKnee, p.FrontAnkle, White, 0.105);
        // Pads follow the shin and preserve a recognizable cricketer silhouette.
        Vec2 padTop = Vec2.Lerp(p.FrontKnee, p.FrontAnkle, 0.08) + new Vec2(-0.035, 0);
        Vec2 padBottom = Vec2.Lerp(p.FrontKnee, p.FrontAnkle, 0.86) + new Vec2(-0.035, 0);
        Limb(padTop, padBottom, new("f0efdc"), 0.15);
        for (int i = 1; i < 5; i++)
        {
            Vector2 mark = W(Vec2.Lerp(padTop, padBottom, i / 5.0));
            canvas.DrawLine(mark + new Vector2(-4, 0) * Zoom, mark + new Vector2(4, 0) * Zoom, new("c1c9b5"), Math.Max(.5f, Zoom), true);
        }
        Shoe(p.FrontFoot, White);
        Limb(p.Hip + new Vec2(0, 0.03), p.Shoulder - new Vec2(0, 0.02), White, 0.24);
        Limb(p.Hip + new Vec2(0.055, 0.05), p.Shoulder + new Vec2(0.055, -0.05), ShadowWhite, 0.045);
        canvas.DrawLine(W(p.Hip + new Vec2(-0.11, 0.03)), W(p.Hip + new Vec2(0.08, 0.03)), new("37545a"), 2);
        Limb(p.Shoulder, H(new Vec2(0, -0.07)), Skin, 0.08);
        canvas.DrawCircle(W(p.Head), 0.112f * PixelsPerMetre, Skin);
        canvas.DrawCircle(W(H(new Vec2(0.015, 0.033))), 0.125f * PixelsPerMetre, new("274752"));
        canvas.DrawLine(W(H(new Vec2(-0.15, 0.02))), W(H(new Vec2(0.05, 0.02))), new("7b9999"), 2, true);
        canvas.DrawLine(W(H(new Vec2(-0.13, 0))), W(H(new Vec2(-0.10, -0.12))), new("a1b9b3"), 1, true);
        canvas.DrawLine(W(H(new Vec2(-0.13, -0.08))), W(H(new Vec2(0.03, -0.10))), new("a1b9b3"), 1, true);
        Limb(p.Shoulder, p.LeadElbow, White, 0.105);
        Limb(Vec2.Lerp(p.Shoulder, p.LeadElbow, 0.7), p.LeadElbow, Skin, 0.075);
        Limb(p.LeadElbow, p.Hands, Skin, 0.065);
        Bat(p.Bat);
        Limb(p.Hands - p.Bat.Direction * 0.035, p.Hands + p.Bat.Direction * 0.075, White, 0.085);
        if (Diagnostics)
        {
            foreach (Vec2 joint in new[] { p.Hip, p.Shoulder, p.LeadElbow, p.RearElbow, p.Hands, p.FrontKnee, p.BackKnee, p.FrontAnkle, p.BackAnkle, p.Head, p.FrontFoot.Toe, p.RearFoot.Toe })
                canvas.DrawCircle(W(joint), 2, VisualTheme.Coral);
        }
        Vec2 H(Vec2 offset) => p.Head + FootPose.Rotate(offset, p.HeadAngle);
        void Bat(BatPose bat)
        {
            Capsule blade = bat.Blade(settings);
            Limb(bat.Grip - bat.Direction * 0.04, blade.A, new("3b5554"), 0.032);
            Limb(blade.A, blade.B, BatWood, settings.BatHalfThickness * 2);
            canvas.DrawLine(W(blade.A), W(blade.B), new("ecdbb3"), 1, true);
            Limb(Vec2.Lerp(blade.A, blade.B, 0.12), Vec2.Lerp(blade.A, blade.B, 0.34), new("557c70"), 0.052);
        }

        void Limb(Vec2 a, Vec2 b, Color color, double width)
        {
            float pixels = (float)width * PixelsPerMetre;
            canvas.DrawLine(W(a), W(b), color, pixels, true);
            canvas.DrawCircle(W(a), pixels / 2, color);
            canvas.DrawCircle(W(b), pixels / 2, color);
        }
        void Shoe(FootPose foot, Color color) => Limb(foot.Toe, foot.Heel, color, .075);
    }
}
