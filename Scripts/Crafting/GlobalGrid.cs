using Godot;

namespace Crafting
{
    // World-anchored build grid (G). Lines are computed from world position in
    // the shader, so the plane can follow the player (and terrain height)
    // without the grid ever shifting.
    public partial class GlobalGrid : MeshInstance3D
    {
        [Export] public uint FloorMask = 2;   // your layer 2 = Floor

        ShaderMaterial _mat;

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
            Mesh = new PlaneMesh { Size = new Vector2(16, 16) };
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            Visible = false;
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;
            var p = CraftingSim.Instance?.Player as Node3D;
            if (p == null || !GodotObject.IsInstanceValid(p)) return;
            float y = 0.03f;
            var from = p.GlobalPosition + Vector3.Up * 2f;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(
                PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 80f, FloorMask));
            if (hit.Count > 0) y = hit["position"].AsVector3().Y + 0.03f;
            GlobalPosition = new Vector3(p.GlobalPosition.X, y, p.GlobalPosition.Z);
            _mat.Set("u_center", new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z));
        }
    }
}