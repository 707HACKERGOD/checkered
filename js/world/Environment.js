// js/world/Environment.js
import * as THREE from 'three';

export class Environment {
    constructor(scene) {
        this.scene = scene;
        this.objects = [];
        this.interactables = [];
        this.colliders = [];
    }
    
    // Create a simple low-poly house
    createHouse(config) {
        const group = new THREE.Group();
        
        // Main building
        const buildingGeo = new THREE.BoxGeometry(config.width, config.height, config.depth);
        const buildingMat = new THREE.MeshBasicMaterial({ 
            color: config.color || 0x6a5a4a,
            flatShading: true 
        });
        const building = new THREE.Mesh(buildingGeo, buildingMat);
        building.position.y = config.height / 2;
        building.castShadow = true;
        building.receiveShadow = true;
        group.add(building);
        
        // Roof
        const roofGeo = new THREE.ConeGeometry(
            Math.max(config.width, config.depth) * 0.7,
            config.height * 0.4,
            4
        );
        const roofMat = new THREE.MeshBasicMaterial({ 
            color: 0x4a3030,
            flatShading: true 
        });
        const roof = new THREE.Mesh(roofGeo, roofMat);
        roof.position.y = config.height + config.height * 0.2;
        roof.rotation.y = Math.PI / 4;
        roof.castShadow = true;
        group.add(roof);
        
        // Door
        const doorGeo = new THREE.BoxGeometry(1.2, 2.2, 0.1);
        const doorMat = new THREE.MeshBasicMaterial({ 
            color: 0x3a2a1a,
            flatShading: true 
        });
        const door = new THREE.Mesh(doorGeo, doorMat);
        door.position.set(0, 1.1, config.depth / 2 + 0.05);
        group.add(door);
        
        // Windows
        this.addWindowsToBuilding(group, config);
        
        group.position.set(config.x, 0, config.z);
        
        // Add collider
        const collider = new THREE.Box3().setFromObject(building);
        collider.translate(new THREE.Vector3(config.x, 0, config.z));
        this.colliders.push(collider);
        
        return group;
    }
    
    addWindowsToBuilding(group, config) {
        const windowMat = new THREE.MeshBasicMaterial({ 
            color: 0xffffaa,
            transparent: true,
            opacity: 0.7
        });
        
        const windowGeo = new THREE.PlaneGeometry(0.8, 1.0);
        
        // Front windows
        const numWindows = Math.floor(config.width / 2.5);
        for (let i = 0; i < numWindows; i++) {
            const win = new THREE.Mesh(windowGeo, windowMat);
            const xOffset = (i - (numWindows - 1) / 2) * 2;
            win.position.set(xOffset, config.height * 0.6, config.depth / 2 + 0.06);
            group.add(win);
        }
    }
    
    // Create the bar building
    createBar(x, z) {
        const group = new THREE.Group();
        
        // Main building - larger and more distinctive
        const buildingGeo = new THREE.BoxGeometry(12, 7, 10);
        const buildingMat = new THREE.MeshBasicMaterial({ 
            color: 0x4a3030,
            flatShading: true 
        });
        const building = new THREE.Mesh(buildingGeo, buildingMat);
        building.position.y = 3.5;
        building.castShadow = true;
        building.receiveShadow = true;
        group.add(building);
        
        // Sign
        const signGeo = new THREE.BoxGeometry(6, 1.5, 0.2);
        const signMat = new THREE.MeshBasicMaterial({ 
            color: 0x2a1a1a,
            flatShading: true 
        });
        const sign = new THREE.Mesh(signGeo, signMat);
        sign.position.set(0, 6, 5.1);
        group.add(sign);
        
        // Sign text (simple glowing box)
        const textGeo = new THREE.BoxGeometry(5, 1, 0.1);
        const textMat = new THREE.MeshBasicMaterial({ 
            color: 0xff6600,
            transparent: true,
            opacity: 0.9
        });
        const text = new THREE.Mesh(textGeo, textMat);
        text.position.set(0, 6, 5.2);
        group.add(text);
        
        // Door (larger)
        const doorGeo = new THREE.BoxGeometry(2, 3, 0.1);
        const doorMat = new THREE.MeshBasicMaterial({ 
            color: 0x5a3a2a,
            flatShading: true 
        });
        const door = new THREE.Mesh(doorGeo, doorMat);
        door.position.set(0, 1.5, 5.05);
        group.add(door);
        
        // Porch light
        const light = new THREE.PointLight(0xffaa66, 0.8, 15);
        light.position.set(0, 4, 6);
        group.add(light);
        
        group.position.set(x, 0, z);
        group.name = 'bar';
        
        return group;
    }
    
