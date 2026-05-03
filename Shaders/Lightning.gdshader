shader_type spatial;
// Keep fog_disabled so it punches through the storm
render_mode unshaded, cull_disabled, blend_add, fog_disabled;

uniform float seed : hint_range(0.0, 100.0) = 0.0;
uniform float vanish_value : hint_range(0.0, 1.0) = 0.0; 

// Simple hash
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

// 2D Noise
float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void fragment() {
    vec2 uv = UV;
    
    // --- JAGGED SHAPE ---
    // High frequency noise for "electric" look
    float jagged = noise(vec2(uv.y * 20.0, seed)) * 0.15; 
    // Add a second layer of detail
    jagged += noise(vec2(uv.y * 50.0, seed * 2.0)) * 0.05;
    
    float center = 0.5 + jagged;
    
    // Taper at bottom (1.0) and top (0.0)
    // We want it thickest in the middle-top, thinnest at ground
    float width_profile = smoothstep(0.0, 0.2, uv.y) * (1.0 - smoothstep(0.8, 1.0, uv.y)); 
    float thickness = 0.01 + (0.04 * width_profile); 
    
    // Distance from center line
    float d = abs(uv.x - center);
    
    // Core (White)
    float core = 1.0 - smoothstep(0.0, thickness * 0.5, d);
    
    // Glow (Blue)
    float glow = 0.7 - smoothstep(0.0, thickness * 3.0, d);
    
    // Combine
    // Core is 10.0 brightness (Pure White)
    // Glow is Blue-ish
    vec3 bolt_color = vec3(2.0, 2.0, 5.0) * core + vec3(0.2, 0.4, 1.0) * glow * 2.0;
    
    float visibility = 1.0 - vanish_value;
    
    ALBEDO = bolt_color;
    ALPHA = clamp(core + glow, 0.0, 1.0) * visibility;
}