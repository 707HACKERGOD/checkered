// js/world/World.js - UPDATED VERSION
import * as THREE from 'three';
import { Environment } from './Environment.js';

export class World {
    constructor(scene) {
        this.scene = scene;
        this.environment = new Environment(scene);
        this.objects = [];
        this.locations = new Map();
        this.interactables = [];
        this.pickupItems = [];
        this.atticUnlocked = false;
        
        // Building positions (clearly defined)
        this.buildingPositions = {
            bar: { x: 0, z: -30, name: 'The Crossroads Bar' },
            school: { x: -50, z: -60, name: 'Millbrook Academy' },
            shop: { x: 25, z: 10, name: "Ada's General Store" },
            house1: { x: 40, z: -20, name: 'Residential House' },
            house2: { x: 50, z: 0, name: 'Residential House' },
            house3: { x: -30, z: 20, name: 'Residential House' },
            park: { x: 0, z: 30, name: 'City Park' }
        };
        
        // Lamp post positions (to avoid for NPC spawns)
        this.lampPositions = [];
    }
    
    async init() {
        console.log('Initializing world...');

        this.addGlobalLights();
        
        // Create ground
        this.createGround();
        
        // Create roads
        this.createRoads();
        
        // Create buildings
        this.createBuildings();
        
        // Create park
        this.createPark();
        
        // Create street lamps (track positions)
        this.createStreetLamps();
        
        // Create forest border
        this.createForest();
        
        // Spawn pickup items
        this.spawnPickupItems();
        
        // Define location zones
        this.defineLocations();
        
        console.log('World initialized!');
    }
    
    createGround() {
        // Large ground plane
        const groundSize = 300;
        const groundGeo = new THREE.PlaneGeometry(groundSize, groundSize);
        
        // Create checkered texture
        const canvas = document.createElement('canvas');
        canvas.width = 512;
        canvas.height = 512;
        const ctx = canvas.getContext('2d');
        
        const tileSize = 64;
        for (let y = 0; y < 8; y++) {
            for (let x = 0; x < 8; x++) {
                ctx.fillStyle = (x + y) % 2 === 0 ? '#3a3a3a' : '#2d2d2d';
                ctx.fillRect(x * tileSize, y * tileSize, tileSize, tileSize);
            }
        }
        
        const texture = new THREE.CanvasTexture(canvas);
        texture.wrapS = THREE.RepeatWrapping;
        texture.wrapT = THREE.RepeatWrapping;
        texture.repeat.set(30, 30);
        texture.magFilter = THREE.NearestFilter;
        
        const groundMat = new THREE.MeshBasicMaterial({
            map: texture,
            side: THREE.DoubleSide
        });
        
        const ground = new THREE.Mesh(groundGeo, groundMat);
        ground.rotation.x = -Math.PI / 2;
        ground.receiveShadow = true;
        this.scene.add(ground);
    }
    
    createRoads() {
        const roadMat = new THREE.MeshLambertMaterial({ color: 0x222222 });
        
        // Main north-south road
        const road1Geo = new THREE.PlaneGeometry(8, 150);
        const road1 = new THREE.Mesh(road1Geo, roadMat);
        road1.rotation.x = -Math.PI / 2;
        road1.position.y = 0.01;
        this.scene.add(road1);
        
        // East-west road
        const road2Geo = new THREE.PlaneGeometry(150, 8);
        const road2 = new THREE.Mesh(road2Geo, roadMat);
        road2.rotation.x = -Math.PI / 2;
        road2.position.y = 0.01;
        this.scene.add(road2);
        
        // Road to school
        const road3Geo = new THREE.PlaneGeometry(8, 40);
        const road3 = new THREE.Mesh(road3Geo, roadMat);
        road3.rotation.x = -Math.PI / 2;
        road3.position.set(-50, 0.01, -40);
        this.scene.add(road3);
    }
    
