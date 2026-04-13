shader_type spatial;
render_mode depth_draw_opaque, cull_disabled, diffuse_burley;

// --- GLOBALS ---
global uniform vec3 wind_direction;
global uniform float wind_strength;
global uniform float global_time; 
global uniform vec3 player_position; 
global uniform float player_speed; 
global uniform float player_on_floor;
global uniform vec3 last_floor_position;
global uniform float last_floor_time;
global uniform float gust_mode;
global uniform float spawn_progress;
global uniform float despawn_progress;

// --- SETTINGS ---
uniform sampler2D leaf_texture : source_color, filter_nearest_mipmap;
uniform vec3 leaf_color : source_color = vec3(0.8, 0.1, 0.1);
uniform float dissolve_amount : hint_range(0.0, 1.0) = 0.0;
uniform float y_offset : hint_range(0.0, 0.5) = 0.02; 
uniform float max_dip_depth = -0.15;

const float INTERACT_RADIUS = 1.5;
const float INTERACT_STRENGTH = 0.5;

void vertex() {
    vec3 instance_pos = MODEL_MATRIX[3].xyz;
    vec3 cam_diff = instance_pos - CAMERA_POSITION_WORLD;
    
        VERTEX.y -= dissolve_amount * 0.2;
        VERTEX.y += y_offset;

        // Safely normalize wind for transitions
        vec3 safe_wind = wind_direction;
        if (length(safe_wind) < 0.01) safe_wind = vec3(1.0, 0.0, 0.0);
        vec3 wind_dir_n = normalize(safe_wind);

        float rand_id = fract(sin(dot(instance_pos.xz, vec2(12.9898,78.233))) * 43758.5453);
        float dist_wind = dot(instance_pos, wind_dir_n);

        // --- DESPAWN (autumn -> winter) ---
        if (despawn_progress > 0.01) {
            float t = clamp((despawn_progress * 2.0) - (dist_wind * 0.05) - 0.5, 0.0, 1.0);
            float t2 = t * t;
            float lift = t * 10.0 + sin(t * 10.0) * 0.5;
            float drift = t2 * 25.0 * min(wind_strength, 2.0);
            float spiral = sin(t * 10.0 + rand_id * 10.0) * t * 2.0;

            VERTEX.y += lift + sin(t * 15.0 * rand_id) * 0.2;
            VERTEX.x += wind_dir_n.x * drift + spiral;
            VERTEX.z += wind_dir_n.z * drift + spiral;
            VERTEX *= smoothstep(0.6, 0.0, t);
        }
        // --- SPAWN (summer -> autumn) ---
        else if (spawn_progress < 0.99) {
            float t = smoothstep(0.0, 1.0, (spawn_progress * 1.5) - (rand_id * 0.5));
            float inv_t = 1.0 - t;
            VERTEX.y += inv_t * 10.0;
            
            float drift = inv_t * 2.5 * min(wind_strength, 2.0);
            VERTEX.x -= wind_dir_n.x * drift;
            VERTEX.z -= wind_dir_n.z * drift;
            VERTEX *= smoothstep(0.3, 0.9, t);
        }
        // --- GROUND PHYSICS & GUSTS ---
        else {
            float final_x = 0.0;
            float final_y = 0.0;
            float final_z = 0.0;

            // Idle ambient wind
            float gust_phase = (global_time * 3.0) - (dist_wind * 0.5);
            float wave = (sin(gust_phase) * 0.5 + 0.5) * 0.1;
            float w_str = min(wind_strength, 4.0) * wave;
            
            float wiggle = sin(gust_phase * 2.0) * 0.1;
            float idle_x = (wind_dir_n.x + wiggle) * w_str * 0.3;
            float idle_y = w_str * 0.2;
            float idle_z = (wind_dir_n.z + wiggle) * w_str * 0.3;

            // Gust shake
            float shake_t = global_time * 3.0 + rand_id * 10.0;
            float shake_x = sin(shake_t) * 0.15;
            float shake_y = sin(shake_t * 2.0) * 0.05;
            float shake_z = cos(shake_t * 1.3) * 0.15;

            float idle_weight = 1.0 - gust_mode;
            final_x += idle_x * idle_weight + shake_x * gust_mode;
            final_y += idle_y * idle_weight + shake_y * gust_mode;
            final_z += idle_z * idle_weight + shake_z * gust_mode;

            if (gust_mode > 0.01) {
                float gust_t = fract((global_time * 0.3) + rand_id);
                float drift_gust = gust_t * 0.2 * min(wind_strength * 0.2, 1.0);
                final_x += wind_dir_n.x * drift_gust * gust_mode;
                final_z += wind_dir_n.z * drift_gust * gust_mode;
            }

            // 🚀 OPTIMIZATION 2: Distance Squared Player Check
            vec3 target_pos = player_on_floor > 0.5 ? player_position : last_floor_position;
            vec3 diff = instance_pos - target_pos;
            
            // Only calculate smoothstep if the leaf is actually near a player (< 2 meters)
            if (dot(diff, diff) < 4.0) {
                float mask_decay = 1.0;
                if (player_on_floor < 0.5) {
                    mask_decay = clamp(1.0 - (global_time - last_floor_time) * 2.0, 0.0, 1.0);
                }

                if (mask_decay > 0.01) {
                    float dist_to_target = length(diff.xz);
                    float interact_mask = (1.0 - smoothstep(0.5, 1.8, dist_to_target)) * mask_decay;

                    if (interact_mask > 0.01) {
                        vec3 push = dist_to_target > 0.01 ? normalize(vec3(diff.x, 0.3, diff.z)) : vec3(0.0, 1.0, 0.0);
                        vec3 i_motion = push * (INTERACT_STRENGTH * 0.8);
                        final_x = mix(final_x, i_motion.x, interact_mask);
                        final_y = mix(final_y, i_motion.y, interact_mask);
                        final_z = mix(final_z, i_motion.z, interact_mask);
                    }
                }
            }

            final_y = max(final_y, max_dip_depth);

            VERTEX.x += final_x;
            VERTEX.y += final_y * (1.0 - dissolve_amount);
            VERTEX.z += final_z;
        }

        // Winter bury for leaves that are close
        VERTEX.y -= dissolve_amount * 0.2;
}

void fragment() {
    vec4 tex = texture(leaf_texture, UV);
    if (tex.a < 0.5) discard;
    ALBEDO = tex.rgb * leaf_color;
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
    NORMAL = normalize(vec3(NORMAL.x, 1.0, NORMAL.z));
}