using Godot;
using System;
using System.Collections.Generic;

public partial class NpcController : CharacterBody3D
{
    [Export] public PackedScene ModelResource;
    [Export] public Shape3D OverrideShape;          // optional per-NPC collision shape
    [Export] public string DisplayName = "Stranger";
    [Export] public bool IsDead = false;

    private NpcEyeTracker _eyeTracker;
    private Area3D _visionArea;

    // Limb definitions for combat colliders
    private static readonly (string limbName, string boneName, Shape3D shape)[] LimbColliders = new (string, string, Shape3D)[]
    {
        ("Head",      "mixamorig_Head",        new SphereShape3D   { Radius = 0.1f }),   // offset applied separately
        ("Torso",     "mixamorig_Spine1",      new CapsuleShape3D  { Radius = 0.13f, Height = 0.6f }),
        ("LeftArm",   "mixamorig_LeftForeArm", new CapsuleShape3D  { Radius = 0.04f, Height = 0.5f }),
        ("LeftArm",   "mixamorig_LeftHand",    new CapsuleShape3D  { Radius = 0.03f, Height = 0.34f }), // much longer hand
        ("RightArm",  "mixamorig_RightForeArm",new CapsuleShape3D  { Radius = 0.04f, Height = 0.5f }),
        ("RightArm",  "mixamorig_RightHand",   new CapsuleShape3D  { Radius = 0.03f, Height = 0.34f }),
        ("LeftLeg",   "mixamorig_LeftUpLeg",     new CapsuleShape3D  { Radius = 0.06f, Height = 0.35f }),
        ("LeftLeg",   "mixamorig_LeftLeg",     new CapsuleShape3D  { Radius = 0.06f, Height = 0.45f }),
        ("LeftLeg",   "mixamorig_LeftFoot",    new CapsuleShape3D  { Radius = 0.04f, Height = 0.3f }),
        ("RightLeg",  "mixamorig_RightUpLeg",    new CapsuleShape3D  { Radius = 0.06f, Height = 0.35f }),
        ("RightLeg",  "mixamorig_RightLeg",    new CapsuleShape3D  { Radius = 0.06f, Height = 0.45f }),
        ("RightLeg",  "mixamorig_RightFoot",   new CapsuleShape3D  { Radius = 0.04f, Height = 0.3f })
    };

    // Maps limb names (used in combat) to the node names of imported split meshes.
    public static readonly Dictionary<string, string> LimbMeshNames = new()
    {
        { "Head",     "head" },
        { "Torso",    "torso" },
        { "LeftArm",  "left arm" },
        { "RightArm", "right arm" },
        { "LeftLeg",  "left leg" },
        { "RightLeg", "right leg" }
    };

