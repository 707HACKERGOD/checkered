shader_type particles;

// --- SETTINGS ---
uniform vec3 direction = vec3(0, -1, 0);
uniform float spread : hint_range(0.0, 90.0) = 5.0;
uniform float scale_min = 1.0;
uniform float scale_max = 2.0;
uniform vec4 color_value : source_color = vec4(1.0);
uniform vec3 box_size = vec3(30.0, 20.0, 30.0); 
uniform vec3 center_offset = vec3(0.0, 10.0, 0.0); 

// Controls brightness (1.0 = Day, 0.2 = Night)
uniform float light_intensity : hint_range(0.0, 1.0) = 1.0; 

// Physics
uniform float fall_speed_min = 2.0;
uniform float fall_speed_max = 5.0;
uniform vec3 wind_force = vec3(0.0, 0.0, 0.0);
uniform float drag = 0.1;

// Ground Interaction
uniform float ground_height = 0.0;
uniform float is_snow = 0.0;

// Controls
uniform float do_recycle = 1.0; 

// --- RANDOMNESS ---
uint pcg_hash(uint seed) {
    uint state = seed * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

float random_float(inout uint seed) {
    seed = pcg_hash(seed);
    return float(seed) / 4294967295.0;
}

vec3 get_start_velocity(uint seed) {
    float speed = mix(fall_speed_min, fall_speed_max, random_float(seed));
    vec3 spread_dir = normalize(direction + vec3(
        (random_float(seed) - 0.5) * (spread / 45.0),
        0.0,
        (random_float(seed) - 0.5) * (spread / 45.0)
    ));
    return spread_dir * speed;
}

void start() {
    uint seed = RANDOM_SEED + NUMBER; 
    vec3 random_pos_in_box = vec3(
        random_float(seed) * box_size.x - (box_size.x * 0.5),
        random_float(seed) * box_size.y - (box_size.y * 0.5),
        random_float(seed) * box_size.z - (box_size.z * 0.5)
    );

    TRANSFORM[3].xyz = random_pos_in_box + center_offset;
    VELOCITY = get_start_velocity(seed);
    CUSTOM.z = mix(scale_min, scale_max, random_float(seed));
    CUSTOM.x = random_float(seed) * 6.28; 
    CUSTOM.y = 1.0; 
    
    // Apply Light Intensity to start color
    COLOR = color_value * vec4(light_intensity, light_intensity, light_intensity, 1.0);
}

void process() {
	// Debug: Ensure we never go below 0.2 brightness, even if C# breaks
    float safe_light = max(light_intensity, 0.2);
	COLOR.rgb = color_value.rgb * safe_light;
	
    // 1. Restore Color & Apply Night Dimming
    // This ensures particles darken at night even if they are "Unshaded"
    //COLOR.rgb = color_value.rgb * light_intensity;
    COLOR.a = max(0.0, CUSTOM.y) * color_value.a;

    bool on_ground = (TRANSFORM[3].y <= ground_height);

    if (on_ground) {
        // --- GROUND LOGIC ---
        if (do_recycle > 0.5) {
            // RECYCLE ON
            if (is_snow > 0.5) {
                VELOCITY = vec3(0.0);
                CUSTOM.y -= DELTA * 2.0; 
                if (CUSTOM.y <= 0.0) {
                    TRANSFORM[3].y += box_size.y; 
                    CUSTOM.y = 1.0;
                    uint seed = RANDOM_SEED + NUMBER + uint(TIME);
                    VELOCITY = get_start_velocity(seed);
                }
            } else {
                TRANSFORM[3].y += box_size.y;
                uint seed = RANDOM_SEED + NUMBER + uint(TIME);
                VELOCITY = get_start_velocity(seed);
            }
        } 
        else {
            // RECYCLE OFF (Stopping)
            if (is_snow > 0.5) {
                VELOCITY = vec3(0.0);
                CUSTOM.y -= DELTA * 1.0; 
            }
        }
    } else {
        // --- AIR LOGIC ---
        vec3 relative_pos = TRANSFORM[3].xyz - EMISSION_TRANSFORM[3].xyz - center_offset;
        vec3 half_box = box_size * 0.5;
        
        bool did_wrap = false;
        
        if (do_recycle > 0.5 && relative_pos.y < -half_box.y) {
            TRANSFORM[3].xyz += vec3(0.0, box_size.y, 0.0);
            did_wrap = true;
        }
        if (relative_pos.x > half_box.x) { TRANSFORM[3].x -= box_size.x; did_wrap = true; }
        if (relative_pos.x < -half_box.x) { TRANSFORM[3].x += box_size.x; did_wrap = true; }
        if (relative_pos.z > half_box.z) { TRANSFORM[3].z -= box_size.z; did_wrap = true; }
        if (relative_pos.z < -half_box.z) { TRANSFORM[3].z += box_size.z; did_wrap = true; }

        if (did_wrap) {
            uint seed = RANDOM_SEED + NUMBER;
            VELOCITY = get_start_velocity(seed);
            CUSTOM.y = 1.0; 
        }

        VELOCITY += wind_force * DELTA;
        VELOCITY -= VELOCITY * drag * DELTA;
        TRANSFORM[3].xyz += VELOCITY * DELTA;
        
        // Visuals (Orientation)
        if (length(VELOCITY) > 0.1 && is_snow < 0.5) {
            float base_scale = CUSTOM.z;
            vec3 y_axis = normalize(VELOCITY);
            vec3 random_vec = vec3(cos(CUSTOM.x), 0.0, sin(CUSTOM.x));
            vec3 z_axis = normalize(cross(random_vec, y_axis));
            if (length(z_axis) < 0.01) z_axis = normalize(cross(vec3(1.0, 0.0, 0.0), y_axis));
            vec3 x_axis = cross(y_axis, z_axis);

            TRANSFORM[0].xyz = x_axis * base_scale;
            TRANSFORM[1].xyz = y_axis * base_scale * (1.0 + length(VELOCITY) * 0.05); 
            TRANSFORM[2].xyz = z_axis * base_scale;
        } else if (is_snow > 0.5) {
             TRANSFORM[0].xyz = vec3(CUSTOM.z);
             TRANSFORM[1].xyz = vec3(CUSTOM.z);
             TRANSFORM[2].xyz = vec3(CUSTOM.z);
        }
    }
}