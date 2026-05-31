shader_type canvas_item;
render_mode unshaded;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

uniform vec2 resolution = vec2(320.0, 240.0);
uniform vec2 pixelation_resolution = vec2(320.0, 240.0);
uniform float pixelation_strength : hint_range(0.0, 1.0) = 0.0;

uniform float sanity   : hint_range(0.0, 1.0) = 1.0;
uniform float distortion : hint_range(0.0, 1.0) = 0.0;
uniform float time = 0.0;

uniform float quantization       : hint_range(0.0, 1.0) = 0.0;
uniform float scanline_intensity : hint_range(0.0, 2.0) = 0.0; // OFF by default
uniform float vignette_intensity : hint_range(0.0, 1.0) = 0.5;

void fragment() {
    vec2 screen_uv = SCREEN_UV;
    vec2 uv = screen_uv;

    // --- 1. Distortion (high-speed wave, constant speed) ---
    if (distortion > 0.0 && sanity < 0.15) {
        float waveIntensity = 1.0 - smoothstep(0.0, 0.15, sanity);
        float t = time * 30.0;
        float jx = sin(uv.y * 40.0 + t) * cos(uv.y * 23.0 + t * 1.3);
        float jy = cos(uv.x * 40.0 + t * 1.1) * sin(uv.x * 27.0 + t * 0.7);
        uv.x += jx * distortion * 0.015 * waveIntensity;
        uv.y += jy * distortion * 0.015 * waveIntensity;
    }

    // --- 2. Screen Shake (CSS .insanity-high equivalent) ---
    // ONLY below sanity 0.15, and intensity is gradual 0 -> 1
    float px_x = 1.0 / resolution.x;
    float px_y = 1.0 / resolution.y;

    if (sanity < 0.15) {
        float shakeIntensity = 1.0 - smoothstep(0.0, 0.15, sanity); // 0 at 0.15, 1 at 0
        float t = time * 20.0;
        uv.x += (sin(t * 3.7) * 2.0 + cos(t * 2.3) * 1.5) * px_x * shakeIntensity;
        uv.y += (cos(t * 4.1) * 1.0 + sin(t * 3.1) * 2.0) * px_y * shakeIntensity;
    }

    // --- 3. Pixelation ---
    vec2 pixel_uv;
    if (pixelation_strength > 0.0) {
        vec2 snapped_uv = floor(uv * pixelation_resolution) / pixelation_resolution;
        pixel_uv = mix(uv, snapped_uv, pixelation_strength);
    } else {
        pixel_uv = uv;
    }

    vec3 color = texture(SCREEN_TEXTURE, pixel_uv).rgb;

    // --- 4. CSS Filter Effects (gradual, no hard cuts) ---
    // Saturation: 1.0 at sanity >= 0.6  ->  0.5 at sanity 0
    float sat = mix(0.5, 1.0, smoothstep(0.0, 0.6, sanity));

    // Contrast: 1.0 at sanity >= 0.6  ->  1.2 at sanity 0
    float con = mix(1.2, 1.0, smoothstep(0.0, 0.6, sanity));

    // Hue rotate: 0 at sanity >= 0.3  ->  10deg at sanity 0
    float hue_rot = mix(10.0 * 3.14159265 / 180.0, 0.0, smoothstep(0.0, 0.3, sanity));

    float lum = dot(color, vec3(0.299, 0.587, 0.114));
    color = mix(vec3(lum), color, sat);
    color = (color - 0.5) * con + 0.5;

    if (hue_rot != 0.0) {
        float c = cos(hue_rot);
        float s = sin(hue_rot);
        mat3 hue_mat = mat3(
            vec3(0.299 + 0.701*c + 0.168*s, 0.587 - 0.587*c + 0.330*s, 0.114 - 0.114*c - 0.497*s),
            vec3(0.299 - 0.299*c - 0.328*s, 0.587 + 0.413*c + 0.035*s, 0.114 - 0.114*c + 0.292*s),
            vec3(0.299 - 0.300*c + 1.250*s, 0.587 - 0.588*c - 1.050*s, 0.114 + 0.886*c - 0.203*s)
        );
        color = hue_mat * color;
    }

    // --- 5. Color Quantization (optional) ---
    if (quantization > 0.0) {
        float levels = mix(256.0, 16.0, quantization);
        color = floor(color * levels) / levels;
    }

    // --- 6. Scanlines ---
    float scanline = sin(screen_uv.y * resolution.y * 3.14159) * 0.03 * scanline_intensity;
    color -= scanline;

    // --- 7. Vignette ---
    vec2 vig_uv = screen_uv - 0.5;
    float vig = 1.0 - dot(vig_uv, vig_uv) * vignette_intensity;
    color *= vig;

    // --- 8. Red tint (gradual: starts at sanity 0.2, reaches max at 0.1) ---
    if (sanity < 0.2) {
        float redMax = (0.3 - 0.1) * 0.2; // 0.04 — matches old shader at sanity=0.1
        float red_t = 1.0 - smoothstep(0.1, 0.2, sanity); // 0 at 0.2, 1 at 0.1 and below
        color.r += red_t * redMax;
    }

    COLOR.rgb = clamp(color, 0.0, 1.0);
}