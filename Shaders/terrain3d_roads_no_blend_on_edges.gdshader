shader_type spatial;
render_mode blend_mix,depth_draw_opaque,cull_back,diffuse_burley,specular_schlick_ggx,skip_vertex_transform;

// Defined Constants
#define SKIP_PASS 0
#define VERTEX_PASS 1
#define FRAGMENT_PASS 2
#define COLOR_MAP_DEF vec4(1.0, 1.0, 1.0, 0.5)
#define DIV_255 0.003921568627450
#define DIV_1024 0.0009765625
#define TAU_16TH -0.392699081698724

// Inline Functions
#define DECODE_BLEND(control) float(control >> 14u & 0xFFu) * DIV_255
#define DECODE_AUTO(control) bool(control & 0x1u)
#define DECODE_BASE(control) int(control >> 27u & 0x1Fu)
#define DECODE_OVER(control) int(control >> 22u & 0x1Fu)
#define DECODE_ANGLE(control) float(control >>10u & 0xFu) * TAU_16TH
#define DECODE_SCALE(control) (0.9 - float(((control >>7u & 0x7u) + 3u) % 8u + 1u) * 0.1)
#define DECODE_HOLE(control) bool(control >>2u & 0x1u)

#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    #define fma(a, b, c) ((a) * (b) + (c))
    #define dFdxCoarse(a) dFdx(a)
    #define dFdyCoarse(a) dFdy(a)
#endif

// Private uniforms
uniform vec3 _camera_pos = vec3(0.f);
uniform float _mesh_size = 48.f;
uniform uint _background_mode = 1u;
uniform uint _mouse_layer = 0x80000000u;
uniform float _vertex_spacing = 1.0;
uniform float _vertex_density = 1.0;
uniform float _region_size = 1024.0;
uniform float _region_texel_size = 0.0009765625;
uniform int _region_map_size = 32;
uniform int _region_map[1024];
uniform vec2 _region_locations[1024];
uniform float _texture_normal_depth_array[32];
uniform float _texture_ao_strength_array[32];
uniform float _texture_roughness_mod_array[32];
uniform float _texture_uv_scale_array[32];
uniform vec2 _texture_detile_array[32];
uniform vec4 _texture_color_array[32];
uniform highp sampler2DArray _height_maps : repeat_disable;
uniform highp sampler2DArray _control_maps : repeat_disable;
#define FILTER_LINEAR
uniform highp sampler2DArray _color_maps : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform highp sampler2DArray _texture_array_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform highp sampler2DArray _texture_array_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;

// Public uniforms
uniform float blend_sharpness : hint_range(0, 1) = 0.5;
uniform bool flat_terrain_normals = false;
uniform bool enable_projection = true;
uniform float projection_threshold : hint_range(0.0, 0.99, 0.01) = 0.8;

uniform float mipmap_bias : hint_range(0.5, 1.5, 0.01) = 1.0;
uniform float depth_blur : hint_range(0.0, 35.0, 0.1) = 0.0;
uniform float bias_distance : hint_range(0.0, 16384.0, 0.1) = 512.0;

uniform bool enable_macro_variation = true;
uniform vec3 macro_variation1 : source_color = vec3(1.);
uniform vec3 macro_variation2 : source_color = vec3(1.);
uniform float macro_variation_slope : hint_range(0., 1.)  = 0.333;

uniform highp sampler2D noise_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float noise1_scale : hint_range(0.001, 1.) = 0.04;
uniform float noise1_angle : hint_range(0, 6.283) = 0.;
uniform vec2 noise1_offset = vec2(0.5);
uniform float noise2_scale : hint_range(0.001, 1.) = 0.076;

