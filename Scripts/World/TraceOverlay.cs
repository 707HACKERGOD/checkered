using Godot;

[Tool]
public partial class TraceOverlay : MeshInstance3D
{
    [Export] public Texture2D SketchTexture;
    [Export] public float MapSize = 1280f;
    [Export] public float YHeight = 150f;
    [Export] public bool Refresh = false;

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() && Refresh)
        {
            Refresh = false;
            Build();
        }
    }

    private void Build()
    {
        if (SketchTexture == null) return;

        Mesh = new PlaneMesh { Size = new Vector2(MapSize, MapSize) };
        Position = new Vector3(MapSize / 2f, YHeight, MapSize / 2f);

        var mat = new StandardMaterial3D();
        mat.AlbedoTexture = SketchTexture;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mat.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;

        MaterialOverride = mat;
    }
}