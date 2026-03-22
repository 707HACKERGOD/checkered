shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded;

global uniform float snow_amount;
global uniform vec3 wind_direction;

// PS1 Noise Settings
const float NOISE_SCALE = 200.0; // Higher = finer grain

// Simple pseudo-random hash (No texture required!)
float hash(vec2 p) {
    vec3 p3  = fract(vec3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

void fragment() {
    // 1. Wind Bias (Snow sticks to windward side + Up)
    vec3 bias = normalize(vec3(0.0, 1.0, 0.0) + (wind_direction * 0.5));
    float up_facing = dot(NORMAL, bias);

    // 2. Procedural Grainy Noise
    // We use World Position (NODE_POSITION_WORLD + VERTEX) so snow matches across objects
    vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float noise = hash(world_pos.xz * NOISE_SCALE);

    // 3. Accumulation Logic
    // We mix the noise into the "Up Facing" value
    float coverage = (up_facing * 0.6) + (noise * 0.4);
    
    // Threshold moves down as snow_amount goes up
    float threshold = 1.0 - (snow_amount * 1.1); 

    // Hard Step = PS1 pixelated edges
    float snow_mask = step(threshold, coverage);
    
    // 4. Output
    ALBEDO = vec3(0.95, 0.95, 1.0);
    
    // Transparency: 0.0 if no snow, 0.9 if snow (slightly see-through)
    ALPHA = snow_mask * 0.9; 
}