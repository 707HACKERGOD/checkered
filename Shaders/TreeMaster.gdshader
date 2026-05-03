shader_type spatial;
render_mode depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

// --- GLOBAL VARIABLES ---
global uniform vec3 wind_direction;
global uniform float wind_strength; 

global uniform float global_time;

// --- PER-MATERIAL SETTINGS ---
uniform sampler2D albedo_texture : source_color, filter_nearest_mipmap;
uniform float sway_speed : hint_range(0.0, 5.0) = 1.0;
uniform float sway_intensity : hint_range(0.0, 2.0) = 0.5;

void vertex() {
    // 1. DETERMINE SWAY MASK
    float height_factor = max(0.0, VERTEX.y); 
    float paint_mask = COLOR.r; 
    float final_mask = paint_mask * height_factor; 
    
    // --- CRITICAL FIX: NORMALIZE WIND DIRECTION ---
    // If wind_direction has length 20, we crush it down to length 1.
    // If it's zero, we use a safe default to prevent errors.
    vec3 safe_wind_dir = wind_direction;
    if (length(safe_wind_dir) < 0.001) {
        safe_wind_dir = vec3(1.0, 0.0, 0.0);
    } else {
        safe_wind_dir = normalize(safe_wind_dir);
    }

    // 2. CALCULATE ANIMATION
    float time = global_time * min(sway_speed, 4.0); 
    float global_offset = (NODE_POSITION_WORLD.x + NODE_POSITION_WORLD.z) * 0.5;
    
    // --- MAIN SWAY FIX ---
    // Now we use 'safe_wind_dir' (Length 1) instead of 'wind_direction' (Length 20)
    // The total force is now strictly: 1.0 * min(20, 2) = 2.0 Max.
    vec3 main_sway = safe_wind_dir * sin(time + global_offset) * min(wind_strength, 4.0) * sway_intensity * 0.03;
    
    // --- FLUTTER FIX ---
    // This part was already okay because it didn't use the wind_direction vector, 
    // but the clamp ensures it stays consistent.
    float flutter = sin(time * 3.0 + (VERTEX.x * 10.0)) * 0.005 * min(wind_strength, 4.0);
    vec3 flutter_vec = vec3(flutter, 0.0, flutter);

    // 3. APPLY
    VERTEX += (main_sway + flutter_vec) * final_mask;
}

void fragment() {
    vec4 tex = texture(albedo_texture, UV);
    if (tex.a < 0.5) discard;
    
    ALBEDO = tex.rgb; 
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
    
    // Fake Volume Normals
    NORMAL = normalize(mix(NORMAL, vec3(0.0, 1.0, 0.0), 0.5));
}