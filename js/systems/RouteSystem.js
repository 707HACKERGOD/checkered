// js/systems/RouteSystem.js
export class RouteSystem {
    constructor() {
        // Route scores (0-100)
        this.routeScores = {
            pacifist: 50,
            insanity: 50,
            criminal: 50,
            tech: 50
        };
        
        // Behavior tracking
        this.behaviors = {
            // Pacifist indicators
            dialogueCount: 0,
            friendsMade: 0,
            violenceAvoided: 0,
            helpfulActions: 0,
            
            // Insanity indicators
            chaosActions: 0,
            randomChoices: 0,
            trollingActions: 0,
            
            // Criminal indicators
            combatInitiated: 0,
            npcDeaths: 0,
            illegalActions: 0,
            intimidations: 0,
            
            // Tech indicators
            craftingAttempts: 0,
            uniqueRecipes: 0,
            baseBuilding: 0,
            contractsCompleted: 0
        };
        
        // Detected route
        this.primaryRoute = null;
        this.routeLocked = false;
        this.detectionThreshold = 70; // Score needed to lock route
        
        // Personality evaluation (for bar opening)
        this.personalityTest = {
            explorationChoices: [],
            npcInteractions: [],
            itemsExamined: [],
            timeSpentExploring: 0
        };
        this.personalityResult = null; // 'good' or 'bad'
        
        // Callbacks
        this.onRouteDetected = null;
        this.onPersonalityDetermined = null;
    }
    
    trackBehavior(data) {
        // Update behavior counts
        if (data.dialogueStarted) this.behaviors.dialogueCount++;
        if (data.friendMade) this.behaviors.friendsMade++;
        if (data.violenceAvoided) this.behaviors.violenceAvoided++;
        if (data.helpfulAction) this.behaviors.helpfulActions++;
        
        if (data.chaosAction) this.behaviors.chaosActions++;
        if (data.randomChoice) this.behaviors.randomChoices++;
        if (data.trolling) this.behaviors.trollingActions++;
        
        if (data.combatInitiated) this.behaviors.combatInitiated++;
        if (data.illegalAction) this.behaviors.illegalActions++;
        if (data.intimidation) this.behaviors.intimidations++;
        
        if (data.craftingAttempts) this.behaviors.craftingAttempts += data.craftingAttempts;
        if (data.uniqueRecipe) this.behaviors.uniqueRecipes++;
        if (data.baseBuilding) this.behaviors.baseBuilding++;
        if (data.contractCompleted) this.behaviors.contractsCompleted++;
        
        // Recalculate route scores
        this.calculateRouteScores();
        
        // Check for route detection
        this.checkRouteDetection();
    }
    
    recordNPCDeath() {
        this.behaviors.npcDeaths++;
        this.adjustRouteScore('criminal', 5);
        this.adjustRouteScore('pacifist', -10);
    }
    
    recordCombat() {
        this.behaviors.combatInitiated++;
        this.adjustRouteScore('criminal', 3);
        this.adjustRouteScore('pacifist', -5);
    }
    
    recordCrafting() {
        this.behaviors.craftingAttempts++;
        this.adjustRouteScore('tech', 2);
    }
    
    recordDialogue(wasHelpful) {
        this.behaviors.dialogueCount++;
        if (wasHelpful) {
            this.behaviors.helpfulActions++;
            this.adjustRouteScore('pacifist', 3);
        }
    }
    
    recordExploration(location, duration) {
        this.personalityTest.explorationChoices.push({ location, duration });
        this.personalityTest.timeSpentExploring += duration;
    }
    
    recordNPCInteraction(npcType, wasPositive) {
        this.personalityTest.npcInteractions.push({ npcType, wasPositive });
        
        if (wasPositive) {
            this.adjustRouteScore('pacifist', 2);
        }
    }
    
    adjustRouteScore(route, amount) {
        if (this.routeLocked) return;
        
        this.routeScores[route] = Math.max(0, Math.min(100, 
            this.routeScores[route] + amount
        ));
    }
    
    calculateRouteScores() {
        // Pacifist score
        this.routeScores.pacifist = Math.min(100, 
            30 + 
            this.behaviors.dialogueCount * 2 +
            this.behaviors.friendsMade * 10 +
            this.behaviors.violenceAvoided * 5 +
            this.behaviors.helpfulActions * 5 -
            this.behaviors.combatInitiated * 10 -
            this.behaviors.npcDeaths * 20
        );
        
        // Insanity score
        this.routeScores.insanity = Math.min(100,
            30 +
            this.behaviors.chaosActions * 5 +
            this.behaviors.randomChoices * 3 +
            this.behaviors.trollingActions * 5
        );
        
        // Criminal score
        this.routeScores.criminal = Math.min(100,
            20 +
            this.behaviors.combatInitiated * 5 +
            this.behaviors.npcDeaths * 15 +
            this.behaviors.illegalActions * 10 +
            this.behaviors.intimidations * 5
        );
        
        // Tech score
        this.routeScores.tech = Math.min(100,
            20 +
            this.behaviors.craftingAttempts * 2 +
            this.behaviors.uniqueRecipes * 10 +
            this.behaviors.baseBuilding * 15 +
            this.behaviors.contractsCompleted * 20
        );
    }
    
