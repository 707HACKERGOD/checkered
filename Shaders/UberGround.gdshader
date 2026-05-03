shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert, specular_schlick_ggx;

// --- GLOBALS ---
global uniform float rain_amount : hint_range(0.0, 1.0) = 0.0;
global uniform float snow_amount : hint_range(0.0, 1.0) = 0.0;
global uniform float ice_amount  : hint_range(0.0, 1.0) = 0.0;
global uniform vec3 wind_direction = vec3(1.0, 0.0, 0.0);

// --- TEXTURE SLOTS ---
group_uniforms Base_Textures;
uniform sampler2D albedo_texture : source_color, filter_nearest_mipmap;
uniform sampler2D normal_texture : hint_normal, filter_nearest_mipmap;
uniform sampler2D roughness_texture : hint_default_white, filter_nearest_mipmap;

group_uniforms Effects;
uniform sampler2D noise_texture : hint_default_black, filter_nearest, repeat_enable;
uniform float noise_scale = 1.0;

// --- SETTINGS ---
const float BLOCK_SIZE = 30.0;       
const float MAX_BLOCK_HEIGHT = 1.0; 
const float HEIGHT_STEP = 0.15;      

// COLORS
const vec3 SNOW_COLOR = vec3(0.7, 0.9, 0.98); 
const vec3 ICE_COLOR = vec3(0.85, 0.92, 0.98); 
const vec3 WALL_BOTTOM_COLOR = vec3(0.4, 0.6, 0.9); 
const vec3 WALL_TOP_COLOR    = vec3(0.7, 0.9, 1.0); 

const float MAX_FLAT_SNOW_COVERAGE = 0.95; 
const float MAX_PUDDLE_COVERAGE = 0.7; 
const float PUDDLE_DARKENING = 0.5;

// Varyings
varying float v_is_cuboid; 
varying vec3 v_flat_normal;
varying vec3 v_world_normal;

// -----------------------------------------------------------------------------
// VERTEX SHADER
// -----------------------------------------------------------------------------
void vertex() {
	v_world_normal = (MODEL_NORMAL_MATRIX * NORMAL);

	// 1. Grid Snap
	vec2 safe_uv = UV + vec2(0.01);
	vec2 grid_uv = floor(safe_uv * BLOCK_SIZE) / BLOCK_SIZE;
	
	// 2. Sample Noise
	float noise_val = textureLod(noise_texture, grid_uv * noise_scale, 0.0).r;
	
	// 3. TARGET HEIGHT CALCULATION
	float height_tier = floor(smoothstep(0.4, 0.9, noise_val) * (MAX_BLOCK_HEIGHT / HEIGHT_STEP));
	float target_height = height_tier * HEIGHT_STEP;
	
	// 4. ANIMATION LOGIC
	float cuboid_timeline = smoothstep(0.4, 1.0, snow_amount);
	float activation_threshold = 1.0 - noise_val;
	float growth_progress = smoothstep(activation_threshold - 0.1, activation_threshold + 0.1, cuboid_timeline * 1.2);
	float current_height = target_height * growth_progress;

	VERTEX.y += current_height;
	
	v_is_cuboid = step(0.01, current_height); 
	v_flat_normal = vec3(0.0, 1.0, 0.0);
}

