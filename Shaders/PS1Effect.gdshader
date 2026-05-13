shader_type canvas_item;
render_mode unshaded;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform vec2 resolution = vec2(320.0, 240.0);
uniform float sanity : hint_range(0.0, 1.0) = 1.0;
uniform float distortion : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    vec2 uv = SCREEN_UV;

    // 1. Distortion at low sanity or during countdown
    if (distortion > 0.0) {
        uv.x += sin(uv.y * 20.0 + TIME * 3.0) * distortion * 0.02;
        uv.y += cos(uv.x * 20.0 + TIME * 2.0) * distortion * 0.02;
    }

    // 2. Pixelate
    vec2 pixel_uv = floor(uv * resolution) / resolution;
    vec3 color = texture(SCREEN_TEXTURE, pixel_uv).rgb;

    // 3. Color quantization (PS1 banding)
    float levels = 32.0;
    color = floor(color * levels) / levels;

    // 4. Scanlines
    float scanline = sin(SCREEN_UV.y * resolution.y * 3.14159) * 0.03;
    color -= scanline;

    // 5. Vignette
    vec2 vig_uv = SCREEN_UV - 0.5;
    float vig = 1.0 - dot(vig_uv, vig_uv) * 0.5;
    color *= vig;

    // 6. Red tint at very low sanity
    if (sanity < 0.3) {
        color.r += (0.3 - sanity) * 0.2;
    }

    COLOR.rgb = color;
}