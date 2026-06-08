using Godot;

[Tool]
public partial class ZoneBorderVisualizer : Node3D
{
    [Export] public bool ShowGrid = true;
    [Export] public float ZoneSize = 256f;
    [Export] public int GridCount = 5;
    [Export] public float LineThickness = 1.0f;
    [Export] public float WallHeight = 300f;
    [Export] public Color WallColor = new Color(1, 0.1f, 0.1f, 0.6f);
    [Export] public bool Rebuild = false;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
        {
            BuildGrid();
        }
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() && Rebuild)
        {
            Rebuild = false;
            BuildGrid();
        }
    }

    private void BuildGrid()
    {
        // Clear old
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }

        if (!ShowGrid)
        {
            GD.Print("ZoneBorderVisualizer: ShowGrid is false, cleared all.");
            return;
        }

        float totalSize = ZoneSize * GridCount;
        int created = 0;

        // Vertical lines (X boundaries)
        for (int i = 0; i <= GridCount; i++)
        {
            float x = i * ZoneSize;
            var wall = CreateWall(
                new Vector3(x, WallHeight / 2f, totalSize / 2f),
                new Vector3(LineThickness, WallHeight, totalSize)
            );
            AddChild(wall);
            if (Engine.IsEditorHint() && GetTree()?.EditedSceneRoot != null)
                wall.Owner = GetTree().EditedSceneRoot;
            created++;
        }

        // Horizontal lines (Z boundaries)
        for (int i = 0; i <= GridCount; i++)
        {
            float z = i * ZoneSize;
            var wall = CreateWall(
                new Vector3(totalSize / 2f, WallHeight / 2f, z),
                new Vector3(totalSize, WallHeight, LineThickness)
            );
            AddChild(wall);
            if (Engine.IsEditorHint() && GetTree()?.EditedSceneRoot != null)
                wall.Owner = GetTree().EditedSceneRoot;
            created++;
        }

        GD.Print($"ZoneBorderVisualizer: CREATED {created} walls. ZoneSize={ZoneSize}, Grid={GridCount}x{GridCount}");
    }

    private MeshInstance3D CreateWall(Vector3 pos, Vector3 size)
    {
        var mi = new MeshInstance3D();
        mi.Position = pos;

        var box = new BoxMesh();
        box.Size = size;
        mi.Mesh = box;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = WallColor;
        mat.Emission = WallColor;
        mat.EmissionEnergyMultiplier = 2.0f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mat.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;

        mi.MaterialOverride = mat;
        return mi;
    }
}