    createBuildings() {
        // BAR - The Crossroads (main story location)
        const bar = this.environment.createBar(
            this.buildingPositions.bar.x,
            this.buildingPositions.bar.z
        );
        this.scene.add(bar);
        this.objects.push(bar);
        
        // Create door interactable for bar
        this.addDoorInteractable(
            this.buildingPositions.bar.x,
            this.buildingPositions.bar.z + 6,
            'bar',
            'Enter The Crossroads Bar'
        );
        
        // SCHOOL - Millbrook Academy
        const school = this.environment.createSchool(
            this.buildingPositions.school.x,
            this.buildingPositions.school.z
        );
        this.scene.add(school);
        this.objects.push(school);
        
        this.addDoorInteractable(
            this.buildingPositions.school.x,
            this.buildingPositions.school.z + 10,
            'school',
            'Enter Millbrook Academy'
        );
        
        // SHOP - Ada's General Store
        const shop = this.environment.createShop(
            this.buildingPositions.shop.x,
            this.buildingPositions.shop.z
        );
        this.scene.add(shop);
        this.objects.push(shop);
        
        this.addDoorInteractable(
            this.buildingPositions.shop.x - 2.5,
            this.buildingPositions.shop.z + 5,
            'shop',
            "Enter Ada's General Store"
        );
        
        // HOUSES
        const houseConfigs = [
            { ...this.buildingPositions.house1, width: 8, height: 6, depth: 8, color: 0x6a5a4a },
            { ...this.buildingPositions.house2, width: 7, height: 5, depth: 7, color: 0x5a6a5a },
            { ...this.buildingPositions.house3, width: 9, height: 6, depth: 8, color: 0x5a5a6a }
        ];
        
        houseConfigs.forEach((config, i) => {
            const house = this.environment.createHouse(config);
            this.scene.add(house);
            this.objects.push(house);
        });
    }
    
    addDoorInteractable(x, z, locationId, promptText) {
        const interactable = {
            position: new THREE.Vector3(x, 0, z),
            type: 'door',
            locationId: locationId,
            promptText: promptText,
            radius: 2
        };
        this.interactables.push(interactable);
    }
    
    createPark() {
        const parkPos = this.buildingPositions.park;
        
        // Grass area
        const grassGeo = new THREE.CircleGeometry(20, 16);
        const grassMat = new THREE.MeshLambertMaterial({ color: 0x2a4a2a });
        const grass = new THREE.Mesh(grassGeo, grassMat);
        grass.rotation.x = -Math.PI / 2;
        grass.position.set(parkPos.x, 0.02, parkPos.z);
        this.scene.add(grass);
        
        // Trees in park
        const treePositions = [
            { x: parkPos.x - 12, z: parkPos.z - 8 },
            { x: parkPos.x + 10, z: parkPos.z - 5 },
            { x: parkPos.x - 8, z: parkPos.z + 10 },
            { x: parkPos.x + 12, z: parkPos.z + 8 },
            { x: parkPos.x, z: parkPos.z + 15 },
            { x: parkPos.x - 15, z: parkPos.z + 3 },
        ];
        
        treePositions.forEach(pos => {
            const tree = this.environment.createTree(pos.x, pos.z, 0.8 + Math.random() * 0.4);
            this.scene.add(tree);
        });
        
        // Benches
        const bench1 = this.environment.createBench(parkPos.x - 5, parkPos.z, 0);
        const bench2 = this.environment.createBench(parkPos.x + 5, parkPos.z, Math.PI);
        this.scene.add(bench1);
        this.scene.add(bench2);
        
        // Gazebo
        this.createGazebo(parkPos.x, parkPos.z);
    }
    
    createGazebo(x, z) {
        const group = new THREE.Group();
        
        // Floor
        const floorGeo = new THREE.CylinderGeometry(4, 4, 0.3, 8);
        const floorMat = new THREE.MeshLambertMaterial({ color: 0x5a4a3a });
        const floor = new THREE.Mesh(floorGeo, floorMat);
        floor.position.y = 0.15;
        group.add(floor);
        
        // Pillars
        for (let i = 0; i < 8; i++) {
            const angle = (i / 8) * Math.PI * 2;
            const pillarGeo = new THREE.CylinderGeometry(0.2, 0.2, 3, 6);
            const pillarMat = new THREE.MeshLambertMaterial({ color: 0xffffff });
            const pillar = new THREE.Mesh(pillarGeo, pillarMat);
            pillar.position.set(Math.cos(angle) * 3.5, 1.5, Math.sin(angle) * 3.5);
            group.add(pillar);
        }
        
        // Roof
        const roofGeo = new THREE.ConeGeometry(5, 2, 8);
        const roofMat = new THREE.MeshLambertMaterial({ color: 0x4a3a2a });
        const roof = new THREE.Mesh(roofGeo, roofMat);
        roof.position.y = 4;
        group.add(roof);
        
        group.position.set(x, 0, z);
        this.scene.add(group);
    }
    
