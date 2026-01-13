import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { PS1Shader } from './PS1Effect.js';

export class Renderer {
    constructor() {
        this.canvas = document.getElementById('game-canvas');
        this.scene = null;
        this.camera = null;
        this.renderer = null;
        this.composer = null;
        this.ps1Pass = null;
        
        // PS1 target resolution (320x240 scaled up)
        this.ps1Width = 320;
        this.ps1Height = 240;
        this.scaleFactor = 2;
    }
    
    async init() {
        console.log('Initializing PS1-style renderer...');
        
        // Create scene
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x1a0a1a);
        this.scene.fog = new THREE.Fog(0x1a0a1a, 10, 100);
        
        // Create camera
        this.camera = new THREE.PerspectiveCamera(
            75,
            window.innerWidth / window.innerHeight,
            0.1,
            1000
        );
        this.camera.position.set(0, 2, 5);
        
        // Create renderer with PS1-style settings
        this.renderer = new THREE.WebGLRenderer({
            canvas: this.canvas,
            antialias: false, // No antialiasing for PS1 look
            powerPreference: 'high-performance'
        });
        
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(1); // Force pixel ratio of 1 for blocky look
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.BasicShadowMap; // Hard shadows
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        
        // Setup post-processing
        this.setupPostProcessing();
        
        // Add ambient light
        const ambientLight = new THREE.AmbientLight(0x404040, 0.5);
        this.scene.add(ambientLight);
        
        // Add main directional light (sun/moon)
        this.sunLight = new THREE.DirectionalLight(0xffeedd, 1);
        this.sunLight.position.set(50, 100, 50);
        this.sunLight.castShadow = true;
        this.sunLight.shadow.mapSize.width = 512; // REDUCED from 1024
        this.sunLight.shadow.mapSize.height = 512; // REDUCED from 1024
        this.sunLight.shadow.camera.near = 0.5;
        this.sunLight.shadow.camera.far = 500;
        this.sunLight.shadow.camera.left = -100;
        this.sunLight.shadow.camera.right = 100;
        this.sunLight.shadow.camera.top = 100;
        this.sunLight.shadow.camera.bottom = -100;
        this.scene.add(this.sunLight);
        
        console.log('PS1 renderer initialized');
    }
    
    setupPostProcessing() {
        console.log('Setting up PS1 post-processing...');
        
        this.composer = new EffectComposer(this.renderer);
        
        // Render pass
        const renderPass = new RenderPass(this.scene, this.camera);
        this.composer.addPass(renderPass);
        
        // PS1 effect pass
        this.ps1Pass = new ShaderPass(PS1Shader);
        this.ps1Pass.uniforms['resolution'].value = new THREE.Vector2(
            this.ps1Width,
            this.ps1Height
        );
        this.composer.addPass(this.ps1Pass);
    }
    
    render(sanity = 100) {
        // Update PS1 shader uniforms based on sanity
        if (this.ps1Pass) {
            this.ps1Pass.uniforms['time'].value = performance.now() / 1000;
            this.ps1Pass.uniforms['sanity'].value = sanity / 100;
            this.ps1Pass.uniforms['distortion'].value = (100 - sanity) / 200;
        }
        
        this.composer.render();
    }
    
    onWindowResize() {
        this.camera.aspect = window.innerWidth / window.innerHeight;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.composer.setSize(window.innerWidth, window.innerHeight);
    }
    
    // Update lighting based on time of day
    updateLighting(normalizedTime) {
        // normalizedTime: 0 = midnight, 0.5 = noon, 1 = midnight
        const sunAngle = normalizedTime * Math.PI * 2 - Math.PI / 2;
        
        this.sunLight.position.x = Math.cos(sunAngle) * 100;
        this.sunLight.position.y = Math.sin(sunAngle) * 100 + 20;
        
        // Adjust light intensity and color based on time
        if (normalizedTime > 0.25 && normalizedTime < 0.75) {
            // Day time
            const dayIntensity = Math.sin((normalizedTime - 0.25) * Math.PI / 0.5);
            this.sunLight.intensity = 0.5 + dayIntensity * 0.5;
            this.sunLight.color.setHex(0xffeedd);
            this.scene.background.setHex(0x87ceeb); // Sky blue
            this.scene.fog.color.setHex(0x87ceeb); // Light blue fog
            this.scene.fog.near = 50;
            this.scene.fog.far = 150;
        } else {
            // Night time - DARK BLUE like original
            this.sunLight.intensity = 0.1; // Much darker
            this.sunLight.color.setHex(0x224466); // Dark blue light
            
            // Dark blue sky with slight purple tint
            this.scene.background.setHex(0x1a1a3a);
            this.scene.fog.color.setHex(0x1a1a3a); // Dark blue fog
            this.scene.fog.near = 20;
            this.scene.fog.far = 80;
        }
    }
}