using Godot;

[Tool]
public partial class TraceOverlay : MeshInstance3D
{
    [Export] public Texture2D SketchTexture;
    [Export] public float MapSize = 1280f; // 5 * 256
    [Export] public float YHeight = 80f;
    [Export] public bool UpdateInEditor = false;

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() && UpdateInEditor)
        {
            UpdateInEditor = false;
            BuildOverlay();
        }
    }

    private void BuildOverlay()
    {
        if (SketchTexture == null)
        {
            GD.PrintErr("TraceOverlay: No SketchTexture assigned!");
            return;
        }

        // Create horizontal plane facing up
        var plane = new PlaneMesh();
        plane.Size = new Vector2(MapSize, MapSize);
        this.Mesh = plane;

        var mat = new StandardMaterial3D();
        mat.AlbedoTexture = SketchTexture;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mat.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
        this.MaterialOverride = mat;

        // Center the overlay over the whole map
        Position = new Vector3(MapSize / 2f, YHeight, MapSize / 2f);
        Rotation = Vector3.Zero; // PlaneMesh is already horizontal

        GD.Print($"TraceOverlay: Built {MapSize}m overlay at Y={YHeight}");
    }
}