    // Create school building
    createSchool(x, z) {
        const group = new THREE.Group();
        
        // Main building - large institutional
        const buildingGeo = new THREE.BoxGeometry(25, 12, 18);
        const buildingMat = new THREE.MeshBasicMaterial({ 
            color: 0x5a5a6a,
            flatShading: true 
        });
        const building = new THREE.Mesh(buildingGeo, buildingMat);
        building.position.y = 6;
        building.castShadow = true;
        building.receiveShadow = true;
        group.add(building);
        
        // Entrance columns
        for (let i = -2; i <= 2; i++) {
            const columnGeo = new THREE.CylinderGeometry(0.4, 0.5, 8, 6);
            const columnMat = new THREE.MeshBasicMaterial({ 
                color: 0x7a7a8a,
                flatShading: true 
            });
            const column = new THREE.Mesh(columnGeo, columnMat);
            column.position.set(i * 3, 4, 9.5);
            column.castShadow = true;
            group.add(column);
        }
        
        // Big entrance door
        const doorGeo = new THREE.BoxGeometry(4, 5, 0.2);
        const doorMat = new THREE.MeshBasicMaterial({ 
            color: 0x3a3a4a,
            flatShading: true 
        });
        const door = new THREE.Mesh(doorGeo, doorMat);
        door.position.set(0, 2.5, 9.1);
        group.add(door);
        
        // Many windows
        const windowMat = new THREE.MeshBasicMaterial({ 
            color: 0xaaccff,
            transparent: true,
            opacity: 0.6
        });
        
        for (let floor = 0; floor < 3; floor++) {
            for (let i = -5; i <= 5; i++) {
                if (Math.abs(i) < 2 && floor === 0) continue; // Skip door area
                
                const winGeo = new THREE.PlaneGeometry(1.5, 2);
                const win = new THREE.Mesh(winGeo, windowMat);
                win.position.set(i * 2, 3 + floor * 3.5, 9.05);
                group.add(win);
            }
        }
        
        group.position.set(x, 0, z);
        group.name = 'school';
        
        return group;
    }
    
    // Create shop building
    createShop(x, z) {
        const group = new THREE.Group();
        
        const buildingGeo = new THREE.BoxGeometry(8, 5, 8);
        const buildingMat = new THREE.MeshBasicMaterial({ 
            color: 0x5a6a5a,
            flatShading: true 
        });
        const building = new THREE.Mesh(buildingGeo, buildingMat);
        building.position.y = 2.5;
        building.castShadow = true;
        group.add(building);
        
        // Awning
        const awningGeo = new THREE.BoxGeometry(9, 0.2, 3);
        const awningMat = new THREE.MeshBasicMaterial({ 
            color: 0x884422,
            flatShading: true 
        });
        const awning = new THREE.Mesh(awningGeo, awningMat);
        awning.position.set(0, 4, 5);
        group.add(awning);
        
        // Shop window (large)
        const windowGeo = new THREE.PlaneGeometry(5, 2.5);
        const windowMat = new THREE.MeshBasicMaterial({ 
            color: 0xffffcc,
            transparent: true,
            opacity: 0.5
        });
        const shopWindow = new THREE.Mesh(windowGeo, windowMat);
        shopWindow.position.set(0, 2.5, 4.05);
        group.add(shopWindow);
        
        // Door
        const doorGeo = new THREE.BoxGeometry(1.5, 2.5, 0.1);
        const doorMat = new THREE.MeshBasicMaterial({ color: 0x4a3a2a });
        const door = new THREE.Mesh(doorGeo, doorMat);
        door.position.set(-2.5, 1.25, 4.05);
        group.add(door);
        
        group.position.set(x, 0, z);
        group.name = 'shop';
        
        return group;
    }
    
