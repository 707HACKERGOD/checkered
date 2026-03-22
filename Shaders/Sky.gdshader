shader_type sky;

// --- GLOBAL UNIFORMS ---
global uniform float rain_amount : hint_range(0.0, 1.0);
global uniform float snow_amount : hint_range(0.0, 1.0);

// --- UNIFORMS ---
uniform vec3 sky_top_color : source_color = vec3(0.1, 0.4, 0.8);
uniform vec3 sky_horizon_color : source_color = vec3(0.4, 0.6, 0.9);
uniform vec3 sky_night_color : source_color = vec3(0.02, 0.02, 0.05);
uniform vec3 sun_color : source_color = vec3(1.0, 0.9, 0.7);
uniform vec3 moon_color : source_color = vec3(0.9, 0.95, 1.0);

uniform float lightning_strength : hint_range(0.0, 1.0) = 0.0;
uniform vec3 lightning_color : source_color = vec3(0.8, 0.9, 1.0);

uniform vec2 wind_offset; 
uniform sampler2D cloud_texture : filter_linear_mipmap, repeat_enable;
uniform sampler2D star_texture : filter_nearest, repeat_enable;
uniform sampler2D moon_texture : filter_linear, repeat_disable;

// --- SETTINGS ---
uniform float time_of_day : hint_range(0, 24) = 12.0;
uniform float cloud_scale : hint_range(0.01, 2.0) = 0.4;
uniform float cloud_coverage : hint_range(0.0, 1.0) = 0.5; 
uniform float cloud_softness : hint_range(0.01, 1.0) = 0.5;
uniform vec3 cloud_tint : source_color = vec3(1.0); 
uniform float moon_size : hint_range(0.0, 0.5) = 0.05; 

