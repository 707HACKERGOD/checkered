// SkyWeaver — gradient-driven seasonal sky. Standalone, no includes.
// CPU pushes a tiny palette (season + time + weather already blended).
// This shader owns the look: gradient dome, sunset band, sun/moon,
// Milky Way + twinkle stars, Sky3D-style cirrus/cumulus, lightning.
shader_type sky;
render_mode use_debanding, use_half_res_pass; // remove half_res for crisper reflections

// ============================================================
// GLOBALS — rain/snow keep your existing names. lightning_strength
// is now a GLOBAL (was a material uniform): call SkyWeaver.SetLightning().
// wind_vector is set by SkyWeaver and read by your foliage shader —
// the SAME vector drives cloud drift. Names must match your foliage.
// ============================================================
global uniform float rain_amount  : hint_range(0.0, 1.0);
global uniform float snow_amount  : hint_range(0.0, 1.0);
global uniform float lightning_strength : hint_range(0.0, 1.0);

// ------------------------------------------------------------
// PALETTE — pushed every update by SkyWeaver.cs
// ------------------------------------------------------------
group_uniforms Palette;
uniform vec3 sky_top_color     : source_color = vec3(0.10, 0.45, 0.85);
uniform vec3 sky_horizon_color : source_color = vec3(0.55, 0.70, 0.95);
uniform vec3 sky_night_color   : source_color = vec3(0.015, 0.02, 0.05);
uniform vec3 sun_color         : source_color = vec3(1.0, 0.9, 0.7);
uniform vec3 moon_color        : source_color = vec3(0.9, 0.95, 1.0);
uniform vec3 cloud_tint        : source_color = vec3(1.0);
uniform vec3 sun_dir  = vec3(0.0, 1.0, 0.0);
uniform vec3 moon_dir = vec3(0.0, -1.0, 0.0);
uniform float cloud_coverage : hint_range(0.0, 1.0) = 0.4;
uniform float storm_darkness  : hint_range(0.0, 1.0) = 0.0;
group_uniforms;

group_uniforms Look;
uniform float horizon_power : hint_range(0.5, 4.0) = 1.6;
uniform float sunset_band_strength = 1.1;
uniform float sun_disc_size : hint_range(0.0002, 0.02) = 0.001; // 0.001 == your old smoothstep(0.998,0.999) exactly
uniform float sun_disc_intensity = 5.0;
uniform float sun_halo_strength = 0.4;
uniform vec3  ground_color : source_color = vec3(0.28, 0.27, 0.26);
group_uniforms;

group_uniforms Moon;
uniform sampler2D moon_texture : source_color, filter_linear, repeat_disable;
uniform float moon_size : hint_range(0.01, 0.3) = 0.07;
uniform float moon_brightness = 1.2;
uniform float moon_fullness : hint_range(0.0, 1.0) = 1.0;

uniform float cirrus_day_intensity    = 2.4;  // noon
uniform float cirrus_night_intensity  = 1.0;  // sunrise/sunset/night
uniform float cumulus_day_intensity   = 4.0;  // noon: white clouds
uniform float cumulus_night_intensity = 0.6;  // sunrise/sunset: the dark look
group_uniforms;

group_uniforms Stars;
uniform sampler2D starmap_texture : source_color, filter_linear;
uniform vec3  starmap_tint : source_color = vec3(0.75, 0.75, 0.8);
uniform float starmap_brightness = 1.0;
uniform vec3  star_alignment = vec3(2.68288, -0.25891, 0.40101);
uniform float star_rotation = 0.0;
uniform float star_tilt = 0.0;
uniform bool  starmap_flip_u = false;
uniform bool  starmap_flip_v = false;
uniform float star_brightness = 2.3;
uniform float twinkle_speed = 0.8;
uniform float star_movement = 0.0002;
group_uniforms;

group_uniforms Clouds;
uniform vec2 cloud_offset = vec2(0.0); // accumulated wind drift, pushed by SkyWeaver
uniform vec3 clouds_night_color : source_color = vec3(0.09, 0.094, 0.129);
uniform float cumulus_moon_intensity = 2.2;  // full-moon: white moonlit clouds
uniform float cirrus_moon_intensity = 1.6;

