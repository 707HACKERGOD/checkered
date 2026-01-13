// js/engine/PS1Effect.js
import * as THREE from 'three';

export const PS1Shader = {
    uniforms: {
        'tDiffuse': { value: null },
        'resolution': { value: new THREE.Vector2(320, 240) },
        'time': { value: 0.0 },
        'sanity': { value: 1.0 },
        'distortion': { value: 0.0 }
    },
    
    vertexShader: `
        varying vec2 vUv;
        
        void main() {
            vUv = uv;
            gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
    `,
    
    fragmentShader: `
        uniform sampler2D tDiffuse;
        uniform vec2 resolution;
        uniform float time;
        uniform float sanity;
        uniform float distortion;
        
        varying vec2 vUv;
        
        // Color quantization for PS1 look
        vec3 quantizeColor(vec3 color, float levels) {
            return floor(color * levels) / levels;
        }
        
        // Dithering pattern
        float dither(vec2 position, float brightness) {
            int x = int(mod(position.x, 4.0));
            int y = int(mod(position.y, 4.0));
            int index = x + y * 4;
            float limit = 0.0;
            
            // Bayer dithering matrix
            if (index == 0) limit = 0.0625;
            if (index == 1) limit = 0.5625;
            if (index == 2) limit = 0.1875;
            if (index == 3) limit = 0.6875;
            if (index == 4) limit = 0.8125;
            if (index == 5) limit = 0.3125;
            if (index == 6) limit = 0.9375;
            if (index == 7) limit = 0.4375;
            if (index == 8) limit = 0.25;
            if (index == 9) limit = 0.75;
            if (index == 10) limit = 0.125;
            if (index == 11) limit = 0.625;
            if (index == 12) limit = 1.0;
            if (index == 13) limit = 0.5;
            if (index == 14) limit = 0.875;
            if (index == 15) limit = 0.375;
            
            return brightness < limit ? 0.0 : 1.0;
        }
        
        // Scanline effect
        float scanline(vec2 uv) {
            return sin(uv.y * resolution.y * 3.14159) * 0.04 + 0.96;
        }
        
        // Chromatic aberration for low sanity
        vec3 chromaticAberration(sampler2D tex, vec2 uv, float amount) {
            vec3 color;
            color.r = texture2D(tex, uv + vec2(amount, 0.0)).r;
            color.g = texture2D(tex, uv).g;
            color.b = texture2D(tex, uv - vec2(amount, 0.0)).b;
            return color;
        }
        
        // Screen distortion
        vec2 distort(vec2 uv, float amount) {
            vec2 center = uv - 0.5;
            float dist = length(center);
            float factor = 1.0 + amount * dist * dist;
            return center * factor + 0.5;
        }
        
        // Noise function
        float noise(vec2 p) {
            return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
        }
        
        void main() {
            // Apply screen distortion based on sanity
            vec2 uv = vUv;
            
            // Barrel distortion for insanity effect
            if (distortion > 0.0) {
                uv = distort(uv, distortion * 0.3);
            }
            
            // Screen shake when sanity is low
            if (sanity < 0.5) {
                float shake = (1.0 - sanity) * 0.01;
                uv.x += sin(time * 50.0) * shake;
                uv.y += cos(time * 45.0) * shake;
            }
            
            // Pixelate to PS1 resolution
            vec2 pixelUV = floor(uv * resolution) / resolution;
            
            // Get base color with chromatic aberration for low sanity
            vec3 color;
            if (sanity < 0.7) {
                float aberration = (0.7 - sanity) * 0.01;
                color = chromaticAberration(tDiffuse, pixelUV, aberration);
            } else {
                color = texture2D(tDiffuse, pixelUV).rgb;
            }
            
            // Quantize colors (reduce color depth)
            float colorLevels = 32.0 - (1.0 - sanity) * 16.0; // Fewer colors at low sanity
            color = quantizeColor(color, colorLevels);
            
            // Apply dithering
            vec2 pixelPos = uv * resolution;
            float brightness = dot(color, vec3(0.299, 0.587, 0.114));
            
            // Apply scanlines
            color *= scanline(uv);
            
            // Add noise at low sanity
            if (sanity < 0.5) {
                float n = noise(pixelPos + time) * (0.5 - sanity) * 0.3;
                color += vec3(n);
            }
            
            // Vignette effect (stronger at low sanity)
            vec2 vigUV = uv - 0.5;
            float vig = 1.0 - dot(vigUV, vigUV) * (1.5 - sanity * 0.5);
            color *= vig;
            
            // Color tint based on sanity
            if (sanity < 0.3) {
                // Red tint at very low sanity
                color.r += (0.3 - sanity) * 0.3;
            }
            
            // Occasional glitch effect at low sanity
            if (sanity < 0.4 && noise(vec2(time * 10.0, 0.0)) > 0.95) {
                color = vec3(1.0) - color; // Invert colors briefly
            }
            
            gl_FragColor = vec4(color, 1.0);
        }
    `
};