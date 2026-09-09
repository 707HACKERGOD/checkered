using Godot;

/// <summary>
/// Attach as a direct child of any RigidBody3D or VehicleBody3D.
/// Freezes the parent body when the player is far away,
/// unfreezes when they approach. WorldObjectManager drives it.
/// </summary>
public partial class WorldObject : Node3D
{
    [ExportGroup("Activation Distances")]
    [Export(PropertyHint.Range, "20,500,10")]
    public float ActivateDistance = 120f;    // unfreeze within this radius

    [Export(PropertyHint.Range, "30,600,10")]
    public float DeactivateDistance = 180f;  // freeze beyond this (larger = hysteresis, no on/off thrashing)

    [ExportGroup("Spawning")]
    [Export] public float SpawnHeightOffset = 0.5f;  // meters above terrain height at spawn
    [Export] public bool SnapToTerrain = true;       // query terrain height on first manager check

    [ExportGroup("Rendering")]
    [Export] public bool HideWhenFrozen = false;     // leave false if using visibility_range on meshes

    public bool IsActive { get; private set; }
    public bool HasSpawned { get; private set; }
    public RigidBody3D Body { get; private set; }

    public override void _Ready()
    {
        Body = GetParent() as RigidBody3D;
        if (Body == null)
        {
            GD.PushWarning($"{Name}: WorldObject must be a direct child of a RigidBody3D/VehicleBody3D.");
            return;
        }

        // Start frozen — the manager activates us when appropriate
        Body.Freeze = true;
        Body.FreezeMode = RigidBody3D.FreezeModeEnum.Static;

        // Register with the manager (deferred so the whole scene is ready first)
        CallDeferred(nameof(RegisterWithManager));
    }

    private void RegisterWithManager()
    {
        // Try walking up first (fast — works if manager is an ancestor)
        Node current = GetParent();
        while (current != null)
        {
            if (current is WorldObjectManager ancestor)
            {
                ancestor.Register(this);
                return;
            }
            current = current.GetParent();
        }

        // Fall back to a full tree search by node name
        // (works regardless of where the manager sits — siblings, etc.)
        var manager = GetTree().Root.FindChild("WorldObjectManager", true, false)
                    as WorldObjectManager;
        if (manager != null)
            manager.Register(this);
        else
            GD.PushWarning($"{Name}: No WorldObjectManager in scene — object stays frozen.");
    }

    /// <summary>One-time terrain snap. Called by manager on its first check.</summary>
    public void SnapToTerrainHeight(TerrainManager terrain)
    {
        if (HasSpawned || terrain == null) return;

        Vector3 pos = Body.GlobalPosition;
        float terrainHeight = terrain.GetHeight(pos);
        Body.GlobalPosition = new Vector3(pos.X, terrainHeight + SpawnHeightOffset, pos.Z);

        HasSpawned = true;
    }

    /// <summary>Mark as spawned without terrain snap (for hand-placed objects).</summary>
    public void MarkSpawned() => HasSpawned = true;

    public void SetActive(bool active)
    {
        if (IsActive == active) return;
        IsActive = active;

        Body.Freeze = !active;  // FreezeMode stays Static — unfrozen = back to Rigid

        if (active)
        {
            Body.Sleeping = false;  // wake it so it starts simulating immediately
        }
        else if (HideWhenFrozen)
        {
            Body.Visible = false;
        }

        if (active && HideWhenFrozen)
            Body.Visible = true;
    }

    /// <summary>Force activation regardless of distance (player entering a frozen car).</summary>
    public void ForceActivate() => SetActive(true);
}