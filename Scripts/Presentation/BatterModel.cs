using Cricket.Physics;
using System.Text.Json;
using Godot;

namespace Cricket2D;

/// <summary>One small transparent 3D render target composited in the existing 2D view. Physics owns every bat transform.</summary>
public partial class BatterModel : Node
{
    public const float WorldSize = 3.2f;
    private SubViewport _viewport = null!;
    private Skeleton3D _skeleton = null!;
    private HumanPose _lastPose;
    private bool _hasPose;
    private readonly int[] _bones = new int[18];
    private float[] _lengths = [];
    private readonly Vector2[] _armRoll = new Vector2[18];
    private sealed record RigAsset(int Version, string[] Names, float[] Lengths);
    private static readonly string[] Names = ["pelvis", "spine", "chest", "neck", "head",
        "front_thigh", "front_shin", "front_foot", "rear_thigh", "rear_shin", "rear_foot",
        "top_upperarm", "top_forearm", "top_hand", "bottom_upperarm", "bottom_forearm", "bottom_hand", "bat"];

    public Texture2D Texture => _viewport.GetTexture();
    public bool Available => _skeleton != null;

    public override void _Ready()
    {
        _viewport = new SubViewport
        {
            Size = new(512, 512),
            TransparentBg = true,
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Msaa3D = Viewport.Msaa.Msaa2X,
            PositionalShadowAtlasSize = 0,
            PhysicsObjectPicking = false
        };
        AddChild(_viewport);
        // Headless tests exercise the core; no GPU resource import is required there.
        if (DisplayServer.GetName() == "headless") return;
        var scene = GD.Load<PackedScene>("res://Assets/Characters/batsman.glb");
        if (scene == null) throw new InvalidOperationException("The batsman GLB asset could not be loaded.");
        var model = scene.Instantiate<Node3D>();
        _viewport.AddChild(model);
        _skeleton = FindSkeleton(model) ?? throw new InvalidOperationException("Batsman GLB has no skeleton.");
        var asset = JsonSerializer.Deserialize<RigAsset>(Godot.FileAccess.GetFileAsString("res://Assets/Characters/batsman-rig.json"))
            ?? throw new InvalidOperationException("Missing batsman rig dimensions.");
        if (asset.Version != 2 || !asset.Names.SequenceEqual(Names) || asset.Lengths.Length != Names.Length)
            throw new InvalidOperationException("Batsman rig asset and runtime versions do not match.");
        _lengths = asset.Lengths;
        for (int i = 0; i < Names.Length; i++)
        {
            _bones[i] = _skeleton.FindBone(Names[i]);
            if (_bones[i] < 0 || _skeleton.GetBoneParent(_bones[i]) != -1)
                throw new InvalidOperationException($"Invalid independent pose bone: {Names[i]}");

        }
        CacheArmRoll(11);
        CacheArmRoll(14);
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = WorldSize,
            Position = new(0, 1.4f, 6),
            Current = true,
            Near = .1f,
            Far = 12
        };
        _viewport.AddChild(camera);
        var environment = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(.88f, .91f, 1),
                AmbientLightEnergy = .45f,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        };
        _viewport.AddChild(environment);
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new(-35, -35, 0), LightEnergy = 1.1f, ShadowEnabled = false });
        _viewport.AddChild(new DirectionalLight3D { RotationDegrees = new(-10, 145, 0), LightEnergy = .3f, ShadowEnabled = false });
    }

    public void UpdatePose(HumanPose p, SimulationSettings physics, float displayedPixelsPerMetre, float displayScale)
    {
        if (!Available) return;
        int desired = displayedPixelsPerMetre * displayScale * WorldSize > 620 ? 1024 :
            displayedPixelsPerMetre * displayScale * WorldSize > 300 ? 512 : 256;
        bool resized = _viewport.Size.X != desired;
        if (resized) _viewport.Size = new(desired, desired);
        if (_hasPose && _lastPose == p && !resized) return;
        _hasPose = true; _lastPose = p;
        var a = p.Anatomy;
        Vector3 torsoForward = new Basis(Vector3.Up, (float)p.TorsoTwist) * Vector3.Back;
        Vector3 pelvisForward = new Basis(Vector3.Up, (float)p.PelvisTwist) * Vector3.Back;
        Apply(0, V(a.Hip), V(a.Spine), pelvisForward);
        Apply(1, V(a.Spine), V(a.Chest), (pelvisForward + torsoForward).Normalized());
        Apply(2, V(a.Chest), V(a.ShoulderCentre), torsoForward);
        Apply(3, V(a.ShoulderCentre), V(a.Neck), new Basis(Vector3.Up, .65f) * Vector3.Back);
        Apply(4, V(a.Neck), V(a.Neck) + new Vector3((float)Math.Sin(p.HeadAngle), (float)Math.Cos(p.HeadAngle), 0) * _lengths[4],
            new Basis(Vector3.Up, 1.12f) * Vector3.Back);
        Legs(5, V(a.FrontHip), V(a.FrontKnee), V(a.FrontAnkle), V(a.FrontToe), p.FrontFoot);
        Legs(8, V(a.RearHip), V(a.RearKnee), V(a.RearAnkle), V(a.RearToe), p.RearFoot);
        Arm(11,V(a.TopShoulder),V(a.TopElbow),V(a.TopGrip));
        Arm(14,V(a.BottomShoulder),V(a.BottomElbow),V(a.BottomGrip));
        Vector3 direction = new(-(float)p.Bat.Direction.X, (float)p.Bat.Direction.Y, 0);
        Apply(13, V(a.TopGrip), V(a.TopGrip) + direction * _lengths[13], V(a.TopElbow - a.TopGrip));
        Apply(16, V(a.BottomGrip), V(a.BottomGrip) + direction * _lengths[16], V(a.BottomElbow - a.BottomGrip));
        var face=p.Bat.FaceNormal;
        Apply(17, V(a.TopGrip), V(a.TopGrip) + direction * (float)physics.BatLength,
            new Vector3(-(float)face.X,(float)face.Y,0));
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

        void Legs(int index, Vector3 hip, Vector3 knee, Vector3 ankle, Vector3 toe, FootPose foot)
        {
            Vector3 forward = new Basis(Vector3.Up, (float)foot.Yaw) * Vector3.Back;
            Apply(index, hip, knee, forward);
            Apply(index + 1, knee, ankle, forward);
            Apply(index + 2, ankle, toe, Vector3.Up);
        }
    }

    public void Draw(CanvasItem canvas, Vector2 groundOrigin, float pixelsPerMetre)
    {
        if (Available) canvas.DrawTextureRect(Texture, new Rect2(groundOrigin + new Vector2(-WorldSize / 2, -3f) * pixelsPerMetre,
            Vector2.One * WorldSize * pixelsPerMetre), false);
    }
    private void CacheArmRoll(int index)
    {
        var upper=_skeleton.GetBoneRest(_bones[index]);
        var lower=_skeleton.GetBoneRest(_bones[index+1]);
        Vector3 wrist=_skeleton.GetBoneRest(_bones[index+2]).Origin;
        Vector3 normal=(lower.Origin-upper.Origin).Cross(wrist-lower.Origin).Normalized();
        for(int bone=index;bone<=index+1;bone++)
        {
            var rest=_skeleton.GetBoneRest(_bones[bone]).Basis.Orthonormalized();
            _armRoll[bone]=new(rest.Z.Dot(normal),rest.Z.Dot(normal.Cross(rest.Y)));
        }
    }

    private void Arm(int index,Vector3 shoulder,Vector3 elbow,Vector3 wrist)
    {
        Vector3 upper=elbow-shoulder,lower=wrist-elbow;
        Vector3 normal=upper.Cross(lower).Normalized();
        // Preserve the anatomical bend plane. Projecting a fixed world-forward vector onto
        // the arm axis can flip the skin's roll as an arm points toward the camera.
        Apply(index,shoulder,elbow,Forward(index,upper));
        Apply(index+1,elbow,wrist,Forward(index+1,lower));
        Vector3 Forward(int bone,Vector3 axis) =>
            normal*_armRoll[bone].X+normal.Cross(axis.Normalized())*_armRoll[bone].Y;
    }

    private void Apply(int index, Vector3 a, Vector3 b, Vector3 forward)
    {
        Vector3 axis = b - a, y = axis.Normalized();
        Vector3 z = forward - y * forward.Dot(y);
        if (z.LengthSquared() < .00001f) z = Vector3.Right - y * y.X;
        z = z.Normalized();
        var basis = new Basis(y.Cross(z), y * (axis.Length() / _lengths[index]), z);
        _skeleton.SetBonePose(_bones[index], new Transform3D(basis, a));
    }
    private static Vector3 V(System.Numerics.Vector3 p) => new(p.X, p.Y, p.Z);

    // Offline capture only: the live path never reads bone transforms back or allocates these arrays.
    internal float[][] CaptureBones()
    {
        var result = new float[_bones.Length][];
        for (int i = 0; i < _bones.Length; i++)
        {
            var t = _skeleton.GetBonePose(_bones[i]);
            result[i] = [t.Basis.X.X,t.Basis.X.Y,t.Basis.X.Z,t.Basis.Y.X,t.Basis.Y.Y,t.Basis.Y.Z,
                t.Basis.Z.X,t.Basis.Z.Y,t.Basis.Z.Z,t.Origin.X,t.Origin.Y,t.Origin.Z];
        }
        return result;
    }

    internal void ValidateAnatomy()
    {
        // Read the resolved skeleton, not only requested local poses: the rendered bat must agree with physics.
        var bat=_skeleton.GetBoneGlobalPose(_bones[17]);
        Vector3 expectedGrip=V(_lastPose.Anatomy.TopGrip);
        Vector3 expectedDirection=new(-(float)_lastPose.Bat.Direction.X,(float)_lastPose.Bat.Direction.Y,0);
        if(bat.Origin.DistanceTo(expectedGrip)>.0001f || bat.Basis.Y.Normalized().DistanceTo(expectedDirection)>.0001f)
            throw new InvalidOperationException("Rendered bat bone does not match the current physical pose.");
        for(int i=0;i<_bones.Length;i++)
        {
            float determinant=_skeleton.GetBonePose(_bones[i]).Basis.Determinant();
            if(!float.IsFinite(determinant) || determinant<=0)
                throw new InvalidOperationException($"Invalid or mirrored pose basis: {Names[i]}");
        }
        foreach (int index in new[] { 5, 6, 8, 9, 11, 12, 14, 15 })
        {
            float expected = index is 5 or 8 ? .45f : index is 6 or 9 ? .43f : index is 11 or 14 ? .32f : .29f;
            float actual = _skeleton.GetBonePose(_bones[index]).Basis.Y.Length() * _lengths[index];
            if (!float.IsFinite(actual) || Math.Abs(actual - expected) > .001f)
                throw new InvalidOperationException($"3D {Names[index]} stretched to {actual:0.0000} m (expected {expected}).");
        }
    }
    private static Skeleton3D? FindSkeleton(Node node)
    {
        if (node is Skeleton3D skeleton) return skeleton;
        foreach (var child in node.GetChildren()) { var found = FindSkeleton(child); if (found != null) return found; }
        return null;
    }
}
