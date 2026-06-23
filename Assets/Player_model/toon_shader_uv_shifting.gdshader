// toon_shader_uv_shifting.gdshader — FULL REPLACEMENT
shader_type spatial;
render_mode cull_disabled, depth_draw_always, specular_disabled;

uniform vec2 eye_uv_offset = vec2(0.0);

// ─── Textures & Base ───
uniform sampler2D albedo_tex : source_color, filter_nearest;
uniform vec4 base_color : source_color = vec4(1.0);

// ─── Toon Ramp Colors ───
uniform vec4 color_shadow : source_color = vec4(0.15, 0.12, 0.22, 1.0);
uniform vec4 color_mid    : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 color_highlight : source_color = vec4(1.0, 0.97, 0.93, 1.0);

// ─── Toon Ramp Thresholds ───
uniform float threshold_shadow : hint_range(-1.0, 1.0) = -0.25;
uniform float threshold_mid    : hint_range(-1.0, 1.0) = 0.25;
uniform float threshold_highlight : hint_range(-1.0, 1.0) = 0.65;
uniform float band_softness : hint_range(0.0, 0.5) = 0.08;

// ─── Rim Light ───
uniform vec4 rim_color : source_color = vec4(0.75, 0.85, 1.0, 1.0);
uniform float rim_strength : hint_range(0.0, 2.0) = 0.35;
uniform float rim_power : hint_range(0.5, 8.0) = 3.5;
uniform float rim_bias : hint_range(0.0, 1.0) = 0.25;

// ─── Stylized Specular ───
uniform vec4 specular_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float specular_strength : hint_range(0.0, 1.0) = 0.0;
uniform float specular_size : hint_range(0.001, 0.5) = 0.08;
uniform float specular_softness : hint_range(0.0, 0.2) = 0.03;

// ─── Ambient Fill ───
uniform vec4 ambient_color : source_color = vec4(0.20, 0.18, 0.28, 1.0);
uniform float ambient_strength : hint_range(0.0, 1.0) = 0.20;

void fragment() {
    // ── Backface normal correction ──
    if (!FRONT_FACING) {
        NORMAL = -NORMAL;
    }

    // ★ FIX #2: Determine UV BEFORE sampling, not inside a dead branch
    vec2 final_uv = UV;
    if (!FRONT_FACING) {
        final_uv = UV + eye_uv_offset;
    }

    vec4 tex_color = texture(albedo_tex, final_uv) * base_color;
    ALBEDO = tex_color.rgb;
    ALPHA = tex_color.a;
    ALPHA_SCISSOR_THRESHOLD = 0.5;

    // ── Ambient fill + Rim (added via EMISSION so it survives custom light()) ──
    vec3 ambient = ALBEDO * ambient_color.rgb * ambient_strength;

    float rim = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    rim = pow(rim, rim_power);
    rim = smoothstep(0.25, 0.65, rim) * rim_strength;
    vec3 rim_light = rim_color.rgb * rim;

    EMISSION = ambient + rim_light;
}

void light() {
    float nDotL = dot(NORMAL, LIGHT);

    // ── 3-Band Toon Ramp ──
    float s1 = smoothstep(threshold_shadow - band_softness, threshold_shadow + band_softness, nDotL);
    float s2 = smoothstep(threshold_mid - band_softness,    threshold_mid + band_softness,    nDotL);
    float s3 = smoothstep(threshold_highlight - band_softness, threshold_highlight + band_softness, nDotL);

    vec3 shadow  = color_shadow.rgb  * (1.0 - s1);
    vec3 mid     = color_mid.rgb     * s1 * (1.0 - s2);
    vec3 highlight = color_highlight.rgb * s2;

    vec3 ramp = shadow + mid + highlight;

    // ── Diffuse ──
    DIFFUSE_LIGHT += ALBEDO * ramp * LIGHT_COLOR * ATTENUATION;

    // ── Stylized Specular (Blinn-Phong with hard toon edge) ──
    if (specular_strength > 0.0) {
        vec3 half_vec = normalize(LIGHT + VIEW);
        float spec_angle = max(dot(NORMAL, half_vec), 0.0);
        float spec = smoothstep(
            1.0 - specular_size - specular_softness,
            1.0 - specular_size + specular_softness,
            spec_angle
        );
        SPECULAR_LIGHT += spec * specular_strength * specular_color.rgb * LIGHT_COLOR * ATTENUATION;
    }

    // ── Light-aware Rim boost ──
    float rim_boost = (1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0));
    rim_boost = pow(rim_boost, rim_power);
    rim_boost = smoothstep(0.3, 0.7, rim_boost);
    rim_boost *= mix(1.0, max(nDotL, 0.0), rim_bias);
    DIFFUSE_LIGHT += ALBEDO * rim_boost * rim_strength * 0.4 * rim_color.rgb * LIGHT_COLOR * ATTENUATION;
}