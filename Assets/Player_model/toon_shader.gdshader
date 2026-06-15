shader_type spatial;
render_mode cull_disabled, depth_draw_always, specular_disabled;

uniform sampler2D albedo_tex : source_color, filter_nearest;
uniform vec4 base_color : source_color = vec4(1.0);

// ── Toon Ramp ──
uniform vec4 color_shadow : source_color = vec4(0.4, 0.35, 0.5, 1.0);
uniform vec4 color_mid    : source_color = vec4(0.55, 0.50, 0.65, 1.0);
uniform vec4 color_lit    : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float threshold_shadow : hint_range(-1.0, 1.0) = -0.2;
uniform float threshold_mid    : hint_range(-1.0, 1.0) = 0.15;
uniform float threshold_lit    : hint_range(-1.0, 1.0) = 0.6;
uniform float band_softness : hint_range(0.0, 0.5) = 0.0;

// ── Scene Light (updated every frame by script) ──
uniform vec3 scene_sun_dir = vec3(0.0, 1.0, 0.0);
uniform vec4 scene_sun_color : source_color = vec4(1.0, 0.95, 0.90, 1.0);
uniform float scene_sun_energy : hint_range(0.0, 5.0) = 1.0;

// ── Scene Ambient (updated every frame by script) ──
uniform vec4 scene_ambient : source_color = vec4(0.15, 0.14, 0.20, 1.0);
uniform float scene_ambient_energy : hint_range(0.0, 1.0) = 0.2;

// ── Rim (optional) ──
uniform vec4 rim_color : source_color = vec4(0.8, 0.9, 1.0, 1.0);
uniform float rim_strength : hint_range(0.0, 1.0) = 0.0;
uniform float rim_power : hint_range(0.5, 8.0) = 3.0;

void fragment() {
	if (!FRONT_FACING) {
		NORMAL = -NORMAL;
	}
	
	vec4 tex_color = texture(albedo_tex, UV) * base_color;
	ALBEDO = tex_color.rgb;
	ALPHA = tex_color.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
	
	// Rim only (no hardcoded ambient — ambient comes from light())
	if (rim_strength > 0.0) {
		float rim = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
		rim = pow(rim, rim_power);
		rim = smoothstep(0.3, 0.7, rim) * rim_strength;
		EMISSION = ALBEDO * rim_color.rgb * rim;
	}
}

void light() {
	float nDotL = max(dot(NORMAL, scene_sun_dir), 0.0);
	
	// 3-band toon ramp
	float s1 = smoothstep(threshold_shadow - band_softness, threshold_shadow + band_softness, nDotL);
	float s2 = smoothstep(threshold_mid - band_softness, threshold_mid + band_softness, nDotL);
	float s3 = smoothstep(threshold_lit - band_softness, threshold_lit + band_softness, nDotL);
	
	vec3 shadow = color_shadow.rgb * (1.0 - s1);
	vec3 mid = color_mid.rgb * s1 * (1.0 - s2);
	vec3 lit = color_lit.rgb * s2;
	
	vec3 toon = shadow + mid + lit;
	
	// Sun contribution
	vec3 sun = ALBEDO * toon * scene_sun_color.rgb * scene_sun_energy;
	
	// Ambient contribution: flat, tinted, always present but dim
	// We multiply by a flattened toon so shadowed areas stay darker than lit areas
	vec3 ambient_toon = mix(vec3(0.6), vec3(1.0), nDotL); // simple hemisphere
	vec3 ambient = ALBEDO * ambient_toon * scene_ambient.rgb * scene_ambient_energy;
	
	DIFFUSE_LIGHT += sun + ambient;
}