    createStreetLamps() {
        // Define lamp positions carefully to avoid NPC spawns
        const lampPositions = [
            // Along main road
            { x: 5, z: -40 },
            { x: 5, z: -20 },
            { x: 5, z: 0 },
            { x: 5, z: 20 },
            { x: -5, z: -40 },
            { x: -5, z: -20 },
            { x: -5, z: 0 },
            { x: -5, z: 20 },
            // Along east-west road
            { x: 20, z: 5 },
            { x: 40, z: 5 },
            { x: -20, z: 5 },
            { x: -40, z: 5 },
            // Near bar
            { x: 8, z: -25 },
            { x: -8, z: -25 },
            // Near park
            { x: 15, z: 30 },
            { x: -15, z: 30 },
        ];
        
        lampPositions.forEach(pos => {
            const lamp = this.environment.createLampPost(pos.x, pos.z);
            this.scene.add(lamp);
            this.lampPositions.push(new THREE.Vector3(pos.x, 0, pos.z));
        });
    }

    // In World.js - add this method and call it in init()
    addGlobalLights() {
        console.log('Adding global lights...');
        
        // 1. Ambient light (makes everything visible)
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        this.scene.add(ambientLight);
        console.log('Added ambient light');
        
        // 2. Main sun/moon light (directional)
        const mainLight = new THREE.DirectionalLight(0xffffff, 0.8);
        mainLight.position.set(100, 100, 50);
        mainLight.castShadow = true;
        
        // Shadow settings (optional but helps)
        mainLight.shadow.mapSize.width = 1024;
        mainLight.shadow.mapSize.height = 1024;
        mainLight.shadow.camera.near = 0.5;
        mainLight.shadow.camera.far = 500;
        mainLight.shadow.camera.left = -100;
        mainLight.shadow.camera.right = 100;
        mainLight.shadow.camera.top = 100;
        mainLight.shadow.camera.bottom = -100;
        
        this.scene.add(mainLight);
        console.log('Added directional light');
        
        // 3. Save for later adjustment
        this.sunLight = mainLight;
        this.ambientLight = ambientLight;
    }    
    
    createForest() {
        // Dense forest around map edges
        const forestRadius = 100;
        const treeCount = 150;
        
        for (let i = 0; i < treeCount; i++) {
            const angle = (i / treeCount) * Math.PI * 2;
            const radiusVariation = forestRadius + Math.random() * 30;
            const x = Math.cos(angle) * radiusVariation;
            const z = Math.sin(angle) * radiusVariation;
            
            const scale = 0.7 + Math.random() * 0.8;
            const tree = this.environment.createTree(x, z, scale);
            this.scene.add(tree);
        }
    }
    
    spawnPickupItems() {
        // Spawn some items around the world for testing
        const itemSpawns = [
            { x: 10, z: 5, item: { id: 'glass_bottle', name: 'Glass Bottle', attributes: ['container', 'glass'] } },
            { x: -5, z: 15, item: { id: 'cloth_rag', name: 'Cloth Rag', attributes: ['cloth', 'flammable'] } },
            { x: 30, z: -10, item: { id: 'wooden_stick', name: 'Wooden Stick', attributes: ['wood', 'blunt', 'handle'] } },
            { x: -15, z: -25, item: { id: 'rope', name: 'Rope', attributes: ['rope', 'cloth'] } },
            { x: 20, z: 25, item: { id: 'matches', name: 'Matches', attributes: ['fire_source', 'flammable'] } },
            { x: -25, z: 5, item: { id: 'metal_can', name: 'Metal Can', attributes: ['container', 'metal'] } },
            { x: 35, z: 15, item: { id: 'bandage', name: 'Bandage', attributes: ['cloth', 'healing'], healAmount: 15 } },
            { x: 0, z: 40, item: { id: 'knife', name: 'Kitchen Knife', attributes: ['sharp', 'handle', 'metal'], damage: 15 } },
            // Near bar
            { x: 5, z: -35, item: { id: 'newspaper', name: 'Newspaper', attributes: ['flammable'] } },
            // Near school
            { x: -45, z: -50, item: { id: 'wire', name: 'Copper Wire', attributes: ['rope', 'metal', 'conductive'] } },
        ];
        
        itemSpawns.forEach(spawn => {
            const pickup = this.createPickupMesh(spawn.x, spawn.z, spawn.item);
            this.scene.add(pickup);
            this.pickupItems.push({
                mesh: pickup,
                item: spawn.item,
                position: new THREE.Vector3(spawn.x, 0, spawn.z)
            });
        });
    }
    
