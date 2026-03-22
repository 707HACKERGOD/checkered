shader_type spatial;
render_mode unshaded, skip_vertex_transform;

uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform vec2 resolution = vec2(320.0, 240.0);
uniform float sanity = 1.0;
uniform float distortion = 0.0;

void vertex() {
    // Snap vertices to grid for PS1 wobble (optional, usually done on models not screen)
    POSITION = vec4(VERTEX, 1.0);
}

void fragment() {
    vec2 uv = SCREEN_UV;
    
    // Barrel distortion (insanity effect)
    if (distortion > 0.0) {
        vec2 center = uv - 0.5;
        float dist = length(center);
        uv = center * (1.0 + distortion * dist * dist) + 0.5;
    }

    // Pixelate
    vec2 grid_uv = floor(uv * resolution) / resolution;
    
    // Chromatic Aberration
    vec3 color;
    if (sanity < 0.7) {
        float offset = (0.7 - sanity) * 0.01;
        float r = texture(screen_texture, grid_uv + vec2(offset, 0.0)).r;
        float g = texture(screen_texture, grid_uv).g;
        float b = texture(screen_texture, grid_uv - vec2(offset, 0.0)).b;
        color = vec3(r, g, b);
    } else {
        color = texture(screen_texture, grid_uv).rgb;
    }
    
    // Color quantization (Color banding)
    float levels = 32.0 - (1.0 - sanity) * 16.0;
    color = floor(color * levels) / levels;

    ALBEDO = color;
}
