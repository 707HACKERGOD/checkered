using Godot;

public partial class InteractableItem : Area3D
{
    public ItemData Data;
    private Vector3 _startPos;
    private float _time;

    public void Initialize(ItemData data)
    {
        Data = data;
        _startPos = GlobalPosition;

        // Outer cube (transparent shell)
        var outerMesh = new MeshInstance3D();
        outerMesh.Mesh = new BoxMesh { Size = Vector3.One * 0.4f };
        var outerMat = new StandardMaterial3D();
        outerMat.AlbedoColor = new Color(data.ThemeColor, 0.4f);
        outerMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        outerMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        outerMesh.MaterialOverride = outerMat;
        AddChild(outerMesh);

        // Inner solid cube
        var innerMesh = new MeshInstance3D();
        innerMesh.Mesh = new BoxMesh { Size = Vector3.One * 0.2f };
        var innerMat = new StandardMaterial3D();
        Color coolColor = new Color(
            Mathf.Lerp(data.ThemeColor.R, 0.8f, 0.3f),
            Mathf.Lerp(data.ThemeColor.G, 1.0f, 0.2f),
            Mathf.Lerp(data.ThemeColor.B, 1.0f, 0.5f)
        );
        innerMat.AlbedoColor = coolColor;
        innerMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        innerMesh.MaterialOverride = innerMat;
        AddChild(innerMesh);

        // Collision shape
        var col = new CollisionShape3D();
        col.Shape = new BoxShape3D { Size = Vector3.One * 0.5f };
        AddChild(col);

        // Set collision layer to 2 (Items)
        // Note: 2 here sets it to the 2nd bit (value 2).
        CollisionLayer = 2;  
        CollisionMask = 0;   // Not needed to detect other objects

        //GD.Print($"[Item] {Data.Name} spawned at {GlobalPosition} with layer {CollisionLayer}");
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        float bounce = Mathf.Sin(_time * 3f) * 0.2f;
        GlobalPosition = new Vector3(_startPos.X, _startPos.Y + bounce, _startPos.Z);
        RotateY((float)delta);
    }

    // REMOVED _InputEvent - It's no longer needed!

    public void Pickup()
    {
        GD.Print($"Picked up: {Data.Name}");
        var player = GetTree().Root.FindChild("Player", true, false);
        var inventory = player?.GetNode<Inventory>("Inventory");
        if (inventory != null && inventory.AddItem(Data))
            QueueFree();
        else
            GD.PrintErr("Could not add item to inventory (maybe full)");
    }
}