    // Create a tree
    createTree(x, z, scale = 1) {
        const group = new THREE.Group();
        
        // Trunk
        const trunkGeo = new THREE.CylinderGeometry(0.2 * scale, 0.35 * scale, 2.5 * scale, 5);
        const trunkMat = new THREE.MeshBasicMaterial({ 
            color: 0x4a3020,
            flatShading: true 
        });
        const trunk = new THREE.Mesh(trunkGeo, trunkMat);
        trunk.position.y = 1.25 * scale;
        trunk.castShadow = true;
        group.add(trunk);
        
        // Foliage layers
        const foliageMat = new THREE.MeshBasicMaterial({ 
            color: 0x2a5a2a,
            flatShading: true 
        });
        
        const layers = [
            { y: 3, radius: 1.5, height: 2 },
            { y: 4.2, radius: 1.2, height: 1.5 },
            { y: 5, radius: 0.8, height: 1 }
        ];
        
        layers.forEach(layer => {
            const foliageGeo = new THREE.ConeGeometry(
                layer.radius * scale, 
                layer.height * scale, 
                6
            );
            const foliage = new THREE.Mesh(foliageGeo, foliageMat);
            foliage.position.y = layer.y * scale;
            foliage.castShadow = true;
            group.add(foliage);
        });
        
        group.position.set(x, 0, z);
        
        return group;
    }
    
    // Create a lamp post
    createLampPost(x, z) {
        const group = new THREE.Group();
        
        // Pole
        const poleGeo = new THREE.CylinderGeometry(0.08, 0.12, 4, 6);
        const poleMat = new THREE.MeshBasicMaterial({ 
            color: 0x2a2a2a,
            flatShading: true 
        });
        const pole = new THREE.Mesh(poleGeo, poleMat);
        pole.position.y = 2;
        pole.castShadow = true;
        group.add(pole);
        
        // Lamp housing
        const lampGeo = new THREE.BoxGeometry(0.6, 0.4, 0.6);
        const lampMat = new THREE.MeshBasicMaterial({ 
            color: 0xffffcc,
            transparent: true,
            opacity: 0.9
        });
        const lamp = new THREE.Mesh(lampGeo, lampMat);
        lamp.position.y = 4.2;
        group.add(lamp);
        
        // Light
        const light = new THREE.PointLight(0xffffaa, 0.6, 12);
        light.position.y = 4;
        light.castShadow = true;
        group.add(light);
        
        group.position.set(x, 0, z);
        
        // Store position for NPC spawning to avoid
        group.userData.avoidRadius = 1;
        
        return group;
    }
    
    // Create a bench
    createBench(x, z, rotation = 0) {
        const group = new THREE.Group();
        
        const woodMat = new THREE.MeshBasicMaterial({ 
            color: 0x6a4a3a,
            flatShading: true 
        });
        
        // Seat
        const seatGeo = new THREE.BoxGeometry(2, 0.1, 0.6);
        const seat = new THREE.Mesh(seatGeo, woodMat);
        seat.position.y = 0.5;
        group.add(seat);
        
        // Back
        const backGeo = new THREE.BoxGeometry(2, 0.8, 0.1);
        const back = new THREE.Mesh(backGeo, woodMat);
        back.position.set(0, 0.9, -0.25);
        group.add(back);
        
        // Legs
        const legGeo = new THREE.BoxGeometry(0.1, 0.5, 0.1);
        const legPositions = [
            [-0.8, 0.25, 0.2],
            [0.8, 0.25, 0.2],
            [-0.8, 0.25, -0.2],
            [0.8, 0.25, -0.2]
        ];
        
        legPositions.forEach(pos => {
            const leg = new THREE.Mesh(legGeo, woodMat);
            leg.position.set(...pos);
            group.add(leg);
        });
        
        group.position.set(x, 0, z);
        group.rotation.y = rotation;
        
        return group;
    }
    
    // Create a pickup item in the world
    createPickupItem(x, z, itemData) {
        const group = new THREE.Group();
        
        // Simple glowing cube to represent item
        const geo = new THREE.BoxGeometry(0.3, 0.3, 0.3);
        const mat = new THREE.MeshBasicMaterial({ 
            color: 0x44ff44,
            transparent: true,
            opacity: 0.8
        });
        const cube = new THREE.Mesh(geo, mat);
        cube.position.y = 0.5;
        group.add(cube);
        
        // Floating animation
        group.userData.floatOffset = Math.random() * Math.PI * 2;
        group.userData.itemData = itemData;
        group.userData.isPickup = true;
        
        group.position.set(x, 0, z);
        
        this.interactables.push(group);
        
        return group;
    }
}