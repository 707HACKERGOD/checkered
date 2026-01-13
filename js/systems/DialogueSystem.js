// js/systems/DialogueSystem.js
import { DIALOGUE_TREES } from '../data/dialogueTrees.js';

export class DialogueSystem {
    constructor(npcManager, routeSystem) {
        this.npcManager = npcManager;
        this.routeSystem = routeSystem;
        
        this.currentNPC = null;
        this.currentDialogue = null;
        this.dialogueHistory = [];
        this.interactionCount = 0;
        
        // Personality test dialogue tracking
        this.testResponses = [];
        
        // Store dialogue trees
        this.dialogueTrees = DIALOGUE_TREES;
    }
    
    startDialogue(npc) {
        this.currentNPC = npc;
        this.interactionCount++;
        
        // Get initial dialogue based on relationship AND first meeting status
        const dialogue = this.getDialogueForNPC(npc);
        this.currentDialogue = dialogue;
        
        // Record interaction
        this.dialogueHistory.push({
            npcId: npc.id,
            timestamp: Date.now(),
            dialogueId: dialogue?.id
        });
        
        return dialogue;
    }
    
    getDialogueForNPC(npc) {
        // Check for special first meeting conditions
        const previousInteractions = this.dialogueHistory.filter(h => h.npcId === npc.id);
        const isFirstMeeting = previousInteractions.length === 0;
        
        // For bar owner - special first visit dialogue
        if (npc.id === 'bar_owner' && isFirstMeeting) {
            const personalityResult = this.routeSystem.personalityResult || 'neutral';
            const hasAtticAccess = this.npcManager?.world?.atticUnlocked || false;
            
            if (personalityResult === 'good') {
                return this.dialogueTrees.marcus.firstVisit_good;
            } else {
                return this.dialogueTrees.marcus.firstVisit_bad;
            }
        }
        
        // Get NPC's specific dialogue tree
        let dialogueTree;
        switch (npc.id) {
            case 'bar_owner':
                dialogueTree = this.dialogueTrees.marcus;
                break;
            case 'shopkeeper':
                dialogueTree = this.dialogueTrees.ada;
                break;
            case 'victor':
                dialogueTree = this.dialogueTrees.victor;
                break;
            case 'student_0':
                dialogueTree = this.dialogueTrees.elena;
                break;
            default:
                dialogueTree = this.dialogueTrees.student_generic;
        }
        
        // Get relationship-based dialogue
        const relationshipKey = `relationship_${npc.relationship || 'neutral'}`;
        let dialogue = dialogueTree[relationshipKey];
        
        if (!dialogue) {
            dialogue = dialogueTree['default'];
        }
        
        // If no dialogue found, use fallback
        if (!dialogue) {
            dialogue = {
                text: `${npc.name} has nothing to say right now.`,
                speaker: npc.name,
                choices: [{ text: "[Leave]", next: 'end' }]
            };
        }
        
        return {
            ...dialogue,
            speaker: npc.name,
            npcId: npc.id
        };
    }
    
    processChoice(choice) {
        if (!this.currentNPC) return null;
        
        console.log('Processing dialogue choice:', choice.text);
        
        // Track for route system
        if (choice.helpful !== undefined) {
            this.routeSystem.recordDialogue(choice.helpful);
        }
        
        // Apply relationship effects
        if (choice.relationshipEffect && this.currentNPC.modifyRelationship) {
            this.currentNPC.modifyRelationship(choice.relationshipEffect);
        }
        
        // Apply any dialogue effects
        if (choice.effect) {
            this.applyDialogueEffect(choice.effect);
        }
        
        // Get next dialogue
        if (choice.next && choice.next !== 'end') {
            // Try to get the next dialogue node from the current NPC's dialogue tree
            const nextDialogue = this.getDialogueNode(this.currentNPC.id, choice.next);
            
            if (nextDialogue) {
                return {
                    ...nextDialogue,
                    speaker: this.currentNPC.name,
                    npcId: this.currentNPC.id
                };
            }
        }
        
        // End dialogue
        this.endDialogue();
        return null;
    }
    
    getDialogueNode(npcId, nodeId) {
        // Get the appropriate dialogue tree
        let dialogueTree;
        switch (npcId) {
            case 'bar_owner':
                dialogueTree = this.dialogueTrees.marcus;
                break;
            case 'shopkeeper':
                dialogueTree = this.dialogueTrees.ada;
                break;
            case 'victor':
                dialogueTree = this.dialogueTrees.victor;
                break;
            case 'student_0':
                dialogueTree = this.dialogueTrees.elena;
                break;
            default:
                dialogueTree = this.dialogueTrees.student_generic;
        }
        
        // Return the specific node
        return dialogueTree[nodeId];
    }
    
    applyDialogueEffect(effect) {
        if (!effect) return;
        
        console.log('Applying dialogue effect:', effect.type);
        
        switch (effect.type) {
            case 'unlock_attic':
                if (this.npcManager?.world) {
                    this.npcManager.world.atticUnlocked = true;
                    console.log('Attic unlocked!');
                }
                break;
            case 'open_shop':
                // Would open shop UI
                console.log('Opening shop...');
                break;
            case 'start_quest':
                console.log(`Quest started: ${effect.quest}`);
                break;
        }
    }
    
    endDialogue() {
        this.currentNPC = null;
        this.currentDialogue = null;
        console.log('Dialogue ended');
    }
    
    // ... rest of the class remains the same ...
    
    // Bar owner specific dialogue
    getBarOwnerDialogue(personalityResult, hasAtticAccess) {
        if (personalityResult === 'good') {
            return {
                speaker: 'Marcus',
                text: "Syl... it really is you. After all these years." + 
                      (hasAtticAccess ? "" : " The attic is yours, old friend. It's not much, but it's safe."),
                choices: [
                    { text: "You remember me?", next: 'remember_good' },
                    { text: "Thank you, Marcus.", next: 'thanks', helpful: true }
                ],
                effect: hasAtticAccess ? null : { type: 'unlock_attic' }
            };
        } else {
            return {
                speaker: 'Marcus',
                text: "You. I wasn't sure if I'd ever see you again. Or if I wanted to.",
                choices: [
                    { text: "Things were complicated.", next: 'complicated' },
                    { text: "I don't need your judgment.", next: 'judgment' },
                    { text: "I need a place to stay.", next: 'need_stay' }
                ]
            };
        }
    }
    
    // Generate dynamic dialogue based on game state
    generateContextualDialogue(npc, gameState) {
        const lines = [];
        
        // Comment on time of day
        if (gameState.timePeriod === 'NIGHT' || gameState.timePeriod === 'NIGHT2') {
            lines.push("It's late. What are you doing out at this hour?");
        }
        
        // Comment on player appearance (based on sanity)
        if (gameState.sanity < 30) {
            lines.push("You don't look well. Are you feeling alright?");
        }
        
        // Comment on recent events
        if (gameState.recentPossession) {
            lines.push("There was some commotion earlier. Did you hear anything?");
        }
        
        // Week-based urgency
        if (gameState.week >= 8) {
            lines.push("Time seems to be running out for something. Can you feel it too?");
        }
        
        return lines[Math.floor(Math.random() * lines.length)] || null;
    }
    
    serialize() {
        return {
            dialogueHistory: this.dialogueHistory,
            interactionCount: this.interactionCount,
            testResponses: this.testResponses
        };
    }
    
    deserialize(data) {
        this.dialogueHistory = data.dialogueHistory || [];
        this.interactionCount = data.interactionCount || 0;
        this.testResponses = data.testResponses || [];
    }
}