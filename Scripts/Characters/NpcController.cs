using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class NpcController : CharacterBody3D
{
    [Export] public PackedScene ModelResource;
    [Export] public Shape3D OverrideShape;
    [Export] public string DisplayName = "Stranger";
    [Export] public bool IsDead = false;

    public enum RigType { Mixamo, ARP_GLTB, Unknown }
    private RigType _detectedRig = RigType.Unknown;

    private NpcEyeTracker _eyeTracker;
    private Area3D _visionArea;

    // reaction control

    private CharacterBody3D _body;
    private NpcController _npcController;
    
    // Stumble state
    private bool _isStumbling = false;
    private Vector3 _stumbleVelocity;
    private float _stumbleTimer;

    // --- BONE NAME MAPS ---
    // ARP_GLTB = standard GLTF export names (what you actually see)
    private static readonly (string limb, string arpGltf, string mixamo, Shape3D shape)[] BoneMap = new (string, string, string, Shape3D)[]
    {
        ("Head",      "spine.006",           "mixamorig_Head",        new SphereShape3D   { Radius = 0.1f }),
        ("Torso",     "spine.003",           "mixamorig_Spine1",      new CapsuleShape3D  { Radius = 0.13f, Height = 0.6f }),
        ("LeftArm",   "forearm.L",           "mixamorig_LeftForeArm", new CapsuleShape3D  { Radius = 0.04f, Height = 0.5f }),
        ("LeftArm",   "hand.L",              "mixamorig_LeftHand",    new CapsuleShape3D  { Radius = 0.03f, Height = 0.34f }),
        ("RightArm",  "forearm.R",           "mixamorig_RightForeArm",new CapsuleShape3D  { Radius = 0.04f, Height = 0.5f }),
        ("RightArm",  "hand.R",              "mixamorig_RightHand",   new CapsuleShape3D  { Radius = 0.03f, Height = 0.34f }),
        ("LeftLeg",   "thigh.L",             "mixamorig_LeftUpLeg",   new CapsuleShape3D  { Radius = 0.06f, Height = 0.35f }),
        ("LeftLeg",   "shin.L",              "mixamorig_LeftLeg",     new CapsuleShape3D  { Radius = 0.06f, Height = 0.45f }),
        ("LeftLeg",   "foot.L",              "mixamorig_LeftFoot",    new CapsuleShape3D  { Radius = 0.04f, Height = 0.3f }),
        ("RightLeg",  "thigh.R",             "mixamorig_RightUpLeg",  new CapsuleShape3D  { Radius = 0.06f, Height = 0.35f }),
        ("RightLeg",  "shin.R",              "mixamorig_RightLeg",    new CapsuleShape3D  { Radius = 0.06f, Height = 0.45f }),
        ("RightLeg",  "foot.R",              "mixamorig_RightFoot",   new CapsuleShape3D  { Radius = 0.04f, Height = 0.3f })
    };

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

        if (ModelResource != null)
        {
            model = ModelResource.Instantiate<Node3D>();

            var skeleton = FindChildOfTypeRecursive<Skeleton3D>(model);
            if (skeleton != null)
            {
                _detectedRig = DetectRigType(skeleton);
                GD.Print($"NpcController: Detected rig type = {_detectedRig}");

                var simulator = skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator3D")
                    ?? skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator");
                if (simulator != null)
                {
                    simulator.Active = false;
                    simulator.PhysicalBonesStopSimulation();
                }
            }

            var modelRoot = GetNodeOrNull<Node3D>("ModelRoot");
            if (modelRoot != null)
            {
                modelRoot.AddChild(model);
                var animPlayer = model.FindChild("AnimationPlayer", recursive: true) as AnimationPlayer;
                if (animPlayer != null && animPlayer.HasAnimation("idle"))
                    animPlayer.Play("idle");
            }
            else
                GD.PrintErr("NpcController: missing ModelRoot child");
        }

        if (OverrideShape != null)
        {
            var bodyShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (bodyShape != null) bodyShape.Shape = OverrideShape;
        }

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

        var interaction = GetNodeOrNull<NpcInteraction>("Interaction");
        if (interaction != null)
        {
            interaction.NpcName = DisplayName;
            interaction.IsDead = IsDead;
        }

        var health = GetNodeOrNull<Health>("Health");
        if (health != null)
        {
            Node3D capturedModel = model;
            health.Died += () =>
            {
                IsDead = true;

                var navAgent = GetNodeOrNull<NavigationAgent3D>("NavAgentNPC");
                if (navAgent != null)
                {
                    navAgent.MaxSpeed = 0f;
                    navAgent.AvoidanceEnabled = false;
                    navAgent.TargetPosition = GlobalPosition;
                }

                if (_eyeTracker != null)
                {
                    _eyeTracker.EnableHeadTracking = false;
                    _eyeTracker.EnableEyeTracking = false;
                    _eyeTracker.EnableBlinking = false;
                    _eyeTracker.Target = null;
                }

                var combat = GetNodeOrNull<Node>("NPCNavCombat");
                if (combat != null)
                    combat.SetProcess(false);

                if (interaction != null)
                {
                    interaction.IsDead = true;
                    HUD.Instance?.RefreshNpcTooltip(interaction);
                }

                if (capturedModel != null)
                {
                    var skeleton = FindChildOfTypeRecursive<Skeleton3D>(capturedModel);
                    if (skeleton != null)
                    {
                        var animPlayer = capturedModel.FindChild("AnimationPlayer", recursive: true) as AnimationPlayer;
                        if (animPlayer != null)
                            animPlayer.Active = false;

                        skeleton.ResetBonePoses();

                        var simulator = skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator3D")
                            ?? skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator");
                        if (simulator != null)
                        {
                            simulator.Active = true;
                            simulator.PhysicalBonesStartSimulation();
                        }
                    }
                }

                var modelRoot = GetNodeOrNull<Node3D>("ModelRoot");
                if (modelRoot != null)
                    OverrideAllMeshesGray(modelRoot);
            };
        }

        _visionArea = GetNodeOrNull<Area3D>("VisionArea");
        if (_visionArea != null)
        {
            _visionArea.BodyEntered += OnBodyEntered;
            _visionArea.BodyExited += OnBodyExited;
        }

        //reaction control

        _body = GetParent<CharacterBody3D>();
        _npcController = _body.GetNodeOrNull<NpcController>(".");
    }

    // --- RIG DETECTION ---
    private RigType DetectRigType(Skeleton3D skeleton)
    {
        var allNames = new List<string>();
        for (int i = 0; i < skeleton.GetBoneCount(); i++)
            allNames.Add(skeleton.GetBoneName(i));

        // ARP GLTF export: spine.001, spine.002, upper_arm.L, forearm.L, etc.
        bool hasArpGltf = allNames.Any(n => n.StartsWith("spine.") && n.Contains("."))
            && allNames.Any(n => n.EndsWith(".L") || n.EndsWith(".R"));

        // Mixamo
        bool hasMixamo = allNames.Any(n => n.Contains("mixamorig"));

        if (hasArpGltf && !hasMixamo) return RigType.ARP_GLTB;
        if (hasMixamo) return RigType.Mixamo;

        return RigType.Unknown;
    }

    // --- RESOLVE BONE NAME ---
    private string ResolveBoneName(string arpGltfName, string mixamoName)
    {
        return _detectedRig switch
        {
            RigType.ARP_GLTB => arpGltfName,
            RigType.Mixamo => mixamoName,
            _ => mixamoName
        };
    }

    private void SetupLimbColliders(Skeleton3D skeleton)
    {
        foreach (var (limbName, arpGltfBone, mixamoBone, shape) in BoneMap)
        {
            string boneName = ResolveBoneName(arpGltfBone, mixamoBone);
            int boneIdx = skeleton.FindBone(boneName);

            if (boneIdx == -1)
            {
                // Fuzzy fallback
                string searchTerm = _detectedRig == RigType.ARP_GLTB
                    ? arpGltfBone.Replace(".L", "").Replace(".R", "").Replace(".", "")
                    : mixamoBone.Replace("mixamorig_", "").Replace("Left", "").Replace("Right", "");
                
                for (int i = 0; i < skeleton.GetBoneCount(); i++)
                {
                    string candidate = skeleton.GetBoneName(i).ToLower();
                    if (candidate.Contains(searchTerm.ToLower()))
                    {
                        boneIdx = i;
                        boneName = skeleton.GetBoneName(i);
                        GD.Print($"NpcController: Fuzzy matched '{arpGltfBone}/{mixamoBone}' -> '{boneName}'");
                        break;
                    }
                }
            }

            if (boneIdx == -1)
            {
                GD.PrintErr($"NpcController: Bone not found for limb '{limbName}' (tried ARP_GLTB:'{arpGltfBone}', Mixamo:'{mixamoBone}')");
                continue;
            }

            var attachment = new BoneAttachment3D();
            attachment.Name = $"{limbName}Collider_{boneName}";
            attachment.BoneName = boneName;
            skeleton.AddChild(attachment);

            var area = new Area3D();
            area.Name = limbName;
            area.CollisionLayer = 1 << 4;
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
            if (boneName.ToLower().Contains("thigh") || boneName.ToLower().Contains("upleg"))
                collShape.Position = new Vector3(0, 0.25f, 0);
        }
    }

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


    public void ApplyHit(ItemData weapon, Vector3 direction, string limb)
    {
        if (_npcController.IsDead) return;

        float knockback = weapon.KnockbackForce ?? 5f;
        _stumbleVelocity = direction * knockback;
        
        if (weapon.ImpactType == null) return;
        switch (weapon.ImpactType.Value)
        {
            case ImpactType.Fist:
                _stumbleTimer = 0.3f;
                // Play anim: "stagger_back"
                break;
            case ImpactType.Pipe:
                _stumbleTimer = 0.6f;
                _stumbleVelocity += Vector3.Up * 2f; // Pop them up a bit
                // Play anim: "spin_fall"
                break;
            case ImpactType.Chair:
                _stumbleTimer = 1.0f;
                TriggerRagdoll(direction * knockback * 2f);
                return; // Skip normal stumble, go straight to physics
        }
        
        _isStumbling = true;
        // Interrupt AI
        var combat = _body.GetNodeOrNull<NpcNavCombat>("NPCNavCombat");
        if (combat != null) combat.SetStunned(_stumbleTimer);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isStumbling || _npcController.IsDead) return;

        float dt = (float)delta;
        _stumbleTimer -= dt;
        
        if (_stumbleTimer <= 0)
        {
            _isStumbling = false;
            return;
        }

        // Apply gravity
        _stumbleVelocity.Y -= 9.8f * dt;

        // Check for walls to lean on!
        var spaceState = _body.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(
            _body.GlobalPosition, 
            _body.GlobalPosition + (_stumbleVelocity.Normalized() * 0.5f),
            1 // Environment collision mask
        );
        var result = spaceState.IntersectRay(query);
        
        if (result.Count > 0)
        {
            // Hit a wall! Lean on it instead of sliding through
            _stumbleVelocity = Vector3.Zero;
            _stumbleTimer = Mathf.Max(_stumbleTimer, 1.0f); // Extend timer to "pin" them to wall
            // Play anim: "wall_lean"
            GD.Print("NPC pinned to wall!");
        }

        _body.Velocity = _stumbleVelocity;
        _body.MoveAndSlide();
    }

    private void TriggerRagdoll(Vector3 impulse)
    {
        // Use your existing PhysicalBoneSimulator3D logic from NpcController
        // Apply impulse to the bone that was hit!
        _npcController.ActivateRagdoll(impulse);
    }

    public void ActivateRagdoll(Vector3 impulse)
    {
        if (IsDead) return;
        
        // Stop AI
        var navAgent = GetNodeOrNull<NavigationAgent3D>("NavAgentNPC");
        if (navAgent != null)
        {
            navAgent.MaxSpeed = 0f;
            navAgent.AvoidanceEnabled = false;
        }

        var combat = GetNodeOrNull<Node>("NPCNavCombat");
        if (combat != null) combat.SetProcess(false);

        // Stop animations
        var modelRoot = GetNodeOrNull<Node3D>("ModelRoot");
        if (modelRoot != null)
        {
            var model = modelRoot.GetChild(0);
            if (model != null)
            {
                var animPlayer = model.FindChild("AnimationPlayer", recursive: true) as AnimationPlayer;
                if (animPlayer != null) animPlayer.Active = false;

                var skeleton = FindChildOfTypeRecursive<Skeleton3D>(model);
                if (skeleton != null)
                {
                    skeleton.ResetBonePoses();

                    var simulator = skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator3D")
                        ?? skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator");
                    if (simulator != null)
                    {
                        simulator.Active = true;
                        simulator.PhysicalBonesStartSimulation();
                        
                        // Apply impulse to physical bones
                        foreach (var child in simulator.GetChildren())
                        {
                            if (child is PhysicalBone3D bone)
                            {
                                bone.ApplyCentralImpulse(impulse);
                            }
                        }
                    }
                }
            }
        }
    }
}