using Godot;

namespace Crafting
{
    // World-anchored build grid that drapes over terrain. The mesh snaps in
    // 1 m cells under the player and its vertices are raycast onto the floor
    // layer; visible lines are computed from world position in the shader, so
    // the grid never shifts as you move.
    public partial class GlobalGrid : MeshInstance3D
    {
        [Export] public uint FloorMask = 2;      // your layer 2 = Floor
        [Export] public float Size = 16f;
        [Export] public int Segments = 16;

        ShaderMaterial _mat;
        Vector3[] _verts;
        int[] _indices;
        ArrayMesh _mesh;
        Vector3 _lastCell = new(float.MaxValue, 0f, float.MaxValue);
        bool _wasVisible;

        const string Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled;
uniform vec4 minor_color : source_color = vec4(1.0, 1.0, 1.0, 0.10);
uniform vec4 major_color : source_color = vec4(1.0, 1.0, 1.0, 0.25);
uniform float cell = 0.25;
uniform float major = 1.0;
uniform vec2 u_center = vec2(0.0);
varying vec3 v_world;
void vertex() { v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    vec2 g = v_world.xz;
    vec2 q1 = abs(fract(g / cell) - 0.5) / max(fwidth(g / cell), vec2(1e-5));
    float minor_l = 1.0 - min(min(q1.x, q1.y), 1.0);
    vec2 q2 = abs(fract(g / major) - 0.5) / max(fwidth(g / major), vec2(1e-5));
    float major_l = 1.0 - min(min(q2.x, q2.y), 1.0);
    float fade = 1.0 - smoothstep(4.5, 7.0, distance(g, u_center));
    float a = max(minor_l * minor_color.a, major_l * major_color.a) * fade;
    if (a < 0.004) discard;
    ALBEDO = major_l > minor_l ? major_color.rgb : minor_color.rgb;
    ALPHA = a;
}
";

        public override void _Ready()
        {
            _mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
            MaterialOverride = _mat;
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

            int n = Segments + 1;
            float cellSize = Size / Segments;
            _verts = new Vector3[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    _verts[z * n + x] = new Vector3((x - Segments * 0.5f) * cellSize, 0f, (z - Segments * 0.5f) * cellSize);

            _indices = new int[Segments * Segments * 6];
            int ii = 0;
            for (int z = 0; z < Segments; z++)
                for (int x = 0; x < Segments; x++)
                {
                    int a = z * n + x, b = a + 1, c = a + n, d = c + 1;
                    _indices[ii++] = a; _indices[ii++] = c; _indices[ii++] = b;
                    _indices[ii++] = b; _indices[ii++] = c; _indices[ii++] = d;
                }

            _mesh = new ArrayMesh();
            BuildSurface();
            Mesh = _mesh;
            Visible = false;
        }

        void BuildSurface()
        {
            while (_mesh.GetSurfaceCount() > 0) _mesh.SurfaceRemove(0);
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _verts;
            arrays[(int)Mesh.ArrayType.Index] = _indices;
            _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }

        public override void _Process(double delta)
        {
            if (!Visible) { _wasVisible = false; return; }
            if (!_wasVisible) { _lastCell = new Vector3(float.MaxValue, 0f, float.MaxValue); _wasVisible = true; }

            var p = CraftingSim.Instance?.Player as Node3D;
            if (p == null || !GodotObject.IsInstanceValid(p)) return;

            float snap = Size / Segments;
            var cellPos = new Vector3(Mathf.Floor(p.GlobalPosition.X / snap) * snap, 0f, Mathf.Floor(p.GlobalPosition.Z / snap) * snap);
            if (cellPos != _lastCell || cellPos != GlobalPosition)
            {
                _lastCell = cellPos;
                GlobalPosition = cellPos;
                _lastCell = cellPos;
                GlobalPosition = cellPos;
                Drape(p.GlobalPosition.Y + 0.05f);
            }
            _mat.Set("u_center", new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z));
        }

        void Drape(float fallbackY)
        {
            var space = GetWorld3D().DirectSpaceState;
            for (int i = 0; i < _verts.Length; i++)
            {
                var wp = GlobalPosition + _verts[i];
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                    new Vector3(wp.X, 500f, wp.Z), new Vector3(wp.X, -500f, wp.Z), FloorMask));
                // miss → keep the grid at the player's feet instead of burying it
                _verts[i].Y = hit.Count > 0 ? hit["position"].AsVector3().Y : fallbackY;
            }
            BuildSurface();
        }
    }
}