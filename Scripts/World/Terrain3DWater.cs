using Godot;

[Tool]
public partial class Terrain3DWater : MeshInstance3D
{
    [Export] public float WaterLevel = 150.0f;
    [Export] public Vector2 WaterSize = new Vector2(1792.0f, 1024.0f);
    [Export] public int Subdivisions = 128;
    [Export] public bool FollowCamera = false;
    [Export] public string ShaderPath = "res://Assets/Water.gdshader";

    private Camera3D _camera;

    public override void _Ready()
    {
        BuildMesh();
        if (FollowCamera)
            _camera = GetViewport().GetCamera3D();
    }

    public override void _Process(double delta)
    {
        if (FollowCamera && _camera != null)
        {
            Vector3 camPos = _camera.GlobalPosition;
            GlobalPosition = new Vector3(camPos.X, WaterLevel, camPos.Z);
        }
    }

    private void BuildMesh()
    {
        PlaneMesh plane = new PlaneMesh();
        plane.Size = WaterSize;
        plane.SubdivideWidth = Subdivisions;
        plane.SubdivideDepth = Subdivisions;
        Mesh = plane;

        GlobalPosition = new Vector3(896.0f, WaterLevel, 512.0f);

        if (!string.IsNullOrEmpty(ShaderPath))
        {
            Shader shader = GD.Load<Shader>(ShaderPath);
            if (shader != null)
            {
                ShaderMaterial mat = new ShaderMaterial();
                mat.Shader = shader;
                Mesh.SurfaceSetMaterial(0, mat);
            }
        }
    }

    public float GetWaterSurfaceY(Vector2 worldPosXZ)
    {
        return GlobalPosition.Y;
    }
}