uniform bool  cirrus_visible = true;
uniform float cirrus_thickness = 2.0;
uniform float cirrus_absorption = 2.0;
uniform float cirrus_coverage_scale : hint_range(0.0, 1.5) = 0.85;
uniform float cirrus_size = 1.0;
uniform vec2  cirrus_uv = vec2(0.16, 0.11);
uniform float cirrus_sky_tint_fade : hint_range(0.0, 1.0) = 0.5;
uniform sampler2D cirrus_texture : filter_linear, repeat_enable;

uniform bool  cumulus_visible = true;
uniform float cumulus_thickness = 0.0243;
uniform float cumulus_absorption = 2.0;
uniform float cumulus_noise_freq = 2.7;
uniform float cumulus_size = 0.5;
uniform float cumulus_mie_intensity = 1.0;
uniform float cumulus_sky_tint_fade : hint_range(0.0, 1.0) = 0.0;
uniform sampler2D cumulus_texture : filter_linear, repeat_enable;
group_uniforms;

group_uniforms Lightning;
uniform vec3 lightning_color : source_color = vec3(0.8, 0.9, 1.0);
group_uniforms;

// ============================================================
// HELPERS
// ============================================================
const int   CUMULUS_STEPS = 8; // Sky3D used 10; 8 + early-out is visually identical here
const float INV_TAU = 0.159154943;
const float INV_PI  = 0.318309886;

float sat(float x) { return clamp(x, 0.0, 1.0); }

float map_range(float v, float a, float b, float c, float d) {
    return c + (v - a) * (d - c) / max(b - a, 1e-5);
}

mat3 rot3(vec3 r) {
    float c1 = cos(r.x), s1 = sin(r.x);
    float c2 = cos(r.y), s2 = sin(r.y);
    float c3 = cos(r.z), s3 = sin(r.z);
    mat3 rx = mat3(vec3(1.0, 0.0, 0.0), vec3(0.0, c1, -s1), vec3(0.0, s1, c1));
    mat3 ry = mat3(vec3(c2, 0.0, s2), vec3(0.0, 1.0, 0.0), vec3(-s2, 0.0, c2));
    mat3 rz = mat3(vec3(c3, -s3, 0.0), vec3(s3, c3, 0.0), vec3(0.0, 0.0, 1.0));
    return rz * ry * rx;
}

vec2 equirect_uv(vec3 n) {
    float fu = starmap_flip_u ? -1.0 : 1.0;
    float fv = starmap_flip_v ? -1.0 : 1.0;
    float u = fu * ((atan(n.y, n.x) + PI) * INV_TAU);
    float v = -fv * (acos(clamp(n.z, -1.0, 1.0)) * INV_PI);
    return vec2(u, v);
}

float hash21(vec2 p) {
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 74.7);
    return fract(p.x * p.y);
}

// Your twinkle layer (seam/pole/perf fixes kept as-is)
float star_layer(vec3 dir, float grid, float density, float base_size) {
    const float SEAM_ROT = -1.5708;
    float cs = cos(SEAM_ROT), sn = sin(SEAM_ROT);
    vec3 sdir = vec3(cs * dir.x - sn * dir.z, dir.y, sn * dir.x + cs * dir.z);
    float lon = atan(sdir.z, sdir.x);
    float lat = asin(clamp(sdir.y, -1.0, 1.0));
    vec2 uv = vec2(lon / 6.2831853 + 0.5, lat / 3.1415926 + 0.5);
    uv.x += TIME * star_movement;
    uv.y += sin(TIME * 0.15) * 0.00015;
    uv *= grid;
    vec2 cell  = floor(uv);
    vec2 local = fract(uv) - 0.5;
    float random = hash21(cell);
    float exists = step(random, density);
    vec2 star_position = vec2(hash21(cell + vec2(12.4, 75.2)),
                              hash21(cell + vec2(83.1, 42.7))) - 0.5;
    float size_random = hash21(cell + vec2(51.7, 19.3));
    float size = base_size * (0.65 + size_random * 1.6);
    float star = 1.0 - smoothstep(0.0, size, length(local - star_position * 0.72));
    star *= smoothstep(0.98, 0.45, abs(sdir.y)); // pole fade
    float phase = hash21(cell + vec2(14.2, 91.7)) * 6.2831853;
    float speed = 0.35 + hash21(cell + vec2(71.3, 23.8)) * 1.4;
    float wave  = 0.5 + 0.5 * sin(TIME * twinkle_speed * speed + phase);
    float wave2 = 0.5 + 0.5 * sin(TIME * twinkle_speed * speed * 0.37 + phase * 1.73);
    star *= 0.65 + wave * 0.25 + wave2 * 0.10;
    star *= 0.40 + hash21(cell + vec2(41.2, 87.6)) * 1.25;
    return star * exists;
}

