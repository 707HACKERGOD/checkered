using Godot;

public partial class ItemSpawner : Node3D
{
    [Export] private int MaxItems = 15;
    [Export] private float SpawnRadius = 30f;   // Reduced from 100
    [Export] private float RespawnTimeSeconds = 300f; // 5 minutes

    private Timer _spawnTimer;

    public override void _Ready()
    {
        _spawnTimer = new Timer();
        _spawnTimer.WaitTime = RespawnTimeSeconds;
        _spawnTimer.Timeout += SpawnRoutine;
        AddChild(_spawnTimer);
        _spawnTimer.Start();

        // Spawn initial items immediately
        SpawnRoutine();
    }

    private void SpawnRoutine()
    {
        int currentCount = GetTree().GetNodesInGroup("DroppedItems").Count;
        int toSpawn = MaxItems - currentCount;
        for (int i = 0; i < toSpawn; i++)
            SpawnSingleItem();
    }

    private void SpawnSingleItem()
    {
        var spaceState = GetWorld3D().DirectSpaceState;

        float randX = GlobalPosition.X + (GD.Randf() * SpawnRadius * 2) - SpawnRadius;
        float randZ = GlobalPosition.Z + (GD.Randf() * SpawnRadius * 2) - SpawnRadius;

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