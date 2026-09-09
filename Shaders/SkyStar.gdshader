shader_type sky;

uniform vec3 space_color : source_color =
	vec3(0.001, 0.0005, 0.010);

uniform vec3 space_blue : source_color =
	vec3(0.004, 0.002, 0.025);

uniform float star_brightness = 2.3;
uniform float twinkle_speed = 0.8;
uniform float star_movement = 0.0008;


float hash21(vec2 p)
{
	p = fract(p * vec2(127.1, 311.7));
	p += dot(p, p + 74.7);
	return fract(p.x * p.y);
}


float star_layer(
	vec3 dir,
	float grid,
	float density,
	float base_size
)
{
	float lon = atan(dir.z, dir.x);
	float lat = asin(clamp(dir.y, -1.0, 1.0));

	vec2 uv = vec2(
		lon / 6.2831853 + 0.5,
		lat / 3.1415926 + 0.5
	);

	uv.x += TIME * star_movement;
	uv.y += sin(TIME * 0.15) * 0.00015;

	uv *= grid;
	vec2 cell = floor(uv);
	vec2 local = fract(uv) - 0.5;

	float random = hash21(cell);
	if (random > density) return 0.0;

	vec2 star_position = vec2(
		hash21(cell + vec2(12.4, 75.2)),
		hash21(cell + vec2(83.1, 42.7))
	) - 0.5;

	float size_random = hash21(cell + vec2(51.7, 19.3));
	float size = base_size * (0.65 + size_random * 1.6);

	float distance_to_star = length(local - star_position * 0.72);
	float star = 1.0 - smoothstep(0.0, size, distance_to_star);

	float phase = hash21(cell + vec2(14.2, 91.7)) * 6.2831853;
	float speed = 0.35 + hash21(cell + vec2(71.3, 23.8)) * 1.4;

	float wave = 0.5 + 0.5 * sin(TIME * twinkle_speed * speed + phase);
	float wave2 = 0.5 + 0.5 * sin(TIME * twinkle_speed * speed * 0.37 + phase * 1.73);

	float twinkle = 0.65 + wave * 0.25 + wave2 * 0.10;
	star *= twinkle;

	float brightness = 0.40 + hash21(cell + vec2(41.2, 87.6)) * 1.25;
	star *= brightness;

	return star;
}


void sky()
{
	vec3 dir = normalize(EYEDIR);

	float depth = 0.5 + 0.5 * dir.y;
	vec3 color = mix(space_color, space_blue, depth * 0.18);

	float stars = 0.0;

	stars += star_layer(dir, 150.0, 0.24, 0.040);
	stars += star_layer(dir, 270.0, 0.12, 0.026);
	stars += star_layer(dir, 430.0, 0.055, 0.017);

	float bright_stars = 0.0;
	bright_stars += star_layer(dir, 80.0, 0.020, 0.070);

	vec3 star_color = vec3(0.72, 0.84, 1.0);
	vec3 bright_color = vec3(0.90, 0.95, 1.0);

	color += star_color * stars * star_brightness;
	color += bright_color * bright_stars * 5.0;

	COLOR = color;
}