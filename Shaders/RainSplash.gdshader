shader_type particles;

// --- SETTINGS ---
uniform vec3 direction = vec3(0, 1, 0); // Upwards bounce
uniform float spread : hint_range(0.0, 90.0) = 25.0;
uniform float scale_min = 0.2; // Smaller than rain drops
uniform float scale_max = 0.5;

uniform vec4 color_value : source_color = vec4(0.8, 0.9, 1.0, 0.8);

// The area on the ground where splashes appear (Matches your rain box width/depth)
uniform vec2 spawn_area = vec2(30.0, 30.0); 

// Physics & Wind
uniform float bounce_strength_min = 0.5;
uniform float bounce_strength_max = 1.0;
uniform float gravity = 15.0; // High gravity for quick up-down feel
uniform vec3 wind_force = vec3(0.0, 0.0, 0.0); // Splashes drift slightly with wind
uniform float drag = 0.5;

// Ground Interaction
uniform float ground_height = 0.0; // Updated by script

// --- RANDOMNESS (Same PCG Hash as your other shader) ---
uint pcg_hash(uint seed) {
    uint state = seed * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

float random_float(inout uint seed) {
    seed = pcg_hash(seed);
    return float(seed) / 4294967295.0;
}

vec3 get_bounce_velocity(uint seed) {
    float speed = mix(bounce_strength_min, bounce_strength_max, random_float(seed));
    
    vec3 spread_dir = normalize(direction + vec3(
        (random_float(seed) - 0.5) * (spread / 45.0),
        0.0,
        (random_float(seed) - 0.5) * (spread / 45.0)
    ));
    
    return spread_dir * speed;
}

void start() {
    uint seed = RANDOM_SEED + NUMBER; 
    
    // SPAWN LOGIC: Always at ground_height
    // We only randomize X and Z. Y is fixed to ground.
    vec3 random_pos = vec3(
        (random_float(seed) - 0.5) * spawn_area.x,
        0.0, // Relative Y is 0
        (random_float(seed) - 0.5) * spawn_area.y
    );
    
    // Apply position relative to emitter, but lock Y to ground_height
    TRANSFORM[3].xyz = random_pos;
    TRANSFORM[3].y = ground_height + 0.05; // Slightly above ground to avoid z-fighting
    
    VELOCITY = get_bounce_velocity(seed);
    
    CUSTOM.z = mix(scale_min, scale_max, random_float(seed)); // Scale
    CUSTOM.x = random_float(seed) * 6.28; // Rotation
    CUSTOM.y = 1.0; // Life/Alpha (Starts full)
    
    COLOR = color_value;
}

void process() {
    // 1. AGE THE PARTICLE
    // Instead of falling and hitting ground, we just fade it out over time
    CUSTOM.y -= DELTA * 2.5; // Fade speed (adjust for longer/shorter splashes)
    COLOR.a = CUSTOM.y;
    
    // 2. RESET (Recycle)
    if (CUSTOM.y <= 0.0) {
        // Respawn at a new random location
        uint seed = RANDOM_SEED + NUMBER + uint(TIME * 100.0); // New seed based on time
        
        vec3 random_pos = vec3(
            (random_float(seed) - 0.5) * spawn_area.x,
            0.0,
            (random_float(seed) - 0.5) * spawn_area.y
        );
        
        // Reset Position to Player X/Z (handled by node position) but fixed Ground Y
        TRANSFORM[3].x = EMISSION_TRANSFORM[3].x + random_pos.x;
        TRANSFORM[3].z = EMISSION_TRANSFORM[3].z + random_pos.z;
        TRANSFORM[3].y = ground_height + 0.05;
        
        // Reset Physics
        VELOCITY = get_bounce_velocity(seed);
        CUSTOM.y = 1.0; // Reset Alpha
        COLOR.a = 1.0;
    }
    else {
        // 3. PHYSICS (Bouncing Up and Falling Down)
        
        // Apply Gravity (Down)
        VELOCITY.y -= gravity * DELTA;
        
        // Apply Wind (Sideways drift)
        VELOCITY.x += wind_force.x * 0.1 * DELTA; // Less wind influence than rain
        VELOCITY.z += wind_force.z * 0.1 * DELTA;
        
        // Apply Drag
        VELOCITY -= VELOCITY * drag * DELTA;
        
        // Move
        TRANSFORM[3].xyz += VELOCITY * DELTA;
        
        // Stop if it falls below ground (prevents clipping through floor)
        if (TRANSFORM[3].y < ground_height) {
            TRANSFORM[3].y = ground_height;
            VELOCITY.y = 0.0;
        }
        
        // 4. VISUALS
        // Simple scaling or rotation if desired
        TRANSFORM[0].xyz = vec3(CUSTOM.z);
        TRANSFORM[1].xyz = vec3(CUSTOM.z);
        TRANSFORM[2].xyz = vec3(CUSTOM.z);
    }
}