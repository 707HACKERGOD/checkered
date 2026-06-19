shader_type spatial;

render_mode depth_draw_always, cull_disabled;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;

uniform vec4 shallow_color : source_color = vec4(0.0, 0.45, 0.55, 0.8);
uniform vec4 deep_color : source_color = vec4(0.0, 0.15, 0.25, 0.9);
uniform vec4 foam_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

uniform float wave_speed : hint_range(0.0, 5.0) = 1.0;
uniform float wave_strength : hint_range(0.0, 2.0) = 0.3;
uniform float wave_frequency : hint_range(0.0, 20.0) = 8.0;

uniform float foam_distance : hint_range(0.0, 10.0) = 2.0;
uniform float foam_amount : hint_range(0.0, 1.0) = 0.5;
uniform float foam_cutoff : hint_range(0.0, 1.0) = 0.3;
uniform float foam_speed : hint_range(0.0, 2.0) = 0.5;

uniform sampler2D normal_map_a : hint_normal;
uniform sampler2D normal_map_b : hint_normal;
uniform float normal_scale : hint_range(0.0, 2.0) = 1.0;
uniform vec2 normal_uv_scale = vec2(0.1, 0.1);

uniform float metallic : hint_range(0.0, 1.0) = 0.1;
uniform float roughness : hint_range(0.0, 1.0) = 0.05;
uniform float specular : hint_range(0.0, 1.0) = 0.8;

uniform float beers_law : hint_range(0.0, 20.0) = 4.0;
uniform float depth_offset : hint_range(-10.0, 10.0) = 0.0;

varying vec3 v_world_pos;
varying vec3 v_vertex_pos;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

vec3 gerstner(vec2 pos, float time, vec2 dir, float freq, float amp) {
    float d = dot(pos, dir);
    float phase = d * freq + time * wave_speed;
    float steepness = 0.5;
    float q = steepness / (freq * amp * 2.0 + 0.001);
    float c = cos(phase);
    float s = sin(phase);
    return vec3(dir.x * q * amp * c, amp * s, dir.y * q * amp * c);
}

void vertex() {
    vec2 pos = VERTEX.xz;
    float t = TIME * wave_speed;
    vec3 wave = vec3(0.0);
    wave += gerstner(pos, t, normalize(vec2(1.0, 0.3)), wave_frequency, wave_strength * 0.5);
    wave += gerstner(pos, t * 1.2, normalize(vec2(-0.4, 0.9)), wave_frequency * 1.3, wave_strength * 0.3);
    wave += gerstner(pos, t * 0.8, normalize(vec2(0.2, -0.7)), wave_frequency * 0.7, wave_strength * 0.2);
    float n = noise(pos * 0.5 + t * 0.2) * wave_strength * 0.3;
    wave.y += n;
    VERTEX += wave;
    v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    v_vertex_pos = VERTEX;
}

void fragment() {
    float depth_raw = texture(DEPTH_TEXTURE, SCREEN_UV).x;
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth_raw);
    vec4 world_pos = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    world_pos.xyz /= world_pos.w;
    float depth_diff = world_pos.z - v_vertex_pos.z + depth_offset;
    float depth_blend = exp(-depth_diff * beers_law);
    depth_blend = clamp(depth_blend, 0.0, 1.0);

    vec2 uv_a = v_world_pos.xz * normal_uv_scale + TIME * vec2(0.02, 0.01) * wave_speed;
    vec2 uv_b = v_world_pos.xz * normal_uv_scale * 1.5 - TIME * vec2(0.015, 0.025) * wave_speed;
    vec3 normal_a = texture(normal_map_a, uv_a).rgb * 2.0 - 1.0;
    vec3 normal_b = texture(normal_map_b, uv_b).rgb * 2.0 - 1.0;
    vec3 normal_blend = normalize(normal_a + normal_b);
    NORMAL = normalize(mix(NORMAL, normal_blend, normal_scale));

    vec2 refract_uv = SCREEN_UV + normal_blend.xy * 0.02;
    vec3 screen_color = texture(SCREEN_TEXTURE, refract_uv).rgb;

    vec4 water_color = mix(deep_color, shallow_color, depth_blend);
    vec3 view_dir = normalize(CAMERA_POSITION_WORLD - v_world_pos);
    float fresnel = pow(1.0 - clamp(dot(NORMAL, view_dir), 0.0, 1.0), 5.0);
    fresnel = clamp(fresnel, 0.0, 1.0);

    float foam_noise = noise(v_world_pos.xz * 3.0 + TIME * foam_speed);
    float foam_edge = 1.0 - smoothstep(0.0, foam_distance, -depth_diff);
    foam_edge = pow(foam_edge, 2.0);
    float foam = foam_edge * foam_amount * (foam_noise * 0.5 + 0.5);
    foam = step(foam_cutoff, foam);

    vec3 color = mix(screen_color * water_color.rgb, water_color.rgb, water_color.a);
    color = mix(color, foam_color.rgb, foam * foam_color.a);
    color = mix(color, vec3(1.0), fresnel * 0.3);

    ALBEDO = color;
    METALLIC = metallic;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    ALPHA = mix(1.0, water_color.a, depth_blend);
    ALPHA = mix(ALPHA, 1.0, foam);
}