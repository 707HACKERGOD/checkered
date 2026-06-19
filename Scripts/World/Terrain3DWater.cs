using Godot;

[Tool]
public partial class Terrain3DWater : MeshInstance3D
{
    [Export] public NodePath Terrain3DPath { get; set; }
    [Export] public string ShaderPath { get; set; } = "res://terrain3d_water.gdshader";
    [Export] public string NormalMapAPath { get; set; } = "res://water_normal_a.png";
    [Export] public string NormalMapBPath { get; set; } = "res://water_normal_b.png";
    [Export] public float WaterLevel { get; set; } = 0.0f;
    [Export] public Vector2 WaterSize { get; set; } = new Vector2(500, 500);
    [Export] public int Subdivisions { get; set; } = 100;
    [Export] public bool FollowCamera { get; set; } = true;
    [Export] public float SnapSize { get; set; } = 10.0f;

    private Node _terrain;
    private Camera3D _camera;
    private ShaderMaterial _waterMaterial;

    public override void _Ready()
    {
        InitializeTerrain();
        SetupWaterMesh();
        SetupMaterial();
        if (FollowCamera) _camera = GetViewport().GetCamera3D();
    }

    public override void _Process(double delta)
    {
        if (FollowCamera && _camera != null) FollowCameraPosition();
    }

    private void InitializeTerrain()
    {
        if (Terrain3DPath != null && !Terrain3DPath.IsEmpty)
            _terrain = GetNode(Terrain3DPath);
        else
        {
            var terrains = GetTree().GetNodesInGroup("Terrain3D");
            if (terrains.Count > 0) _terrain = terrains[0] as Node;
        }

        if (_terrain == null)
            GD.PrintErr("Terrain3DWater: No Terrain3D found! Set Terrain3DPath or add Terrain3D to group 'Terrain3D'");
    }

    private void SetupWaterMesh()
    {
        PlaneMesh planeMesh = new PlaneMesh();
        planeMesh.Size = WaterSize;
        planeMesh.SubdivideWidth = Subdivisions;
        planeMesh.SubdivideDepth = Subdivisions;
        Mesh = planeMesh;
        Position = new Vector3(Position.X, WaterLevel, Position.Z);
    }

    private void SetupMaterial()
    {
        if (!FileAccess.FileExists(ShaderPath))
        {
            GD.PrintErr($"Terrain3DWater: Shader not found at '{ShaderPath}'! Create the file or set ShaderPath.");
            return;
        }

        Shader waterShader = ResourceLoader.Load<Shader>(ShaderPath);
        if (waterShader == null)
        {
            GD.PrintErr($"Terrain3DWater: Failed to load shader at '{ShaderPath}'");
            return;
        }

        _waterMaterial = new ShaderMaterial();
        _waterMaterial.Shader = waterShader;

        _waterMaterial.SetShaderParameter("shallow_color", new Color(0.0f, 0.45f, 0.55f, 0.8f));
        _waterMaterial.SetShaderParameter("deep_color", new Color(0.0f, 0.15f, 0.25f, 0.9f));
        _waterMaterial.SetShaderParameter("foam_color", Colors.White);
        _waterMaterial.SetShaderParameter("wave_speed", 1.0f);
        _waterMaterial.SetShaderParameter("wave_strength", 0.3f);
        _waterMaterial.SetShaderParameter("wave_frequency", 8.0f);
        _waterMaterial.SetShaderParameter("foam_distance", 2.0f);
        _waterMaterial.SetShaderParameter("foam_amount", 0.5f);
        _waterMaterial.SetShaderParameter("metallic", 0.1f);
        _waterMaterial.SetShaderParameter("roughness", 0.05f);
        _waterMaterial.SetShaderParameter("beers_law", 4.0f);

        // Only load normal maps if they exist
        if (FileAccess.FileExists(NormalMapAPath))
        {
            Texture2D normalA = ResourceLoader.Load<Texture2D>(NormalMapAPath);
            if (normalA != null) _waterMaterial.SetShaderParameter("normal_map_a", normalA);
        }

        if (FileAccess.FileExists(NormalMapBPath))
        {
            Texture2D normalB = ResourceLoader.Load<Texture2D>(NormalMapBPath);
            if (normalB != null) _waterMaterial.SetShaderParameter("normal_map_b", normalB);
        }

        MaterialOverride = _waterMaterial;
    }

    private void FollowCameraPosition()
    {
        if (_camera == null) return;
        Vector3 camPos = _camera.GlobalPosition;
        float snapX = Mathf.Floor(camPos.X / SnapSize) * SnapSize;
        float snapZ = Mathf.Floor(camPos.Z / SnapSize) * SnapSize;
        GlobalPosition = new Vector3(snapX, WaterLevel, snapZ);
    }

    public void SetWaterLevel(float level)
    {
        WaterLevel = level;
        Position = new Vector3(Position.X, WaterLevel, Position.Z);
    }

    public float GetTerrainHeight(Vector2 worldPos)
    {
        if (_terrain == null) return 0;
        Variant heightVar = _terrain.Call("get_height", worldPos);
        if (heightVar.VariantType != Variant.Type.Nil)
            return heightVar.As<float>();
        return 0;
    }

    public bool IsUnderwater(Vector3 worldPos)
    {
        float terrainHeight = GetTerrainHeight(new Vector2(worldPos.X, worldPos.Z));
        return worldPos.Y < WaterLevel && worldPos.Y > terrainHeight - 50.0f;
    }

    public float GetWaterDepth(Vector3 worldPos)
    {
        if (!IsUnderwater(worldPos)) return 0;
        return WaterLevel - worldPos.Y;
    }

    public float GetWaveHeight(Vector3 worldPos)
    {
        if (_waterMaterial == null) return 0;

        double waveSpeed = _waterMaterial.GetShaderParameter("wave_speed").As<double>();
        double waveFreq = _waterMaterial.GetShaderParameter("wave_frequency").As<double>();
        double waveStrength = _waterMaterial.GetShaderParameter("wave_strength").As<double>();

        float t = (float)Time.GetTimeDictFromSystem()["second"] * (float)waveSpeed;
        float freq = (float)waveFreq;
        float strength = (float)waveStrength;

        float wave1 = Mathf.Sin(worldPos.X * freq + t) * strength * 0.5f;
        float wave2 = Mathf.Sin(worldPos.Z * freq * 1.3f + t * 1.2f) * strength * 0.3f;
        float wave3 = Mathf.Sin((worldPos.X + worldPos.Z) * freq * 0.7f + t * 0.8f) * strength * 0.2f;

        return wave1 + wave2 + wave3;
    }

    public float GetWaterSurfaceY(Vector2 worldPosXZ)
    {
        return WaterLevel + GetWaveHeight(new Vector3(worldPosXZ.X, 0, worldPosXZ.Y));
    }
}