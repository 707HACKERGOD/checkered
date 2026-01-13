// js/systems/PossessionSystem.js - FIXED
import * as THREE from 'three';

export class PossessionSystem {
    constructor(player, npcManager, sanitySystem, healthSystem, renderer) {
        this.player = player;
        this.npcManager = npcManager;
        this.sanitySystem = sanitySystem;
        this.healthSystem = healthSystem;
        this.renderer = renderer;
        
        // Possession state
        this.isActive = false;
        this.isComplete = false;
        this.phase = 'none'; // none, warning, active, ending
        
        // Timing
        this.warningDuration = 5; // seconds of warning before possession
        this.maxPossessionDuration = 45; // seconds of possession
        this.currentDuration = 0;
        this.warningProgress = 0;
        
        // GRACE PERIOD - No possession for first 5 minutes of gameplay
        this.gameStartTime = Date.now();
        this.gracePeriod = 300; // 5 minutes in seconds
        
        // Cooldown between possessions
        this.lastPossessionTime = 0;
        this.baseCooldown = 180; // 3 minutes minimum between possessions
        this.cooldownVariance = 60; // +/- 1 minute random
        this.currentCooldown = this.baseCooldown;
        
        // Phantoms
        this.phantoms = [];
        this.maxPhantoms = 4;
        this.phantomsKilled = 0;
        
        // Location tracking for repeat possession logic
        this.possessionHistory = new Map();
        
        // Traps
        this.appliedTraps = [];
        this.trapEffectsOnPhantoms = [];
        this.trapEffectsOnPlayer = [];
        
        // Combat
        this.attackCooldown = 0;
        this.attackSpeed = 0.4;
        this.attackRange = 2.5;
        this.attackDamage = 25;
        
        // Aftermath
        this.killedNPCs = [];
        this.remainingPhantom = null;
    }
    
    checkTrigger(sanity, comfort, currentDay, location) {
        // Don't trigger if already active
        if (this.isActive || this.phase !== 'none') {
            return false;
        }
        
        // GRACE PERIOD CHECK - No possession in first 5 minutes
        const timeSinceStart = (Date.now() - this.gameStartTime) / 1000;
        if (timeSinceStart < this.gracePeriod) {
            return false;
        }
        
        // Cooldown check
        const timeSinceLast = (Date.now() - this.lastPossessionTime) / 1000;
        if (this.lastPossessionTime > 0 && timeSinceLast < this.currentCooldown) {
            return false;
        }
        
        // Base possession chance (very low)
        let chance = 0.0002; // 0.02% per frame
        
        // Sanity modifier
        const sanityMod = this.sanitySystem.getPossessionChanceModifier();
        chance *= sanityMod;
        
        // Comfort modifier - high comfort increases chance
        if (comfort > 70) {
            chance *= 1.3 + (comfort - 70) / 60;
        }
        
        // Location history - more likely where it happened before (if low sanity)
        const locationCount = this.possessionHistory.get(location) || 0;
        if (sanity < 50 && locationCount > 0) {
            chance *= 1 + locationCount * 0.15;
        } else if (sanity > 70 && locationCount > 0) {
            chance *= Math.max(0.5, 1 - locationCount * 0.1);
        }
        
        // Time pressure - slightly more likely as days pass
        chance *= 1 + (currentDay / 100);
        
        // Random roll
        return Math.random() < chance;
    }
    
    start(config) {
        console.log('Possession sequence starting!');
        
        this.isActive = true;
        this.isComplete = false;
        this.phase = 'warning';
        this.currentDuration = 0;
        this.warningProgress = 0;
        this.phantomsKilled = 0;
        this.killedNPCs = [];
        this.remainingPhantom = null;
        this.phantoms = [];
        
        // Store traps
        this.appliedTraps = config.appliedTraps || [];
        this.processTraps();
        
        // Record possession at this location
        const locationCount = this.possessionHistory.get(config.location) || 0;
        this.possessionHistory.set(config.location, locationCount + 1);
    }
    
