using Godot;

public partial class WorldGenerator : Node3D
{
    public override void _Ready()
    {
        CreateGround();
        CreateHouse(new Vector3(40, 0, -20), 0x6a5a4a);
        CreateHouse(new Vector3(50, 0, 0), 0x5a6a5a);
    }

    private void CreateGround()
    {
        var mesh = new PlaneMesh();
        mesh.Size = new Vector2(300, 300);
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.2f, 0.2f, 0.2f); // Replace with texture later
        
        var instance = new MeshInstance3D();
        instance.Mesh = mesh;
        instance.MaterialOverride = material;
        
        // Static body for collision
        var staticBody = new StaticBody3D();
        var shape = new CollisionShape3D();
        shape.Shape = new WorldBoundaryShape3D(); // Simple infinite plane or use BoxShape
        staticBody.AddChild(shape);
        instance.AddChild(staticBody);
        
        AddChild(instance);
    }

    private void CreateHouse(Vector3 pos, int colorHex)
    {
        // In Godot, it's better to make a 'House.tscn' and Instance() it here.
        // But for code generation:
        var meshInstance = new MeshInstance3D();
        var box = new BoxMesh();
        box.Size = new Vector3(8, 6, 8);
        meshInstance.Mesh = box;
        meshInstance.Position = new Vector3(pos.X, 3, pos.Z);
        
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = Color.FromHtml(colorHex.ToString("X"));
        meshInstance.MaterialOverride = mat;

        AddChild(meshInstance);
    }
}