// -----------------------------------------------------------------------------
// FRAGMENT SHADER
// -----------------------------------------------------------------------------
void fragment() {
	vec4 albedo_read = texture(albedo_texture, UV);
	vec3 base_albedo = albedo_read.rgb;
	vec3 base_normal = texture(normal_texture, UV).rgb;
	float base_rough = texture(roughness_texture, UV).r;
	float noise_val = texture(noise_texture, UV * noise_scale).r;
	
	bool is_wall = (v_is_cuboid > 0.01 && v_is_cuboid < 0.99);
	bool is_top  = (v_is_cuboid >= 0.99);
	
// =========================================================================
// LAYER 1: GROUND EFFECTS (MULTIPLY & SUBTLE WATER)
// =========================================================================

// --- SETTINGS ---
float rock_min_roughness = 0.4; 
vec3 puddle_tint = vec3(0.01, 0.02, 0.04); // Deep Navy
float PIXEL_DENSITY = 300.0; 

// -------------------

float final_specular = 0.5;

if (!is_top && !is_wall) {

    // --- 1. PIXEL GEOMETRY ---
    vec2 pixel_uv = floor(UV * PIXEL_DENSITY) / PIXEL_DENSITY;

    // --- 2. MASKING ---
    float noise_val_snapped = texture(noise_texture, pixel_uv * noise_scale).r;
    float water_level = rain_amount * MAX_PUDDLE_COVERAGE;
    float puddle_mask = 1.0 - smoothstep(water_level - 0.05, water_level, noise_val_snapped);

    // --- 3. GROUND VISUALS ---
    vec3 damp_albedo = base_albedo * mix(1.0, 0.5, rain_amount);
    float damp_rough = clamp(base_rough, rock_min_roughness, 1.0); 

    // --- 4. WATER VISUALS (Multiply & Subtle) ---
    
    float pixel_noise = texture(noise_texture, pixel_uv * 10.0).r;
    
    // STEP A: THE MULTIPLY EFFECT (Contrast)
    // We darken the ground by 40% (multiplying by 0.6). 
    // This simulates the "Wet" look where materials get darker and richer.
    vec3 wet_ground_dark = damp_albedo * 0.6;
    
    // STEP B: TRANSPARENCY (20% Tint)
    // Mix: 80% Dark Wet Ground + 20% Water Tint.
    // This allows the rock texture to show through clearly.
    vec3 puddle_albedo = mix(wet_ground_dark, puddle_tint, 0.2);

    // STEP C: ROUGHNESS
    float water_rough = mix(0.02, 0.1, pixel_noise);
    
    // --- 5. NORMALS ---
    vec3 final_normal = base_normal;
    
    if (rain_amount > 0.01) {
        float time_scale = TIME * 2.0; 
        
        vec2 ripple_uv = pixel_uv * 2.0 + vec2(0.0, time_scale * 0.05);
        float ripple = texture(noise_texture, ripple_uv).r;
        
        vec3 pixel_tilt = vec3(pixel_noise - 0.5, fract(pixel_noise * 10.0) - 0.5, 0.0);
        
        // Tilt Intensity: 0.15 (Subtle pixelation)
        vec3 water_normal = normalize(vec3(0.0, 0.0, 1.0) + (pixel_tilt * 0.15) + vec3(ripple * 0.1));
        
        // 80% Water Normal, 20% Rock Normal
        vec3 hybrid_normal = mix(base_normal, water_normal, 0.8);
        
        final_normal = mix(base_normal, hybrid_normal, puddle_mask);
    }

    // --- 6. APPLY ---
    base_albedo = mix(damp_albedo, puddle_albedo, puddle_mask);
    base_rough = mix(damp_rough, water_rough, puddle_mask);
    base_normal = final_normal;
    
    // SPECULAR FIX: Lowered to 0.15. 
    // This reduces the "Sky Glare" in the distance so it doesn't look like white snow.
    final_specular = mix(0.2, 0.15, puddle_mask);

    // --- ICE (Unchanged) ---
    float ice_level = ice_amount * 1.1;
    float ice_mask = 1.0 - smoothstep(ice_level - 0.1, ice_level, noise_val);
    float ice_opacity = ice_mask * clamp(0.4 + 0.6 * noise_val, 0.0, 0.9);
    
    base_albedo = mix(base_albedo, ICE_COLOR, ice_opacity);
    base_rough = mix(base_rough, 0.05, ice_opacity);
    base_normal = mix(base_normal, vec3(0.0, 0.0, 1.0), ice_opacity * 0.8);
    final_specular = mix(final_specular, 1.0, ice_opacity);

    // --- FLAT SNOW (Unchanged) ---
    float flat_snow_timeline = smoothstep(0.0, 0.6, snow_amount);
    float chunk_noise = floor(noise_val * 8.0) / 8.0;
    float start_fade = 1.0 - chunk_noise;
    float chunk_alpha = smoothstep(start_fade - 0.1, start_fade + 0.1, flat_snow_timeline * 1.1);
    
    vec3 snow_bias_vec = normalize(vec3(0.0, 1.0, 0.0) + (wind_direction * 0.5));
    float surface_exposure = dot(normalize(v_world_normal), snow_bias_vec);
    float slope_mask = step(0.3, surface_exposure);
    
    float final_snow_mask = chunk_alpha * slope_mask * MAX_FLAT_SNOW_COVERAGE;
    
    base_albedo = mix(base_albedo, SNOW_COLOR, final_snow_mask);
    base_rough = mix(base_rough, 1.0, final_snow_mask);
    base_normal = mix(base_normal, vec3(0.5, 0.5, 1.0), final_snow_mask);
    final_specular = mix(final_specular, 0.1, final_snow_mask); 
}

SPECULAR = final_specular;
METALLIC = 0.0;


	
	// =========================================================================
	// LAYER 2: CUBOID BLOCKS
	// =========================================================================
	if (is_top) {
		base_albedo = SNOW_COLOR;
		base_rough = 1.0;
		NORMAL = (VIEW_MATRIX * vec4(v_flat_normal, 0.0)).xyz;
		NORMAL_MAP = vec3(0.5, 0.5, 1.0);
	}
	else if (is_wall) {
		vec3 wall_color = mix(WALL_BOTTOM_COLOR, WALL_TOP_COLOR, v_is_cuboid);
		base_albedo = wall_color;
		base_rough = 1.0;
		NORMAL = (VIEW_MATRIX * vec4(v_flat_normal, 0.0)).xyz;
		NORMAL_MAP = vec3(0.5, 0.5, 1.0);
	}

	ALBEDO = base_albedo;
	ROUGHNESS = base_rough;
	NORMAL_MAP = NORMAL_MAP;
}