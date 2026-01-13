// js/systems/SanitySystem.js
export class SanitySystem {
    constructor() {
        // Core stats
        this.sanity = 100;
        this.maxSanity = 100;
        this.comfort = 0;
        this.maxComfort = 100;
        
        // Decay rates
        this.sanityDecayRate = 0.5; // Per minute
        this.comfortGainRate = 1.0; // Per minute in comfortable areas
        this.comfortDecayRate = 0.3; // Per minute outside comfortable areas
        
        // Thresholds
        this.lowSanityThreshold = 30;
        this.criticalSanityThreshold = 15;
        this.highComfortThreshold = 70;
        
        // Location comfort modifiers
        this.locationComfort = {
            'bar': 15,
            'park': 5,
            'shop': 3,
            'house1': 10,
            'house2': 10,
            'house3': 10,
            'school': -5,
            'streets': 0,
            'forest': -10
        };
        
        // State tracking
        this.currentLocation = 'streets';
        this.timeInLocation = 0;
        this.safeLocations = new Set(); // Locations made "safe" by high sanity visits
        
        // Visual/audio cue thresholds
        this.lastCueTime = 0;
        this.cueInterval = 10000; // 10 seconds between cues
        
        // Callbacks
        this.onSanityChange = null;
        this.onComfortChange = null;
        this.onLowSanity = null;
        this.onCriticalSanity = null;
    }
    
    update(deltaTime, context) {
        const { playerPosition, nearbyNPCs, currentLocation, timeOfDay } = context;
        
        // Update location tracking
        if (currentLocation !== this.currentLocation) {
            this.currentLocation = currentLocation;
            this.timeInLocation = 0;
        } else {
            this.timeInLocation += deltaTime;
        }
        
        // Calculate sanity modifiers
        let sanityMod = 0;
        
        // Base decay
        sanityMod -= this.sanityDecayRate * deltaTime / 60;
        
        // Night time increases decay
        if (timeOfDay === 'NIGHT' || timeOfDay === 'NIGHT2') {
            sanityMod -= 0.2 * deltaTime / 60;
        }
        
        // Being near friendly NPCs helps
        const friendlyNPCs = nearbyNPCs.filter(npc => 
            npc.relationship === 'friendly' || npc.relationship === 'trusted'
        );
        sanityMod += friendlyNPCs.length * 0.1 * deltaTime / 60;
        
        // Suspicious/fearful NPCs hurt sanity
        const negativeNPCs = nearbyNPCs.filter(npc =>
            npc.relationship === 'suspicious' || npc.relationship === 'fearful'
        );
        sanityMod -= negativeNPCs.length * 0.15 * deltaTime / 60;
        
        // Location effects
        const locationMod = this.locationComfort[currentLocation] || 0;
        if (locationMod > 0) {
            sanityMod += locationMod * 0.01 * deltaTime / 60;
        }
        
        // Apply sanity changes
        this.modifySanity(sanityMod);
        
        // Update comfort
        this.updateComfort(deltaTime, currentLocation, nearbyNPCs);
        
        // Check for visual/audio cues
        this.checkForCues();
        
        // Update safe locations
        this.updateSafeLocations(currentLocation);
    }
    
    updateComfort(deltaTime, currentLocation, nearbyNPCs) {
        let comfortMod = 0;
        
        // Location comfort
        const locationComfort = this.locationComfort[currentLocation] || 0;
        if (locationComfort > 0) {
            comfortMod += this.comfortGainRate * deltaTime / 60;
        } else {
            comfortMod -= this.comfortDecayRate * deltaTime / 60;
        }
        
        // Time in location bonus
        if (this.timeInLocation > 60) { // After 1 minute
            comfortMod += 0.5 * deltaTime / 60;
        }
        
        // Nearby friendly NPCs increase comfort
        const friendlyCount = nearbyNPCs.filter(n => 
            n.relationship === 'friendly' || n.relationship === 'trusted'
        ).length;
        comfortMod += friendlyCount * 0.2 * deltaTime / 60;
        
        // Apply comfort changes
        this.modifyComfort(comfortMod);
    }
    
