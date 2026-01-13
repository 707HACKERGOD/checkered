// js/character/NPCManager.js
// js/character/NPCManager.js - Fix imports
import * as THREE from 'three';
import { NPC } from './NPC.js';
import { NPC_DATA } from '../data/npcs.js';
import { DIALOGUE_DATA } from '../data/dialogue.js';  // Make sure this is imported

export class NPCManager {
    constructor(scene, world) {
        this.scene = scene;
        this.world = world;
        this.npcs = new Map();
        this.keyCharacters = new Map();
        this.currentWeek = 1;
        
        // Store dialogue data reference
        this.dialogueData = DIALOGUE_DATA;
    }
    
        // js/character/NPCManager.js - Fix NPC positioning

    async init() {
        console.log('Initializing NPCs...');
        
        // Create Bar Owner (Marcus) - INSIDE the bar area, not on a lamp
        await this.createBarOwner();
        
        // Create Shopkeeper (Ada)
        await this.createShopkeeper();
        
        // Create students
        await this.createStudents();
        
        // Create Victor (rich student) - near the main road
        await this.createVictor();
        
        // First key character
        this.spawnKeyCharacter(1);
        
        console.log('NPCs initialized:', this.npcs.size);
    }

    // In NPCManager.js - Update the createBarOwner method:
    async createBarOwner() {
        const npc = new NPC({
            id: 'bar_owner',
            name: 'Marcus',
            type: 'bar_owner',
            personality: { friendly: false, mysterious: true, guilty: true },
            appearance: {
                hairColor: 0x4a4a4a,
                skinColor: 0xd4a574,
                clothesColor: 0x2a1a1a
            },
            homeLocation: 'bar',
            workLocation: 'bar',
            dialogueTree: this.dialogueData.marcus || NPC_DATA.barOwner.dialogueTree  // Use actual dialogue data
        });
        
        // Position INSIDE bar
        npc.position.set(0, 0, -28);
        const mesh = npc.createMesh();
        this.scene.add(mesh);
        
        this.npcs.set('bar_owner', npc);
    }

    // Update createShopkeeper:
    async createShopkeeper() {
        const npc = new NPC({
            id: 'shopkeeper',
            name: 'Ada',
            type: 'shopkeeper',
            personality: { helpful: true, curious: true },
            appearance: {
                hairColor: 0x8b4513,
                skinColor: 0xffdbac,
                clothesColor: 0x556b2f
            },
            homeLocation: 'shop',
            workLocation: 'shop',
            dialogueTree: this.dialogueData.ada || NPC_DATA.shopkeeper.dialogueTree  // Use actual dialogue data
        });
        
        // Position near shop entrance
        npc.position.set(22, 0, 12);
        const mesh = npc.createMesh();
        this.scene.add(mesh);
        
        this.npcs.set('shopkeeper', npc);
    }

    // Update createVictor:
    async createVictor() {
        const npc = new NPC({
            id: 'victor',
            name: 'Victor',
            type: 'regular',
            personality: { arrogant: true, wealthy: true },
            appearance: {
                hairColor: 0xdaa520,
                skinColor: 0xffefd5,
                clothesColor: 0x800020
            },
            homeLocation: 'house3',
            workLocation: 'streets',
            dialogueTree: this.dialogueData.victor || NPC_DATA.students[1].dialogueTree  // Use actual dialogue data
        });
        
        // Position on the main street
        npc.position.set(8, 0, 5);
        const mesh = npc.createMesh();
        this.scene.add(mesh);
        
        this.npcs.set('victor', npc);
    }

    async createStudents() {
        const studentData = [
            { name: 'Elena', x: -45, z: -55 },
            { name: 'Student', x: -48, z: -52 }
        ];
        
        studentData.forEach((data, index) => {
            const npc = new NPC({
                id: `student_${index}`,
                name: data.name,
                type: 'regular',
                appearance: {
                    hairColor: index === 0 ? 0x1a1a1a : 0x4a3a2a,
                    skinColor: 0xffdbac,
                    clothesColor: 0x4169e1
                },
                homeLocation: 'house1',
                workLocation: 'school'
            });
            
            npc.position.set(data.x, 0, data.z);
            const mesh = npc.createMesh();
            this.scene.add(mesh);
            
            this.npcs.set(`student_${index}`, npc);
        });
    }
    
