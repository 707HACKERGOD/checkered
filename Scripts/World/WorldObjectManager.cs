using Godot;
using System.Collections.Generic;

public partial class WorldObjectManager : Node
{
    [ExportGroup("References (optional — auto-found if empty)")]
    [Export] public NodePath PlayerPath;
    [Export] public NodePath TerrainManagerPath;

    [ExportGroup("Performance")]
    [Export] public float CheckInterval = 0.25f;
    [Export] public int MaxActivationsPerCheck = 10;

    // How far below an object we search for ground (meters).
    // 25m covers terrain + road surface + most props.
    [Export] public float GroundCheckDepth = 25f;

    private readonly List<WorldObject> _objects = new();
    private readonly List<WorldObject> _pendingActivation = new();
    private Node3D _player;
    private TerrainManager _terrain;
    private float _checkTimer;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _PhysicsProcess(double delta)
    {
        _checkTimer -= (float)delta;
        if (_checkTimer > 0) return;
        _checkTimer = CheckInterval;

        if (_player == null) _player = FindPlayer();
        if (_terrain == null) _terrain = FindTerrain();
        if (_player == null) return;

        // Sweep for new WorldObjects
        foreach (Node node in GetTree().GetNodesInGroup("world_objects"))
        {
            if (node is WorldObject wo && !_objects.Contains(wo))
                _objects.Add(wo);
        }

        Vector3 playerPos = _player.GlobalPosition;
        PhysicsDirectSpaceState3D spaceState = _player.GetWorld3D().DirectSpaceState;

        foreach (WorldObject obj in _objects)
        {
            if (!obj.HasSpawned)
            {
                obj.SnapToTerrainHeight(_terrain);
                if (!obj.HasSpawned) obj.MarkSpawned();
            }

            if (!obj.IsActive)
            {
                float dSq = obj.ActivateDistance * obj.ActivateDistance;
                if (playerPos.DistanceSquaredTo(obj.GlobalPosition) < dSq)
                {
                    // ─── THE FIX ───
                    // Only unfreeze if there's actual collision beneath the object.
                    // If terrain collision hasn't loaded yet (camera too far),
                    // the ray misses, and we simply try again next check.
                    if (HasGroundBelow(spaceState, obj))
                        _pendingActivation.Add(obj);
                }
            }
            else
            {
                float dSq = obj.DeactivateDistance * obj.DeactivateDistance;
                if (playerPos.DistanceSquaredTo(obj.GlobalPosition) > dSq)
                    obj.SetActive(false);
            }
        }

        // Batch activation, closest first
        if (_pendingActivation.Count > 0)
        {
            Vector3 p = playerPos;
            _pendingActivation.Sort((a, b) =>
                a.GlobalPosition.DistanceSquaredTo(p)
                .CompareTo(b.GlobalPosition.DistanceSquaredTo(p)));

            int count = Mathf.Min(_pendingActivation.Count, MaxActivationsPerCheck);
            for (int i = 0; i < count; i++)
                _pendingActivation[i].SetActive(true);
            _pendingActivation.RemoveRange(0, count);
        }
    }

    /// <summary>
    /// Casts a ray straight down from the object. Returns true if any
    /// physics collider exists below it (terrain, road, another object).
    /// If false, the terrain collision hasn't loaded at that position yet.
    /// </summary>
    private bool HasGroundBelow(PhysicsDirectSpaceState3D spaceState, WorldObject obj)
    {
        Vector3 origin = obj.GlobalPosition;
        Vector3 end = origin + Vector3.Down * GroundCheckDepth;

        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        // Exclude the object's own body so the ray doesn't hit itself
        query.Exclude = new Godot.Collections.Array<Rid> { obj.Body.GetRid() };

        var result = spaceState.IntersectRay(query);
        return result.Count > 0;
    }

    private Node3D FindPlayer()
    {
        if (PlayerPath != null)
            return GetNodeOrNull<Node3D>(PlayerPath);

        var group = GetTree().GetNodesInGroup("player");
        if (group.Count > 0) return group[0] as Node3D;

        var byName = GetTree().Root.FindChild("Player", true, false) as Node3D;
        return byName;
    }

    private TerrainManager FindTerrain()
    {
        if (TerrainManagerPath != null)
            return GetNodeOrNull<TerrainManager>(TerrainManagerPath);

        return FindNodeByScript<TerrainManager>(GetTree().Root);
    }

    private static T FindNodeByScript<T>(Node from) where T : class
    {
        if (from.GetScript().AsGodotObject() is T typed) return typed;
        foreach (Node child in from.GetChildren())
        {
            T found = FindNodeByScript<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    public void Register(WorldObject obj)
    {
        if (!_objects.Contains(obj)) _objects.Add(obj);
    }

    public void Unregister(WorldObject obj)
    {
        _objects.Remove(obj);
        _pendingActivation.Remove(obj);
    }
}