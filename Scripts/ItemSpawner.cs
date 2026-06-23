using Godot;

public partial class ItemSpawner : Node3D
{
    [Export] private int MaxItems = 15;
    [Export] private float SpawnRadius = 30f;
    [Export] private float RespawnTimeSeconds = 300f;
    [Export] private float WeaponSpawnDistance = 2f; // Distance in front of player to spawn weapons
    [Export] public Node3D SpawnCenter;

    private Timer _spawnTimer;
    private bool _weaponsSpawned = false;

    public override void _Ready()
    {
        _spawnTimer = new Timer();
        _spawnTimer.WaitTime = RespawnTimeSeconds;
        _spawnTimer.Timeout += SpawnRoutine;
        AddChild(_spawnTimer);
        _spawnTimer.Start();

        // Spawn initial items immediately
        SpawnRoutine();

        // Spawn the 3 weapons lined up in front of player
        CallDeferred(nameof(SpawnStarterWeapons));
    }

    private void SpawnStarterWeapons()
    {
        if (_weaponsSpawned) return;

        var player = GetTree().Root.FindChild("Player", true, false) as Node3D;
        if (player == null)
        {
            GD.PrintErr("ItemSpawner: Player not found");
            return;
        }

        Vector3 forward = -player.GlobalTransform.Basis.Z.Normalized();
        Vector3 right = player.GlobalTransform.Basis.X.Normalized();
        Vector3 origin = player.GlobalPosition + forward * 2f;

        var weapons = new[] { ImpactType.Fist, ImpactType.Pipe, ImpactType.Chair };
        float spacing = 1.2f;

        for (int i = 0; i < weapons.Length; i++)
        {
            float offsetX = (i - 1) * spacing;
            Vector3 spawnXZ = origin + right * offsetX;

            // Raycast from high above player down to ground
            var spaceState = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(
                new Vector3(spawnXZ.X, player.GlobalPosition.Y + 200f, spawnXZ.Z),
                new Vector3(spawnXZ.X, player.GlobalPosition.Y - 200f, spawnXZ.Z)
            );
            query.CollisionMask = 1 | 2;
            query.CollideWithBodies = true;

            var result = spaceState.IntersectRay(query);
            Vector3 finalPos;
            if (result.Count > 0)
                finalPos = result["position"].AsVector3() + Vector3.Up * 0.4f;
            else
                finalPos = new Vector3(spawnXZ.X, player.GlobalPosition.Y + 0.4f, spawnXZ.Z);

            var itemData = ItemRegistry.GetWeapon(weapons[i]);
            if (itemData == null) continue;

            var item = new InteractableItem();
            GetTree().Root.AddChild(item);
            item.GlobalPosition = finalPos;
            item.Initialize(itemData);
            item.AddToGroup("StarterWeapons");
        }

        _weaponsSpawned = true;
        GD.Print("Starter weapons spawned");
    }

    private void SpawnRoutine()
    {
        // Only count non-starter items
        int currentCount = 0;
        foreach (var node in GetTree().GetNodesInGroup("DroppedItems"))
        {
            if (!node.IsInGroup("StarterWeapons"))
                currentCount++;
        }

        int toSpawn = MaxItems - currentCount;
        for (int i = 0; i < toSpawn; i++)
            SpawnSingleItem();
    }

    private void SpawnSingleItem()
    {
        var spaceState = GetWorld3D().DirectSpaceState;


        Vector3 center = SpawnCenter != null ? SpawnCenter.GlobalPosition : GlobalPosition;

        float randX = center.X + (GD.Randf() * SpawnRadius * 2) - SpawnRadius;
        float randZ = center.Z + (GD.Randf() * SpawnRadius * 2) - SpawnRadius;

        Vector3 rayStart = new Vector3(randX, 100f, randZ);
        Vector3 rayEnd = new Vector3(randX, -100f, randZ);

        var query = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);
        query.CollisionMask = 2; // Layer 2 = Floor

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            Vector3 hitPos = (Vector3)result["position"];
            float cubeHalfHeight = 0.2f;
            float floatOffset = 0.2f;
            float totalOffset = cubeHalfHeight + floatOffset;

            var itemData = ItemRegistry.GetRandomItem();
            var item = new InteractableItem();
            AddChild(item);
            item.GlobalPosition = hitPos + Vector3.Up * totalOffset;
            item.Initialize(itemData);
            item.AddToGroup("DroppedItems");
        }
    }
}