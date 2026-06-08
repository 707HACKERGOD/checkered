using Godot;
using System.Collections.Generic;

public partial class ZoneStreamingManager : Node
{
    [Export] public int StreamRadiusZones = 2;
    [Export] public PackedScene[] ZoneScenes = new PackedScene[28];

    private Dictionary<Vector2I, Node3D> _loadedZones = new();
    private HashSet<Vector2I> _loading = new();
    private Node3D _player;

    public override void _Ready()
    {
        CallDeferred(nameof(DeferredReady));
    }

    private void DeferredReady()
    {
        _player = GetTree()?.GetFirstNodeInGroup("Player") as Node3D;
    }

    public override void _Process(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree()?.GetFirstNodeInGroup("Player") as Node3D;
            if (_player == null) return;
        }

        UpdateStreaming();
    }

    private void UpdateStreaming()
    {
        Vector3 playerPos = _player.GlobalPosition;
        Vector2I center = WorldMapManager.WorldToZone(playerPos);

        HashSet<Vector2I> desired = new();
        for (int x = -StreamRadiusZones; x <= StreamRadiusZones; x++)
        {
            for (int z = -StreamRadiusZones; z <= StreamRadiusZones; z++)
            {
                Vector2I c = center + new Vector2I(x, z);
                if (c.X >= 0 && c.X < WorldMapManager.ZONE_GRID_X &&
                    c.Y >= 0 && c.Y < WorldMapManager.ZONE_GRID_Z)
                {
                    desired.Add(c);
                }
            }
        }

        // Unload
        List<Vector2I> toRemove = new();
        foreach (var kvp in _loadedZones)
            if (!desired.Contains(kvp.Key)) toRemove.Add(kvp.Key);
        foreach (var c in toRemove) UnloadZone(c);

        // Load
        foreach (var c in desired)
        {
            if (!_loadedZones.ContainsKey(c) && !_loading.Contains(c))
            {
                _loading.Add(c);
                LoadZone(c);
            }
        }
    }

    private void LoadZone(Vector2I zone)
    {
        int zoneNum = WorldMapManager.ZoneToNumber(zone);
        bool completed = WorldMapManager.IsCompletedZone(zoneNum);

        // Placeholder: empty node. Later you'll instance actual zone scenes here.
        Node3D node = new() { Name = $"Zone_{zoneNum}" };
        node.GlobalPosition = new Vector3(
            zone.X * WorldMapManager.ZONE_SIZE,
            0,
            zone.Y * WorldMapManager.ZONE_SIZE);

        AddChild(node);
        _loadedZones[zone] = node;
        _loading.Remove(zone);

        GD.Print($"ZoneStreamingManager: Loaded zone {zoneNum} at {zone}");
    }

    private void UnloadZone(Vector2I zone)
    {
        if (_loadedZones.TryGetValue(zone, out var node))
        {
            node.QueueFree();
            _loadedZones.Remove(zone);
            GD.Print($"ZoneStreamingManager: Unloaded zone {WorldMapManager.ZoneToNumber(zone)}");
        }
    }
}