    modifySanity(amount) {
        const oldSanity = this.sanity;
        this.sanity = Math.max(0, Math.min(this.maxSanity, this.sanity + amount));
        
        if (this.sanity !== oldSanity && this.onSanityChange) {
            this.onSanityChange(this.sanity, oldSanity);
        }
        
        // Check thresholds
        if (oldSanity >= this.lowSanityThreshold && this.sanity < this.lowSanityThreshold) {
            if (this.onLowSanity) this.onLowSanity();
        }
        
        if (oldSanity >= this.criticalSanityThreshold && this.sanity < this.criticalSanityThreshold) {
            if (this.onCriticalSanity) this.onCriticalSanity();
        }
    }
    
    modifyComfort(amount) {
        const oldComfort = this.comfort;
        this.comfort = Math.max(0, Math.min(this.maxComfort, this.comfort + amount));
        
        if (this.comfort !== oldComfort && this.onComfortChange) {
            this.onComfortChange(this.comfort, oldComfort);
        }
    }
    
    checkForCues() {
        const now = Date.now();
        if (now - this.lastCueTime < this.cueInterval) return;
        
        // Trigger visual/audio cues based on sanity level
        if (this.sanity < this.criticalSanityThreshold) {
            this.triggerCue('critical');
            this.lastCueTime = now;
        } else if (this.sanity < this.lowSanityThreshold) {
            this.triggerCue('low');
            this.lastCueTime = now;
        }
    }
    
    triggerCue(level) {
        // Dispatch event for UI/audio to handle
        const event = new CustomEvent('sanityCue', { detail: { level } });
        document.dispatchEvent(event);
    }
    
    updateSafeLocations(currentLocation) {
        // If sanity is high and we've been here a while, mark as safe
        if (this.sanity > 70 && this.timeInLocation > 120) { // 2 minutes
            this.safeLocations.add(currentLocation);
        }
        
        // If sanity is low, locations become unsafe
        if (this.sanity < 30) {
            // Chance to remove safe status
            if (Math.random() < 0.01) { // 1% chance per update
                this.safeLocations.delete(currentLocation);
            }
        }
    }
    
    isLocationSafe(location) {
        return this.safeLocations.has(location);
    }
    
    // Get possession chance modifier based on sanity
    getPossessionChanceModifier() {
        // High sanity = slightly lower chance
        // Low sanity = slightly higher chance but not dramatic
        if (this.sanity > 70) return 0.8;
        if (this.sanity > 50) return 1.0;
        if (this.sanity > 30) return 1.2;
        return 1.5;
    }
    
    // Get phantom difficulty modifier based on sanity
    getPhantomDifficultyModifier() {
        // High sanity = fewer, slower phantoms
        // Low sanity = more, faster phantoms
        if (this.sanity > 70) return 0.5;
        if (this.sanity > 50) return 0.75;
        if (this.sanity > 30) return 1.0;
        return 1.5;
    }
    
    // Called when possession ends
    onPossessionEnd(aftermath) {
        // Sanity hit based on what happened
        let sanityLoss = 10; // Base loss
        
        // Additional loss for killed NPCs
        sanityLoss += aftermath.killedNPCs.length * 5;
        
        // Reduced loss if player fought well
        if (aftermath.phantomsKilled > aftermath.killedNPCs.length) {
            sanityLoss *= 0.7;
        }
        
        this.modifySanity(-sanityLoss);
        
        // Reset comfort after possession
        this.comfort = Math.max(0, this.comfort - 30);
    }
    
    // Instant sanity effects from actions
    onActionPerformed(action) {
        switch (action) {
            case 'killed_npc':
                this.modifySanity(-15);
                break;
            case 'helped_npc':
                this.modifySanity(5);
                break;
            case 'crafted_gory':
                this.modifySanity(-8);
                break;
            case 'found_safe_place':
                this.modifySanity(3);
                break;
            case 'witnessed_death':
                this.modifySanity(-10);
                break;
            case 'made_friend':
                this.modifySanity(10);
                break;
            case 'betrayed_friend':
                this.modifySanity(-20);
                break;
        }
    }
    
    serialize() {
        return {
            sanity: this.sanity,
            comfort: this.comfort,
            safeLocations: Array.from(this.safeLocations),
            currentLocation: this.currentLocation,
            timeInLocation: this.timeInLocation
        };
    }
    
    deserialize(data) {
        this.sanity = data.sanity;
        this.comfort = data.comfort;
        this.safeLocations = new Set(data.safeLocations);
        this.currentLocation = data.currentLocation;
        this.timeInLocation = data.timeInLocation;
    }
}