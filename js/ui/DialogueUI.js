// js/ui/DialogueUI.js - FIXED
export class DialogueUI {
    constructor(uiManager) {
        this.uiManager = uiManager;
        this.currentDialogue = null;
        this.currentNPC = null;
        this.onComplete = null;
        this.isOpen = false;
    }
    
    show(dialogue, npc, onComplete = null) {
        if (!dialogue) {
            console.error('No dialogue provided');
            if (onComplete) onComplete();
            return;
        }
        
        this.currentDialogue = dialogue;
        this.currentNPC = npc;
        this.onComplete = onComplete;
        this.isOpen = true;
        
        const panel = document.getElementById('dialogue-panel');
        if (!panel) {
            console.error('Dialogue panel not found');
            return;
        }
        
        panel.classList.remove('hidden');
        
        // Update speaker name
        const speakerEl = document.getElementById('dialogue-speaker');
        if (speakerEl) {
            speakerEl.textContent = dialogue.speaker || npc?.name || '???';
        }
        
        // Update dialogue text (immediate, no typewriter)
        const textEl = document.getElementById('dialogue-text');
        if (textEl) {
            textEl.textContent = dialogue.text || '';
        }
        
        // Update choices
        this.updateChoices(dialogue.choices);
        
        // Update portrait placeholder
        const portrait = document.getElementById('dialogue-portrait');
        if (portrait) {
            portrait.style.background = this.getPortraitColor(npc);
            portrait.innerHTML = `<div style="display:flex;align-items:center;justify-content:center;height:100%;font-size:3rem;color:#666;">${(npc?.name || '?')[0]}</div>`;
        }
        
        console.log('Dialogue shown:', dialogue.text);
    }
    
    hide() {
        const panel = document.getElementById('dialogue-panel');
        if (panel) {
            panel.classList.add('hidden');
        }
        
        this.isOpen = false;
        this.currentDialogue = null;
        this.currentNPC = null;
        
        if (this.onComplete) {
            const callback = this.onComplete;
            this.onComplete = null;
            callback();
        }
    }
    
    updateChoices(choices) {
        const choicesDiv = document.getElementById('dialogue-choices');
        if (!choicesDiv) return;
        
        choicesDiv.innerHTML = '';
        
        if (choices && choices.length > 0) {
            choices.forEach((choice, index) => {
                const btn = document.createElement('button');
                btn.className = 'dialogue-choice';
                btn.textContent = choice.text;
                btn.addEventListener('click', () => this.selectChoice(choice, index));
                choicesDiv.appendChild(btn);
            });
        } else {
            // Continue button
            const btn = document.createElement('button');
            btn.className = 'dialogue-choice';
            btn.textContent = '[Continue]';
            btn.addEventListener('click', () => this.hide());
            choicesDiv.appendChild(btn);
        }
    }
    
    selectChoice(choice, index) {
        console.log('Selected choice:', choice.text);
        
        // Play sound
        if (this.uiManager.game.audioManager) {
            this.uiManager.game.audioManager.playSound('click');
        }
        
        // Track for route system
        if (this.uiManager.game.routeSystem) {
            this.uiManager.game.routeSystem.recordDialogue(choice.helpful || false);
        }
        
        // Apply relationship effect
        if (choice.relationshipEffect && this.currentNPC) {
            this.currentNPC.modifyRelationship(choice.relationshipEffect, 'dialogue');
        }
        
        // Apply other effects
        if (choice.effect) {
            this.applyEffect(choice.effect);
        }
        
        // Get next dialogue if specified
        if (choice.next && this.currentNPC && this.currentNPC.dialogueTree) {
            const nextDialogue = this.currentNPC.dialogueTree[choice.next];
            if (nextDialogue) {
                this.show(
                    { ...nextDialogue, speaker: this.currentNPC.name },
                    this.currentNPC,
                    this.onComplete
                );
                return;
            }
        }
        
        // Close dialogue
        this.hide();
    }
    
    applyEffect(effect) {
        const game = this.uiManager.game;
        
        switch (effect.type) {
            case 'unlock_attic':
                if (game.world) {
                    game.world.atticUnlocked = true;
                }
                this.uiManager.showNotification('Attic access granted!');
                break;
                
            case 'start_quest':
                this.uiManager.showNotification(`Quest started: ${effect.quest}`);
                break;
                
            case 'give_item':
                if (game.craftingSystem && effect.item) {
                    game.craftingSystem.addItem(effect.item);
                    this.uiManager.showNotification(`Received: ${effect.item.name}`);
                }
                break;
                
            case 'open_shop':
                this.uiManager.showNotification('Shop interface coming soon!');
                break;
        }
    }
    
    getPortraitColor(npc) {
        if (!npc) return '#333';
        
        const colors = {
            'Marcus': 'linear-gradient(135deg, #4a3030, #2a1a1a)',
            'Victor': 'linear-gradient(135deg, #800020, #400010)',
            'Ada': 'linear-gradient(135deg, #556b2f, #2a3a1a)',
            'Elena': 'linear-gradient(135deg, #4169e1, #203080)'
        };
        
        return colors[npc.name] || 'linear-gradient(135deg, #444, #222)';
    }
}