bool ray_sphere_intersect(float r, vec3 origin, vec3 dir, out float t, out vec3 nrm) {
    origin += vec3(0.0, 450.0, 0.0); // cloud shell height, as in Sky3D
    float a = dot(dir, dir);
    float b = 2.0 * dot(origin, dir);
    float c = dot(origin, origin) - r * r;
    float d = b * b - 4.0 * a * c;
    float valid = step(0.0, d);
    d = sqrt(max(d, 0.0));
    a *= 2.0;
    float t1 = 0.5 * (-b + d);
    float t2 = 0.5 * (-b - d);
    t = mix(t2, t1, step(t2, t1) * step(0.0, t1));
    t = mix(0.0, t, step(0.0, t) * valid);
    nrm = mix(vec3(0.0), origin + t * dir, step(0.0, t) * valid);
    return bool(step(0.0, t) * valid);
}

// ============================================================
// CLOUDS (Sky3D look, wind + coverage now unified)
// ============================================================
float sample_cumulus_noise(vec3 p) {
    vec3 pos = p * 0.01;
    pos.z *= 256.0;
    vec2 offset = vec2(0.317, 0.123);
    vec2 uv1 = pos.xy + offset * floor(pos.z);
    vec2 uv2 = uv1 + offset;
    float x1 = textureLod(cumulus_texture, uv1, 0.0).r;
    float x2 = textureLod(cumulus_texture, uv2, 0.0).r;
    return mix(x1, x2, fract(pos.z));
}

float cumulus_fbm(vec3 p) {
    float l = cumulus_noise_freq;
    float ret = 0.51749673 * sample_cumulus_noise(p);
    p *= l; ret += 0.25584929 * sample_cumulus_noise(p);
    p *= l; ret += 0.12527603 * sample_cumulus_noise(p);
    p *= l; ret += 0.06255931 * sample_cumulus_noise(p);
    return ret;
}

float cumulus_density(vec3 p, vec3 wind, float t) {
    vec3 pos = p * 0.0212242 - wind;
    float dens = cumulus_fbm(pos);
    dens += dens;
    float cov = 1.0 - cloud_coverage;
    cov = smoothstep(0.0, (cov * 3.5) + t, dens);
    dens *= cov;
    return sat(map_range(dens, 1.0 - cov, 1.0, 0.0, 1.0));
}

vec4 render_cirrus(vec3 rd) {
    vec2 p = (rd.xz / max(rd.y, 0.05)) * cirrus_size;
    vec2 off = cloud_offset * 0.25; // cirrus drifts slower (parallax)
    float a = textureLod(cirrus_texture, p * cirrus_uv - off, 0.0).r;
	float b = textureLod(cirrus_texture, p * cirrus_uv - off * 1.6 + vec2(0.37, 0.11), 0.0).r;
	float d = (a + b) * 0.5;
    float cov = 1.0 - sat(cloud_coverage * cirrus_coverage_scale);
    float density = sat(d - cov);
    float sh = sat(exp(-cirrus_absorption * density));
    return vec4(vec3(density * sh), sat(density * cirrus_thickness));
}

