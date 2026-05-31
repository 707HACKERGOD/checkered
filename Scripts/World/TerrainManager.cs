using Godot;

public partial class TerrainManager : Node3D
{
    [Export] public NodePath TerrainNodePath;
    [Export] public int RegionSize = 256; // Must match Terrain3D inspector setting
    [Export] public bool InitializeInEditor = true;

    private GodotObject _terrainData;
    private Node3D _terrain;

    public override void _Ready()
    {
        _terrain = GetNode<Node3D>(TerrainNodePath);
        if (_terrain == null)
        {
            GD.PrintErr("TerrainManager: No Terrain3D node found at path: " + TerrainNodePath);
            return;
        }

        // Terrain3D stores its data in a 'data' property
        _terrainData = _terrain.Get("data").AsGodotObject();

        if (InitializeInEditor && Engine.IsEditorHint())
        {
            InitializeAllRegions();
        }
    }

    private void InitializeAllRegions()
    {
        if (_terrainData == null) return;

        int created = 0;
        for (int x = 0; x < 5; x++)
        {
            for (int z = 0; z < 5; z++)
            {
                Vector2I loc = new Vector2I(x, z);
                bool hasRegion = _terrainData.Call("has_region", loc).AsBool();

                if (!hasRegion)
                {
                    // false = don't update maps yet (bulk operation)
                    _terrainData.Call("add_region_blank", loc, false);
                    created++;
                }
            }
        }

        if (created > 0)
        {
            _terrainData.Call("force_update_maps");
            GD.Print($"TerrainManager: Created {created} blank regions. Total 25 zones ready.");
        }
        else
        {
            GD.Print("TerrainManager: All 25 regions already exist.");
        }
    }

    // Helpers for gameplay code
    public float GetHeight(Vector3 worldPos)
    {
        if (_terrainData == null) return 0f;
        Variant h = _terrainData.Call("get_height", worldPos);
        return h.VariantType == Variant.Type.Float ? h.AsSingle() : 0f;
    }

    public Vector3 GetNormal(Vector3 worldPos)
    {
        if (_terrainData == null) return Vector3.Up;
        Variant n = _terrainData.Call("get_normal", worldPos);
        return n.VariantType == Variant.Type.Vector3 ? n.AsVector3() : Vector3.Up;
    }

    public Vector2I WorldToRegion(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.X / RegionSize);
        int z = Mathf.FloorToInt(worldPos.Z / RegionSize);
        return new Vector2I(x, z);
    }
}