// --- WEATHER UNIFORMS (Migrated from your beautiful shader) ---
global uniform float rain_amount : hint_range(0.0, 1.0) = 0.0;
global uniform float snow_amount : hint_range(0.0, 1.0) = 0.0;
global uniform vec3 wind_direction = vec3(0.0, 0.0, 1.0);
uniform float snow_max_height : hint_range(0.0, 3.0) = 1.0;
uniform float snow_slope_cutoff : hint_range(0.0, 1.0) = 0.5;

// Varyings & Types
struct material {
    vec4 albedo_height;
    vec4 normal_rough;
    float normal_map_depth;
    float ao_strength;
    float total_weight;
};

varying float v_vertex_xz_dist;
varying vec3 v_vertex;
varying float v_pile_height; // Snow pile height

////////////////////////
// Vertex
////////////////////////

ivec3 get_index_coord(const vec2 uv, const int search) {
    vec2 r_uv = round(uv);
    vec2 o_uv = mod(r_uv,_region_size);
    ivec2 pos;
    int bounds, layer_index = -1;
    for (int i = -1; i < clamp(search, SKIP_PASS, FRAGMENT_PASS); i++) {
        if ((layer_index == -1 && _background_mode == 0u ) || i < 0) {
            r_uv -= i == -1 ? vec2(0.0) : vec2(float(o_uv.x <= o_uv.y), float(o_uv.y <= o_uv.x));
            pos = ivec2(floor((r_uv) * _region_texel_size)) + (_region_map_size / 2);
            bounds = int(uint(pos.x | pos.y) < uint(_region_map_size));
            layer_index = (_region_map[ pos.y * _region_map_size + pos.x ] * bounds - 1);
        }
    }
    return ivec3(ivec2(mod(r_uv,_region_size)), layer_index);
}

vec3 get_index_uv(const vec2 uv2) {
    ivec2 pos = ivec2(floor(uv2)) + (_region_map_size / 2);
    int bounds = int(uint(pos.x | pos.y) < uint(_region_map_size));
    int layer_index = _region_map[ pos.y * _region_map_size + pos.x ] * bounds - 1;
    return vec3(uv2 - _region_locations[layer_index], float(layer_index));
}

