// EmacEArt Stylized Grass - a Godot 4 port of the URP shader "EmacEArt/StylizedGrass".
//
// LICENCE
//   EmacEArt Asset License - free for personal and commercial use, but resale or
//   redistribution of the assets as standalone files or asset packs is prohibited.
//   No credit is required, though it is always welcome.
//   EmacEArt - https://emaceart.itch.io
//
// Matches the original one to one: three-layer wind anchored at the root, a two-layer
// procedural tint (blob or stripes mode, each with its own set of parameters), and
// foliage shading (normal biased toward world up + additive ambient lift).
//
// Group names and parameter order follow the [Header(...)] sections from Unity, so the
// Godot inspector panel lines up with the Unity one side by side.
//
// DIFFERENCES FROM UNITY - deliberate, listed here so nobody "fixes" them:
//   * Gust: Unity samples the _GustNoise texture. In the package materials that texture
//     is empty (None), so Unity reads white and the gust ends up CONSTANT. Here the
//     noise is generated procedurally by default (looks better, no texture dependency);
//     plug a texture into gust_noise if you want Unity's exact behaviour.
//   * With cull_disabled Godot already flips the normal on back faces, so there is no
//     equivalent of multiplying by VFACE - adding one would flip it a second time.
//   * Unity's additive ambient lift (color.rgb += albedo * tint * lift) is done through
//     EMISSION: Godot gives no access to the lighting result inside the fragment shader,
//     and EMISSION adds to the final color in exactly the same way.

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

group_uniforms Base;
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 base_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.4;

group_uniforms Normal;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;
uniform float normal_strength : hint_range(0.0, 2.0) = 1.0;

group_uniforms PBR;
uniform float smoothness : hint_range(0.0, 1.0) = 0.2;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

group_uniforms Wind;
uniform float wind_strength : hint_range(0.0, 2.0) = 0.15;
uniform float wind_speed : hint_range(0.0, 10.0) = 1.5;
uniform float wind_frequency : hint_range(0.0, 5.0) = 0.5;
uniform float wind_direction_x : hint_range(-1.0, 1.0) = 1.0;
uniform float wind_direction_z : hint_range(-1.0, 1.0) = 0.0;
// Model height in local units. Unity assumes 1.0 (saturate(y)); change this only if the
// clump is noticeably taller and should bend along its whole length.
uniform float anchor_height : hint_range(0.05, 10.0) = 1.0;

group_uniforms Gust;
// Empty = procedural noise. Plug a texture in to get Unity's behaviour.
uniform sampler2D gust_noise : hint_default_white, filter_linear, repeat_enable;
uniform bool gust_use_texture = false;
uniform float gust_strength : hint_range(0.0, 2.0) = 0.5;
uniform float gust_scale : hint_range(0.0, 1.0) = 0.05;
uniform float gust_speed : hint_range(0.0, 5.0) = 0.4;

group_uniforms Tint_Small;
uniform vec4 tint_color_small : source_color = vec4(1.0, 0.62, 0.05, 1.0);
uniform bool stripes_mode_small = false;
group_uniforms Tint_Small.Blob;
uniform float tint_scale_small_blob : hint_range(0.01, 1.0) = 0.814;
uniform float tint_intensity_small_blob : hint_range(0.0, 1.0) = 0.928;
uniform float tint_width_small_blob : hint_range(0.0, 1.0) = 0.867;
uniform float tint_speed_small_blob : hint_range(0.0, 2.0) = 0.806;
group_uniforms Tint_Small.Stripes;
uniform float tint_scale_small_stripes : hint_range(0.01, 1.0) = 0.814;
uniform float tint_intensity_small_stripes : hint_range(0.0, 1.0) = 0.928;
uniform float tint_width_small_stripes : hint_range(0.0, 1.0) = 0.867;
uniform float tint_speed_small_stripes : hint_range(0.0, 2.0) = 0.806;