    processTraps() {
        this.trapEffectsOnPhantoms = [];
        this.trapEffectsOnPlayer = [];
        
        this.appliedTraps.forEach(trap => {
            const effects = this.getTrapEffects(trap);
            this.trapEffectsOnPhantoms.push(...effects);
            this.trapEffectsOnPlayer.push(...effects);
        });
    }
    
    getTrapEffects(trap) {
        const effects = [];
        
        if (!trap.trapEffect) return effects;
        
        const trapEffects = Array.isArray(trap.trapEffect) ? trap.trapEffect : [trap.trapEffect];
        
        trapEffects.forEach(effect => {
            switch (effect) {
                case 'slow':
                    effects.push({ type: 'slow', value: 0.5, duration: Infinity });
                    break;
                case 'stun':
                    effects.push({ type: 'stun', value: 3, duration: 5 });
                    break;
                case 'damage':
                    effects.push({ type: 'damage', value: trap.damage || 15, continuous: trap.continuous });
                    break;
            }
        });
        
        return effects;
    }
    
    update(deltaTime, input) {
        if (!this.isActive) return;
        
        switch (this.phase) {
            case 'warning':
                this.updateWarning(deltaTime);
                break;
            case 'active':
                this.updateActive(deltaTime, input);
                break;
            case 'ending':
                this.updateEnding(deltaTime);
                break;
        }
    }
    
    updateWarning(deltaTime) {
        this.warningProgress += deltaTime / this.warningDuration;
        
        if (this.warningProgress >= 1) {
            this.phase = 'active';
            this.spawnPhantoms();
            console.log('Possession active! Phantoms spawned:', this.phantoms.length);
        }
    }
    
    spawnPhantoms() {
        const difficultyMod = this.sanitySystem.getPhantomDifficultyModifier();
        const phantomCount = Math.min(this.maxPhantoms, Math.ceil(2 + Math.random() * 2 * difficultyMod));
        
        // Get nearby NPCs as targets
        const nearbyNPCs = this.npcManager.getNearbyNPCs(this.player.position, 50);
        
        for (let i = 0; i < phantomCount; i++) {
            const phantom = this.createPhantom(i, nearbyNPCs[i % Math.max(1, nearbyNPCs.length)]);
            this.phantoms.push(phantom);
            this.renderer.scene.add(phantom.mesh);
        }
        
        // Apply trap effects to phantoms
        this.trapEffectsOnPhantoms.forEach(effect => {
            this.phantoms.forEach(phantom => {
                this.applyEffectToPhantom(phantom, effect);
            });
        });
    }
    
    createPhantom(index, targetNPC) {
        const group = new THREE.Group();
        
        // Ghostly humanoid shape
        const bodyMat = new THREE.MeshBasicMaterial({
            color: 0x660066,
            transparent: true,
            opacity: 0.7
        });
        
        // Body
        const bodyGeo = new THREE.BoxGeometry(0.5, 1.0, 0.3);
        const body = new THREE.Mesh(bodyGeo, bodyMat);
        body.position.y = 1.0;
        group.add(body);
        
        // Head
        const headGeo = new THREE.BoxGeometry(0.35, 0.35, 0.35);
        const head = new THREE.Mesh(headGeo, bodyMat);
        head.position.y = 1.6;
        group.add(head);
        
        // Glowing eyes
        const eyeMat = new THREE.MeshBasicMaterial({ color: 0xff0000 });
        const eyeGeo = new THREE.BoxGeometry(0.08, 0.05, 0.02);
        
        const leftEye = new THREE.Mesh(eyeGeo, eyeMat);
        leftEye.position.set(-0.08, 1.65, 0.18);
        group.add(leftEye);
        
        const rightEye = new THREE.Mesh(eyeGeo, eyeMat);
        rightEye.position.set(0.08, 1.65, 0.18);
        group.add(rightEye);
        
        // Arms
        const armGeo = new THREE.BoxGeometry(0.12, 0.5, 0.12);
        const leftArm = new THREE.Mesh(armGeo, bodyMat);
        leftArm.position.set(-0.35, 0.9, 0);
        group.add(leftArm);
        
        const rightArm = new THREE.Mesh(armGeo, bodyMat);
        rightArm.position.set(0.35, 0.9, 0);
        group.add(rightArm);
        
        // Spawn position around player
        const angle = (index / this.maxPhantoms) * Math.PI * 2 + Math.random() * 0.5;
        const distance = 8 + Math.random() * 5;
        
        group.position.set(
            this.player.position.x + Math.cos(angle) * distance,
            0,
            this.player.position.z + Math.sin(angle) * distance
        );
        
        return {
            mesh: group,
            health: 100,
            maxHealth: 100,
            speed: 2.5 * this.sanitySystem.getPhantomDifficultyModifier(),
            targetNPC: targetNPC,
            position: group.position,
            effects: [],
            isStunned: false,
            stunTimer: 0
        };
    }
    
