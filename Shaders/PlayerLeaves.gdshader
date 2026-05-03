shader_type particles;
render_mode disable_velocity;

// --- SETTINGS ---
uniform float spread_radius = 0.8;
// MODE: 0=Walk (stream), 1=Land (burst), 2=Falling (ambient)
uniform int spawn_mode = 0;

global uniform vec3 wind_direction;
global uniform float wind_strength;

// Falling Mode specific
uniform vec3 fall_box_size = vec3(40.0, 10.0, 40.0);
uniform float ground_height = 0.0;

// --- RANDOMNESS & HASHING ---

// A proper hash to break the sequential nature of INDEX
uint hash(uint x) {
    x = ((x >> uint(16)) ^ x) * uint(0x45d9f3b);
    x = ((x >> uint(16)) ^ x) * uint(0x45d9f3b);
    x = (x >> uint(16)) ^ x;
    return x;
}

float rand_from_seed(inout uint seed) {
	int k;
	int s = int(seed);
	if (s == 0) s = 305420679;
	k = s / 127773;
	s = 16807 * (s - k * 127773) - 2836 * k;
	if (s < 0) s += 2147483647;
	seed = uint(s);
	return float(seed % uint(65536)) / 65535.0;
}

void start() {
	// Use the hash function to scramble the seed based on INDEX and TIME
	uint seed = hash(uint(INDEX) + uint(TIME * 161.0));
	
	vec3 pos = vec3(0.0);
	vec3 vel = vec3(0.0);
	
	if (spawn_mode == 2) {
		// --- FALLING MODE ---
		pos.x = (rand_from_seed(seed) - 0.5) * fall_box_size.x;
		pos.y = (rand_from_seed(seed) * 0.5 + 0.5) * fall_box_size.y + ground_height;
		pos.z = (rand_from_seed(seed) - 0.5) * fall_box_size.z;
		vel = vec3(0.0, -1.0, 0.0);
		
		TRANSFORM = EMISSION_TRANSFORM;
		TRANSFORM[3].xyz = pos;
	} else {
		// --- INTERACTION MODES (Walk & Land) ---
		float angle = rand_from_seed(seed) * 6.28318;
		// sqrt(rand) ensures uniform distribution in the circle
		float dist = sqrt(rand_from_seed(seed)) * spread_radius; 
		
		// Height offset to spawn above ground
		vec3 local_offset = vec3(cos(angle) * dist, 0.2625, sin(angle) * dist);
		
		TRANSFORM = EMISSION_TRANSFORM;
		TRANSFORM[3].xyz = (EMISSION_TRANSFORM * vec4(local_offset, 1.0)).xyz;

		if (spawn_mode == 0) {
			// WALK MODE: Kicked footprint scatter
			float side_bias = (rand_from_seed(seed) - 0.5) * 2.0;
			float back_speed = rand_from_seed(seed) * 1.5 + 0.5;
			
			vec3 player_back = -EMISSION_TRANSFORM[2].xyz;
			vec3 player_right = EMISSION_TRANSFORM[0].xyz;
			vec3 kick_dir = normalize(player_back + player_right * side_bias);
			
			vel = (kick_dir * back_speed + vec3(0.0, 0.8, 0.0)) * (rand_from_seed(seed) * 0.5 + 0.75);
		} else {
			// LAND BURST MODE: Stronger, higher, more random
			float burst_angle = rand_from_seed(seed) * 6.28318;
			float burst_dist = rand_from_seed(seed) * 0.3 + 0.8;      // was 0.3..0.8 → now 0.5..1.3
			float burst_up   = rand_from_seed(seed) * 0.4 + 1.6;      // was 0.4..1.6 → now 0.8..2.4
			
			vec3 burst_dir = vec3(cos(burst_angle) * burst_dist, burst_up, sin(burst_angle) * burst_dist);
			vel = burst_dir * (rand_from_seed(seed) * 1.5 + 1.5);      // was 1.5..3.0 → now 2.5..5.0
		}
	}

	VELOCITY = vel;

	// Scale: using the 0.1 - 0.15 range
	float s = rand_from_seed(seed) * 0.2 + 0.2;
	TRANSFORM[0] *= s;
	TRANSFORM[1] *= s;
	TRANSFORM[2] *= s;

	// Random Rotation Speed
	CUSTOM.x = (rand_from_seed(seed) - 0.5) * 12.0;
	CUSTOM.y = (rand_from_seed(seed) - 0.5) * 12.0;
}

void process() {
	bool can_move = true;

	if (spawn_mode == 2) {
		// FALLING PHYSICS
		if (TRANSFORM[3].y <= ground_height + 0.05) {
			TRANSFORM[3].y = ground_height + 0.05;
			can_move = false;
		} else {
			VELOCITY.y = -1.5;
			VELOCITY.x += wind_direction.x * wind_strength * 0.2 * DELTA;
			VELOCITY.z += wind_direction.z * wind_strength * 0.2 * DELTA;
			
			// Gentle sway
			float wave = sin(TIME * 2.0 + float(INDEX)) * 1.5 * DELTA;
			TRANSFORM[3].x += wave;
			TRANSFORM[3].z += wave;
		}
	} else {
		// KICK PHYSICS
		VELOCITY.y -= 9.0 * DELTA; // Gravity
		VELOCITY.x *= 0.88; // Friction
		VELOCITY.z *= 0.88;
		
		// Apply Global Wind
		if (length(wind_direction) > 0.01) {
			VELOCITY += normalize(wind_direction) * min(wind_strength, 3.0) * 0.8 * DELTA;
		}
	}

	if (can_move) {
		TRANSFORM[3].xyz += VELOCITY * DELTA;
	}

	// Dynamic Rotation Logic
	float rot_x = CUSTOM.x * DELTA;
	float rot_y = CUSTOM.y * DELTA;
	
	float cx = cos(rot_x); float sx = sin(rot_x);
	vec3 y_new = TRANSFORM[1].xyz * cx - TRANSFORM[2].xyz * sx;
	vec3 z_new = TRANSFORM[1].xyz * sx + TRANSFORM[2].xyz * cx;
	TRANSFORM[1].xyz = y_new; TRANSFORM[2].xyz = z_new;

	float cy = cos(rot_y); float sy = sin(rot_y);
	vec3 x_new = TRANSFORM[0].xyz * cy + TRANSFORM[2].xyz * sy;
	z_new = -TRANSFORM[0].xyz * sy + TRANSFORM[2].xyz * cy;
	TRANSFORM[0].xyz = x_new; TRANSFORM[2].xyz = z_new;
}