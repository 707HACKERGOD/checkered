shader_type spatial;

global uniform float rain_amount; 
global uniform float snow_amount;
global uniform vec3 wind_direction; // Used for snow accumulation bias

uniform sampler2D texture_albedo : source_color, filter_nearest_mipmap;
uniform sampler2D noise_texture; // Ensure this is set in Inspector!

// Tiling controls: Higher = Smaller, grainier noise. Lower = Big chunks.
uniform float noise_scale = 12.0; 

void fragment() {
    vec4 base_tex = texture(texture_albedo, UV);
    
    // 1. GRAIN LOGIC: High scale for grainy PS1 look
    float noise = texture(noise_texture, UV * noise_scale).r; 

    vec3 final_albedo = base_tex.rgb;
    float final_roughness = 0.9; 
    float final_specular = 0.1;

    // --- PUDDLE LOGIC ---
    // We invert noise for puddles so they form in "valleys" (dark spots of noise)
    // Threshold increases with rain_amount. 
    // Fixed: Added a small offset (0.1) so they appear sooner.
    float puddle_threshold = (rain_amount * 0.7); 
    float is_puddle = step(noise, puddle_threshold); // 1.0 if puddle
    
    // Ripple Animation
    float ripple = sin(UV.x * 50.0 + TIME * 5.0) * cos(UV.y * 50.0 + TIME * 5.0);
    ripple *= 0.02 * rain_amount;

    // Wetness (Darker ground)
    final_albedo *= (1.0 - (rain_amount * 0.4)); 
    final_roughness = mix(final_roughness, 0.4, rain_amount);

    if (is_puddle > 0.5) {
        final_albedo = mix(final_albedo, final_albedo * 0.4, 1.0); // Darker water
        final_roughness = 0.1;
        final_specular = 0.8;
        NORMAL = normalize(vec3(NORMAL.x + ripple, NORMAL.y, NORMAL.z + ripple));
    }

    // --- SNOW LOGIC ---
    // 1. Wind Bias: Snow sticks to side facing wind + Up
    vec3 bias = normalize(vec3(0.0, 1.0, 0.0) + (wind_direction * 0.5));
    float up_facing = dot(NORMAL, bias);

    if (up_facing > 0.0) {
        // 2. Mix Noise + Angle for natural "patchy" look
        float accumulation = (up_facing * 0.5) + (noise * 0.5);
        
        // 3. Smooth Transition (Not hard chunks)
        // As snow_amount goes 0->1, we lower the threshold required to show snow
        float snow_threshold = 1.0 - (snow_amount * 1.2); // *1.2 ensures we reach full coverage easier
        
        // Soft edge (smoothstep) for better blending
        float snow_mask = smoothstep(snow_threshold, snow_threshold + 0.2, accumulation);
        
        // 4. CAP COVERAGE & OPACITY
        // Don't let it be 100% white. 
        // 0.9 = Max coverage (patches of ground still show)
        // 0.8 = Opacity (See slightly through the snow)
        snow_mask = clamp(snow_mask * snow_amount, 0.0, 0.9); 
        
        // Apply Snow: Mix heavily towards white but keep some original texture
        vec3 snow_color = vec3(0.9, 0.9, 0.95); // Slightly blueish white
        final_albedo = mix(final_albedo, snow_color, snow_mask * 0.85); // 0.85 Opacity
        final_roughness = mix(final_roughness, 1.0, snow_mask);
    }

    ALBEDO = final_albedo;
    ROUGHNESS = final_roughness;
    SPECULAR = final_specular;
}