    createPickupMesh(x, z, itemData) {
        const group = new THREE.Group();
        
        // Glowing cube
        const geo = new THREE.BoxGeometry(0.4, 0.4, 0.4);
        const mat = new THREE.MeshBasicMaterial({
            color: this.getItemColor(itemData),
            transparent: true,
            opacity: 0.8
        });
        const cube = new THREE.Mesh(geo, mat);
        cube.position.y = 0.5;
        group.add(cube);
        
        // Outer glow
        const glowGeo = new THREE.BoxGeometry(0.6, 0.6, 0.6);
        const glowMat = new THREE.MeshBasicMaterial({
            color: this.getItemColor(itemData),
            transparent: true,
            opacity: 0.3,
            side: THREE.BackSide
        });
        const glow = new THREE.Mesh(glowGeo, glowMat);
        glow.position.y = 0.5;
        group.add(glow);
        
        group.position.set(x, 0, z);
        group.userData.itemData = itemData;
        group.userData.floatOffset = Math.random() * Math.PI * 2;
        
        return group;
    }
    
    getItemColor(item) {
        if (item.attributes?.includes('healing')) return 0x44ff44;
        if (item.attributes?.includes('weapon') || item.attributes?.includes('sharp')) return 0xff4444;
        if (item.attributes?.includes('flammable')) return 0xff8800;
        if (item.attributes?.includes('container')) return 0x4488ff;
        if (item.attributes?.includes('rope')) return 0xaa8844;
        return 0xaaaaaa;
    }
    
    defineLocations() {
        // Define zones for location detection
        Object.entries(this.buildingPositions).forEach(([id, pos]) => {
            this.locations.set(id, {
                position: new THREE.Vector3(pos.x, 0, pos.z),
                radius: 15,
                name: pos.name
            });
        });
        
        // Streets is default / fallback
        this.locations.set('streets', {
            position: new THREE.Vector3(0, 0, 0),
            radius: 1000, // Covers everything
            name: 'City Streets'
        });
    }
    
    getCurrentLocation(position) {
        // Check specific locations first (smaller radius wins)
        let currentLocation = 'streets';
        let smallestRadius = Infinity;
        
        for (const [id, loc] of this.locations) {
            if (id === 'streets') continue;
            
            const distance = position.distanceTo(loc.position);
            if (distance < loc.radius && loc.radius < smallestRadius) {
                currentLocation = id;
                smallestRadius = loc.radius;
            }
        }
        
        // Check if in forest (far from center)
        if (position.length() > 80) {
            return 'forest';
        }
        
        return currentLocation;
    }
    
    getNearbyInteractables(position, radius = 3) {
        const nearby = [];
        
        // Check door interactables
        this.interactables.forEach(interactable => {
            const dist = position.distanceTo(interactable.position);
            if (dist <= (interactable.radius || radius)) {
                nearby.push({ ...interactable, distance: dist });
            }
        });
        
        // Check pickup items
        this.pickupItems.forEach(pickup => {
            const dist = position.distanceTo(pickup.position);
            if (dist <= radius) {
                nearby.push({
                    type: 'pickup',
                    item: pickup.item,
                    mesh: pickup.mesh,
                    position: pickup.position,
                    distance: dist,
                    promptText: `Pick up ${pickup.item.name}`
                });
            }
        });
        
        // Sort by distance
        nearby.sort((a, b) => a.distance - b.distance);
        
        return nearby;
    }
    
    removePickupItem(pickup) {
        const index = this.pickupItems.findIndex(p => p.mesh === pickup.mesh);
        if (index !== -1) {
            this.scene.remove(pickup.mesh);
            this.pickupItems.splice(index, 1);
        }
    }
    
