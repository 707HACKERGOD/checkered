using Godot;

public partial class WorldMapManager : Node
{
    public const int ZONE_GRID_SIZE = 5;
    public const float ZONE_SIZE = 256.0f;

    [Signal] public delegate void ZoneChangedEventHandler(Vector2I newZone, Vector2I oldZone);
    [Signal] public delegate void RegionEnteredEventHandler(Vector2I zone, bool isCompleted);

    public Vector2I CurrentZone { get; private set; } = new Vector2I(-1, -1);
    private Node3D _player;

    public override void _Ready()
    {
        // Autoloads run _Ready before the main scene exists, so defer
        CallDeferred(nameof(DeferredReady));
    }

    private void DeferredReady()
    {
        _player = GetTree()?.GetFirstNodeInGroup("Player") as Node3D;
        if (_player == null)
            GD.PrintErr("WorldMapManager: No node in group 'Player' found!");
        else
            GD.Print("WorldMapManager: Player found.");
    }

    public override void _Process(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree()?.GetFirstNodeInGroup("Player") as Node3D;
            if (_player == null) return;
        }

        Vector3 p = _player.GlobalPosition;
        Vector2I zone = new(
            Mathf.Clamp(Mathf.FloorToInt(p.X / ZONE_SIZE), 0, ZONE_GRID_SIZE - 1),
            Mathf.Clamp(Mathf.FloorToInt(p.Z / ZONE_SIZE), 0, ZONE_GRID_SIZE - 1)
        );

        if (zone != CurrentZone)
        {
            Vector2I oldZone = CurrentZone;
            CurrentZone = zone;
            bool completed = IsCompletedZone(zone);

            EmitSignal(SignalName.ZoneChanged, zone, oldZone);
            EmitSignal(SignalName.RegionEntered, zone, completed);
            GD.Print($"WorldMapManager: Entered zone {zone} (Completed: {completed})");
        }
    }

    public static bool IsCompletedZone(Vector2I zone)
    {
        return zone == new Vector2I(0, 0) || zone == new Vector2I(1, 0)
            || zone == new Vector2I(0, 1) || zone == new Vector2I(1, 1)
            || zone == new Vector2I(2, 0) || zone == new Vector2I(2, 1);
    }

    public static Vector2I WorldToZone(Vector3 worldPos)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(worldPos.X / ZONE_SIZE), 0, ZONE_GRID_SIZE - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(worldPos.Z / ZONE_SIZE), 0, ZONE_GRID_SIZE - 1);
        return new Vector2I(x, z);
    }
}