vec4 render_cumulus(vec3 rd, float night_f, float wet) {
    vec3 wind = vec3(cloud_offset.x, 0.0, cloud_offset.y); // drifts downwind; negate if backwards
    if (rd.y <= 0.02) return vec4(0.0);
    float tt; vec3 n;
    if (!ray_sphere_intersect(500.0, vec3(0.0), rd, tt, n)) return vec4(0.0);

    float march = float(CUMULUS_STEPS) * cumulus_thickness;
    vec3 dir_step = (rd / max(rd.y, 1e-4)) * march;
    vec3 pos = n * cumulus_size;

    float mu_s = sat(dot(sun_dir, rd));
    float mu_m = sat(dot(moon_dir, rd));
    vec3 glow = sun_color * pow(mu_s, 3.0) * cumulus_mie_intensity
              + moon_color * pow(mu_m, 3.0) * 0.35 * night_f;

    vec4 t = vec4(1.0);
    t.rgb += glow;
    vec4 ret = vec4(0.0);
    float alpha = 0.0;
    float trans = 1.0;
    float absorb = cumulus_absorption * (1.0 + wet * 1.5); // darker cores in storms

    for (int i = 0; i < CUMULUS_STEPS; i++) {
        float h = float(i) * 0.1;
        float density = cumulus_density(pos, wind, h);
        float sh = sat(exp(-absorb * density * march));
        t *= sh;
        trans *= sh;
        ret += t * (exp(h) * 0.571428571) * density * march;
        alpha += (1.0 - sh) * (1.0 - alpha);
        pos += dir_step;
        if (trans < 0.03) break; // early-out, big perf win
    }
    return vec4(ret.rgb, alpha);
}