    // Get safe spawn position for NPCs (away from lamp posts)
    getSafeSpawnPosition(baseX, baseZ, radius = 5) {
        const maxAttempts = 20;
        
        for (let i = 0; i < maxAttempts; i++) {
            const x = baseX + (Math.random() - 0.5) * radius * 2;
            const z = baseZ + (Math.random() - 0.5) * radius * 2;
            const testPos = new THREE.Vector3(x, 0, z);
            
            // Check against lamp posts
            let isSafe = true;
            for (const lampPos of this.lampPositions) {
                if (testPos.distanceTo(lampPos) < 2) {
                    isSafe = false;
                    break;
                }
            }
            
            if (isSafe) {
                return testPos;
            }
        }
        
        // Fallback to base position
        return new THREE.Vector3(baseX, 0, baseZ);
    }
    
    update(deltaTime, normalizedTime) {
        // Animate pickup items (floating effect)
        const time = performance.now() / 1000;
        
        this.pickupItems.forEach(pickup => {
            if (pickup.mesh) {
                const offset = pickup.mesh.userData.floatOffset || 0;
                pickup.mesh.position.y = 0.5 + Math.sin(time * 2 + offset) * 0.15;
                pickup.mesh.rotation.y = time * 0.5;
            }
        });
    }

updateLighting(normalizedTime) {
    if (!this.sunLight || !this.ambientLight) return;
    
    // Smooth time-based factors for transitions
    const dawnFactor = Math.max(0, 1 - Math.abs(normalizedTime - 0.25) * 20);
    const duskFactor = Math.max(0, 1 - Math.abs(normalizedTime - 0.75) * 20);
    const dayFactor = Math.sin(normalizedTime * Math.PI * 2) * 0.5 + 0.5;
    const nightFactor = 1 - dayFactor;
    
    // === SUN/MOON POSITION ===
    // Continuous elliptical orbit
    const sunAngle = normalizedTime * Math.PI * 2;
    const sunX = Math.cos(sunAngle) * 100;
    const sunY = Math.max(10, Math.sin(sunAngle) * 40 + 50);
    const sunZ = Math.sin(sunAngle) * 50;
    this.sunLight.position.set(sunX, sunY, sunZ);
    
    // === LIGHT INTENSITIES ===
    // Smooth intensity curve
    const sunIntensity = Math.max(0.1, 
        0.3 + 0.9 * Math.pow(Math.sin(normalizedTime * Math.PI), 4)
    );
    this.sunLight.intensity = sunIntensity;
    
    // Ambient light follows sun but with softer curve
    this.ambientLight.intensity = 0.2 + 0.3 * dayFactor;
    
    // === COLOR TRANSITIONS ===
    // Time-based color interpolation
    const interpolateColors = (color1, color2, factor) => {
        const r = Math.floor(color1.r + (color2.r - color1.r) * factor);
        const g = Math.floor(color1.g + (color2.g - color1.g) * factor);
        const b = Math.floor(color1.b + (color2.b - color1.b) * factor);
        return (r << 16) | (g << 8) | b;
    };
    
    // Color definitions
    const dawnSunColor = {r: 255, g: 140, b: 50};     // Warm orange
    const daySunColor = {r: 255, g: 250, b: 240};    // Warm white
    const duskSunColor = {r: 255, g: 100, b: 50};    // Deep orange
    const nightSunColor = {r: 170, g: 190, b: 255};  // Cool blue (moonlight)
    
    const dawnAmbientColor = {r: 255, g: 200, b: 150}; // Soft warm glow
    const dayAmbientColor = {r: 255, g: 255, b: 255};  // Neutral white
    const nightAmbientColor = {r: 120, g: 140, b: 200}; // Cool blue ambient
    
    const dawnSkyColor = {r: 135, g: 206, b: 235};    // Morning blue
    const daySkyColor = {r: 135, g: 206, b: 250};     // Bright sky blue
    const duskSkyColor = {r: 255, g: 153, b: 102};    // Sunset orange
    const nightSkyColor = {r: 10, g: 15, b: 40};      // Deep navy blue
    const midnightSkyColor = {r: 5, g: 8, b: 25};     // Very dark blue
    
    // Determine current phase
    let sunColor, ambientColor, skyColor;
    
    if (normalizedTime > 0.2 && normalizedTime < 0.3) {
        // Dawn phase
        const t = (normalizedTime - 0.2) * 10;
        sunColor = interpolateColors(dawnSunColor, daySunColor, t);
        ambientColor = interpolateColors(dawnAmbientColor, dayAmbientColor, t);
        skyColor = interpolateColors(dawnSkyColor, daySkyColor, t);
    } else if (normalizedTime >= 0.3 && normalizedTime < 0.7) {
        // Day phase
        const t = (normalizedTime - 0.3) * 2.5;
        sunColor = interpolateColors(daySunColor, daySunColor, t);
        ambientColor = interpolateColors(dayAmbientColor, dayAmbientColor, t);
        skyColor = interpolateColors(daySkyColor, daySkyColor, t);
    } else if (normalizedTime >= 0.7 && normalizedTime < 0.8) {
        // Dusk phase
        const t = (normalizedTime - 0.7) * 10;
        sunColor = interpolateColors(duskSunColor, nightSunColor, t);
        ambientColor = interpolateColors(dawnAmbientColor, nightAmbientColor, t);
        skyColor = interpolateColors(duskSkyColor, nightSkyColor, t);
    } else {
        // Night phase with midnight peak
        const midnightFactor = Math.sin(normalizedTime * Math.PI * 4);
        sunColor = interpolateColors(nightSunColor, nightSunColor, 0);
        ambientColor = interpolateColors(nightAmbientColor, nightAmbientColor, 0);
        skyColor = interpolateColors(nightSkyColor, midnightSkyColor, Math.abs(midnightFactor));
    }
    
    // Apply colors
    this.sunLight.color.setHex(sunColor);
    this.ambientLight.color.setHex(ambientColor);
    
    // === SKY & FOG ===
    // Add subtle gradient effect by mixing with sun position
    const skyBrightness = 0.7 + 0.3 * Math.max(0, sunY / 100);
    const skyHex = new THREE.Color(skyColor);
    skyHex.multiplyScalar(skyBrightness);
    
    this.scene.background.copy(skyHex);
    this.scene.fog.color.copy(skyHex);
    
    // Dynamic fog based on time
    const fogDensity = 0.8 - 0.4 * dayFactor; // Thicker fog at night
    this.scene.fog.near = 30 + 30 * dayFactor;
    this.scene.fog.far = 120 + 60 * dayFactor;
    
    // Add subtle rim lighting for visibility
    if (nightFactor > 0.7) {
        const rimLightIntensity = 0.1 * nightFactor;
        // You could add a rim light here if you have one
    }
    
    // === ADD STARS AT NIGHT ===
    if (nightFactor > 0.8 && !this.starsAdded) {
        this.addStars();
        this.starsAdded = true;
    } else if (nightFactor < 0.5 && this.starsAdded) {
        this.removeStars();
        this.starsAdded = false;
    }
}

// Helper function to add stars (optional enhancement)
addStars() {
    if (this.stars) return;
    
    const starGeometry = new THREE.BufferGeometry();
    const starCount = 1000;
    const positions = new Float32Array(starCount * 3);
    
    for (let i = 0; i < starCount * 3; i += 3) {
        positions[i] = (Math.random() - 0.5) * 2000;
        positions[i + 1] = (Math.random() - 0.5) * 2000 + 500;
        positions[i + 2] = (Math.random() - 0.5) * 2000;
    }
    
    starGeometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    
    const starMaterial = new THREE.PointsMaterial({
        color: 0xffffff,
        size: 1.5,
        sizeAttenuation: true,
        transparent: true,
        opacity: 0.8
    });
    
    this.stars = new THREE.Points(starGeometry, starMaterial);
    this.scene.add(this.stars);
}

removeStars() {
    if (this.stars) {
        this.scene.remove(this.stars);
        this.stars = null;
    }
}
    
    serialize() {
        return {
            atticUnlocked: this.atticUnlocked,
            pickupItems: this.pickupItems.map(p => ({
                x: p.position.x,
                z: p.position.z,
                item: p.item
            }))
        };
    }
    
    deserialize(data) {
        this.atticUnlocked = data.atticUnlocked || false;
        // Would need to respawn pickup items based on saved state
    }
}