group_uniforms Tint_Large;
uniform vec4 tint_color_large : source_color = vec4(0.32, 0.62, 0.55, 1.0);
uniform bool stripes_mode_large = true;
group_uniforms Tint_Large.Blob;
uniform float tint_scale_large_blob : hint_range(0.005, 0.3) = 0.265;
uniform float tint_intensity_large_blob : hint_range(0.0, 1.0) = 0.706;
uniform float tint_width_large_blob : hint_range(0.0, 1.0) = 0.846;
uniform float tint_speed_large_blob : hint_range(0.0, 2.0) = 0.534;
group_uniforms Tint_Large.Stripes;
uniform float tint_scale_large_stripes : hint_range(0.005, 0.3) = 0.265;
uniform float tint_intensity_large_stripes : hint_range(0.0, 1.0) = 0.706;
uniform float tint_width_large_stripes : hint_range(0.0, 1.0) = 0.846;
uniform float tint_speed_large_stripes : hint_range(0.0, 2.0) = 0.534;

group_uniforms Foliage_Shading;
uniform float ambient_lift : hint_range(0.0, 1.0) = 0.35;
uniform vec4 ambient_tint : source_color = vec4(0.55, 0.7, 0.55, 1.0);
// Bias of the normal toward world up. A grass card's normal points perpendicular to its
// own plane and the scatter randomises yaw, so half the clumps face the sun and half turn
// away - without this bias neighbouring clumps flip light, dark, light, and the meadow
// crawls when the camera moves. Drawing both sides doubles the effect.
//
// Unity hardcodes 0.5 and this port shipped that value until 1.2.0, where it was measured
// against the actual image: at 0.5 the flipping is plain to see, 0.65 calms most of it,
// 0.75 removes it while a blade still has visible shape, and 1.0 turns the meadow into a
// flat carpet. 0.75 is the default for that reason - lower it only if you want the blades
// picked out harder and can live with the flicker.
uniform float normal_up_bias : hint_range(0.0, 1.0) = 0.75;

group_uniforms Winter;
// Winter is one dial, not a second shader. At 0 nothing below is computed and the frame
// time is the same as before this block existed - the branch is on a uniform, so every
// pixel of the draw call takes the same path.
//
// Two things happen as the dial goes up, and they are separate on purpose:
//   * the whole blade fades from its summer colour to winter_color - dry, bleached grass
//     standing under snow, which is what a meadow actually looks like in winter;
//   * snow settles on the blade from the tip downwards, controlled by the Snow subgroup.
// Set snow_coverage to 1.0 and the snow reaches the ground, so the field turns white.
uniform float winter_amount : hint_range(0.0, 1.0) = 0.0;
// Colour the blade fades to. Not grey: dead grass in winter keeps a warm straw cast, and
// a neutral grey next to white snow comes out looking like dirt.
uniform vec4 winter_color : source_color = vec4(0.66, 0.62, 0.52, 1.0);
// How much of the procedural tint survives the winter fade. At 0 the field goes one flat
// colour; at 1 the summer patches and bands stay at full strength under the winter cast.
// The default keeps them faint - enough that the meadow is not a single sheet of colour.
uniform float winter_tint_keep : hint_range(0.0, 1.0) = 0.3;
// How much of the texture's own colour is drained before the winter colour goes on.
// The grass texture is green, and a straw colour MULTIPLIED over green stays green - the
// blades come out olive instead of dry. Draining first is what makes them look dead.
// Leave some in: at 1.0 every blade takes exactly one colour and the field goes flat.
uniform float winter_desaturate : hint_range(0.0, 1.0) = 0.85;
// Ambient lift tint used in place of ambient_tint once winter_amount is up. The summer
// one is green, and green bounce light on snow turns it into moss.
uniform vec4 winter_ambient_tint : source_color = vec4(0.72, 0.80, 0.95, 1.0);

