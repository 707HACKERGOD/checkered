// ps1_post_process_modular.gdshader
shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

// --- MODULAR CONTROLS ---
uniform bool enable_jitter = true;
uniform bool enable_affine_warp = true;
uniform bool enable_color_banding = true;

// --- EFFECT PARAMETERS ---
uniform int color_levels = 16;
uniform float vertex_jitter_amount = 0.5;
uniform float affine_texture_intensity = 0.1;
uniform vec2 viewport_size; // Passed from the C# script

void fragment() {
    vec2 final_uv = SCREEN_UV;

    // 1. Vertex Jitter ('Wobble') Simulation
    if (enable_jitter) {
        vec2 jitter_offset;
        jitter_offset.x = sin(TIME * 20.0) * vertex_jitter_amount / viewport_size.x;
        jitter_offset.y = cos(TIME * 15.0) * vertex_jitter_amount / viewport_size.y;
        final_uv += jitter_offset;
    }

    // 2. Affine Texture Mapping Emulation
    if (enable_affine_warp) {
        float perspective_factor = (final_uv.y - 0.5) * affine_texture_intensity;
        final_uv.x -= perspective_factor * (final_uv.x - 0.5);
    }

    vec3 screen_color = texture(SCREEN_TEXTURE, final_uv).rgb;

    // 3. Color Quantization (Banding)
    if (enable_color_banding) {
        float levels = float(color_levels);
        screen_color = round(screen_color * levels) / levels;
    }

    COLOR.rgb = screen_color;
}