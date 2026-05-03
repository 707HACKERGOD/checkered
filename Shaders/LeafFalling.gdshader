shader_type spatial;
render_mode depth_draw_opaque, cull_disabled, diffuse_lambert, skip_vertex_transform;

uniform sampler2D leaf_texture : source_color, filter_nearest_mipmap;
uniform vec3 leaf_color : source_color = vec3(0.8, 0.4, 0.1);

global uniform float global_time;
global uniform sampler2D wind_history : filter_linear;
global uniform float wind_history_duration;

// PHYSICS
uniform float drop_speed : hint_range(0.1, 10.0) = 2.0;
uniform float max_life_seconds : hint_range(1.0, 30.0) = 10.0;
uniform float shrink_duration : hint_range(0.1, 3.0) = 1.0;

uniform float visibility_progress : hint_range(0.0, 1.0) = 1.0;
uniform float leaf_scale_multiplier : hint_range(0.1, 3.0) = 1.0;
uniform float ground_height = 0.0;

vec3 rotate_vector(vec3 v, vec3 axis, float angle) {
    return mix(dot(axis, v) * axis, v, cos(angle)) + cross(axis, v) * sin(angle);
}

void vertex() {
    vec3 instance_center = MODEL_MATRIX[3].xyz;

    // Random seed per instance
    float random_seed = fract(sin(dot(instance_center, vec3(12.9898, 78.233, 45.164))) * 43758.5453);

    float appear_start = random_seed * 0.4;
    float appear_end = appear_start + 0.6;
    float local_visibility = smoothstep(appear_start, appear_end, visibility_progress);

    float disappear_start = random_seed * 0.3;
    float disappear_end = disappear_start + 0.5;
    float disappear_factor = 1.0 - smoothstep(disappear_start, disappear_end, 1.0 - visibility_progress);

    float final_visibility = min(local_visibility, disappear_factor);

    float r_channel = COLOR.r;
    float g_channel = COLOR.g;
    float normalized_height = r_channel + (g_channel / 255.0);
    float max_fall_height = 50.0;
    float fall_distance = normalized_height * max_fall_height;

    float land_y = instance_center.y - fall_distance;

    float local_time = global_time + (random_seed * max_life_seconds);
    float cycle_t = mod(local_time, max_life_seconds);

    // --- NEW: Wind history sampling ---
    float birth_time = global_time - cycle_t;
    float t = birth_time / wind_history_duration;
    vec4 wind_sample = texture(wind_history, vec2(t, 0.5));
    float hist_strength = wind_sample.r;
    vec3 hist_dir = vec3(wind_sample.g, 0.0, wind_sample.b);   // (x, 0, z)

    vec3 safe_wind_dir;
    if (hist_strength < 0.01) {
        safe_wind_dir = vec3(0.0);
    } else {
        safe_wind_dir = normalize(hist_dir);
    }
    // ---------------------------------

    float time_to_hit_ground = fall_distance / max(drop_speed, 0.001);
    float current_dist = cycle_t * drop_speed;
    float current_y = instance_center.y - current_dist;

    bool is_landed = current_y <= land_y;
    current_y = max(current_y, land_y);

    float active_wind_time = min(cycle_t, time_to_hit_ground);

    float scale = 1.0;
    if (is_landed) {
        float time_on_ground = cycle_t - time_to_hit_ground;
        float shrink_progress = smoothstep(0.0, shrink_duration, time_on_ground);
        scale = 1.0 - shrink_progress;
    }

    float fade_in = smoothstep(0.0, 0.5, cycle_t);
    scale *= fade_in;
    scale *= final_visibility;
    scale *= leaf_scale_multiplier;

    // Wind and drift (using history values)
    vec3 wind_offset = vec3(0.0);
    if (hist_strength >= 0.01) {
        vec3 drift_noise = vec3(
            sin(active_wind_time * 2.0 + random_seed * 10.0),
            0.0,
            cos(active_wind_time * 1.5 + random_seed * 25.0)
        );
        wind_offset = (safe_wind_dir * min(hist_strength, 7.0) * active_wind_time) + (drift_noise * 0.5);
    }

    // Rotation (Tumble)
    float tumble_t = active_wind_time * 3.0;
    vec3 axis = normalize(vec3(sin(random_seed * 10.0), cos(random_seed * 10.0), 0.5));
    vec3 local_vertex = VERTEX;
    local_vertex = rotate_vector(local_vertex, axis, tumble_t + (random_seed * 10.0));

    if (is_landed) {
        float time_on_ground = cycle_t - time_to_hit_ground;
        float flatten_progress = smoothstep(0.0, 0.5, time_on_ground);
        local_vertex.y = mix(local_vertex.y, 0.0, flatten_progress);
    }

    // Final position
    vec3 final_world_pos = vec3(instance_center.x, current_y, instance_center.z);
    final_world_pos += wind_offset;
    final_world_pos += local_vertex * scale;

    VERTEX = (VIEW_MATRIX * vec4(final_world_pos, 1.0)).xyz;

    // Normals
    vec3 n_tumbled = rotate_vector(vec3(0.0, 1.0, 0.0), axis, tumble_t + (random_seed * 10.0));
    float land_blend = is_landed ? smoothstep(0.0, 0.5, cycle_t - time_to_hit_ground) : 0.0;
    vec3 final_normal_world = mix(n_tumbled, vec3(0.0, 1.0, 0.0), land_blend);
    NORMAL = (VIEW_MATRIX * vec4(final_normal_world, 0.0)).xyz;
}

void fragment() {
    vec4 tex = texture(leaf_texture, UV);
    if (tex.a < 0.5) discard;
    ALBEDO = tex.rgb * leaf_color;
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
    if (!FRONT_FACING) {
        NORMAL = -NORMAL;
    }
}