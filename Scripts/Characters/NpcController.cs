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
        if (!IsInGroup("NPC"))
            AddToGroup("NPC");

        Node3D model = null;

        // Load and attach the model
        if (ModelResource != null)
        {
            model = ModelResource.Instantiate<Node3D>();

            // --- prevent ragdoll on spawn ---
            var skeleton = FindChildOfTypeRecursive<Skeleton3D>(model);
            if (skeleton != null)
            {
                var simulator = skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator3D");
                if (simulator == null)
                    simulator = skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator"); // fallback name
                if (simulator != null)
                {
                    simulator.Active = false;                  // ensure simulator is off
                    simulator.PhysicalBonesStopSimulation();   // also stop any lingering simulation
                }
            }

            var modelRoot = GetNodeOrNull<Node3D>("ModelRoot");
            if (modelRoot != null)
            {
                modelRoot.AddChild(model);
                var animPlayer = model.FindChild("AnimationPlayer", recursive: true) as AnimationPlayer;
                if (animPlayer != null)
                {
                    if (animPlayer.HasAnimation("idle"))
                        animPlayer.Play("idle");
                }
            }
            else
                GD.PrintErr("NpcController: missing ModelRoot child");
        }

        // Apply optional collision shape override
        if (OverrideShape != null)
        {
            var bodyShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (bodyShape != null) bodyShape.Shape = OverrideShape;
        }

        // Wire eye tracker
        _eyeTracker = GetNodeOrNull<NpcEyeTracker>("EyeTrackerComponent");
        if (_eyeTracker != null && model != null)
        {
            var skeleton = FindChildOfTypeRecursive<Skeleton3D>(model);
            var faceMesh = FindBestFaceMesh(model);
            if (skeleton != null)
            {
                _eyeTracker.CharacterSkeleton = skeleton;
                SetupLimbColliders(skeleton);
            }
            if (faceMesh != null) _eyeTracker.FaceMesh = faceMesh;
        }

        // Interaction tooltip
        var interaction = GetNodeOrNull<NpcInteraction>("Interaction");
        if (interaction != null)
        {
            interaction.NpcName = DisplayName;
            interaction.IsDead = IsDead;
        }

        // Health – death logic
        var health = GetNodeOrNull<Health>("Health");
        if (health != null)
        {
            // Capture model explicitly for the lambda
            Node3D capturedModel = model;
            health.Died += () =>
            {
                IsDead = true;

                // Freeze navigation
                var navAgent = GetNodeOrNull<NavigationAgent3D>("NavAgentNPC");
                if (navAgent != null)
                {
                    navAgent.MaxSpeed = 0f;
                    navAgent.AvoidanceEnabled = false;
                    navAgent.TargetPosition = GlobalPosition;
                }

                // Disable eye tracker
                if (_eyeTracker != null)
                {
                    _eyeTracker.EnableHeadTracking = false;
                    _eyeTracker.EnableEyeTracking = false;
                    _eyeTracker.EnableBlinking = false;
                    _eyeTracker.Target = null;
                }

                // Disable combat AI
                var combat = GetNodeOrNull<Node>("NPCNavCombat");
                if (combat != null)
                    combat.SetProcess(false);

                // Update tooltip
                if (interaction != null)
                {
                    interaction.IsDead = true;
                    HUD.Instance?.RefreshNpcTooltip(interaction);
                }

                // --- RAGDOLL ACTIVATION ---
                if (capturedModel != null)
                {
                    var skeleton = FindChildOfTypeRecursive<Skeleton3D>(capturedModel);
                    if (skeleton != null)
                    {
                        // Stop any animation – important!
                        var animPlayer = capturedModel.FindChild("AnimationPlayer", recursive: true) as AnimationPlayer;
                        if (animPlayer != null)
                            animPlayer.Active = false;

                        // Release any bone pose override from animation blending
                        skeleton.ResetBonePoses();

                        var simulator = skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator3D");
                        if (simulator == null)
                            simulator = skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator");
                        if (simulator != null)
                        {
                            simulator.Active = true;          // enable the simulator
                            simulator.PhysicalBonesStartSimulation();
                        }
                    }
                }

                // Apply gray material AFTER starting ragdoll so it's visible
                var modelRoot = GetNodeOrNull<Node3D>("ModelRoot");
                if (modelRoot != null)
                    OverrideAllMeshesGray(modelRoot);
            };
        }

        // Vision area
        _visionArea = GetNodeOrNull<Area3D>("VisionArea");
        if (_visionArea != null)
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

    // Helper to turn every MeshInstance3D gray
    private void OverrideAllMeshesGray(Node3D root)
    {
        var grayMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.3f, 0.3f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        foreach (var mi in FindAllMeshes(root))
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                mi.SetSurfaceOverrideMaterial(i, grayMat);
    }

    private static List<MeshInstance3D> FindAllMeshes(Node node, List<MeshInstance3D> list = null)
    {
        list ??= new List<MeshInstance3D>();
        if (node is MeshInstance3D mi) list.Add(mi);
        foreach (Node child in node.GetChildren())
            FindAllMeshes(child, list);
        return list;
    }
}