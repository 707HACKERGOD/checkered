shader_type spatial;
render_mode cull_disabled, depth_draw_opaque, diffuse_burley, specular_schlick_ggx;

// ============================================================
// GLOBALS — the ones your project already sets
// ============================================================
global uniform vec3 wind_direction;      // world space, any length
global uniform float wind_strength;      // assumed ~0..20 (like your trees)
global uniform float global_time;        // your custom clock
global uniform vec3 player_position;
global uniform float player_speed;
global uniform float player_on_floor;
global uniform vec3 last_floor_position;
global uniform float last_floor_time;
global uniform float rain_amount;
global uniform float snow_amount;

// ---- blade shape ----
uniform float blade_height_taper : hint_range(0.5, 5.0) = 2.0;
uniform float blade_width = 1.0;
uniform float blade_bend = 0.5;

// ---- color ----
uniform vec4 color_top : source_color = vec4(0.667, 0.922, 0.431, 1.0);
uniform vec4 color_bottom : source_color = vec4(0.651, 0.282, 0.0, 1.0);
uniform vec3 color_variation : source_color = vec3(0.9, 0.9, 0.2);
uniform float patch_scale = 5.0;

// ---- wind ----
uniform sampler2D wind_noise : hint_default_black; // assign a seamless NoiseTexture2D!
uniform float wind_texture_scale = 20.0;
uniform float wind_speed = 0.1;
uniform float still_sway_speed = 4.5;
uniform float still_sway_strength = 0.1;
uniform float gust_strength = 0.35;   // raise for dramatic gusts
uniform float gust_frequency = 0.08;

// ---- player interaction ----
uniform float interaction_radius = 1.4;
uniform float interaction_push = 0.5; // 0.3 subtle, 1.0 dramatic

varying float v_patch;
varying float v_wind_intensity;
varying float v_height;

void vertex() {
    v_height = UV.y; // 0 = tip, 1 = base

    // per-blade world position (blade origin, NOT per-vertex, so blades don't shred)
    vec3 world_pos = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;

    // ---- safe horizontal wind (same normalize trick as your leaf shader) ----
    vec2 wind_xz = wind_direction.xz;
    wind_xz = length(wind_xz) > 0.001 ? normalize(wind_xz) : vec2(1.0, 0.0);
    // assumes wind_strength ~0..20; if yours is 0..1, remove the * 0.25
    float wind_scale = clamp(wind_strength, 0.0, 4.0) * 0.25;

    // ---- color patches ----
    v_patch = texture(wind_noise, world_pos.xz / patch_scale).r;

    // ---- gusts: scrolling noise along the wind direction ----
    vec2 move_uv = world_pos.xz / wind_texture_scale + wind_xz * (global_time * wind_speed);
    float gust_mask = smoothstep(gust_frequency, 1.0, texture(wind_noise, move_uv).r);

    float idle_sway = sin(global_time * still_sway_speed + world_pos.x + world_pos.z)
                    * still_sway_strength * (0.25 + 0.75 * wind_scale);
    float flutter = sin(global_time * 20.0 + world_pos.x) * 0.01 * wind_scale;
    float gust_push = gust_mask * (gust_strength + flutter) * wind_scale;

    v_wind_intensity = clamp(gust_mask * wind_scale, 0.0, 1.0);

    // ---- static bend ----
    vec3 vertex_pos = VERTEX;
    float height_factor = 1.0 - UV.y;
    float bend_strength = height_factor * height_factor;
    vertex_pos += vec3(0.0, 0.0, 1.0) * blade_bend * bend_strength;

    // ---- wind as a WORLD direction, converted into this blade's local space ----
    // (the tutorial pushed along each blade's local X, so randomly-rotated blades
    //  blew in random directions; this makes the whole field lean together,
    //  matching your tree/leaf wind)
    mat3 world_to_local = mat3(inverse(MODEL_MATRIX)); // transpose(mat3(MODEL_MATRIX)) if you keep uniform scale
    vec3 local_wind = world_to_local * vec3(wind_xz.x, 0.0, wind_xz.y);
    vec3 total_offset = local_wind * (idle_sway + gust_push) * bend_strength;

    // ---- player interaction (same globals + decay logic as your leaves) ----
    vec3 target_pos = player_on_floor > 0.5 ? player_position : last_floor_position;
    vec3 diff = world_pos - target_pos;
    if (dot(diff, diff) < 4.0) { // cheap early-out within ~2m
        float decay = 1.0;
        if (player_on_floor < 0.5) {
            decay = clamp(1.0 - (global_time - last_floor_time) * 2.0, 0.0, 1.0);
        }
        vec3 flat_diff = vec3(diff.x, 0.0, diff.z);
        if (length(flat_diff) > 0.001 && decay > 0.01) {
            float falloff = 1.0 - smoothstep(0.0, interaction_radius, length(flat_diff));
            float speed_boost = clamp(player_speed / 4.0, 0.25, 1.5); // running flattens more
            total_offset += (world_to_local * normalize(flat_diff))
                * falloff * decay * interaction_push * speed_boost * bend_strength;
        }
    }

    // ---- optional winter: squash + calm the grass as snow builds (delete if unwanted) ----
    float winter = smoothstep(0.15, 0.8, snow_amount);
    total_offset *= 1.0 - 0.8 * winter;
    vertex_pos.y -= bend_strength * winter * 0.35;

    // ---- keep "volume": drop tips slightly when pushed sideways ----
    VERTEX = vertex_pos + total_offset;
    VERTEX.y -= length(total_offset.xz) * 0.2 * bend_strength;
}

void fragment() {
    if (!FRONT_FACING) { NORMAL = -NORMAL; } // fixes the diagonal lit/shadow artifact

    // ---- blade shape (clip the quad) ----
    float center_dist = abs(UV.x - 0.5) * 2.0;
    float shape_mask = pow(UV.y, 1.0 / blade_height_taper);
    if (center_dist > shape_mask * blade_width) discard;

    // ---- color ----
    float winter = smoothstep(0.15, 0.8, snow_amount);
    vec3 gradient = mix(color_top.rgb, color_bottom.rgb, UV.y);
    vec3 wind_highlight = color_top.rgb * 1.5;
    vec3 varied_color = mix(gradient, color_variation, v_patch * 0.3);
    vec3 final_color = mix(varied_color, wind_highlight, v_wind_intensity * 0.4);

    // rain: darker, wetter (delete if unwanted)
    final_color = mix(final_color, final_color * vec3(0.55, 0.62, 0.6), rain_amount * 0.8);
    // snow: frosted
    final_color = mix(final_color, vec3(0.75, 0.82, 0.9), winter * 0.85);

    ALBEDO = final_color;

    float bottom_to_top = v_height; // 0 = tip, 1 = base
    AO = clamp(bottom_to_top + v_wind_intensity * 0.3, 0.0, 1.0);
    AO_LIGHT_AFFECT = 1.0;

    vec3 world_up_view = normalize(mat3(VIEW_MATRIX) * vec3(0.0, 1.0, 0.0));
    NORMAL = mix(NORMAL, world_up_view, bottom_to_top * 0.5);

    ROUGHNESS = mix(0.8, 0.45, rain_amount);
    SPECULAR = 0.2;
    BACKLIGHT = ALBEDO * 0.3;
}