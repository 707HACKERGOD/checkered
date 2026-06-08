shader_type spatial;
render_mode unshaded, cull_disabled, depth_test_disabled;

uniform float zone_size : hint_range(1.0, 1000.0) = 256.0;
uniform float line_width : hint_range(0.1, 10.0) = 2.0;
uniform vec4 line_color : source_color = vec4(1.0, 0.0, 0.0, 0.6);

varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    float x_dist = min(mod(world_position.x, zone_size), zone_size - mod(world_position.x, zone_size));
    float z_dist = min(mod(world_position.z, zone_size), zone_size - mod(world_position.z, zone_size));
    
    float x_line = 1.0 - smoothstep(0.0, line_width, x_dist);
    float z_line = 1.0 - smoothstep(0.0, line_width, z_dist);
    
    float grid = max(x_line, z_line);
    
    if (grid < 0.01) {
        discard;
    }
    
    ALBEDO = line_color.rgb;
    ALPHA = grid * line_color.a;
}