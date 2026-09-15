shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear;
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform vec3 tint : source_color = vec3(0.18, 0.36, 0.72);
uniform float cell_px = 44.0;

float luma(vec2 uv) {
    return dot(texture(screen_tex, uv).rgb, vec3(0.299, 0.587, 0.114));
}

void fragment() {
    vec3 base = texture(screen_tex, SCREEN_UV).rgb;
    float l = luma(SCREEN_UV);
    vec3 blue = mix(vec3(l), tint * (0.25 + l * 1.6), 0.85);

    vec2 uv_px = SCREEN_UV / SCREEN_PIXEL_SIZE;
    vec2 g = fract(uv_px / cell_px);
    vec2 dg = min(g, 1.0 - g);
    float grid = 1.0 - smoothstep(0.0, 0.06, min(dg.x, dg.y));
    vec2 g5 = fract(uv_px / (cell_px * 5.0));
    vec2 dg5 = min(g5, 1.0 - g5);
    float grid5 = 1.0 - smoothstep(0.0, 0.03, min(dg5.x, dg5.y));

    vec2 px = SCREEN_PIXEL_SIZE;
    vec2 grad = vec2(luma(SCREEN_UV + vec2(px.x, 0.0)) - luma(SCREEN_UV - vec2(px.x, 0.0)),
                     luma(SCREEN_UV + vec2(0.0, px.y)) - luma(SCREEN_UV - vec2(0.0, px.y)));
    float edge = clamp(length(grad) * 4.0, 0.0, 1.0);

    float sweep = 1.0 - smoothstep(0.0, 0.02, abs(fract(TIME * 0.11) - SCREEN_UV.y));

    vec3 col = mix(base, blue, intensity * 0.9);
    col += tint * (grid * 0.10 + grid5 * 0.35) * intensity;
    col += vec3(0.85, 0.92, 1.0) * (edge * 0.55 + sweep * 0.10) * intensity;
    COLOR = vec4(col, 1.0);
}