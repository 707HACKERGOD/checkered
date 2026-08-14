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

    // Stumble state
    private bool _isStumbling = false;
    private Vector3 _stumbleVelocity;
    private float _stumbleTimer;
    public bool IsStumbling => _isStumbling;

    // --- BONE NAME MAPS ---
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

    [Signal] public delegate void HitReceivedEventHandler(float damage, string limb);

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
                
                // Setup the hitbox on the right hand
                SetupDynamicHitbox(skeleton);

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
                StripBlendShapeTracks(animPlayer);
                if (animPlayer != null && animPlayer.HasAnimation("idle"))
                    animPlayer.Play("idle");
            }
            else
            {
                GD.PrintErr("NpcController: missing ModelRoot child");
            }
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
                _isStumbling = false; // Stop any active stumble

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
                        if (animPlayer != null) animPlayer.Active = false;

                        skeleton.ResetBonePoses();

                        var simulator = skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator3D")
                            ?? skeleton.GetNodeOrNull<PhysicalBoneSimulator3D>("PhysicalBoneSimulator");
                        if (simulator != null)
                        {
                            // Ensure physical bones exist
                            if (simulator.GetChildCount() == 0)
                            {
                                GD.PrintErr("NpcController: No PhysicalBone3D nodes found! Ragdoll requires bones in editor or SetupRagdoll().");
                            }
                            else
                            {
                                simulator.Active = true;
                                simulator.PhysicalBonesStartSimulation();
                                
                                foreach (var child in simulator.GetChildren())
                                {
                                    if (child is PhysicalBone3D bone)
                                        bone.ApplyCentralImpulse(new Vector3(0, 2f, -1f));
                                }
                            }
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
    }

    // --- HIT REACTION ---

    public void ApplyHit(ItemData weapon, Vector3 knockbackDir, string limb)
    {
        if (IsDead) return;

        var navAgent = GetNodeOrNull<NavAgentNPC>("NavAgentNPC");
        if (navAgent != null)
        {
            float force = weapon?.KnockbackForce ?? 5f;
            navAgent.ApplyKnockback(knockbackDir, force);
        }

        StartStumble(knockbackDir, weapon?.StumbleDuration ?? 0.3f);
        EmitSignal(SignalName.HitReceived, weapon?.Damage ?? 10f, limb);
    }

    private void StartStumble(Vector3 direction, float duration)
    {
        if (_isStumbling) return;
        _isStumbling = true;
        _stumbleVelocity = direction * 2.0f;
        _stumbleTimer = duration;
    }

    private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;
        if (!_isStumbling) return;

        float dt = (float)delta;
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity.Y -= _gravity * dt;
        }

        if (_stumbleVelocity.LengthSquared() > 0.01f)
        {
            var spaceState = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(
                GlobalPosition,
                GlobalPosition + (_stumbleVelocity.Normalized() * 0.5f),
                1u 
            );
            var result = spaceState.IntersectRay(query);

            if (result.Count > 0)
            {
                _stumbleVelocity = Vector3.Zero;
                _stumbleTimer = Mathf.Max(_stumbleTimer, 1.0f);
            }
        }

        Velocity = new Vector3(_stumbleVelocity.X, velocity.Y, _stumbleVelocity.Z);
        _stumbleVelocity = _stumbleVelocity.Lerp(Vector3.Zero, dt * 8f);
        _stumbleTimer -= dt;

        if (_stumbleTimer <= 0f)
        {
            _isStumbling = false;
            _stumbleVelocity = Vector3.Zero;
        }
    }

    // --- RAGDOLL ---

    public void ActivateRagdoll(Vector3 impulse)
    {
        if (IsDead) return;

        IsDead = true;
        _isStumbling = false;

        var navAgent = GetNodeOrNull<NavigationAgent3D>("NavAgentNPC");
        if (navAgent != null)
        {
            navAgent.MaxSpeed = 0f;
            navAgent.AvoidanceEnabled = false;
        }

        var combat = GetNodeOrNull<Node>("NPCNavCombat");
        if (combat != null) combat.SetProcess(false);

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

                        foreach (var child in simulator.GetChildren())
                        {
                            if (child is PhysicalBone3D bone)
                                bone.ApplyCentralImpulse(impulse);
                        }
                    }
                }
            }
        }
    }

    // --- RIG DETECTION ---

    private RigType DetectRigType(Skeleton3D skeleton)
    {
        var allNames = new List<string>();
        for (int i = 0; i < skeleton.GetBoneCount(); i++)
            allNames.Add(skeleton.GetBoneName(i));

        bool hasArpGltf = allNames.Any(n => n.StartsWith("spine.") && n.Contains("."))
            && allNames.Any(n => n.EndsWith(".L") || n.EndsWith(".R"));

        bool hasMixamo = allNames.Any(n => n.Contains("mixamorig"));

        if (hasArpGltf && !hasMixamo) return RigType.ARP_GLTB;
        if (hasMixamo) return RigType.Mixamo;

        return RigType.Unknown;
    }

    private string ResolveBoneName(string arpGltfName, string mixamoName)
    {
        return _detectedRig switch
        {
            RigType.ARP_GLTB => arpGltfName,
            RigType.Mixamo => mixamoName,
            _ => mixamoName
        };
    }

    // --- LIMB COLLIDERS ---

    private void SetupLimbColliders(Skeleton3D skeleton)
    {
        foreach (var (limbName, arpGltfBone, mixamoBone, shape) in BoneMap)
        {
            string boneName = ResolveBoneName(arpGltfBone, mixamoBone);
            int boneIdx = skeleton.FindBone(boneName);

            if (boneIdx == -1)
            {
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

    // --- VISION ---

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

    // --- UTILITIES ---

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

    private void StripBlendShapeTracks(AnimationPlayer animPlayer) => AnimationFixer.StripBlendShapeTracks(animPlayer);

    private void SetupDynamicHitbox(Skeleton3D skeleton)
    {
        string[] possibleNames = { "hand.R", "RightHand", "mixamorig_RightHand", "hand_R", "Right_ForeArm" };
        int boneIdx = -1;
        string boneName = "";

        foreach (var name in possibleNames)
        {
            boneIdx = skeleton.FindBone(name);
            if (boneIdx != -1)
            {
                boneName = name;
                break;
            }
        }

        if (boneIdx == -1)
        {
            for (int i = 0; i < skeleton.GetBoneCount(); i++)
            {
                string bn = skeleton.GetBoneName(i).ToLower();
                if (bn.Contains("hand") && (bn.Contains(".r") || bn.Contains("right")))
                {
                    boneIdx = i;
                    boneName = skeleton.GetBoneName(i);
                    break;
                }
            }
        }

        if (boneIdx != -1)
        {
            var attachment = new BoneAttachment3D();
            attachment.Name = "RightHandAttachment";
            attachment.BoneName = boneName;
            skeleton.AddChild(attachment);

            var hitbox = new MeleeHitbox();
            hitbox.Name = "NPCHitbox";
            attachment.AddChild(hitbox);

            var coll = new CollisionShape3D();
            coll.Shape = new BoxShape3D { Size = new Vector3(0.4f, 0.4f, 0.4f) };
            hitbox.AddChild(coll);

            var combat = GetNodeOrNull<NpcNavCombat>("NPCNavCombat");
            if (combat != null)
            {
                combat.SetHitbox(hitbox);
            }
        }
        else
        {
            GD.PrintErr("NpcController: Could not find a right hand bone to attach the hitbox!");
        }
    }
}