group_uniforms Winter.Snow;
uniform vec4 snow_color : source_color = vec4(0.94, 0.96, 1.0, 1.0);
// How far down the blade the snow reaches. 0 - nothing; 0.5 - the top half; 1 - the whole
// blade, right down to the root.
uniform float snow_coverage : hint_range(0.0, 1.0) = 0.55;
// The edge between snow and blade. 0 leaves a long fade, 1 cuts a crisp line.
uniform float snow_hardness : hint_range(0.0, 1.0) = 0.45;
// Breaks the snow line so it does not sit at one height across the whole meadow: some
// clumps are buried deeper, others are barely dusted.
uniform float snow_patchiness : hint_range(0.0, 1.0) = 0.4;
// Size of those patches in world units - lower is broader.
uniform float snow_patch_scale : hint_range(0.005, 1.0) = 0.12;
// Snow is smoother than a leaf, so it takes a highlight where the blade under it does not.
uniform float snow_smoothness : hint_range(0.0, 1.0) = 0.45;
// How much of the blade underneath still shows through the snow. At 0 the snow is a flat
// white shape and the clump loses every edge inside it; the default keeps enough variation
// that individual blades are still separate up close.
uniform float snow_texture_keep : hint_range(0.0, 1.0) = 0.4;

group_uniforms Performance;
// Where the procedural tint is computed.
//   2 - both layers per pixel. Original look, the most expensive setting.
//   1 - the large layer moves to the vertices, the small one stays per pixel.
//       The large layer is low frequency (its pattern is metres across), so interpolating
//       it across a clump is not visible. This is the recommended setting.
//   0 - both layers on the vertices. Cheapest; the small layer has a pattern about one
//       metre across, so at this setting it loses definition up close.
uniform int tint_quality : hint_range(0, 2) = 1;

varying vec3 v_world;
varying vec3 v_tint;
// 0 at the root, 1 at the tip. The snow line is measured on it.
varying float v_blade_height;


// --- procedural noise (Inigo Quilez style hash + value noise) ----------------
float ea_hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float ea_value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	// Quintic (6t^5-15t^4+10t^3) instead of the cubic smoothstep. The cubic curve has a
	// non-zero second derivative at the cell edges, and the seams of the noise grid show
	// up as faint rectangular patches across the meadow. Quintic flattens the curve at
	// both ends, so neighbouring cells join without a visible edge. Costs a few multiplies
	// per sample and no extra texture or hash lookups.
	vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	float a = ea_hash21(i);
	float b = ea_hash21(i + vec2(1.0, 0.0));
	float c = ea_hash21(i + vec2(0.0, 1.0));
	float d = ea_hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}


// Three octaves of the noise above, each rotated and doubled in frequency. One octave of
// value noise always keeps the square outline of its own lattice, and on a meadow that
// shows up as rectangular patches. Stacking rotated octaves breaks the lattice apart and
// gives a shape closer to how colour actually varies across a field.
//
// The measurement that justified this: moving the tint off the pixels and onto the
// vertices changed the frame time by less than half a percent, so the tint was never what
// the GPU was waiting on. The spare pixel budget went here.
float ea_fbm(vec2 p) {
	mat2 rot = mat2(vec2(0.80, 0.60), vec2(-0.60, 0.80));
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 3; i++) {
		v += a * ea_value_noise(p);
		p = rot * p * 2.03;
		a *= 0.5;
	}
	// Averaging octaves pulls every value toward the middle of the range, and the pattern
	// comes out flat. This stretches the range back open. smoothstep and not clamp, so the
	// ends of the curve stay round and nothing lands on a plateau.
	return smoothstep(0.12, 0.88, v / 0.875);
}


// Saturation without a ceiling. clamp() and min() both flatten everything above the limit
// onto one value, and the tint then ends at a hard rim - the thing that made the bands
// look cut out of the grass rather than lying in it. This curve approaches 1.0 and never
// reaches it, so there is no flat top anywhere and every edge stays a gradient.
float ea_soft_saturate(float x, float k) {
	return 1.0 - exp(-k * max(x, 0.0));
}