    spawnKeyCharacter(week) {
        if (week > 10) return;
        
        const keyCharConfig = NPC_DATA.keyCharacters[week - 1];
        if (!keyCharConfig) return;
        
        const npc = new NPC({
            ...keyCharConfig,
            id: `key_char_${week}`,
            type: 'key',
            isKeyCharacter: true,
            weekIntroduced: week
        });
        
        // Spawn at appropriate location based on character
        const spawnPos = this.getKeyCharacterSpawnPosition(week);
        npc.position.copy(spawnPos);
        
        const mesh = npc.createMesh();
        this.scene.add(mesh);
        
        this.keyCharacters.set(week, npc);
        this.npcs.set(`key_char_${week}`, npc);
        
        console.log(`Key character ${keyCharConfig.name} has appeared! (Week ${week})`);
    }
    
    getKeyCharacterSpawnPosition(week) {
        // Different spawn locations for variety
        const locations = [
            new THREE.Vector3(5, 0, 18), // Park
            new THREE.Vector3(-15, 0, -28), // Near school
            new THREE.Vector3(30, 0, 8), // Residential
            new THREE.Vector3(-8, 0, 3), // Near shop
            new THREE.Vector3(12, 0, -10), // Near bar
            new THREE.Vector3(0, 0, 0), // Center
            new THREE.Vector3(25, 0, -5), // East side
            new THREE.Vector3(-20, 0, 10), // West side
            new THREE.Vector3(10, 0, 25), // Park edge
            new THREE.Vector3(-5, 0, -15) // South
        ];
        
        return locations[(week - 1) % locations.length];
    }
    
    update(deltaTime, playerPosition, timePeriod) {
        // Update current week and spawn new key character if needed
        const newWeek = Math.ceil(this.world?.timeManager?.currentDay / 7) || 1;
        if (newWeek > this.currentWeek) {
            this.currentWeek = newWeek;
            this.spawnKeyCharacter(this.currentWeek);
        }
        
        // Update all NPCs
        this.npcs.forEach(npc => {
            npc.update(deltaTime, timePeriod, playerPosition);
        });
    }
    
    getNearbyNPCs(position, radius) {
        const nearby = [];
        
        this.npcs.forEach(npc => {
            if (npc.isAlive && npc.position.distanceTo(position) <= radius) {
                nearby.push(npc);
            }
        });
        
        return nearby;
    }
    
    getInteractableNPC(position, interactRadius = 2) {
        let closest = null;
        let closestDist = interactRadius;
        
        this.npcs.forEach(npc => {
            if (npc.isAlive) {
                const dist = npc.position.distanceTo(position);
                if (dist < closestDist) {
                    closest = npc;
                    closestDist = dist;
                }
            }
        });
        
        return closest;
    }
    
    killNPC(npc) {
        if (typeof npc === 'string') {
            npc = this.npcs.get(npc);
        }
        
        if (npc) {
            npc.kill();
            
            // If key character, track for ending
            if (npc.isKeyCharacter) {
                console.log(`Key character ${npc.name} has been killed!`);
            }
        }
    }
    
    getAliveKeyCharacterCount() {
        let count = 0;
        this.keyCharacters.forEach(npc => {
            if (npc.isAlive) count++;
        });
        return count;
    }
    
    getBefriendedKeyCharacterCount() {
        let count = 0;
        this.keyCharacters.forEach(npc => {
            if (npc.isAlive && npc.isBefriended) count++;
        });
        return count;
    }
    
    checkPeacefulEnding() {
        // All 10 key characters befriended and alive
        return this.getBefriendedKeyCharacterCount() >= 10;
    }
    
    notifyAllNPCs(event) {
        this.npcs.forEach(npc => {
            if (npc.isAlive) {
                npc.witnessEvent(event);
            }
        });
    }
    
    serialize() {
        const data = {
            npcs: {},
            currentWeek: this.currentWeek
        };
        
        this.npcs.forEach((npc, id) => {
            data.npcs[id] = npc.serialize();
        });
        
        return data;
    }
    
    deserialize(data) {
        this.currentWeek = data.currentWeek;
        
        Object.entries(data.npcs).forEach(([id, npcData]) => {
            const npc = this.npcs.get(id);
            if (npc) {
                npc.deserialize(npcData);
            }
        });
    }
}