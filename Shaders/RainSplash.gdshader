shader_type particles;

uniform vec3 direction = vec3(0.0, 1.0, 0.0);
uniform float spread : hint_range(0.0, 90.0) = 25.0;
uniform float scale_min = 0.2;
uniform float scale_max = 0.5;
uniform vec4 color_value : source_color = vec4(0.8, 0.9, 1.0, 0.8);
uniform vec2 spawn_area = vec2(30.0, 30.0);
uniform float bounce_strength_min = 0.5;
uniform float bounce_strength_max = 1.0;
uniform float gravity = 15.0;
uniform float drag = 2.0;
uniform float ground_height = 0.0;
uniform float fade_seconds : hint_range(0.1, 2.0) = 0.45;

global uniform vec3 wind_direction;
global uniform float wind_strength;

uint pcg_hash(uint seed) {
    uint state = seed * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}
float random_float(inout uint seed) {
    seed = pcg_hash(seed);
    return float(seed) / 4294967295.0;
}
vec3 get_bounce_velocity(inout uint seed) {
    float speed = mix(bounce_strength_min, bounce_strength_max, random_float(seed));
    return normalize(direction + vec3(
        (random_float(seed) - 0.5) * (spread / 45.0), 0.0,
        (random_float(seed) - 0.5) * (spread / 45.0))) * speed;
}

void start() {
    uint seed = RANDOM_SEED + NUMBER;
    TRANSFORM[3].xyz = vec3(
        (random_float(seed) - 0.5) * spawn_area.x, 0.0,
        (random_float(seed) - 0.5) * spawn_area.y);
    TRANSFORM[3].y = ground_height + 0.05;   // world ground Y — see node checklist
    VELOCITY = get_bounce_velocity(seed);
    CUSTOM.z = mix(scale_min, scale_max, random_float(seed));
    CUSTOM.x = random_float(seed) * 6.28;
    CUSTOM.y = 1.0;
    CUSTOM.w = 0.0; // age
    COLOR = color_value;
}

void process() {
    CUSTOM.w += DELTA;
    CUSTOM.y = 1.0 - clamp(CUSTOM.w / fade_seconds, 0.0, 1.0);
    COLOR.a = CUSTOM.y;

    VELOCITY.y -= gravity * DELTA;
    VELOCITY.xz += wind_direction.xz * min(wind_strength, 10.0) * 0.15 * DELTA;
    VELOCITY -= VELOCITY * drag * DELTA;
    TRANSFORM[3].xyz += VELOCITY * DELTA;
    if (TRANSFORM[3].y < ground_height) { TRANSFORM[3].y = ground_height; VELOCITY.y = 0.0; }
    TRANSFORM[0].xyz = vec3(CUSTOM.z);
    TRANSFORM[1].xyz = vec3(CUSTOM.z);
    TRANSFORM[2].xyz = vec3(CUSTOM.z);
}