// --- tint --------------------------------------------------------------------
// Width is a power curve applied to the noise:
//   width=0 -> pow(n, 4)    : a few sharp patches, most of the surface left clean
//   width=1 -> pow(n, 0.25) : noise floor lifted, tint covers almost everything
float ea_tint_noise(vec2 world_xz, vec2 wind_dir, float scale, float scroll_speed,
		float width, float phase_offset, bool stripes) {
	float n_mul;
	if (stripes) {
		// Stripes mode. The band coordinate runs along the axis perpendicular to the
		// wind, so the bands themselves lie downwind and drift with it.
		//
		// A plain dot product has perfectly straight isolines, and a warp that reads the
		// perpendicular axis ALONE cannot bend them - it can only change their spacing.
		// That is what left every band edge looking drawn with a ruler. The band
		// coordinate is therefore displaced by noise sampled on BOTH world axes (a 2D
		// domain warp), so an edge bends along its own length.
		vec2 perp = vec2(-wind_dir.y, wind_dir.x);
		float perp_coord = dot(world_xz, perp);
		float t = TIME * scroll_speed;
		vec2 uv = world_xz * scale;

		// 1) Spacing: some bands come out wide and lazy, others narrow and sharp.
		float spacing = ea_fbm(vec2(perp_coord * scale * 0.18 + t * 0.4,
			3.7 + phase_offset));

		// 2) Wander: a long, slow displacement that carries a whole stretch of band
		//    sideways by a good part of its own width, so it crosses the meadow as a
		//    curve. Amplitude is deliberately below one band width - above that the
		//    bands fold over each other and stop being bands.
		float wander = ea_fbm(uv * 0.30 + vec2(t * 0.12, 4.1 + phase_offset)) - 0.5;

		// 3) Fray: short-wavelength displacement that breaks the boundary into a ragged
		//    line, the way one patch of grass ends against another.
		float fray = ea_fbm(uv * 1.35 + vec2(t * 0.20, 12.9 + phase_offset)) - 0.5;

		float band_coord = perp_coord * scale + spacing * 4.0
			+ wander * 1.15 + fray * 0.38 + t;
		float n_main = ea_fbm(vec2(band_coord, 1.7 + phase_offset));

		// Width envelope, sampled in 2D as well: a band fades out in one place and
		// strengthens in another instead of holding one value from one edge of the
		// meadow to the other.
		float n_env = ea_fbm(uv * 0.38 + vec2(t * 0.30, 9.1 + phase_offset));
		n_mul = ea_soft_saturate(n_main * (0.20 + n_env * 2.1), 1.15);
	} else {
		// Blob mode: two crossed layers of 2D noise, multiplied together. A single layer
		// would produce a visible band sweeping across the field; crossing them breaks it
		// into organic, drifting patches.
		vec2 base_uv = world_xz * scale;
		vec2 scroll_a = wind_dir * TIME * scroll_speed;
		vec2 scroll_b = vec2(-wind_dir.y, wind_dir.x) * TIME * scroll_speed * 0.7;
		float n_a = ea_fbm(base_uv + scroll_a + phase_offset);
		// The second layer is rotated by ~31 degrees. Value noise carries faint streaks
		// along its own X and Y axes; with both layers on the same axes those streaks line
		// up and read as a grid. Rotating one layer crosses them instead. Four multiplies,
		// no extra sample.
		vec2 rot_uv = vec2(base_uv.x * 0.857 - base_uv.y * 0.515,
			base_uv.x * 0.515 + base_uv.y * 0.857);
		float n_b = ea_fbm(rot_uv * 1.6 + scroll_b + 7.37 + phase_offset);
		n_mul = ea_soft_saturate(n_a * n_b * 3.2, 1.3);
	}
	float curve = mix(4.0, 0.25, clamp(width, 0.0, 1.0));
	return pow(n_mul, curve);
}


