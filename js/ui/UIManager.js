// js/ui/UIManager.js - RESTORED with all UI functionality built-in
export class UIManager {
    constructor(game) {
        this.game = game;
        
        // Cache DOM elements
        this.elements = {
            sanityFill: document.getElementById('sanity-fill'),
            sanityValue: document.getElementById('sanity-value'),
            comfortFill: document.getElementById('comfort-fill'),
            comfortValue: document.getElementById('comfort-value'),
            currentDay: document.getElementById('current-day'),
            currentWeek: document.getElementById('current-week'),
            timePeriod: document.getElementById('time-period'),
            cameraModeText: document.getElementById('camera-mode-text'),
            interactionPrompt: document.getElementById('interaction-prompt'),
            interactionText: document.getElementById('interaction-text'),
            possessionWarning: document.getElementById('possession-warning'),
            possessionProgress: document.getElementById('possession-progress'),
            phantomCount: document.getElementById('phantom-count'),
            inventoryPanel: document.getElementById('inventory-panel'),
            inventoryGrid: document.getElementById('inventory-grid'),
            craftingPanel: document.getElementById('crafting-panel'),
            healthPanel: document.getElementById('health-panel'),
            calendarPanel: document.getElementById('calendar-panel'),
            dialoguePanel: document.getElementById('dialogue-panel'),
            narrativeOverlay: document.getElementById('narrative-overlay')
        };
        
        this.activePanel = null;
        
        // Crafting state
        this.craftSlot1 = null; // { item, inventoryIndex }
        this.craftSlot2 = null;
        
        // Selected inventory slot
        this.selectedInventorySlot = null;
        
        // Dialogue state
        this.currentDialogueNPC = null;
        this.dialogueCallback = null;
        
        this.initializeUI();
    }
    
    initializeUI() {
        this.createInventorySlots();
        this.createCalendarGrid();
        this.setupCraftingUI();
        this.setupHealthUI();
        this.setupCloseButtons();
    }
    
