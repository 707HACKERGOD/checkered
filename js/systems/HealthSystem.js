// js/systems/HealthSystem.js
export class HealthSystem {
    constructor() {
        // Body parts and their health
        this.bodyParts = {
            'head': { health: 100, maxHealth: 100, lethalThreshold: 0, vital: true },
            'left-eye': { health: 0, maxHealth: 100, lethalThreshold: 0, vital: false, missing: true },
            'right-eye': { health: 100, maxHealth: 100, lethalThreshold: 0, vital: false },
            'torso': { health: 100, maxHealth: 100, lethalThreshold: 20, vital: true },
            'left-arm': { health: 100, maxHealth: 100, lethalThreshold: 0, vital: false },
            'right-arm': { health: 100, maxHealth: 100, lethalThreshold: 0, vital: false },
            'left-leg': { health: 100, maxHealth: 100, lethalThreshold: 0, vital: false },
            'right-leg': { health: 100, maxHealth: 100, lethalThreshold: 0, vital: false }
        };
        
        // Internal organs (part of torso but tracked separately for detail)
        this.organs = {
            'heart': { health: 100, maxHealth: 100, lethalThreshold: 30, vital: true },
            'lungs': { health: 100, maxHealth: 100, lethalThreshold: 20, vital: true },
            'liver': { health: 100, maxHealth: 100, lethalThreshold: 10, vital: true },
            'stomach': { health: 100, maxHealth: 100, lethalThreshold: 0, vital: false },
            'intestines': { health: 100, maxHealth: 100, lethalThreshold: 0, vital: false }
        };
        
        // Active effects
        this.effects = [];
        
        // Regeneration
        this.regenerationRate = 0.5; // Per minute for non-vital parts
        this.hasRegenerated = false; // Track if lethal damage triggered regen
        
        // Callbacks
        this.onPartDamaged = null;
        this.onLethalDamage = null;
        this.onRegeneration = null;
    }
    
    update(deltaTime) {
        // Update effects
        this.effects = this.effects.filter(effect => {
            effect.duration -= deltaTime;
            
            // Apply continuous effects
            if (effect.continuous && effect.type === 'damage') {
                this.takeDamage(effect.bodyPart, effect.value * deltaTime);
            }
            
            return effect.duration > 0;
        });
        
        // Natural regeneration (slow, only for non-missing parts)
        Object.entries(this.bodyParts).forEach(([name, part]) => {
            if (!part.missing && part.health < part.maxHealth && part.health > 0) {
                part.health = Math.min(
                    part.maxHealth,
                    part.health + this.regenerationRate * deltaTime / 60
                );
            }
        });
    }
    
    takeDamage(partName, amount) {
        const part = this.bodyParts[partName];
        if (!part || part.missing) return;
        
        const oldHealth = part.health;
        part.health = Math.max(0, part.health - amount);
        
        if (this.onPartDamaged) {
            this.onPartDamaged(partName, amount, part.health);
        }
        
        // Check for lethal damage
        if (this.checkLethalDamage()) {
            this.triggerRegeneration();
        }
        
        // Check for limb loss
        if (part.health <= 0 && !part.vital) {
            this.loseLimb(partName);
        }
    }
    
    healPart(partName, amount) {
        const part = this.bodyParts[partName];
        if (!part || part.missing) return false;
        
        part.health = Math.min(part.maxHealth, part.health + amount);
        return true;
    }
    
    applyEffect(effect) {
        this.effects.push({
            ...effect,
            duration: effect.duration || 0
        });
        
        // Immediate effects
        switch (effect.type) {
            case 'damage':
                if (!effect.continuous) {
                    this.takeDamage(effect.bodyPart, effect.value);
                }
                break;
            case 'heal':
                this.healPart(effect.bodyPart, effect.value);
                break;
            case 'slow':
                // Handled by movement system
                break;
        }
    }
    
    loseLimb(partName) {
        const part = this.bodyParts[partName];
        if (!part) return;
        
        // Don't "lose" vital parts - triggers regen instead
        if (part.vital) return;
        
        // Mark as crippled but not missing (for immortal character)
        part.health = 0;
        
        console.log(`${partName} has been crippled!`);
    }
    