    applyEffectToPhantom(phantom, effect) {
        phantom.effects.push({ ...effect });
        
        switch (effect.type) {
            case 'slow':
                phantom.speed *= effect.value;
                break;
            case 'stun':
                phantom.isStunned = true;
                phantom.stunTimer = effect.value;
                break;
            case 'damage':
                phantom.health -= effect.value;
                break;
        }
    }
    
    updateActive(deltaTime, input) {
        this.currentDuration += deltaTime;
        this.attackCooldown = Math.max(0, this.attackCooldown - deltaTime);
        
        // Update phantoms
        this.phantoms = this.phantoms.filter(phantom => {
            // Check if dead
            if (phantom.health <= 0) {
                this.renderer.scene.remove(phantom.mesh);
                this.phantomsKilled++;
                return false;
            }
            
            // Update stun
            if (phantom.isStunned) {
                phantom.stunTimer -= deltaTime;
                if (phantom.stunTimer <= 0) {
                    phantom.isStunned = false;
                }
                return true;
            }
            
            // Apply continuous damage
            phantom.effects.forEach(effect => {
                if (effect.continuous && effect.type === 'damage') {
                    phantom.health -= effect.value * deltaTime * 0.5;
                }
            });
            
            // Move toward target NPC
            if (phantom.targetNPC && phantom.targetNPC.isAlive && !phantom.isStunned) {
                const npcPos = phantom.targetNPC.position;
                const dir = new THREE.Vector3()
                    .subVectors(npcPos, phantom.position)
                    .normalize();
                
                phantom.position.addScaledVector(dir, phantom.speed * deltaTime);
                phantom.mesh.position.copy(phantom.position);
                phantom.mesh.lookAt(npcPos.x, phantom.position.y, npcPos.z);
                
                // Check if reached NPC
                if (phantom.position.distanceTo(npcPos) < 1.5) {
                    this.killedNPCs.push(phantom.targetNPC);
                    phantom.targetNPC.kill();
                    phantom.targetNPC = this.findNewTarget(phantom);
                }
            } else if (!phantom.targetNPC || !phantom.targetNPC.isAlive) {
                // Find new target
                phantom.targetNPC = this.findNewTarget(phantom);
            }
            
            return true;
        });
        
        // Player combat - attack with left mouse
        if (input.isAttackPressed() && this.attackCooldown <= 0) {
            this.performAttack();
        }
        
        // Check end conditions
        if (this.phantoms.length === 0) {
            console.log('All phantoms defeated!');
            this.phase = 'ending';
        } else if (this.currentDuration >= this.maxPossessionDuration) {
            console.log('Possession timed out');
            this.remainingPhantom = this.phantoms[0];
            this.phase = 'ending';
        }
    }
    
