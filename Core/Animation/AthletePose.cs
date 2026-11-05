using System.Numerics;

namespace Cricket.Physics;

/// <summary>Allocation-free anatomical joints in metres, X toward the bowler and Z toward the camera.</summary>
public readonly record struct AthletePose(
    Vector3 Hip, Vector3 Spine, Vector3 Chest, Vector3 ShoulderCentre, Vector3 Neck,
    Vector3 TopShoulder, Vector3 TopElbow, Vector3 TopGrip,
    Vector3 BottomShoulder, Vector3 BottomElbow, Vector3 BottomGrip,
    Vector3 FrontHip, Vector3 FrontKnee, Vector3 FrontAnkle, Vector3 FrontToe,
    Vector3 RearHip, Vector3 RearKnee, Vector3 RearAnkle, Vector3 RearToe);

internal static class AthleteMath
{
    public static Vector3 Turn(Vector3 p, double yaw)
    {
        float c = (float)Math.Cos(yaw), s = (float)Math.Sin(yaw);
        return new(p.X*c+p.Z*s,p.Y,-p.X*s+p.Z*c);
    }

    public static Vector3 Joint(Vector3 root, Vector3 target, float upper, float lower, Vector3 guide)
    {
        Vector3 axis = Vector3.Normalize(target-root);
        float distance = Math.Clamp(Vector3.Distance(root,target),Math.Abs(upper-lower)+.0001f,upper+lower-.0001f);
        float along = (upper*upper-lower*lower+distance*distance)/(2*distance);
        Vector3 pole = guide-root;
        Vector3 bend = pole-axis*Vector3.Dot(pole,axis);
        if (bend.LengthSquared()<1e-8f) bend=Vector3.Cross(axis,Vector3.UnitX);
        return root+axis*along+Vector3.Normalize(bend)*MathF.Sqrt(Math.Max(0,upper*upper-along*along));
    }
}