    checkLethalDamage() {
        // Check vital body parts
        for (const [name, part] of Object.entries(this.bodyParts)) {
            if (part.vital && part.health <= part.lethalThreshold) {
                return true;
            }
        }
        
        // Check vital organs
        for (const [name, organ] of Object.entries(this.organs)) {
            if (organ.vital && organ.health <= organ.lethalThreshold) {
                return true;
            }
        }
        
        return false;
    }
    
    triggerRegeneration() {
        console.log('Lethal damage detected - triggering immortal regeneration!');
        
        if (this.onLethalDamage) {
            this.onLethalDamage();
        }
        
        // Regenerate all parts to full health (except left eye)
        Object.entries(this.bodyParts).forEach(([name, part]) => {
            if (name !== 'left-eye') {
                part.health = part.maxHealth;
                part.missing = false;
            }
        });
        
        // Regenerate all organs
        Object.values(this.organs).forEach(organ => {
            organ.health = organ.maxHealth;
        });
        
        // Clear negative effects
        this.effects = this.effects.filter(e => e.type === 'buff');
        
        this.hasRegenerated = true;
        
        if (this.onRegeneration) {
            this.onRegeneration();
        }
    }
    
    getOverallHealth() {
        let totalHealth = 0;
        let totalMax = 0;
        
        Object.values(this.bodyParts).forEach(part => {
            if (!part.missing) {
                totalHealth += part.health;
                totalMax += part.maxHealth;
            }
        });
        
        return (totalHealth / totalMax) * 100;
    }
    
    getPartStatus(partName) {
        const part = this.bodyParts[partName];
        if (!part) return null;
        
        let status = 'healthy';
        if (part.missing) {
            status = 'missing';
        } else if (part.health <= 0) {
            status = 'crippled';
        } else if (part.health < 30) {
            status = 'critical';
        } else if (part.health < 60) {
            status = 'damaged';
        }
        
        return {
            health: part.health,
            maxHealth: part.maxHealth,
            status: status,
            missing: part.missing,
            vital: part.vital
        };
    }
    
    getAllPartStatuses() {
        const statuses = {};
        Object.keys(this.bodyParts).forEach(name => {
            statuses[name] = this.getPartStatus(name);
        });
        return statuses;
    }
    
    getActiveEffects() {
        return this.effects.map(e => ({
            type: e.type,
            bodyPart: e.bodyPart,
            value: e.value,
            duration: e.duration
        }));
    }
    
    // Check if player can perform actions requiring specific limbs
    canUseArms() {
        const leftArm = this.bodyParts['left-arm'];
        const rightArm = this.bodyParts['right-arm'];
        return (leftArm.health > 0 || rightArm.health > 0);
    }
    
    canWalk() {
        const leftLeg = this.bodyParts['left-leg'];
        const rightLeg = this.bodyParts['right-leg'];
        return (leftLeg.health > 20 || rightLeg.health > 20);
    }
    
    canSee() {
        const rightEye = this.bodyParts['right-eye'];
        return rightEye.health > 0;
    }
    
    getMovementModifier() {
        const leftLeg = this.bodyParts['left-leg'];
        const rightLeg = this.bodyParts['right-leg'];
        
        const leftMod = leftLeg.health / leftLeg.maxHealth;
        const rightMod = rightLeg.health / rightLeg.maxHealth;
        
        // Apply slow effects
        let effectMod = 1;
        this.effects.forEach(effect => {
            if (effect.type === 'slow') {
                effectMod *= effect.value;
            }
        });
        
        return Math.max(0.2, ((leftMod + rightMod) / 2) * effectMod);
    }
    
    serialize() {
        return {
            bodyParts: this.bodyParts,
            organs: this.organs,
            effects: this.effects,
            hasRegenerated: this.hasRegenerated
        };
    }
    
    deserialize(data) {
        this.bodyParts = data.bodyParts;
        this.organs = data.organs;
        this.effects = data.effects;
        this.hasRegenerated = data.hasRegenerated;
    }
}