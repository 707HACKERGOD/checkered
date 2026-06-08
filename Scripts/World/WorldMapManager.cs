using Godot;

public partial class WorldMapManager : Node
{
    public const int ZONE_GRID_X = 7;
    public const int ZONE_GRID_Z = 4;
    public const float ZONE_SIZE = 256.0f;

    [Signal] public delegate void ZoneChangedEventHandler(Vector2I newZone, Vector2I oldZone);
    [Signal] public delegate void RegionEnteredEventHandler(int zoneNumber, bool isCompleted);

    public Vector2I CurrentZone { get; private set; } = new Vector2I(-1, -1);
    public int CurrentZoneNumber => ZoneToNumber(CurrentZone);
    private Node3D _player;

    public override void _Ready()
    {
        CallDeferred(nameof(DeferredReady));
    }

    private void DeferredReady()
    {
        _player = GetTree()?.GetFirstNodeInGroup("Player") as Node3D;
        if (_player == null) GD.PrintErr("WorldMapManager: No 'Player' group node found.");
        else GD.Print("WorldMapManager: Player found.");
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
            Mathf.Clamp(Mathf.FloorToInt(p.X / ZONE_SIZE), 0, ZONE_GRID_X - 1),
            Mathf.Clamp(Mathf.FloorToInt(p.Z / ZONE_SIZE), 0, ZONE_GRID_Z - 1)
        );

        if (zone != CurrentZone)
        {
            Vector2I oldZone = CurrentZone;
            CurrentZone = zone;
            int zoneNum = ZoneToNumber(zone);
            bool completed = IsCompletedZone(zoneNum);
            EmitSignal(SignalName.ZoneChanged, zone, oldZone);
            EmitSignal(SignalName.RegionEntered, zoneNum, completed);
            GD.Print($"WorldMapManager: Entered zone {zoneNum} at {zone} (Completed: {completed})");
        }
    }

    // 1–28 numbering: left-to-right, top-to-bottom
    public static int ZoneToNumber(Vector2I zone)
    {
        return (zone.Y * ZONE_GRID_X) + zone.X + 1;
    }

    public static Vector2I NumberToZone(int number)
    {
        int idx = number - 1;
        return new Vector2I(idx % ZONE_GRID_X, idx / ZONE_GRID_X);
    }

    public static bool IsCompletedZone(int zoneNumber)
    {
        // UPDATE THIS with your 6 completed zones from the screenshot
        // Placeholder: zones 10,11,12,17,18,19 (the 3×2 center of your demo border)
        return zoneNumber == 10 || zoneNumber == 11 || zoneNumber == 12
            || zoneNumber == 17 || zoneNumber == 18 || zoneNumber == 19;
    }

    public static Vector2I WorldToZone(Vector3 worldPos)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(worldPos.X / ZONE_SIZE), 0, ZONE_GRID_X - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(worldPos.Z / ZONE_SIZE), 0, ZONE_GRID_Z - 1);
        return new Vector2I(x, z);
    }
}