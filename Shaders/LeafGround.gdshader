shader_type spatial;
render_mode depth_draw_opaque, cull_disabled, diffuse_burley;

// --- GLOBALS (existing) ---
global uniform vec3 wind_direction;
global uniform float wind_strength;
global uniform float global_time; 
global uniform vec3 player_position; 
global uniform float player_speed; 
global uniform float player_on_floor;
global uniform vec3 last_floor_position;
global uniform float last_floor_time;
global uniform float gust_mode;  // now a global uniform, smoothly lerped in C#

// --- GLOBAL uniforms for seasonal transitions ---
global uniform float spawn_progress;
global uniform float despawn_progress;

// --- SETTINGS (regular uniforms) ---
uniform sampler2D leaf_texture : source_color, filter_nearest_mipmap;
uniform vec3 leaf_color : source_color = vec3(0.8, 0.1, 0.1);
uniform float dissolve_amount : hint_range(0.0, 1.0) = 0.0;

// FIX: Pivot offset
uniform float y_offset : hint_range(0.0, 0.5) = 0.02; 
uniform float max_dip_depth = -0.15; 

const float INTERACT_RADIUS = 1.5;
const float INTERACT_STRENGTH = 0.5;

void vertex() {
    float dist_cam = distance(NODE_POSITION_WORLD, CAMERA_POSITION_WORLD);

    if (dist_cam > 150.0) {
        VERTEX.y -= dissolve_amount * 0.2;
    } else {
        VERTEX.y += y_offset;

        vec3 safe_wind = wind_direction;
        if (length(safe_wind) < 0.01) safe_wind = vec3(1.0, 0.0, 0.0);
        vec3 wind_dir_n = normalize(safe_wind);
        float rand_id = fract(sin(dot(MODEL_MATRIX[3].xz, vec2(12.9898,78.233))) * 43758.5453);

        // --- DESPAWN (autumn → winter) ---
        if (despawn_progress > 0.01) {
            float dist_wind = dot(MODEL_MATRIX[3].xyz, wind_dir_n);
            float t = clamp((despawn_progress * 2.0) - (dist_wind * 0.05) - 0.5, 0.0, 1.0);
            float t2 = t * t;

            // Original wave animation
            float lift = t * 10.0 + sin(t * 10.0) * 0.5;
            float drift = t2 * 25.0 * min(wind_strength, 2.0);
            float spiral = sin(t * 10.0 + rand_id * 10.0) * t * 2.0;

            VERTEX.y += lift;
            VERTEX.x += wind_dir_n.x * drift + spiral;
            VERTEX.z += wind_dir_n.z * drift + spiral;
            VERTEX.y += sin(t * 15.0 * rand_id) * 0.2;
            VERTEX *= smoothstep(0.6, 0.0, t);
        }
        // --- SPAWN (summer → autumn) ---
        else {
            float t = smoothstep(0.0, 1.0, (spawn_progress * 1.5) - (rand_id * 0.5));

            float start_height = 10.0;
            VERTEX.y += (1.0 - t) * start_height;
            float drift = (1.0 - t) * 2.5 * min(wind_strength, 2.0);
            VERTEX.x -= wind_dir_n.x * drift;
            VERTEX.z -= wind_dir_n.z * drift;
            VERTEX *= smoothstep(0.3, 0.9, t);

            // --- GROUND PHYSICS & GUSTS ---
            if (spawn_progress > 0.99) {
                float final_x = 0.0;
                float final_y = 0.0;
                float final_z = 0.0;

                // --- BLENDED GUST MODE ---
                // Calculate both idle and gust displacements, then blend with gust_mode

                // Idle ambient wind (wave)
                float dist_wind = dot(MODEL_MATRIX[3].xyz, wind_dir_n);
                float gust_phase = (global_time * 3.0) - (dist_wind * 0.5);
                float wave = (sin(gust_phase) * 0.5 + 0.5) * 0.1;
                float w_str = min(wind_strength, 4.0) * wave;
                float idle_y = w_str * 0.2;
                float wiggle = sin(gust_phase * 2.0) * 0.1;
                float idle_x = (wind_dir_n.x + wiggle) * w_str * 0.3;
                float idle_z = (wind_dir_n.z + wiggle) * w_str * 0.3;

                // Gust shake (high‑frequency, grounded)
                float shake_t = global_time * 3.0 + rand_id * 10.0;
                float shake_x = sin(shake_t) * 0.15;
                float shake_z = cos(shake_t * 1.3) * 0.15;
                float shake_y = sin(shake_t * 2.0) * 0.05;

                // Blend between idle and gust using gust_mode
                // Idle fades out as gust increases, gust fades in
                float idle_weight = 1.0 - gust_mode;
                float gust_weight = gust_mode;

                final_x += idle_x * idle_weight + shake_x * gust_weight;
                final_y += idle_y * idle_weight + shake_y * gust_weight;
                final_z += idle_z * idle_weight + shake_z * gust_weight;

                // (Optional) add a tiny amount of wind drift to gust mode as well
                if (gust_mode > 0.01) {
                    float gust_t = fract((global_time * 0.3) + rand_id);
                    float drift_gust = gust_t * 0.2 * min(wind_strength * 0.2, 1.0);
                    final_x += wind_dir_n.x * drift_gust * gust_weight;
                    final_z += wind_dir_n.z * drift_gust * gust_weight;
                }

                // --- PLAYER INTERACTION (unchanged) ---
                if (player_on_floor > 0.5 && length(player_position) > 0.1) {
                    float dist_to_player = distance(MODEL_MATRIX[3].xz, player_position.xz);
                    float interact_mask = 1.0 - smoothstep(0.5, 1.8, dist_to_player);

                    if (interact_mask > 0.01) {
                        vec3 diff = MODEL_MATRIX[3].xyz - player_position;
                        vec3 push = length(diff.xz) > 0.01 ? normalize(vec3(diff.x, 0.3, diff.z)) : vec3(0, 1, 0);
                        vec3 i_motion = push * (INTERACT_STRENGTH * 0.8);

                        final_x = mix(final_x, i_motion.x, interact_mask);
                        final_y = mix(final_y, i_motion.y, interact_mask);
                        final_z = mix(final_z, i_motion.z, interact_mask);
                    }
                }

                // --- JUMP DEFORMATION (unchanged) ---
                if (player_on_floor < 0.5) {
                    float dist_to_last = distance(MODEL_MATRIX[3].xz, last_floor_position.xz);
                    float time_since_last = global_time - last_floor_time;
                    float decay = clamp(1.0 - time_since_last * 2.0, 0.0, 1.0);
                    float last_mask = (1.0 - smoothstep(0.5, 1.8, dist_to_last)) * decay;

                    if (last_mask > 0.01) {
                        vec3 diff = MODEL_MATRIX[3].xyz - last_floor_position;
                        vec3 push = length(diff.xz) > 0.01 ? normalize(vec3(diff.x, 0.3, diff.z)) : vec3(0, 1, 0);
                        vec3 i_motion = push * (INTERACT_STRENGTH * 0.8);

                        final_x = mix(final_x, i_motion.x, last_mask);
                        final_y = mix(final_y, i_motion.y, last_mask);
                        final_z = mix(final_z, i_motion.z, last_mask);
                    }
                }

                final_y = max(final_y, max_dip_depth);
                VERTEX.x += final_x;
                VERTEX.y += final_y * (1.0 - dissolve_amount);
                VERTEX.z += final_z;
            }
        }

        // Winter bury
        VERTEX.y -= dissolve_amount * 0.2;
    }
}

void fragment() {
    vec4 tex = texture(leaf_texture, UV);
    if (tex.a < 0.5) discard;
    ALBEDO = tex.rgb * leaf_color;
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
    NORMAL = normalize(vec3(NORMAL.x, 1.0, NORMAL.z));
}