void sky() {
    // 1. TIME & ORBIT
    float sun_angle = (time_of_day - 6.0) * (3.14159 / 12.0);
    vec3 sun_dir = normalize(vec3(0.0, sin(sun_angle), cos(sun_angle)));
    vec3 moon_dir = -sun_dir; 
    
    float sun_mask = clamp(dot(EYEDIR, sun_dir), 0.0, 1.0);
    float base_moon = clamp(dot(EYEDIR, moon_dir), 0.0, 1.0);
    
    // Cloud Glow Mask (Tight radius for lighting clouds)
    float moon_glow_mask = pow(base_moon, 50.0); 

    float day_amt = smoothstep(-0.1, 0.1, sin(sun_angle));
    float sunset_amt = 1.0 - abs(sin(sun_angle));
    sunset_amt = pow(sunset_amt, 2.0);

    // 2. BACKGROUND SKY
    float precip_amt = clamp(rain_amount + snow_amount, 0.0, 1.0);
    
    vec3 day_sky = mix(sky_horizon_color, sky_top_color, clamp(EYEDIR.y, 0.0, 1.0));
    vec3 final_bg = mix(sky_night_color, day_sky, day_amt);

    // Sun Disc
    float sun_core = smoothstep(0.998, 0.999, sun_mask);
    final_bg += sun_color * sun_core * day_amt * (1.0 - precip_amt);
    
    // --- MOON TEXTURE LOGIC ---
    // This replaces the simple circle logic.
    // Calculate visibility (Fade to 30% in rain, don't disappear)
    float moon_visibility = mix(1.0, 0.3, precip_amt);

    // Construct Moon Basis for UVs
    vec3 moon_up = vec3(0.0, 1.0, 0.0);
    vec3 moon_right = normalize(cross(moon_dir, moon_up));
    vec3 moon_actual_up = cross(moon_right, moon_dir);
    
    float uv_x = dot(EYEDIR, moon_right);
    float uv_y = dot(EYEDIR, moon_actual_up);
    
    vec2 moon_uv = vec2(uv_x, uv_y) / (moon_size * 2.0) + 0.5;
    
    // Mask to only draw within the square 0-1
    float in_moon_box = step(0.0, moon_uv.x) * step(moon_uv.x, 1.0) * step(0.0, moon_uv.y) * step(moon_uv.y, 1.0);
    float facing_moon = step(0.0, dot(EYEDIR, moon_dir)); 
    
    vec4 moon_tex = texture(moon_texture, moon_uv);
    
    // Draw Textured Moon
    final_bg += moon_tex.rgb * moon_tex.a * in_moon_box * facing_moon * 0.8 * (1.0 - day_amt) * moon_visibility;

    // 3. CLOUDS
    vec4 cloud_res = vec4(0.0);
    
    if (EYEDIR.y > 0.0) {
        // Mapping
        vec2 sky_uv = (EYEDIR.xz / (EYEDIR.y + 0.15)) * cloud_scale;
        vec2 warp_uv = sky_uv * 0.4 - (wind_offset * 0.1); 
        float warp_noise = texture(cloud_texture, warp_uv).r;
        vec2 distorted_uv = sky_uv - wind_offset + (vec2(warp_noise) * 0.04);
        
        float noise1 = texture(cloud_texture, distorted_uv).r;
        float noise2 = texture(cloud_texture, distorted_uv * 0.5 + vec2(0.5)).r; 
        float boil = sin(wind_offset.x * 1.5 + sky_uv.x) * 0.05;
        float combined = mix(noise1, noise2, 0.5 + boil);
        
        float density = smoothstep(1.0 - cloud_coverage, 1.0 - cloud_coverage + cloud_softness, combined);
        density *= smoothstep(0.0, 0.3, EYEDIR.y); 
        
        // --- LIGHTING ---
        float height_gradient = pow(EYEDIR.y, 0.5); 
        vec3 local_ambient = mix(sky_horizon_color, sky_top_color, height_gradient);

        vec3 sunset_scatter = vec3(1.0, 0.6, 0.4); 
        vec3 scatter_tint = mix(vec3(1.0), sunset_scatter, sunset_amt);
        scatter_tint = mix(scatter_tint, vec3(0.95), precip_amt);

        float shadow_depth = exp(-density * (1.5 + precip_amt * 0.5));
        float min_brightness = mix(0.0, 0.8, precip_amt); 
        float final_shadow = max(shadow_depth, min_brightness);
        
        vec3 core_col = local_ambient * final_shadow * 1.0; 
        
        float scatter_strength = (1.0 - density) * density * 4.0;
        vec3 scatter_col = scatter_tint * sun_color * scatter_strength * 1.5;
        scatter_col *= (0.5 + 0.5 * sun_mask);

        float sun_rim = (1.0 - density) * sun_mask * 4.0 * final_shadow;
        vec3 rim_col = sun_color * sun_rim;
        
        // Moon Rim (Using tight glow mask)
        float moon_rim = (1.0 - density) * moon_glow_mask * 0.2 * final_shadow * (1.0 - day_amt);
        rim_col += moon_color * moon_rim;
        
        // Combine
        vec3 final_rgb = (core_col + scatter_col + rim_col) * day_amt;
        
        // Night base (Using tight glow mask for internal glow)
        vec3 night_base = local_ambient * 0.02;
        vec3 night_scatter = moon_color * 0.02 * moon_glow_mask * (1.0 - day_amt);
        
        vec3 night_cloud_col = night_base + night_scatter + (moon_color * moon_rim);
        final_rgb += night_cloud_col * (1.0 - day_amt) * density;

        final_rgb *= cloud_tint;

        cloud_res = vec4(final_rgb, density);
    }
    
    // 4. COMPOSITE
    vec2 star_uv = vec2(atan(EYEDIR.z, EYEDIR.x), EYEDIR.y * 2.0);
    float stars = texture(star_texture, star_uv * 2.0).r;
    float star_vis = step(0.85, stars) * (1.0 - day_amt) * (1.0 - precip_amt) * (1.0 - cloud_res.a);
    
    vec3 final_output = mix(final_bg, cloud_res.rgb, cloud_res.a);
    final_output += vec3(star_vis);
	
	// --- LIGHTNING FLASH FIX ---
    // This lights up the entire atmosphere
    // 0.2 strength ensures it doesn't blind the player, just flashes the fog/sky
    final_output += lightning_color * lightning_strength * 0.3;

    COLOR = final_output;
}