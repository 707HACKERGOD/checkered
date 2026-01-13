// js/character/NPC.js
import * as THREE from 'three';

export class NPC {
    constructor(config) {
        this.id = config.id || 'npc_' + Math.random();
        this.name = config.name || 'Unknown';
        this.type = config.type || 'regular';
        this.personality = config.personality || {};
        this.appearance = config.appearance || {};
        this.homeLocation = config.homeLocation;
        this.workLocation = config.workLocation;
        this.dialogueTree = config.dialogueTree || {};
        this.isKeyCharacter = config.isKeyCharacter || false;
        this.weekIntroduced = config.weekIntroduced || 0;
        this.isAlive = true;
        this.isBefriended = false;
        this.relationship = 'neutral';
        this.relationshipValue = 0;
        this.state = 'idle';
        this.schedule = config.schedule || {};
        this.memory = {
            witnessedEvents: []
        };
        
        this.position = new THREE.Vector3();
        this.mesh = null;
    }
    
    createMesh() {
        const group = new THREE.Group();
        
        // Simple NPC character
        const bodyGeo = new THREE.CylinderGeometry(0.3, 0.3, 1.5, 8);
        const bodyMat = new THREE.MeshBasicMaterial({ 
            color: this.appearance.clothesColor || 0x808080 
        });
        const body = new THREE.Mesh(bodyGeo, bodyMat);
        body.position.y = 0.75;
        group.add(body);
        
        // Head
        const headGeo = new THREE.SphereGeometry(0.25, 8, 8);
        const headMat = new THREE.MeshBasicMaterial({ 
            color: this.appearance.skinColor || 0xffdbac 
        });
        const head = new THREE.Mesh(headGeo, headMat);
        head.position.y = 1.5;
        group.add(head);
        
        // Hair
        const hairGeo = new THREE.SphereGeometry(0.28, 8, 8);
        const hairMat = new THREE.MeshBasicMaterial({ 
            color: this.appearance.hairColor || 0x000000 
        });
        const hair = new THREE.Mesh(hairGeo, hairMat);
        hair.position.y = 1.65;
        group.add(hair);
        
        group.position.copy(this.position);
        this.mesh = group;
        
        return group;
    }
    
    update(deltaTime, timePeriod, playerPosition) {
        // Simple idle animation or state changes
        if (this.mesh) {
            this.mesh.rotation.y += 0.01;
        }
    }
    
    kill() {
        this.isAlive = false;
        if (this.mesh) {
            // Change to gray material
            this.mesh.traverse(child => {
                if (child.isMesh) {
                    child.material.color.setHex(0x666666);
                }
            });
        }
    }
    
    witnessEvent(event) {
        this.memory.witnessedEvents.push({
            event: event,
            time: Date.now()
        });
    }
    
    modifyRelationship(change, reason) {
        this.relationshipValue += change;
        if (this.relationshipValue > 50) {
            this.relationship = 'trusted';
        } else if (this.relationshipValue > 20) {
            this.relationship = 'friendly';
        } else if (this.relationshipValue < -20) {
            this.relationship = 'fearful';
        } else if (this.relationshipValue < -50) {
            this.relationship = 'hostile';
        } else {
            this.relationship = 'neutral';
        }
    }
    
    serialize() {
        return {
            id: this.id,
            isAlive: this.isAlive,
            isBefriended: this.isBefriended,
            relationship: this.relationship,
            relationshipValue: this.relationshipValue,
            memory: this.memory
        };
    }
    
    deserialize(data) {
        this.isAlive = data.isAlive;
        this.isBefriended = data.isBefriended;
        this.relationship = data.relationship;
        this.relationshipValue = data.relationshipValue;
        this.memory = data.memory;
    }
}