void vertex() {
    v_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    v_vertex_xz_dist = length(v_vertex.xz - _camera_pos.xz);

    float scale = MODEL_MATRIX[0][0];
    float vertex_lerp = smoothstep(0.55, 0.95, (v_vertex_xz_dist / scale - _mesh_size - 4.0) / (_mesh_size - 2.0));
    vec2 v_fract = fract(VERTEX.xz * 0.5) * 2.0;
    vec2 shift = (scale < _vertex_spacing + 1e-6) ? 
        mix(v_fract, vec2(v_fract.x, -v_fract.y),
            round(fract(round(mod(v_vertex.z * _vertex_density, 4.0)) *
            round(mod(v_vertex.x * _vertex_density, 4.0)) * 0.25))
            ) :
        v_fract * round((fract(v_vertex.xz * 0.25 / scale) - 0.5) * 4.0);
    vec2 start_pos = v_vertex.xz * _vertex_density;
    vec2 end_pos = (v_vertex.xz - shift * scale) * _vertex_density;
    v_vertex.xz -= shift * scale * vertex_lerp;

    UV = v_vertex.xz * _vertex_density;
    UV2 = fma(UV, vec2(_region_texel_size), vec2(0.5 * _region_texel_size));

    ivec3 v_region = get_index_coord(start_pos, VERTEX_PASS);
    uint control = floatBitsToUint(texelFetch(_control_maps, v_region, 0)).r;
    bool hole = DECODE_HOLE(control);

    if ( !(CAMERA_VISIBLE_LAYERS == _mouse_layer) && 
            (hole || (_background_mode == 0u && v_region.z == -1))) {
        v_vertex.x = 0. / 0.;
    } else {		
        ivec3 coord_a = get_index_coord(start_pos, VERTEX_PASS);
        ivec3 coord_b = get_index_coord(end_pos, VERTEX_PASS);
        float h = mix(texelFetch(_height_maps, coord_a, 0).r,texelFetch(_height_maps, coord_b, 0).r,vertex_lerp);
        v_vertex.y = h;
    }

    // --- SMOOTH SNOW PILES ---
    v_pile_height = 0.0;
    if (snow_amount > 0.2) {
        ivec3 c_x = get_index_coord(start_pos + vec2(1.0, 0.0), VERTEX_PASS);
        ivec3 c_z = get_index_coord(start_pos + vec2(0.0, 1.0), VERTEX_PASS);
        float h_x = texelFetch(_height_maps, c_x, 0).r;
        float h_z = texelFetch(_height_maps, c_z, 0).r;
        vec3 vert_normal = normalize(vec3(v_vertex.y - h_x, _vertex_spacing, v_vertex.y - h_z));
        
        if (vert_normal.y >= snow_slope_cutoff) {
            float height_amount = clamp((snow_amount - 0.2) / 0.8, 0.0, 1.0);
            
            float n1 = textureLod(noise_texture, v_vertex.xz * 0.002, 0.0).r; 
            float n2 = textureLod(noise_texture, v_vertex.xz * 0.008 + vec2(100.0), 0.0).r; 
            float pile_noise = max(n1, n2 * 0.5); 
            
            // Smoothstep instead of step fixes the ugly sharp lines
            float pile_cutoff = mix(0.8, 0.2, height_amount);
            float pile_mask = smoothstep(pile_cutoff, pile_cutoff + 0.3, pile_noise);
            
            v_pile_height = pile_mask * height_amount;
            v_vertex.y += v_pile_height * snow_max_height;
        }
    }

    VERTEX = (VIEW_MATRIX * vec4(v_vertex, 1.0)).xyz;
    NORMAL = normalize((MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
    BINORMAL = normalize((MODELVIEW_MATRIX * vec4(BINORMAL, 0.0)).xyz);
    TANGENT = normalize((MODELVIEW_MATRIX * vec4(TANGENT, 0.0)).xyz);
}

////////////////////////
// Fragment
////////////////////////

float random(in vec2 xy) {
    return fract(sin(dot(xy, vec2(12.9898, 78.233))) * 43758.5453);
}

vec2 rotate_vec2(const vec2 v, const vec2 cs) {
    return vec2(fma(cs.x, v.x,  cs.y * v.y), fma(cs.x, v.y, -cs.y * v.x));
}

void accumulate_material(const vec3 base_ddx, const vec3 base_ddy, const float weight, const ivec3 index,
            const uint control, const vec2 texture_weight, const ivec2 texture_id, const vec3 i_normal,
            const float h, inout material mat) {

    vec2 i_pos = fma(_region_locations[index.z], vec2(_region_size), vec2(index.xy)) * _vertex_spacing;
    vec2 i_uv = v_vertex.xz;
    vec4 i_dd = vec4(base_ddx.xz, base_ddy.xz);
    mat2 p_align = mat2(1.);
    if (i_normal.y <= projection_threshold && enable_projection) {
        p_align = mat2(vec2(i_normal.z, -i_normal.x), vec2(i_normal.x, i_normal.z));
        vec2 xz = round(normalize(-i_normal.xz) * 1.3065629648763765);
        xz *= abs(xz.x) + abs(xz.y) > 1.5 ? 0.7071067811865475 : 1.0;
        xz = vec2(-xz.y, xz.x);
        i_pos = floor(vec2(dot(i_pos, xz), -h));
        i_uv = vec2(dot(v_vertex.xz, xz), -v_vertex.y);
        i_dd.xy = vec2(dot(base_ddx.xz, xz), -base_ddx.y);
        i_dd.zw = vec2(dot(base_ddy.xz, xz), -base_ddy.y);
    }

    float control_scale = DECODE_SCALE(control);
    i_dd *= control_scale;
    i_uv *= control_scale;
    i_pos *= control_scale;

    float c_angle = DECODE_ANGLE(control);
    vec2 c_cs_angle = vec2(cos(c_angle), sin(c_angle));
    i_uv = rotate_vec2(i_uv, c_cs_angle);
    i_pos = rotate_vec2(i_pos, c_cs_angle);

    float world_normal = 1.;
    vec3 T = normalize(base_ddx), B = -normalize(base_ddy);
    #define FAST_WORLD_NORMAL(n) fma(T, vec3(n.x), fma(B, vec3(n.z), i_normal * vec3(n.y)))
    
    float blend = DECODE_BLEND(control);
    float sharpness = fma(56., blend_sharpness, 8.);

    if (blend < 1.0) {
        int id = texture_id[0];
        float id_w = texture_weight[0];
        float id_scale = _texture_uv_scale_array[id];
        vec4 id_dd = i_dd * id_scale;

        vec2 uv_center = floor(fma(i_pos, vec2(id_scale), vec2(0.5)));
        vec2 id_detile = fma(random(uv_center), 2.0, -1.0) * _texture_detile_array[id] * TAU;
        vec2 id_cs_angle = vec2(cos(id_detile.x), sin(id_detile.x));
        vec2 id_uv = rotate_vec2(fma(i_uv, vec2(id_scale), -uv_center), id_cs_angle) + uv_center + id_detile.y - 0.5;
        id_cs_angle = vec2(
            fma(id_cs_angle.x, c_cs_angle.x, -id_cs_angle.y * c_cs_angle.y),
            fma(id_cs_angle.y, c_cs_angle.x, id_cs_angle.x * c_cs_angle.y));
        id_dd.xy = rotate_vec2(id_dd.xy, id_cs_angle);
        id_dd.zw = rotate_vec2(id_dd.zw, id_cs_angle);

        vec4 alb = textureGrad(_texture_array_albedo, vec3(id_uv, float(id)), id_dd.xy, id_dd.zw);
        vec4 nrm = textureGrad(_texture_array_normal, vec3(id_uv, float(id)), id_dd.xy, id_dd.zw);

        alb.rgb *= _texture_color_array[id].rgb;
        nrm.a = clamp(nrm.a + _texture_roughness_mod_array[id], 0., 1.);
        nrm.xyz = fma(nrm.xzy, vec3(2.0), vec3(-1.0));
        nrm.xz = rotate_vec2(nrm.xz, id_cs_angle) * p_align;

        world_normal = FAST_WORLD_NORMAL(nrm).y;

        float id_weight = exp2(sharpness * log2(weight + id_w + alb.a)) * weight;
        mat.albedo_height = fma(alb, vec4(id_weight), mat.albedo_height);
        mat.normal_rough = fma(nrm, vec4(id_weight), mat.normal_rough);
        mat.normal_map_depth = fma(_texture_normal_depth_array[id], id_weight, mat.normal_map_depth);
        mat.ao_strength = fma(_texture_ao_strength_array[id], id_weight, mat.ao_strength);
        mat.total_weight += id_weight;
    }

    if (blend > 0.0 && texture_id[1] != texture_id[0]) {
        int id = texture_id[1];
        float id_w = texture_weight[1];
        float id_scale = _texture_uv_scale_array[id];
        vec4 id_dd = i_dd * id_scale;
        
        float region_mip = log2(max(length(base_ddx.xz), length(base_ddy.xz)) * _vertex_density);

        vec2 uv_center = floor(fma(i_pos, vec2(id_scale), vec2(0.5)));
        vec2 id_detile = _texture_detile_array[id] * fma(random(uv_center), 2.0, -1.0) * TAU;
        vec2 id_cs_angle = vec2(cos(id_detile.x), sin(id_detile.x));
        vec2 id_uv = rotate_vec2(fma(i_uv, vec2(id_scale), -uv_center), id_cs_angle) + uv_center + id_detile.y - 0.5;
        id_cs_angle = vec2(
            fma(id_cs_angle.x, c_cs_angle.x, -id_cs_angle.y * c_cs_angle.y),
            fma(id_cs_angle.y, c_cs_angle.x,  id_cs_angle.x * c_cs_angle.y));
        id_dd.xy = rotate_vec2(id_dd.xy, id_cs_angle);
        id_dd.zw = rotate_vec2(id_dd.zw, id_cs_angle);

        vec4 alb = textureGrad(_texture_array_albedo, vec3(id_uv, float(id)), id_dd.xy, id_dd.zw);
        vec4 nrm = textureGrad(_texture_array_normal, vec3(id_uv, float(id)), id_dd.xy, id_dd.zw);
        alb.rgb *= _texture_color_array[id].rgb;
        nrm.a = clamp(nrm.a + _texture_roughness_mod_array[id], 0., 1.);
        nrm.xyz = fma(nrm.xzy, vec3(2.0), vec3(-1.0));
        nrm.xz = rotate_vec2(nrm.xz, id_cs_angle) * p_align;

        float id_weight = exp2(sharpness * log2(weight + id_w + alb.a * clamp(world_normal, 0., 1.))) * weight;
        mat.albedo_height = fma(alb, vec4(id_weight), mat.albedo_height);
        mat.normal_rough = fma(nrm, vec4(id_weight), mat.normal_rough);
        mat.normal_map_depth = fma(_texture_normal_depth_array[id], id_weight, mat.normal_map_depth);
        mat.ao_strength = fma(_texture_ao_strength_array[id], id_weight, mat.ao_strength);
        mat.total_weight += id_weight;
    }
}

void fragment() {
    vec2 uv = UV;
    vec2 uv2 = UV2;
    vec2 world_pos = v_vertex.xz;
    
    vec3 region_uv = get_index_uv(uv2);
    const vec3 offsets = vec3(0, 1, 2);
    vec2 index_id = floor(uv);
    vec2 weight = fract(uv);
    vec2 invert = 1.0 - weight;
    vec4 weights = vec4(
        invert.x * weight.y, 
        weight.x * weight.y, 
        weight.x * invert.y, 
        invert.x * invert.y  
    );

    ivec3 index[4];
    index[0] = get_index_coord(index_id + offsets.xy, FRAGMENT_PASS);
    index[1] = get_index_coord(index_id + offsets.yy, FRAGMENT_PASS);
    index[2] = get_index_coord(index_id + offsets.yx, FRAGMENT_PASS);
    index[3] = get_index_coord(index_id + offsets.xx, FRAGMENT_PASS);
    
    vec3 base_ddx = dFdxCoarse(v_vertex);
    vec3 base_ddy = dFdyCoarse(v_vertex);
    float region_mip = log2(max(length(base_ddx.xz), length(base_ddy.xz)) * _vertex_density);

    uint control_center = floatBitsToUint(texelFetch(_control_maps, index[3], 0).r);
    int base_id_center = int(control_center >> 27u & 0x1Fu);
    bool is_road = (base_id_center >= 0 && base_id_center <= 7); 

    bool bilerp = region_mip < 0.0 && region_uv.z > -1. && !is_road;
    
    vec3 index_normal[4];
    float h[4];
    float u = 0.0;
    float v = 0.0;
    
    h[3] = texelFetch(_height_maps, index[3], 0).r; 
    h[2] = texelFetch(_height_maps, index[2], 0).r; 
    h[0] = texelFetch(_height_maps, index[0], 0).r; 
    index_normal[3] = normalize(vec3(h[3] - h[2] + u, _vertex_spacing, h[3] - h[0] + v));

    vec3 w_normal = index_normal[3];

    float bias = mix(mipmap_bias,
        depth_blur + 1.,
        smoothstep(0.0, 1.0, (v_vertex_xz_dist - bias_distance) * DIV_1024));
    base_ddx *= bias;
    base_ddy *= bias;

    vec4 color_map = region_uv.z > -1.0 ? textureLod(_color_maps, region_uv, region_mip) : COLOR_MAP_DEF;

    if (bilerp) {
        vec4 col_map[4];
        col_map[3] = index[3].z > -1 ? texelFetch(_color_maps, index[3], 0) : COLOR_MAP_DEF;
        #ifdef FILTER_LINEAR
        col_map[0] = index[0].z > -1 ? texelFetch(_color_maps, index[0], 0) : COLOR_MAP_DEF;
        col_map[1] = index[1].z > -1 ? texelFetch(_color_maps, index[1], 0) : COLOR_MAP_DEF;
        col_map[2] = index[2].z > -1 ? texelFetch(_color_maps, index[2], 0) : COLOR_MAP_DEF;

        color_map =
            col_map[0] * weights[0] +
            col_map[1] * weights[1] +
            col_map[2] * weights[2] +
            col_map[3] * weights[3] ;
        #else
        color_map = col_map[3];
        #endif

        h[1] = texelFetch(_height_maps, index[1], 0).r; 
        float h_4 = texelFetch(_height_maps, get_index_coord(index_id + offsets.yz, FRAGMENT_PASS), 0).r; 
        float h_5 = texelFetch(_height_maps, get_index_coord(index_id + offsets.zy, FRAGMENT_PASS), 0).r; 
        float h_6 = texelFetch(_height_maps, get_index_coord(index_id + offsets.zx, FRAGMENT_PASS), 0).r; 
        float h_7 = texelFetch(_height_maps, get_index_coord(index_id + offsets.xz, FRAGMENT_PASS), 0).r; 

        index_normal[0] = normalize(vec3(h[0] - h[1] + u, _vertex_spacing, h[0] - h_7 + v));
        index_normal[1] = normalize(vec3(h[1] - h_5 + u, _vertex_spacing, h[1] - h_4 + v));
        index_normal[2] = normalize(vec3(h[2] - h_6 + u, _vertex_spacing, h[2] - h[1] + v));

        w_normal =
            index_normal[0] * weights[0] +
            index_normal[1] * weights[1] +
            index_normal[2] * weights[2] +
            index_normal[3] * weights[3] ;
    }

    if (flat_terrain_normals) {
        NORMAL = normalize(cross(dFdyCoarse(VERTEX),dFdxCoarse(VERTEX)));
        TANGENT = normalize(cross(NORMAL, VIEW_MATRIX[2].xyz));
        BINORMAL = normalize(cross(NORMAL, TANGENT));
    } else {
        vec3 w_tangent = normalize(cross(w_normal, vec3(0.0, 0.0, 1.0)));
        vec3 w_binormal = normalize(cross(w_normal, w_tangent));
        NORMAL = mat3(VIEW_MATRIX) * w_normal;
        TANGENT = mat3(VIEW_MATRIX) * w_tangent;
        BINORMAL = mat3(VIEW_MATRIX) * w_binormal;
    }

    uvec4 control = uvec4(floatBitsToUint(texelFetch(_control_maps, index[3], 0).r));
    if (bilerp) {
        control = uvec4(
        floatBitsToUint(texelFetch(_control_maps, index[0], 0).r),
        floatBitsToUint(texelFetch(_control_maps, index[1], 0).r),
        floatBitsToUint(texelFetch(_control_maps, index[2], 0).r),
        control[3]);
    }

    ivec4 t_id[2] = {ivec4(control >> uvec4(27u) & uvec4(0x1Fu)),
        ivec4(control >> uvec4(22u) & uvec4(0x1Fu))};
    ivec2 texture_ids[4] = ivec2[4](
        ivec2(t_id[0].x, t_id[1].x),
        ivec2(t_id[0].y, t_id[1].y),
        ivec2(t_id[0].z, t_id[1].z),
        ivec2(t_id[0].w, t_id[1].w));

    vec4 weights_id_1 = vec4(control >> uvec4(14u) & uvec4(0xFFu)) * DIV_255;
    vec4 weights_id_0 = 1.0 - weights_id_1;
    vec2 t_weights[4] = vec2[4](
                vec2(weights_id_0[0], weights_id_1[0]),
                vec2(weights_id_0[1], weights_id_1[1]),
                vec2(weights_id_0[2], weights_id_1[2]),
                vec2(weights_id_0[3], weights_id_1[3]));
                
    #if CURRENT_RENDERER == RENDERER_FORWARD_PLUS
    if (bilerp) {
        t_weights = {vec2(0), vec2(0), vec2(0), vec2(0)};
        weights_id_0 *= weights;
        weights_id_1 *= weights;
        for (int i = 0; i < 4; i++) {
            vec2 w_0 = vec2(weights_id_0[i]);
            vec2 w_1 = vec2(weights_id_1[i]);
            ivec2 id_0 = texture_ids[i].xx;
            ivec2 id_1 = texture_ids[i].yy;
            t_weights[0] += fma(w_0, vec2(equal(texture_ids[0], id_0)), w_1 * vec2(equal(texture_ids[0], id_1)));
            t_weights[1] += fma(w_0, vec2(equal(texture_ids[1], id_0)), w_1 * vec2(equal(texture_ids[1], id_1)));
            t_weights[2] += fma(w_0, vec2(equal(texture_ids[2], id_0)), w_1 * vec2(equal(texture_ids[2], id_1)));
            t_weights[3] += fma(w_0, vec2(equal(texture_ids[3], id_0)), w_1 * vec2(equal(texture_ids[3], id_1)));
        }
    }
    #endif

    material mat = material(vec4(0.0), vec4(0.0), 0., 0., 0.);

    accumulate_material(base_ddx, base_ddy, weights[3], index[3], control[3], t_weights[3],
        texture_ids[3], index_normal[3], h[3], mat);

    if (bilerp) {
        accumulate_material(base_ddx, base_ddy, weights[2], index[2], control[2], t_weights[2],
            texture_ids[2], index_normal[2], h[2], mat);
        accumulate_material(base_ddx, base_ddy, weights[1], index[1], control[1], t_weights[1],
            texture_ids[1], index_normal[1], h[1], mat);
        accumulate_material(base_ddx, base_ddy, weights[0], index[0], control[0], t_weights[0],
            texture_ids[0], index_normal[0], h[0], mat);
    }

    float weight_inv = 1.0 / mat.total_weight;
    mat.albedo_height *= weight_inv;
    mat.normal_rough *= weight_inv;
    mat.normal_map_depth *= weight_inv;
    mat.ao_strength *= weight_inv;

    vec3 macrov = vec3(1.);
    if (enable_macro_variation) {
        float noise1 = texture(noise_texture, rotate_vec2(fma(uv, vec2(noise1_scale * .1), noise1_offset) , vec2(cos(noise1_angle), sin(noise1_angle)))).r;
        float noise2 = texture(noise_texture, uv * noise2_scale * .1).r;
        macrov = mix(macro_variation1, vec3(1.), noise1);
        macrov *= mix(macro_variation2, vec3(1.), noise2);
        macrov = mix(vec3(1.0), macrov, clamp(w_normal.y + macro_variation_slope, 0., 1.));
    }
    
    float roughness = clamp(fma(color_map.a - 0.5, 2.0, mat.normal_rough.a), 0., 1.);
    
    ALBEDO = mat.albedo_height.rgb * color_map.rgb * macrov;
    ROUGHNESS = roughness;
    SPECULAR = 1. - mat.normal_rough.a;
    NORMAL_MAP = fma(normalize(mat.normal_rough.xzy), vec3(0.5), vec3(0.5));
    NORMAL_MAP_DEPTH = mat.normal_map_depth;

    float ao = (1. - (mat.albedo_height.a * log(2.1 - mat.ao_strength))) * (1. - mat.normal_rough.y);
    AO = clamp(1. - ao * mat.ao_strength, mat.albedo_height.a, 1.0);
    AO_LIGHT_AFFECT = (1.0 - mat.albedo_height.a) * clamp(mat.normal_rough.y, 0., 1.);

    // =========================================================================
    // MIGRATED BEAUTIFUL WEATHER LOGIC
    // =========================================================================
    
    // 1. Manual Paint Mask: Use Terrain3D Color Map Alpha channel. 
    // Default is 0.5. We multiply by 2.0 so 0.5 = 1.0 (Full snow). 0.0 = No snow.
    float snow_paint_mask = clamp(color_map.a * 2.0, 0.0, 1.0);
    
    float noise_val = texture(noise_texture, v_vertex.xz * 0.05).r;

    // --- RAIN & PUDDLES ---
    if (rain_amount > 0.01) {
        float water_level = rain_amount * 0.7;
        float puddle_mask = 1.0 - smoothstep(water_level - 0.05, water_level, noise_val);
        
        vec3 damp_albedo = ALBEDO * mix(1.0, 0.5, rain_amount);
        vec3 wet_ground_dark = damp_albedo * 0.6;
        vec3 puddle_tint = vec3(0.01, 0.02, 0.04); 
        vec3 puddle_albedo = mix(wet_ground_dark, puddle_tint, 0.2);
        
        float water_rough = mix(0.02, 0.1, noise_val);
        
        float time_scale = TIME * 2.0; 
        vec2 ripple_uv = v_vertex.xz * 0.2 + vec2(0.0, time_scale * 0.05);
        float ripple = texture(noise_texture, ripple_uv).r;
        
        vec3 pixel_tilt = vec3(noise_val - 0.5, fract(noise_val * 10.0) - 0.5, 0.0);
        vec3 water_normal_tangent = normalize(vec3(0.5, 0.5, 1.0) + (pixel_tilt * 0.15) + vec3(ripple * 0.1));
        
        ALBEDO = mix(damp_albedo, puddle_albedo, puddle_mask);
        ROUGHNESS = mix(ROUGHNESS, water_rough, puddle_mask);
        NORMAL_MAP = mix(NORMAL_MAP, water_normal_tangent, puddle_mask);
        SPECULAR = mix(SPECULAR, 0.8, puddle_mask);
    }

    // --- SNOW ---
    if (snow_amount > 0.01) {
        float snow_timeline = smoothstep(0.0, 0.6, snow_amount);
        float chunk_noise = floor(noise_val * 8.0) / 8.0;
        float start_fade = 1.0 - chunk_noise;
        float chunk_alpha = smoothstep(start_fade - 0.1, start_fade + 0.1, snow_timeline * 1.1);
        
        vec3 snow_bias_vec = normalize(vec3(0.0, 1.0, 0.0) + (wind_direction * 0.5));
        float surface_exposure = dot(w_normal, snow_bias_vec);
        float slope_mask = step(0.3, surface_exposure); 
        
        float final_snow_mask = chunk_alpha * slope_mask * snow_paint_mask * 0.95;
        
        // Force snow to be fully solid on vertex 3D piles
        if (v_pile_height > 0.01) {
            final_snow_mask = 1.0;
        }
        
        vec3 snow_color = vec3(0.7, 0.9, 0.98); // Beautiful blueish white
        ALBEDO = mix(ALBEDO, snow_color, final_snow_mask);
        ROUGHNESS = mix(ROUGHNESS, 1.0, final_snow_mask);
        NORMAL_MAP = mix(NORMAL_MAP, vec3(0.5, 0.5, 1.0), final_snow_mask);
        SPECULAR = mix(SPECULAR, 0.1, final_snow_mask);
    }
}