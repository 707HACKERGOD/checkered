using Godot;
using System;

public partial class NpcController : CharacterBody3D
{
    [Export] public PackedScene ModelResource;
    [Export] public Shape3D OverrideShape;          // optional per-NPC collision shape
    [Export] public string DisplayName = "Stranger";
    [Export] public bool IsDead = false;

    private NpcEyeTracker _eyeTracker;
    private Area3D _visionArea;

    public override void _Ready()
    {
        Node3D model = null;

        // --- Load and attach the model ---
        if (ModelResource != null)
        {
            model = ModelResource.Instantiate<Node3D>();
            var modelRoot = GetNodeOrNull<Node3D>("ModelRoot");
            if (modelRoot != null)
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
            if (skeleton != null) _eyeTracker.CharacterSkeleton = skeleton;
            if (faceMesh != null) _eyeTracker.FaceMesh = faceMesh;
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
            // Also search deeper
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