// want_small / want_large pick which layers this call produces. The vertex stage and the
// fragment stage each take a subset, so no layer is ever computed twice.
vec3 ea_dual_noise_tint(vec3 world_pos, bool want_small, bool want_large) {
	vec2 wind_dir = normalize(vec2(wind_direction_x, wind_direction_z) + vec2(1e-4, 0.0));

	// Each layer keeps a separate set of parameters for blob and stripes mode, so
	// switching the mode back and forth never loses the other one's settings.
	float s_scale = stripes_mode_small ? tint_scale_small_stripes : tint_scale_small_blob;
	float s_intensity = stripes_mode_small ? tint_intensity_small_stripes : tint_intensity_small_blob;
	float s_width = stripes_mode_small ? tint_width_small_stripes : tint_width_small_blob;
	float s_speed = stripes_mode_small ? tint_speed_small_stripes : tint_speed_small_blob;

	float l_scale = stripes_mode_large ? tint_scale_large_stripes : tint_scale_large_blob;
	float l_intensity = stripes_mode_large ? tint_intensity_large_stripes : tint_intensity_large_blob;
	float l_width = stripes_mode_large ? tint_width_large_stripes : tint_width_large_blob;
	float l_speed = stripes_mode_large ? tint_speed_large_stripes : tint_speed_large_blob;

	vec3 tint_s = vec3(1.0);
	if (want_small) {
		float n_s = ea_tint_noise(world_pos.xz, wind_dir, s_scale, s_speed, s_width,
			0.0, stripes_mode_small);
		tint_s = mix(vec3(1.0), tint_color_small.rgb, n_s * s_intensity);
	}
	vec3 tint_l = vec3(1.0);
	if (want_large) {
		float n_l = ea_tint_noise(world_pos.xz, wind_dir, l_scale, l_speed, l_width,
			11.3, stripes_mode_large);
		tint_l = mix(vec3(1.0), tint_color_large.rgb, n_l * l_intensity);
	}
	return tint_s * tint_l;
}


// --- wind --------------------------------------------------------------------
// Three layers:
//   1) per-blade jitter - phase derived from the VERTEX position in object space, so
//      blades within one clump move independently of each other;
//   2) coherent gust - noise over world XZ scrolled along the wind direction, so
//      neighbouring clumps lean together, like a wave crossing the meadow;
//   3) micro-tremble - fast, small shaking of the leaf.
// Anchoring: y=0 (root) stays put, y=1 (tip) swings the most.
void vertex() {
	vec3 position_os = VERTEX;
	float anchor = clamp(position_os.y / max(anchor_height, 0.001), 0.0, 1.0);
	v_blade_height = anchor;
	vec3 world_pos = (MODEL_MATRIX * vec4(position_os, 1.0)).xyz;
	v_world = world_pos;

	vec2 dir = normalize(vec2(wind_direction_x, wind_direction_z) + vec2(1e-4, 0.0));

	// 1) per-blade jitter
	float world_phase = TIME * wind_speed
		+ world_pos.x * wind_frequency
		+ world_pos.z * wind_frequency;
	float blade_phase = position_os.x * 12.7 + position_os.z * 8.3;
	float wave = sin(world_phase + blade_phase);
	float jitter = sin(TIME * wind_speed * 3.1 + blade_phase * 2.1) * 0.35;
	float per_blade = wave + jitter;

	// 2) coherent gust
	vec2 gust_uv = world_pos.xz * gust_scale - dir * TIME * gust_speed;
	float gust_sample = gust_use_texture
		? textureLod(gust_noise, gust_uv, 0.0).r
		: ea_value_noise(gust_uv);
	float gust = (gust_sample - 0.5) * 2.0 * gust_strength;

	float total = per_blade + gust;
	vec3 push_ws = vec3(dir.x, 0.0, dir.y) * total * wind_strength * anchor;

	// The push is computed in world space but applied in object space - otherwise rotated
	// instances (the scatter randomises yaw) would each bend in their own direction.
	// Instead of a costly inverse() we use transpose(M) = s^2 * M^-1 for a rotation with
	// uniform scale, so we just divide by the squared column length.
	mat3 m = mat3(MODEL_MATRIX);
	float s2 = max(dot(m[0], m[0]), 1e-6);
	vec3 push_os = (push_ws * m) / s2;

	VERTEX = position_os + push_os;

	// The tint is sampled at the UNBENT world position. Sampling after the wind push would
	// drag the colour pattern along with every blade instead of leaving it lying on the
	// meadow, and the patches would visibly shiver.
	v_tint = (tint_quality < 2)
		? ea_dual_noise_tint(world_pos, tint_quality == 0, true)
		: vec3(1.0);
}


