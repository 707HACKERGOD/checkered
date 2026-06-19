shader_type spatial;
render_mode cull_disabled, depth_draw_always, specular_disabled, ambient_light_disabled;

uniform sampler2D albedo_tex : source_color, filter_nearest;
uniform vec4 base_color : source_color = vec4(1.0);

// Toon Ramp
uniform vec4 color_shadow : source_color = vec4(0.35, 0.25, 0.38, 1.0);
uniform vec4 color_mid    : source_color = vec4(0.70, 0.62, 0.72, 1.0);
uniform vec4 color_lit    : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float threshold_shadow : hint_range(-1.0, 1.0) = 0.05;
uniform float threshold_mid    : hint_range(-1.0, 1.0) = 0.45;
uniform float band_softness : hint_range(0.0, 0.5) = 0.02;

// Ambient
uniform vec4 scene_ambient : source_color = vec4(0.25, 0.22, 0.32, 1.0);
uniform float scene_ambient_energy : hint_range(0.0, 1.0) = 0.35;

// Rim
uniform vec4 rim_color : source_color = vec4(0.8, 0.9, 1.0, 1.0);
uniform float rim_strength : hint_range(0.0, 1.0) = 0.25;
uniform float rim_power : hint_range(0.5, 8.0) = 3.0;

void fragment() {
    if (!FRONT_FACING) NORMAL = -NORMAL;

    vec4 tex_color = texture(albedo_tex, UV) * base_color;
    ALBEDO = tex_color.rgb;
    ALPHA = tex_color.a;
    ALPHA_SCISSOR_THRESHOLD = 0.5;

    // Ambient (applied once, not per-light)
    vec3 ambient = ALBEDO * scene_ambient.rgb * scene_ambient_energy;

    // Rim light
    float rim = 0.0;
    if (rim_strength > 0.0) {
        rim = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
        rim = pow(rim, rim_power);
        rim = smoothstep(0.3, 0.7, rim) * rim_strength;
    }

    EMISSION = ambient + ALBEDO * rim_color.rgb * rim;
}

void light() {
    // Use Godot's built-in light direction and shadow
    float nDotL = dot(NORMAL, LIGHT);
    float lit_amount = nDotL * ATTENUATION;

    // 2-threshold toon ramp (3 bands)
    float s1 = smoothstep(threshold_shadow - band_softness, threshold_shadow + band_softness, lit_amount);
    float s2 = smoothstep(threshold_mid - band_softness, threshold_mid + band_softness, lit_amount);

    vec3 toon = color_shadow.rgb;
    toon = mix(toon, color_mid.rgb, s1);
    toon = mix(toon, color_lit.rgb, s2);

    DIFFUSE_LIGHT += ALBEDO * toon * LIGHT_COLOR;
}