    performAttack() {
        this.attackCooldown = this.attackSpeed;
        this.player.combatActions++;
        
        // Attack in front of player
        const attackPos = this.player.position.clone();
        const forward = this.player.getForwardDirection();
        attackPos.addScaledVector(forward, 1.5);
        
        let hitAny = false;
        
        this.phantoms.forEach(phantom => {
            const dist = phantom.position.distanceTo(attackPos);
            if (dist < this.attackRange) {
                phantom.health -= this.attackDamage;
                hitAny = true;
                
                // Visual feedback - flash red
                phantom.mesh.children.forEach(child => {
                    if (child.material) {
                        const originalColor = child.material.color.getHex();
                        child.material.color.setHex(0xff0000);
                        setTimeout(() => {
                            child.material.color.setHex(originalColor);
                        }, 100);
                    }
                });
                
                // Knockback
                const knockbackDir = new THREE.Vector3()
                    .subVectors(phantom.position, this.player.position)
                    .normalize();
                phantom.position.addScaledVector(knockbackDir, 1);
                phantom.mesh.position.copy(phantom.position);
            }
        });
        
        // Play attack sound
        if (hitAny) {
            // Hit sound would play here
        }
    }
    
    findNewTarget(phantom) {
        const nearbyNPCs = this.npcManager.getNearbyNPCs(phantom.position, 50)
            .filter(npc => npc.isAlive && !this.killedNPCs.includes(npc));
        
        if (nearbyNPCs.length > 0) {
            // Pick closest
            nearbyNPCs.sort((a, b) => 
                phantom.position.distanceTo(a.position) - phantom.position.distanceTo(b.position)
            );
            return nearbyNPCs[0];
        }
        
        return null;
    }
    
    updateEnding(deltaTime) {
        // Clean up phantoms
        this.phantoms.forEach(phantom => {
            this.renderer.scene.remove(phantom.mesh);
        });
        this.phantoms = [];
        
        this.isComplete = true;
    }
    
    getAftermath() {
        // Update cooldown for next possession
        this.lastPossessionTime = Date.now();
        this.currentCooldown = this.baseCooldown + (Math.random() - 0.5) * 2 * this.cooldownVariance;
        
        // Reset state
        this.isActive = false;
        this.phase = 'none';
        
        const result = {
            killedNPCs: this.killedNPCs,
            phantomsKilled: this.phantomsKilled,
            remainingPhantom: this.remainingPhantom,
            trapEffects: this.trapEffectsOnPlayer,
            duration: this.currentDuration
        };
        
        // Clear traps
        this.appliedTraps = [];
        this.trapEffectsOnPhantoms = [];
        this.trapEffectsOnPlayer = [];
        
        return result;
    }
    
    getStatus() {
        return {
            phase: this.phase,
            warningProgress: this.warningProgress,
            duration: this.currentDuration,
            maxDuration: this.maxPossessionDuration,
            phantomCount: this.phantoms.length,
            phantomsKilled: this.phantomsKilled,
            killedNPCs: this.killedNPCs.length
        };
    }
    
    // For debug - get time until possession is possible
    getTimeUntilPossessionPossible() {
        const timeSinceStart = (Date.now() - this.gameStartTime) / 1000;
        if (timeSinceStart < this.gracePeriod) {
            return this.gracePeriod - timeSinceStart;
        }
        
        if (this.lastPossessionTime > 0) {
            const timeSinceLast = (Date.now() - this.lastPossessionTime) / 1000;
            if (timeSinceLast < this.currentCooldown) {
                return this.currentCooldown - timeSinceLast;
            }
        }
        
        return 0;
    }
    
    serialize() {
        return {
            lastPossessionTime: this.lastPossessionTime,
            currentCooldown: this.currentCooldown,
            gameStartTime: this.gameStartTime,
            possessionHistory: Object.fromEntries(this.possessionHistory)
        };
    }
    
    deserialize(data) {
        this.lastPossessionTime = data.lastPossessionTime || 0;
        this.currentCooldown = data.currentCooldown || this.baseCooldown;
        this.gameStartTime = data.gameStartTime || Date.now();
        this.possessionHistory = new Map(Object.entries(data.possessionHistory || {}));
    }
}