    checkRouteDetection() {
        if (this.routeLocked) return;
        
        // Find highest scoring route
        let highestRoute = null;
        let highestScore = 0;
        
        for (const [route, score] of Object.entries(this.routeScores)) {
            if (score > highestScore) {
                highestScore = score;
                highestRoute = route;
            }
        }
        
        // Lock route if threshold reached
        if (highestScore >= this.detectionThreshold) {
            this.primaryRoute = highestRoute;
            this.routeLocked = true;
            
            console.log(`Route detected: ${highestRoute} (Score: ${highestScore})`);
            
            if (this.onRouteDetected) {
                this.onRouteDetected(highestRoute);
            }
        }
    }
    
    // Determine personality for bar opening
    determinePersonality() {
        if (this.personalityResult) return this.personalityResult;
        
        let goodScore = 0;
        let badScore = 0;
        
        // Analyze exploration choices
        this.personalityTest.explorationChoices.forEach(choice => {
            if (choice.location === 'park' || choice.location === 'shop') {
                goodScore += 2;
            } else if (choice.location === 'forest' || choice.location === 'streets') {
                badScore += 1;
            }
        });
        
        // Analyze NPC interactions
        this.personalityTest.npcInteractions.forEach(interaction => {
            if (interaction.wasPositive) {
                goodScore += 3;
            } else {
                badScore += 3;
            }
        });
        
        // Time spent exploring peacefully
        if (this.personalityTest.timeSpentExploring > 300) { // 5 minutes
            goodScore += 5;
        }
        
        // Combat and violence
        goodScore -= this.behaviors.combatInitiated * 5;
        badScore += this.behaviors.combatInitiated * 5;
        
        this.personalityResult = goodScore >= badScore ? 'good' : 'bad';
        
        console.log(`Personality determined: ${this.personalityResult} (Good: ${goodScore}, Bad: ${badScore})`);
        
        if (this.onPersonalityDetermined) {
            this.onPersonalityDetermined(this.personalityResult);
        }
        
        return this.personalityResult;
    }
    
    // Get difficulty modifiers based on route
    getDifficultyModifiers() {
        if (!this.primaryRoute) {
            return {
                dialogueDifficulty: 1,
                sanityPressure: 1,
                combatDifficulty: 1,
                craftingComplexity: 1
            };
        }
        
        switch (this.primaryRoute) {
            case 'pacifist':
                return {
                    dialogueDifficulty: 1.5, // Harder dialogue choices
                    sanityPressure: 1.3, // More brutal sanity system
                    combatDifficulty: 0.8, // Easier combat (not the focus)
                    craftingComplexity: 1
                };
            
            case 'insanity':
                return {
                    dialogueDifficulty: 1,
                    sanityPressure: 1,
                    combatDifficulty: 1,
                    craftingComplexity: 1
                    // Insanity route gets different content, not difficulty changes
                };
            
            case 'criminal':
                return {
                    dialogueDifficulty: 0.8,
                    sanityPressure: 0.8,
                    combatDifficulty: 1.5, // Harder combat
                    craftingComplexity: 1,
                    policeAggression: 1.5 // More aggressive law enforcement
                };
            
            case 'tech':
                return {
                    dialogueDifficulty: 1,
                    sanityPressure: 1,
                    combatDifficulty: 0.8,
                    craftingComplexity: 1.5, // More complex crafting requirements
                    contractRewards: 1.5 // Better contract rewards
                };
            
            default:
                return {
                    dialogueDifficulty: 1,
                    sanityPressure: 1,
                    combatDifficulty: 1,
                    craftingComplexity: 1
                };
        }
    }
    
    // Get Syl's personality traits based on route
    getSylPersonality() {
        if (!this.primaryRoute) {
            return {
                demeanor: 'neutral',
                speechStyle: 'reserved',
                reactions: 'measured'
            };
        }
        
        switch (this.primaryRoute) {
            case 'pacifist':
                return {
                    demeanor: 'gentle',
                    speechStyle: 'thoughtful',
                    reactions: 'empathetic'
                };
            case 'insanity':
                return {
                    demeanor: 'unpredictable',
                    speechStyle: 'erratic',
                    reactions: 'whimsical'
                };
            case 'criminal':
                return {
                    demeanor: 'intimidating',
                    speechStyle: 'blunt',
                    reactions: 'aggressive'
                };
            case 'tech':
                return {
                    demeanor: 'analytical',
                    speechStyle: 'technical',
                    reactions: 'logical'
                };
        }
    }
    
    serialize() {
        return {
            routeScores: this.routeScores,
            behaviors: this.behaviors,
            primaryRoute: this.primaryRoute,
            routeLocked: this.routeLocked,
            personalityTest: this.personalityTest,
            personalityResult: this.personalityResult
        };
    }
    
    deserialize(data) {
        this.routeScores = data.routeScores;
        this.behaviors = data.behaviors;
        this.primaryRoute = data.primaryRoute;
        this.routeLocked = data.routeLocked;
        this.personalityTest = data.personalityTest;
        this.personalityResult = data.personalityResult;
    }
}