// ============================================================
// MAIN RENDER
// ============================================================
vec3 render(vec3 dir, float wet) {
    vec3 sun_pos = normalize(sun_dir);
    vec3 moon_pos = normalize(moon_dir);
    float sun_h  = sun_pos.y;
    float day_f   = smoothstep(-0.22, 0.12, sun_h); // wide, gradient-authored twilight
    float night_f = 1.0 - smoothstep(-0.18, 0.02, sun_h);
    float dusk    = smoothstep(-0.10, 0.02, sun_h) * (1.0 - smoothstep(0.18, 0.55, sun_h));

    // ---- gradient dome (your season identity) ----
    float h = sat(dir.y);
    vec3 day_sky = mix(sky_horizon_color, sky_top_color, pow(h, horizon_power));

    // sunset band: sun-colored, follows the sun's azimuth — this + your
    // gradients produce beach-magenta, wild-west red, pink dawn, green spring
    vec2 dxz = dir.xz * inversesqrt(max(dot(dir.xz, dir.xz), 1e-6));
    vec2 sxz = sun_pos.xz * inversesqrt(max(dot(sun_pos.xz, sun_pos.xz), 1e-6));
    float az = sat(dot(dxz, sxz));
    day_sky += sun_color * (pow(az, 2.5) * exp(-h * 4.5) * dusk * sunset_band_strength);

    vec3 col = mix(sky_night_color, day_sky, day_f);

    // ---- sun halo + disc (disc color = your sun gradient) ----
    float sd = sat(dot(dir, sun_pos));
    float sun_up = smoothstep(-0.08, 0.02, sun_h);
    col += sun_color * pow(sd, 20.0) * sun_halo_strength * sun_up;
    float disc = smoothstep(1.0 - sun_disc_size * 2.0, 1.0 - sun_disc_size, sd);
    col += sun_color * disc * sun_disc_intensity * sun_up * (1.0 - wet * 0.85);

    // ---- moon glow + textured disc ----
    float md = sat(dot(dir, moon_pos));
    col += moon_color * pow(md, 20.0) * 0.15 * night_f;
    vec3 up_hint = abs(moon_pos.y) > 0.99 ? vec3(0.0, 0.0, 1.0) : vec3(0.0, 1.0, 0.0);
    vec3 m_right = normalize(cross(moon_pos, up_hint));
    vec3 m_up = cross(m_right, moon_pos);
    vec2 muv = vec2(dot(dir, m_right), dot(dir, m_up)) / (moon_size * 2.0) + 0.5;
    float in_box = step(0.0, muv.x) * step(muv.x, 1.0) * step(0.0, muv.y) * step(muv.y, 1.0);
    float facing = step(0.0, dot(dir, moon_pos));
    vec4 mtex = textureLod(moon_texture, muv, 0.0);
    col += mtex.rgb * mtex.a * in_box * facing * moon_brightness * moon_color
         * (1.0 - day_f * 0.9) * (1.0 - wet * 0.7);
    float moon_mask = 1.0 - in_box * facing;

    // ---- deep space: milky way + twinkle (skipped entirely in daytime) ----
    if (night_f > 0.001) {
        mat3 tilt = rot3(vec3(star_tilt, 0.0, 0.0));
        mat3 spin = rot3(vec3(0.0, star_rotation, 0.0));
        mat3 align = rot3(star_alignment);
        vec3 adir = normalize(align * spin * tilt * dir);
        vec3 mw = textureLod(starmap_texture, equirect_uv(adir), 0.0).rgb
                * starmap_tint * starmap_brightness;
        float tw = 0.0;
        tw += star_layer(adir, 150.0, 0.24, 0.040);
        tw += star_layer(adir, 270.0, 0.12, 0.026);
        tw += star_layer(adir, 430.0, 0.055, 0.017);
        vec3 stars = mw + vec3(0.72, 0.84, 1.0) * tw * star_brightness;
        col += stars * night_f * (1.0 - wet * 0.85) * moon_mask;
    }

    // ---- clouds (tinted by the SAME gradient colors) ----
    // Brightness follows the sun: white at noon, dark near sunrise/sunset,
    // moonlit-white on full-moon nights.
    float horizon_blend = sat((dir.y - 0.02) * 8.0);
    vec3 cloud_light = mix(clouds_night_color, sun_color, day_f);
    cloud_light = mix(cloud_light, moon_color * 1.6, moon_fullness * night_f); // full-moon nights
    float noon_f = smoothstep(0.12, 0.5, sun_h); // 0 near the horizon, 1 at midday
    float cir_int = mix(cirrus_night_intensity, cirrus_day_intensity, noon_f);
    float cum_int = mix(cumulus_night_intensity, cumulus_day_intensity, noon_f);
    cum_int = mix(cum_int, cumulus_moon_intensity, moon_fullness * night_f);
    cir_int = mix(cir_int, cirrus_moon_intensity, moon_fullness * night_f);

    if (cirrus_visible && dir.y > 0.015) {
        vec4 cir = render_cirrus(dir);
        cir.a = sat(cir.a) * horizon_blend;
        cir.rgb *= cloud_light * cloud_tint * cir_int;
        cir.rgb *= mix(1.0, 0.45, storm_darkness);
        col = mix(col, cir.rgb + col * cirrus_sky_tint_fade, cir.a);
    }
    if (cumulus_visible) {
        vec4 cum = render_cumulus(dir, night_f, wet);
        cum.a = sat(cum.a) * horizon_blend;
        cum.rgb *= cloud_light * cloud_tint * cum_int;
        cum.rgb *= mix(1.0, 0.35, storm_darkness);
        col = mix(col, cum.rgb + col * cumulus_sky_tint_fade, cum.a);
    }

    // ---- below-horizon ground ----
    float below = sat(-dir.y * 30.0);
    vec3 ground = ground_color * mix(sky_night_color * 2.0, sky_horizon_color, day_f);
    col = mix(col, ground, below);

    // ---- lightning + precipitation haze ----
    col += lightning_color * lightning_strength * 0.3;
    float lum = dot(col, vec3(0.299, 0.587, 0.114));
    col = mix(col, vec3(lum), wet * 0.3);
    col *= mix(1.0, 0.75, wet);
    return col;
}

void sky() {
    float wet = clamp(rain_amount + snow_amount, 0.0, 1.0);
    COLOR = render(normalize(EYEDIR), wet);
}