    public override void _Ready()
    {
        Node3D model = null;

        // --- Load and attach the model ---
        if (ModelResource != null)
        {
            model = ModelResource.Instantiate<Node3D>();
            var modelRoot = GetNodeOrNull<Node3D>("ModelRoot");
            if (modelRoot != null)
            {
                modelRoot.AddChild(model);
                // Play the idle animation
                var animPlayer = model.FindChild("AnimationPlayer", recursive: true) as AnimationPlayer;
                if (animPlayer != null)
                {
                    if (animPlayer.HasAnimation("idle"))
                        animPlayer.Play("idle");
                    else
                        GD.Print($"AnimationPlayer found but no 'idle' animation. Available: {animPlayer.GetAnimationList()}");
                }
            }
            else
                GD.PrintErr("NpcController: missing ModelRoot child");
        }

        // --- Apply optional collision shape override ---
        if (OverrideShape != null)
        {
            var bodyShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (bodyShape != null)
                bodyShape.Shape = OverrideShape;
        }

        // --- Wire the eye tracker (direct child) to the model's skeleton and face mesh ---
        _eyeTracker = GetNodeOrNull<NpcEyeTracker>("EyeTrackerComponent");
        if (_eyeTracker != null && model != null)
        {
            var skeleton = FindChildOfTypeRecursive<Skeleton3D>(model);
            var faceMesh = FindBestFaceMesh(model);
            if (skeleton != null)
            {
                _eyeTracker.CharacterSkeleton = skeleton;
                // Attach limb colliders for combat
                SetupLimbColliders(skeleton);
            }
            if (faceMesh != null) _eyeTracker.FaceMesh = faceMesh;
            var health = GetNodeOrNull<Health>("Health");
            if (health != null)
            {
                health.Died += () =>
                {
                    IsDead = true;
                    var interaction = GetNodeOrNull<NpcInteraction>("Interaction");
                    if (interaction != null)
                    {
                        interaction.IsDead = true;
                        HUD.Instance?.RefreshNpcTooltip(interaction);
                    }
                };
            }
        }

        // --- Fill the interaction tooltip component (direct child) ---
        var interaction = GetNodeOrNull<NpcInteraction>("Interaction");
        if (interaction != null)
        {
            interaction.NpcName = DisplayName;
            interaction.IsDead = IsDead;
        }

        // --- Vision area setup (unchanged) ---
        _visionArea = GetNodeOrNull<Area3D>("VisionArea");
        if (_visionArea == null)
            GD.PrintErr("NPC Brain: Cannot find VisionArea!");
        else
        {
            _visionArea.BodyEntered += OnBodyEntered;
            _visionArea.BodyExited += OnBodyExited;
        }
    }

    private void SetupLimbColliders(Skeleton3D skeleton)
    {
        foreach (var (limbName, boneName, shape) in LimbColliders)
        {
            int boneIdx = skeleton.FindBone(boneName);
            if (boneIdx == -1)
            {
                GD.PrintErr($"NpcController: Bone '{boneName}' not found for limb '{limbName}'");
                continue;
            }

            var attachment = new BoneAttachment3D();
            attachment.Name = $"{limbName}Collider_{boneName}";   // unique name
            attachment.BoneName = boneName;
            skeleton.AddChild(attachment);

            var area = new Area3D();
            area.Name = limbName;               // same limb name for multiple colliders
            area.CollisionLayer = 1 << 4;       // layer 5 = BodyParts
            area.CollisionMask = 0;
            area.Monitorable = true;
            area.Monitoring = false;
            attachment.AddChild(area);

            var collShape = new CollisionShape3D();
            collShape.Shape = shape;
            area.AddChild(collShape);
            if (limbName == "Head")
                collShape.Position = new Vector3(0, 0.05f, 0);
            if (limbName == "Torso")
                collShape.Position = new Vector3(0, -0.05f, 0);
            if (boneName == "mixamorig_LeftUpLeg")
                collShape.Position = new Vector3(0, 0.25f, 0);
            if (boneName == "mixamorig_RightUpLeg")
                collShape.Position = new Vector3(0, 0.25f, 0);
        }
    }

    // Helper to recursively find the first child of type T
    private T FindChildOfTypeRecursive<T>(Node node) where T : class
    {
        if (node is T t) return t;
        foreach (Node child in node.GetChildren())
        {
            var found = FindChildOfTypeRecursive<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("Player") && _eyeTracker != null)
            _eyeTracker.Target = body;
    }

    private void OnBodyExited(Node3D body)
    {
        if (body == _eyeTracker?.Target)
            _eyeTracker.Target = null;
    }

    private MeshInstance3D FindBestFaceMesh(Node startNode)
    {
        MeshInstance3D best = null;
        int bestSurfaces = -1;
        foreach (Node child in startNode.GetChildren())
        {
            if (child is MeshInstance3D mi)
            {
                int surfaces = mi.Mesh.GetSurfaceCount();
                if (surfaces > bestSurfaces)
                {
                    bestSurfaces = surfaces;
                    best = mi;
                }
            }
            var found = FindBestFaceMesh(child);
            if (found != null && found.Mesh.GetSurfaceCount() > bestSurfaces)
            {
                best = found;
                bestSurfaces = found.Mesh.GetSurfaceCount();
            }
        }
        return best;
    }
}