void fragment() {
	vec4 albedo_a = texture(albedo_texture, UV);

	// The normal is assembled by hand instead of through NORMAL_MAP, because it still has
	// to be biased toward world up - and Godot applies NORMAL_MAP only AFTER fragment().
	vec3 n_ts = texture(normal_texture, UV).rgb * 2.0 - 1.0;
	n_ts.xy *= normal_strength;
	vec3 n_view = normalize(mat3(TANGENT, BINORMAL, NORMAL) * n_ts);
	// World up in view space - inside Godot's fragment shader normals are view-space.
	vec3 up_view = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	NORMAL = normalize(mix(n_view, up_view, normal_up_bias));

	// Whatever the vertex stage already produced comes in through v_tint; only the layers
	// left over are computed here.
	vec3 tint = v_tint;
	if (tint_quality == 2) {
		tint = ea_dual_noise_tint(v_world, true, true);
	} else if (tint_quality == 1) {
		tint *= ea_dual_noise_tint(v_world, true, false);
	}

	vec3 color = albedo_a.rgb * base_color.rgb * tint;
	vec3 ambient = ambient_tint.rgb;
	float surface_smoothness = smoothness;

	// --- winter ---------------------------------------------------------------
	// The whole block hangs off a uniform, so a summer material never runs it.
	if (winter_amount > 0.0) {
		// 1) The blade itself. The procedural tint is pulled toward neutral first, then
		//    the winter colour is applied on top of the texture - so the pattern of the
		//    field is still there, just faded, instead of being painted over.
		float luma = dot(albedo_a.rgb, vec3(0.2126, 0.7152, 0.0722));
		vec3 drained = mix(albedo_a.rgb, vec3(luma), winter_desaturate);
		vec3 faded_tint = mix(vec3(1.0), tint, winter_tint_keep);
		vec3 winter_blade = drained * winter_color.rgb * faded_tint;
		color = mix(color, winter_blade, winter_amount);
		ambient = mix(ambient, winter_ambient_tint.rgb, winter_amount);

		// 2) The snow. It is measured on the blade, not on the normal: a grass card has a
		//    normal that the shader already bends toward world up, so a "how upward does
		//    this face point" test would cover the whole blade evenly and look like paint.
		//    Height down from the tip is what actually distinguishes a dusted blade from
		//    a buried one.
		float line = 1.0 - snow_coverage;
		// One noise sample moves the line up in some places and down in others, so the
		// snow does not stop at one height across the entire meadow.
		float patch = (ea_fbm(v_world.xz * snow_patch_scale) - 0.5) * snow_patchiness;
		float edge = mix(0.30, 0.015, snow_hardness);
		float snow = smoothstep(line + patch - edge, line + patch + edge, v_blade_height);
		snow *= winter_amount;

		// The snow keeps a trace of the blade's own light and dark, otherwise a covered
		// clump renders as one white silhouette with no edges inside it.
		vec3 snow_rgb = snow_color.rgb * mix(1.0, 0.72 + luma * 0.56, snow_texture_keep);
		color = mix(color, snow_rgb, snow);
		// Snow catches a highlight the leaf under it does not, and it does not take the
		// green bounce either.
		surface_smoothness = mix(surface_smoothness, snow_smoothness, snow);
		ambient = mix(ambient, vec3(1.0), snow);
	}

	ALBEDO = color;
	ALPHA = albedo_a.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_cutoff;

	ROUGHNESS = 1.0 - surface_smoothness;
	METALLIC = metallic;

	// Ambient lift: keeps shaded sides from dropping to black. Added proportionally to
	// albedo so the color stays grassy instead of turning grey.
	EMISSION = ALBEDO * ambient * ambient_lift;
}