    setupCloseButtons() {
        document.querySelectorAll('.close-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const panel = e.target.closest('.panel');
                if (panel) {
                    panel.classList.add('hidden');
                    this.activePanel = null;
                    // Re-lock pointer when closing panels
                    if (this.game.gameState === 'playing') {
                        document.getElementById('game-canvas').requestPointerLock();
                    }
                }
            });
        });
    }
    
    // ===================
    // INVENTORY UI
    // ===================
    
    createInventorySlots() {
        const grid = this.elements.inventoryGrid;
        if (!grid) return;
        
        grid.innerHTML = '';
        
        for (let i = 0; i < 40; i++) {
            const slot = document.createElement('div');
            slot.className = 'inventory-slot';
            slot.dataset.slot = i;
            slot.draggable = false;
            
            // Click to select
            slot.addEventListener('click', () => this.onInventorySlotClick(i));
            
            // Drag and drop
            slot.addEventListener('dragstart', (e) => this.onInventoryDragStart(e, i));
            slot.addEventListener('dragover', (e) => e.preventDefault());
            slot.addEventListener('drop', (e) => this.onInventoryDrop(e, i));
            
            grid.appendChild(slot);
        }
    }
    
    updateInventoryDisplay() {
        const grid = this.elements.inventoryGrid;
        if (!grid) return;
        
        const inventory = this.game.craftingSystem.inventory;
        const slots = grid.querySelectorAll('.inventory-slot');
        
        slots.forEach((slot, index) => {
            slot.innerHTML = '';
            slot.classList.remove('selected');
            slot.draggable = false;
            
            const item = inventory[index];
            if (item) {
                const colorClass = this.getItemColorClass(item);
                slot.innerHTML = `
                    <div class="item-icon ${colorClass}" title="${item.name}">
                        ${item.name.substring(0, 2).toUpperCase()}
                    </div>
                    ${(item.quantity || 1) > 1 ? `<span class="item-count">${item.quantity}</span>` : ''}
                `;
                slot.draggable = true;
            }
            
            if (index === this.selectedInventorySlot) {
                slot.classList.add('selected');
            }
        });
    }
    
    getItemColorClass(item) {
        if (!item.attributes) return '';
        if (item.attributes.includes('weapon') || item.attributes.includes('sharp')) return 'item-weapon';
        if (item.attributes.includes('healing')) return 'item-healing';
        if (item.attributes.includes('flammable')) return 'item-flammable';
        if (item.attributes.includes('trap_component')) return 'item-trap';
        return '';
    }
    
    onInventorySlotClick(index) {
        const inventory = this.game.craftingSystem.inventory;
        const item = inventory[index];
        
        this.selectedInventorySlot = index;
        this.updateInventoryDisplay();
        
        if (item) {
            this.showItemDetails(item);
        } else {
            this.clearItemDetails();
        }
    }
    
    showItemDetails(item) {
        const nameEl = document.getElementById('item-name');
        const descEl = document.getElementById('item-description');
        const attrsEl = document.getElementById('item-attributes');
        
        if (nameEl) nameEl.textContent = item.name;
        if (descEl) descEl.textContent = item.description || 'No description available.';
        
        if (attrsEl) {
            attrsEl.innerHTML = '';
            if (item.attributes && item.attributes.length > 0) {
                item.attributes.forEach(attr => {
                    const tag = document.createElement('span');
                    tag.className = 'attribute-tag';
                    tag.textContent = attr.replace(/_/g, ' ');
                    attrsEl.appendChild(tag);
                });
            }
        }
    }
    
    clearItemDetails() {
        const nameEl = document.getElementById('item-name');
        const descEl = document.getElementById('item-description');
        const attrsEl = document.getElementById('item-attributes');
        
        if (nameEl) nameEl.textContent = 'Select an item';
        if (descEl) descEl.textContent = '';
        if (attrsEl) attrsEl.innerHTML = '';
    }
    
    onInventoryDragStart(e, index) {
        const inventory = this.game.craftingSystem.inventory;
        if (!inventory[index]) {
            e.preventDefault();
            return;
        }
        e.dataTransfer.setData('text/plain', JSON.stringify({ source: 'inventory', index }));
        e.dataTransfer.effectAllowed = 'move';
    }
    
    onInventoryDrop(e, targetIndex) {
        e.preventDefault();
        try {
            const data = JSON.parse(e.dataTransfer.getData('text/plain'));
            if (data.source === 'inventory') {
                // Swap items
                const inventory = this.game.craftingSystem.inventory;
                const temp = inventory[targetIndex];
                inventory[targetIndex] = inventory[data.index];
                inventory[data.index] = temp;
                this.updateInventoryDisplay();
            }
        } catch (err) {
            console.error('Drop error:', err);
        }
    }
    
    // ===================
    // CRAFTING UI
    // ===================
    
    setupCraftingUI() {
        const slot1 = document.getElementById('craft-slot-1');
        const slot2 = document.getElementById('craft-slot-2');
        const craftBtn = document.getElementById('craft-btn');
        
        if (slot1) {
            slot1.addEventListener('dragover', (e) => e.preventDefault());
            slot1.addEventListener('drop', (e) => this.onCraftSlotDrop(e, 1));
            slot1.addEventListener('click', () => this.onCraftSlotClick(1));
        }
        
        if (slot2) {
            slot2.addEventListener('dragover', (e) => e.preventDefault());
            slot2.addEventListener('drop', (e) => this.onCraftSlotDrop(e, 2));
            slot2.addEventListener('click', () => this.onCraftSlotClick(2));
        }
        
        if (craftBtn) {
            craftBtn.addEventListener('click', () => this.performCraft());
        }
    }
    
    onCraftSlotDrop(e, slotNum) {
        e.preventDefault();
        try {
            const data = JSON.parse(e.dataTransfer.getData('text/plain'));
            if (data.source === 'inventory') {
                const inventory = this.game.craftingSystem.inventory;
                const item = inventory[data.index];
                
                if (item) {
                    if (slotNum === 1) {
                        this.craftSlot1 = { item: { ...item }, inventoryIndex: data.index };
                    } else {
                        this.craftSlot2 = { item: { ...item }, inventoryIndex: data.index };
                    }
                    this.updateCraftingDisplay();
                }
            }
        } catch (err) {
            console.error('Craft drop error:', err);
        }
    }
    
    onCraftSlotClick(slotNum) {
        // If slot has item, remove it
        if (slotNum === 1 && this.craftSlot1) {
            this.craftSlot1 = null;
            this.updateCraftingDisplay();
        } else if (slotNum === 2 && this.craftSlot2) {
            this.craftSlot2 = null;
            this.updateCraftingDisplay();
        } else {
            // If slot is empty and we have a selected inventory item, add it
            if (this.selectedInventorySlot !== null) {
                const inventory = this.game.craftingSystem.inventory;
                const item = inventory[this.selectedInventorySlot];
                
                if (item) {
                    if (slotNum === 1 && !this.craftSlot1) {
                        this.craftSlot1 = { item: { ...item }, inventoryIndex: this.selectedInventorySlot };
                    } else if (slotNum === 2 && !this.craftSlot2) {
                        this.craftSlot2 = { item: { ...item }, inventoryIndex: this.selectedInventorySlot };
                    }
                    this.updateCraftingDisplay();
                }
            }
        }
    }
    
    updateCraftingDisplay() {
        const slot1El = document.getElementById('craft-slot-1');
        const slot2El = document.getElementById('craft-slot-2');
        const resultEl = document.getElementById('craft-result');
        const craftBtn = document.getElementById('craft-btn');
        const previewEl = document.getElementById('craft-preview');
        
        // Clear all slots first
        if (slot1El) {
            slot1El.innerHTML = '';
            slot1El.classList.remove('filled');
        }
        if (slot2El) {
            slot2El.innerHTML = '';
            slot2El.classList.remove('filled');
        }
        
        // Update slot 1 if it has an item
        if (this.craftSlot1 && slot1El) {
            const item = this.craftSlot1.item;
            slot1El.innerHTML = `
                <div class="craft-item-icon">${item.name.substring(0, 3)}</div>
                <span class="slot-label">${item.name}</span>
                <button class="clear-slot" style="position:absolute;top:2px;right:2px;background:#8b0000;border:none;color:white;width:12px;height:12px;font-size:8px;line-height:8px;cursor:pointer;">×</button>
            `;
            slot1El.classList.add('filled');
            
            // Add click handler to clear button
            slot1El.querySelector('.clear-slot')?.addEventListener('click', (e) => {
                e.stopPropagation();
                this.craftSlot1 = null;
                this.updateCraftingDisplay();
            });
        } else if (slot1El) {
            slot1El.innerHTML = '<span class="slot-label">Drag item here or click</span>';
        }
        
        // Update slot 2 if it has an item
        if (this.craftSlot2 && slot2El) {
            const item = this.craftSlot2.item;
            slot2El.innerHTML = `
                <div class="craft-item-icon">${item.name.substring(0, 3)}</div>
                <span class="slot-label">${item.name}</span>
                <button class="clear-slot" style="position:absolute;top:2px;right:2px;background:#8b0000;border:none;color:white;width:12px;height:12px;font-size:8px;line-height:8px;cursor:pointer;">×</button>
            `;
            slot2El.classList.add('filled');
            
            // Add click handler to clear button
            slot2El.querySelector('.clear-slot')?.addEventListener('click', (e) => {
                e.stopPropagation();
                this.craftSlot2 = null;
                this.updateCraftingDisplay();
            });
        } else if (slot2El) {
            slot2El.innerHTML = '<span class="slot-label">Drag item here or click</span>';
        }
        
        // Update preview if at least one item is selected
        if (this.craftSlot1) {
            const preview = this.game.craftingSystem.previewCraft(
                this.craftSlot1.item.id,
                this.craftSlot2 ? this.craftSlot2.item.id : null
            );
            
            if (preview && resultEl) {
                resultEl.innerHTML = `
                    <div class="craft-item-icon preview">${preview.name.substring(0, 3)}</div>
                    <span class="slot-label">${preview.name}</span>
                `;
                resultEl.classList.add('ready');
            }
            
            if (preview && previewEl) {
                previewEl.innerHTML = `
                    <strong>${preview.name}</strong><br>
                    <em>${preview.description}</em>
                    ${preview.predictedAttributes ? `<br><br><small>Attributes: ${preview.predictedAttributes.join(', ')}</small>` : ''}
                `;
            }
            
            if (craftBtn) craftBtn.disabled = false;
        } else {
            if (resultEl) {
                resultEl.innerHTML = '<span class="slot-label">Result</span>';
                resultEl.classList.remove('ready');
            }
            if (previewEl) previewEl.innerHTML = 'Select items to see preview';
            if (craftBtn) craftBtn.disabled = true;
        }
    }
    
    performCraft() {
        if (!this.craftSlot1) return;
        
        const result = this.game.craftingSystem.craft(
            this.craftSlot1.item.id,
            this.craftSlot2 ? this.craftSlot2.item.id : null
        );
        
        if (result) {
            this.showNotification(`Crafted: ${result.name}`, 'success');
            this.game.audioManager.playSound('craft');
            
            // Clear craft slots
            this.craftSlot1 = null;
            this.craftSlot2 = null;
            this.updateCraftingDisplay();
            this.updateInventoryDisplay();
        } else {
            this.showNotification('Crafting failed!', 'error');
        }
    }
    
    clearCraftingSlots() {
        this.craftSlot1 = null;
        this.craftSlot2 = null;
        this.updateCraftingDisplay();
    }

    // Add this method to UIManager class:
    selectItemForCrafting(item, index) {
        // If slot 1 is empty, fill it
        if (!this.craftSlot1) {
            this.craftSlot1 = { item: { ...item }, inventoryIndex: index };
        } 
        // If slot 1 is filled but slot 2 is empty, fill slot 2
        else if (!this.craftSlot2) {
            this.craftSlot2 = { item: { ...item }, inventoryIndex: index };
        }
        // If both are filled, replace slot 1
        else {
            this.craftSlot1 = { item: { ...item }, inventoryIndex: index };
        }
        this.updateCraftingDisplay();
    }
    
    // ===================
    // HEALTH UI
    // ===================
    
    setupHealthUI() {
        const bodyParts = document.querySelectorAll('.body-part');
        
        bodyParts.forEach(part => {
            part.addEventListener('click', () => {
                this.showPartStatus(part.dataset.part);
            });
        });
    }
    
    updateHealthDisplay() {
        const statuses = this.game.healthSystem.getAllPartStatuses();
        
        Object.entries(statuses).forEach(([part, status]) => {
            const element = document.querySelector(`[data-part="${part}"]`);
            if (!element) return;
            
            element.classList.remove('healthy', 'damaged', 'critical', 'missing');
            
            if (status.missing) {
                element.classList.add('missing');
            } else if (status.health <= 0) {
                element.classList.add('critical');
            } else if (status.health < 50) {
                element.classList.add('damaged');
            } else {
                element.classList.add('healthy');
            }
        });
    }
    
    showPartStatus(partName) {
        const status = this.game.healthSystem.getPartStatus(partName);
        if (!status) return;
        
        const formattedName = partName.split('-').map(
            word => word.charAt(0).toUpperCase() + word.slice(1)
        ).join(' ');
        
        const nameEl = document.getElementById('part-name');
        const fillEl = document.getElementById('part-health-fill');
        const condEl = document.getElementById('part-condition');
        
        if (nameEl) nameEl.textContent = formattedName;
        
        if (fillEl) {
            fillEl.style.width = `${status.health}%`;
            if (status.missing) {
                fillEl.style.background = '#333';
            } else if (status.health > 60) {
                fillEl.style.background = '#228b22';
            } else if (status.health > 30) {
                fillEl.style.background = '#daa520';
            } else {
                fillEl.style.background = '#8b0000';
            }
        }
        
        if (condEl) {
            let condition = 'Healthy';
            if (status.missing) {
                condition = 'MISSING - Cannot regenerate';
            } else if (status.health <= 0) {
                condition = 'CRIPPLED - Severely impaired';
            } else if (status.health < 30) {
                condition = 'CRITICAL - Needs attention';
            } else if (status.health < 60) {
                condition = 'DAMAGED - Reduced function';
            }
            condEl.textContent = condition;
        }
    }
    
    // ===================
    // CALENDAR UI
    // ===================
    
    createCalendarGrid() {
        const grid = document.getElementById('calendar-grid');
        if (!grid) return;
        
        grid.innerHTML = '';
        
        // Day headers
        const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
        days.forEach(day => {
            const header = document.createElement('div');
            header.className = 'day-label';
            header.textContent = day;
            grid.appendChild(header);
        });
        
        // 70 day cells (10 weeks)
        for (let i = 1; i <= 70; i++) {
            const cell = document.createElement('div');
            cell.className = 'calendar-day';
            cell.dataset.day = i;
            cell.innerHTML = `<span>${i}</span>`;
            cell.addEventListener('click', () => this.onCalendarDayClick(i));
            grid.appendChild(cell);
        }
    }
    
    updateCalendarDisplay(currentDay) {
        const cells = document.querySelectorAll('.calendar-day');
        cells.forEach(cell => {
            const day = parseInt(cell.dataset.day);
            cell.classList.remove('current', 'past');
            
            if (day === currentDay) {
                cell.classList.add('current');
            } else if (day < currentDay) {
                cell.classList.add('past');
            }
        });
        
        // Update weeks remaining
        const weeksEl = document.getElementById('weeks-remaining');
        if (weeksEl) {
            weeksEl.textContent = Math.max(0, 10 - Math.ceil(currentDay / 7));
        }
    }
    
    onCalendarDayClick(day) {
        const titleEl = document.getElementById('selected-day-title');
        const eventsEl = document.getElementById('day-events');
        
        if (titleEl) titleEl.textContent = `Day ${day}`;
        
        if (eventsEl) {
            const events = this.game.calendarSystem.getEventsForDay(day);
            if (events.length > 0) {
                eventsEl.innerHTML = events.map(e => `
                    <div class="day-event">
                        <span class="event-type">${e.type}</span>: ${e.description}
                    </div>
                `).join('');
            } else {
                eventsEl.innerHTML = '<p class="no-events">No recorded events</p>';
            }
        }
    }
    
    // ===================
    // DIALOGUE UI
    // ===================
    
    showDialogue(dialogueData, npc, onComplete = null) {
        if (!dialogueData) {
            console.error('No dialogue data provided');
            if (onComplete) onComplete();
            return;
        }
        
        this.game.gameState = 'dialogue';
        document.exitPointerLock();
        
        this.currentDialogueNPC = npc;
        this.dialogueCallback = onComplete;
        
        const panel = this.elements.dialoguePanel;
        if (!panel) return;
        
        panel.classList.remove('hidden');
        
        // Update speaker
        const speakerEl = document.getElementById('dialogue-speaker');
        if (speakerEl) {
            speakerEl.textContent = dialogueData.speaker || npc?.name || '???';
        }
        
        // Update text
        const textEl = document.getElementById('dialogue-text');
        if (textEl) {
            textEl.textContent = dialogueData.text || '';
        }
        
        // Update portrait
        const portraitEl = document.getElementById('dialogue-portrait');
        if (portraitEl && npc) {
            portraitEl.style.background = this.getNPCPortraitColor(npc);
            portraitEl.innerHTML = `<div style="display:flex;align-items:center;justify-content:center;height:100%;font-size:48px;color:#888;">${npc.name[0]}</div>`;
        }
        
        // Update choices
        this.updateDialogueChoices(dialogueData.choices);
    }
    
    getNPCPortraitColor(npc) {
        const colors = {
            'Marcus': 'linear-gradient(135deg, #4a3030, #2a1a1a)',
            'Victor': 'linear-gradient(135deg, #800020, #400010)',
            'Ada': 'linear-gradient(135deg, #556b2f, #2a3a1a)',
            'Elena': 'linear-gradient(135deg, #4169e1, #203080)'
        };
        return colors[npc?.name] || 'linear-gradient(135deg, #444, #222)';
    }
    
    updateDialogueChoices(choices) {
        const choicesEl = document.getElementById('dialogue-choices');
        if (!choicesEl) return;
        
        choicesEl.innerHTML = '';
        
        if (choices && choices.length > 0) {
            choices.forEach((choice, index) => {
                const btn = document.createElement('button');
                btn.className = 'dialogue-choice';
                btn.textContent = choice.text;
                btn.addEventListener('click', () => this.selectDialogueChoice(choice, index));
                choicesEl.appendChild(btn);
            });
        } else {
            const btn = document.createElement('button');
            btn.className = 'dialogue-choice';
            btn.textContent = '[Continue]';
            btn.addEventListener('click', () => this.hideDialogue());
            choicesEl.appendChild(btn);
        }
    }
    
    selectDialogueChoice(choice, index) {
        this.game.audioManager.playSound('click');
        
        // Track for route system
        this.game.routeSystem.recordDialogue(choice.helpful || false);
        
        // Apply relationship effect
        if (choice.relationshipEffect && this.currentDialogueNPC) {
            this.currentDialogueNPC.modifyRelationship(choice.relationshipEffect, 'dialogue');
        }
        
        // Apply other effects
        if (choice.effect) {
            this.applyDialogueEffect(choice.effect);
        }
        
        // Continue to next dialogue
        if (choice.next && this.currentDialogueNPC && this.currentDialogueNPC.dialogueTree) {
            const nextDialogue = this.currentDialogueNPC.dialogueTree[choice.next];
            if (nextDialogue) {
                this.showDialogue(
                    { ...nextDialogue, speaker: this.currentDialogueNPC.name },
                    this.currentDialogueNPC,
                    this.dialogueCallback
                );
                return;
            }
        }
        
        this.hideDialogue();
    }
    
    applyDialogueEffect(effect) {
        switch (effect.type) {
            case 'unlock_attic':
                this.game.world.atticUnlocked = true;
                this.showNotification('Attic access granted!');
                break;
            case 'start_quest':
                this.showNotification(`Quest started: ${effect.quest}`);
                break;
            case 'give_item':
                if (effect.item) {
                    this.game.craftingSystem.addItem(effect.item);
                    this.showNotification(`Received: ${effect.item.name}`);
                }
                break;
            case 'open_shop':
                this.showNotification('Shop coming soon!');
                break;
        }
    }
    
    hideDialogue() {
        const panel = this.elements.dialoguePanel;
        if (panel) {
            panel.classList.add('hidden');
        }
        
        this.currentDialogueNPC = null;
        
        // Return to gameplay
        this.game.gameState = 'playing';
        document.getElementById('game-canvas').requestPointerLock();
        
        if (this.dialogueCallback) {
            const cb = this.dialogueCallback;
            this.dialogueCallback = null;
            cb();
        }
    }
    
    // ===================
    // PANEL TOGGLES
    // ===================
    
    toggleInventory() {
        this.togglePanel('inventory');
        if (this.activePanel === 'inventory') {
            this.updateInventoryDisplay();
        }
    }
    
    toggleCrafting() {
        this.togglePanel('crafting');
        if (this.activePanel === 'crafting') {
            this.updateInventoryDisplay(); // Show inventory for dragging
            this.updateCraftingDisplay();
        }
    }
    
    toggleHealth() {
        this.togglePanel('health');
        if (this.activePanel === 'health') {
            this.updateHealthDisplay();
        }
    }
    
    toggleCalendar() {
        this.togglePanel('calendar');
        if (this.activePanel === 'calendar') {
            this.updateCalendarDisplay(this.game.calendarSystem.currentDay);
        }
    }
    
    togglePanel(panelName) {
        const panelEl = this.elements[`${panelName}Panel`];
        if (!panelEl) return;
        
        if (this.activePanel === panelName) {
            // Close panel
            panelEl.classList.add('hidden');
            this.activePanel = null;
            
            // Re-lock pointer
            if (this.game.gameState === 'playing') {
                document.getElementById('game-canvas').requestPointerLock();
            }
        } else {
            // Close any open panel
            if (this.activePanel) {
                const currentPanel = this.elements[`${this.activePanel}Panel`];
                if (currentPanel) currentPanel.classList.add('hidden');
            }
            
            // Open new panel
            panelEl.classList.remove('hidden');
            this.activePanel = panelName;
            
            // Release pointer
            document.exitPointerLock();
        }
    }
    
    closeAllPanels() {
        ['inventory', 'crafting', 'health', 'calendar'].forEach(name => {
            const panel = this.elements[`${name}Panel`];
            if (panel) panel.classList.add('hidden');
        });
        this.activePanel = null;
    }
    
    // ===================
    // NARRATIVE OVERLAY
    // ===================
    
    async showNarrative(texts, onComplete = null) {
        const overlay = this.elements.narrativeOverlay;
        const textEl = document.getElementById('narrative-text');
        const continueBtn = document.getElementById('narrative-continue');
        
        if (!overlay || !textEl || !continueBtn) {
            if (onComplete) onComplete();
            return;
        }
        
        overlay.classList.remove('hidden');
        
        for (const text of texts) {
            textEl.innerHTML = text;
            continueBtn.classList.remove('hidden');
            
            await new Promise(resolve => {
                const handler = () => {
                    this.game.audioManager.playSound('click');
                    continueBtn.removeEventListener('click', handler);
                    resolve();
                };
                continueBtn.addEventListener('click', handler);
            });
        }
        
        overlay.classList.add('hidden');
        
        if (onComplete) onComplete();
    }
    
    // ===================
    // HUD UPDATES
    // ===================
    
    update(data) {
        // Sanity bar
        if (this.elements.sanityFill) {
            this.elements.sanityFill.style.width = `${data.sanity}%`;
        }
        if (this.elements.sanityValue) {
            this.elements.sanityValue.textContent = Math.round(data.sanity);
        }
        
        // Comfort bar
        if (this.elements.comfortFill) {
            this.elements.comfortFill.style.width = `${data.comfort}%`;
        }
        if (this.elements.comfortValue) {
            this.elements.comfortValue.textContent = Math.round(data.comfort);
        }
        
        // Time display
        if (this.elements.currentDay) {
            this.elements.currentDay.textContent = `Day ${data.day}`;
        }
        if (this.elements.currentWeek) {
            this.elements.currentWeek.textContent = `Week ${data.week}`;
        }
        if (this.elements.timePeriod) {
            this.elements.timePeriod.textContent = data.timePeriod;
        }
        
        // Apply sanity visual effects to game container
        this.applySanityEffects(data.sanity);
    }
    
    applySanityEffects(sanity) {
        const container = document.getElementById('game-container');
        if (!container) return;
        
        container.classList.remove('insanity-low', 'insanity-medium', 'insanity-high');
        
        if (sanity < 20) {
            container.classList.add('insanity-high');
        } else if (sanity < 40) {
            container.classList.add('insanity-medium');
        } else if (sanity < 60) {
            container.classList.add('insanity-low');
        }
    }
    
    updateCameraMode(mode) {
        if (this.elements.cameraModeText) {
            this.elements.cameraModeText.textContent = mode === 'first' ? 'First Person' : 'Third Person';
        }
    }
    
    // ===================
    // INTERACTION PROMPTS
    // ===================
    
    showInteractionPrompt(text) {
        if (this.elements.interactionText) {
            this.elements.interactionText.textContent = text;
        }
        if (this.elements.interactionPrompt) {
            this.elements.interactionPrompt.classList.remove('hidden');
        }
    }
    
    hideInteractionPrompt() {
        if (this.elements.interactionPrompt) {
            this.elements.interactionPrompt.classList.add('hidden');
        }
    }
    
    // ===================
    // POSSESSION UI
    // ===================
    
    showPossessionWarning() {
        if (this.elements.possessionWarning) {
            this.elements.possessionWarning.classList.remove('hidden');
        }
    }
    
    hidePossessionWarning() {
        if (this.elements.possessionWarning) {
            this.elements.possessionWarning.classList.add('hidden');
        }
    }
    
    updatePossessionUI(status) {
        // Update warning progress
        if (this.elements.possessionProgress) {
            this.elements.possessionProgress.style.width = `${status.warningProgress * 100}%`;
        }
        
        // Update phantom count
        if (this.elements.phantomCount) {
            this.elements.phantomCount.textContent = status.phantomCount;
        }
        
        // Update timer
        const timerFill = document.getElementById('possession-timer-fill');
        if (timerFill && status.maxDuration > 0) {
            const remaining = (status.maxDuration - status.duration) / status.maxDuration * 100;
            timerFill.style.width = `${remaining}%`;
        }
    }
    
    // ===================
    // NOTIFICATIONS
    // ===================
    
    showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        notification.className = `game-notification notification-${type}`;
        notification.textContent = message;
        
        Object.assign(notification.style, {
            position: 'fixed',
            bottom: '100px',
            right: '20px',
            background: type === 'error' ? 'rgba(139, 0, 0, 0.95)' : 'rgba(20, 60, 20, 0.95)',
            border: `2px solid ${type === 'error' ? '#ff4444' : '#44ff44'}`,
            padding: '12px 20px',
            color: '#e0d5c7',
            fontFamily: "'Courier New', monospace",
            fontSize: '14px',
            zIndex: '500',
            animation: 'fadeIn 0.3s ease'
        });
        
        document.body.appendChild(notification);
        
        setTimeout(() => {
            notification.style.opacity = '0';
            notification.style.transition = 'opacity 0.3s';
            setTimeout(() => notification.remove(), 300);
        }, 3000);
    }
    
    // ===================
    // PAUSE MENU
    // ===================
    
    showPauseMenu() {
        let menu = document.getElementById('pause-menu');
        
        if (!menu) {
            menu = document.createElement('div');
            menu.id = 'pause-menu';
            menu.innerHTML = `
                <div style="text-align:center;">
                    <h1 style="color:#ff4444; margin-bottom:30px;">PAUSED</h1>
                    <button id="resume-btn" class="menu-btn" style="margin:10px;">Resume</button><br>
                    <button id="save-btn" class="menu-btn" style="margin:10px;">Save Game</button><br>
                    <button id="quit-btn" class="menu-btn" style="margin:10px;">Quit to Title</button>
                </div>
            `;
            
            Object.assign(menu.style, {
                position: 'fixed',
                top: '0',
                left: '0',
                width: '100%',
                height: '100%',
                background: 'rgba(0, 0, 0, 0.9)',
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'center',
                zIndex: '1000'
            });
            
            document.body.appendChild(menu);
            
            document.getElementById('resume-btn').addEventListener('click', () => {
                this.game.togglePause();
            });
            
            document.getElementById('save-btn').addEventListener('click', () => {
                this.game.saveGame();
            });
            
            document.getElementById('quit-btn').addEventListener('click', () => {
                location.reload();
            });
        }
        
        menu.classList.remove('hidden');
        menu.style.display = 'flex';
    }
    
    hidePauseMenu() {
        const menu = document.getElementById('pause-menu');
        if (menu) {
            menu.classList.add('hidden');
            menu.style.display = 'none';
        }
    }
}