// js/main.js
import * as THREE from 'three';
import { Renderer } from './engine/Renderer.js';
import { InputManager } from './engine/InputManager.js';
import { AudioManager } from './engine/AudioManager.js';
import { World } from './world/World.js';
import { Environment } from './world/Environment.js';
import { TimeManager } from './world/TimeManager.js';
import { Player } from './character/Player.js';
import { NPCManager } from './character/NPCManager.js';
import { CraftingSystem } from './systems/CraftingSystem.js';
import { STARTING_INVENTORY } from './data/items.js';
import { DIALOGUE_DATA } from './data/dialogue.js';
import { NPC_DATA } from './data/npcs.js';
import { SanitySystem } from './systems/SanitySystem.js';
import { PossessionSystem } from './systems/PossessionSystem.js';
import { HealthSystem } from './systems/HealthSystem.js';
import { CalendarSystem } from './systems/CalendarSystem.js';
import { RouteSystem } from './systems/RouteSystem.js';
import { DialogueSystem } from './systems/DialogueSystem.js';
import { UIManager } from './ui/UIManager.js';
import { DialogueUI } from './ui/DialogueUI.js';
import { DebugUI } from './ui/DebugUI.js';

class Game {
    constructor() {
        
        this.isRunning = false;
        this.isPaused = false;
        this.gameState = 'loading';
        
        // Cheat flags
        this.godMode = false;
        this.noPossession = false;
        
        // Core systems
        this.renderer = null;
        this.inputManager = null;
        this.audioManager = null;
        this.world = null;
        this.timeManager = null;
        this.player = null;
        this.npcManager = null;
        
        // Game systems
        this.craftingSystem = null;
        this.sanitySystem = null;
        this.possessionSystem = null;
        this.healthSystem = null;
        this.calendarSystem = null;
        this.routeSystem = null;
        this.dialogueSystem = null;
        
        // UI
        this.uiManager = null;
        this.debugUI = null;
        
        // Game clock
        this.clock = new THREE.Clock();
        this.deltaTime = 0;
        
        // Interaction
        this.currentInteractable = null;

        // Locations
        this.barVisited = false;
        this.hasSchoolAccess = false;
        
        this.init();
    }
    
    async init() {
        console.log('Initializing Checkered...');
        
        try {
            this.updateLoadingProgress(10, 'Initializing renderer...');
            this.renderer = new Renderer();
            await this.renderer.init();
            
            this.updateLoadingProgress(20, 'Setting up input...');
            this.inputManager = new InputManager();
            
            this.updateLoadingProgress(30, 'Loading audio...');
            this.audioManager = new AudioManager();
            
            this.updateLoadingProgress(40, 'Creating world...');
            this.world = new World(this.renderer.scene);
            await this.world.init();
            
            this.updateLoadingProgress(50, 'Initializing time system...');
            this.timeManager = new TimeManager();
            
            this.updateLoadingProgress(60, 'Creating player...');
            this.player = new Player(this.renderer.scene, this.renderer.camera);
            await this.player.init();
            
            this.updateLoadingProgress(70, 'Populating NPCs...');
            this.npcManager = new NPCManager(this.renderer.scene, this.world);
            await this.npcManager.init();
            
            this.updateLoadingProgress(80, 'Setting up game systems...');
            this.calendarSystem = new CalendarSystem(this.timeManager);
            this.healthSystem = new HealthSystem();
            this.sanitySystem = new SanitySystem();
            this.craftingSystem = new CraftingSystem();
            this.routeSystem = new RouteSystem();
            this.dialogueSystem = new DialogueSystem(this.npcManager, this.routeSystem);
            this.possessionSystem = new PossessionSystem(
                this.player,
                this.npcManager,
                this.sanitySystem,
                this.healthSystem,
                this.renderer
            );
            
            // Give starting inventory
            this.giveStartingItems();
            
            this.updateLoadingProgress(90, 'Initializing UI...');
            this.uiManager = new UIManager(this);
            this.debugUI = new DebugUI(this);
            
            this.updateLoadingProgress(100, 'Ready!');
            
            this.setupEventListeners();
            
            setTimeout(() => {
                this.showTitleScreen();
            }, 500);
            
            console.log('Checkered initialized successfully!');
            console.log('Press F3 or ` to open debug menu');
            
        } catch (error) {
            console.error('Failed to initialize game:', error);
            this.updateLoadingProgress(0, 'Error: ' + error.message);

            // Show error on screen
            const loadingText = document.querySelector('.loading-text');
            if (loadingText) {
                loadingText.textContent = `Failed to load: ${error.message}`;
                loadingText.style.color = '#ff4444';
            }
        }
    }
    
    giveStartingItems() {
        const startingItems = [
            { id: 'cloth_rag', name: 'Cloth Rag', attributes: ['cloth', 'flammable'], quantity: 3 },
            { id: 'matches', name: 'Matches', attributes: ['fire_source', 'flammable'], quantity: 1 },
            { id: 'bandage', name: 'Bandage', attributes: ['cloth', 'healing'], healAmount: 15, quantity: 2 },
            { id: 'newspaper', name: 'Newspaper', attributes: ['flammable'], quantity: 1 }
        ];
        
        startingItems.forEach(item => {
            this.craftingSystem.addItem(item);
        });
    }
    
    updateLoadingProgress(percent, text) {
        const progressBar = document.querySelector('.loading-progress');
        const loadingText = document.querySelector('.loading-text');
        
        if (progressBar) progressBar.style.width = `${percent}%`;
        if (loadingText) loadingText.textContent = text;
    }
    
    showTitleScreen() {
        document.getElementById('loading-screen').classList.add('hidden');
        document.getElementById('title-screen').classList.remove('hidden');
        this.gameState = 'title';
        this.audioManager.playMusic('menu');
    }
    
    setupEventListeners() {
        // Start game
        document.getElementById('start-game').addEventListener('click', () => {
            this.startNewGame();
        });
        
        // Continue
        document.getElementById('continue-game').addEventListener('click', () => {
            this.loadGame();
        });
        
        // Options
        document.getElementById('options-btn').addEventListener('click', () => {
            this.showOptions();
        });
        
        // Close buttons
        document.querySelectorAll('.close-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.target.closest('.panel').classList.add('hidden');
            });
        });
        
        // Keyboard
        document.addEventListener('keydown', (e) => {
            this.handleKeyDown(e);
        });
        
        // Window resize
        window.addEventListener('resize', () => {
            this.renderer.onWindowResize();
        });
        
        // Check for save
        if (localStorage.getItem('checkered_save')) {
            document.getElementById('continue-game').disabled = false;
        }
    }
    
    handleKeyDown(e) {
        // Global keys work regardless of state
        if (e.code === 'F3' || e.code === 'Backquote') {
            e.preventDefault();
            return; // Handled by DebugUI
        }
        
        if (this.gameState !== 'playing') return;
        
        switch (e.code) {
            case 'Tab':
                e.preventDefault();
                this.uiManager.toggleInventory();
                break;
            case 'KeyR':
                this.uiManager.toggleCrafting();
                break;
            case 'KeyH':
                this.uiManager.toggleHealth();
                break;
            case 'KeyJ':
                this.uiManager.toggleCalendar();
                break;
            case 'Escape':
                this.togglePause();
                break;
            case 'KeyC':
                this.player.toggleCameraMode();
                this.uiManager.updateCameraMode(this.player.cameraMode);
                break;
            case 'KeyE':
                this.interact();
                break;
        }
    }
    
    showOptions() {
        document.getElementById('options-panel').classList.remove('hidden');
        
        // Setup options controls if not already done
        const masterVolume = document.getElementById('master-volume');
        const sensitivity = document.getElementById('mouse-sensitivity');
        const ps1Intensity = document.getElementById('ps1-intensity');
        
        if (masterVolume && !masterVolume.dataset.setup) {
            masterVolume.dataset.setup = 'true';
            masterVolume.addEventListener('input', (e) => {
                this.audioManager.setMasterVolume(e.target.value / 100);
            });
        }
        
        if (sensitivity && !sensitivity.dataset.setup) {
            sensitivity.dataset.setup = 'true';
            sensitivity.addEventListener('input', (e) => {
                this.player.mouseSensitivity = e.target.value * 0.001;
            });
        }
    }
    
    async startNewGame() {
        document.getElementById('title-screen').classList.add('hidden');
        document.getElementById('game-container').classList.remove('hidden');
        
        // Start intro
        this.gameState = 'intro';
        await this.showIntroNarrative();
        
        // Start game
        this.gameState = 'playing';
        this.isRunning = true;
        this.audioManager.playMusic('ambient');
        
        // Update lighting for current time
        this.world.updateLighting(this.timeManager.normalizedTime);
        
        this.gameLoop();
    }
    
    // In js/main.js - Replace showIntroNarrative with this:

    async showIntroNarrative() {
        const narrativeOverlay = document.getElementById('narrative-overlay');
        const narrativeText = document.getElementById('narrative-text');
        const continueBtn = document.getElementById('narrative-continue');
        
        const introTexts = [
            "The forest has been your home for as long as you can remember...",
            "Years of isolation. Years of fighting the curse that lives within you.",
            "They call it 'Chichiru' - the thing that takes control, that makes you kill.",
            "You massacred your entire hometown as a child. You've lived as a cryptid ever since.",
            "But one man took you in. Marcus, the bar owner from a nearby village.",
            "For years, you kept your distance. Hid the curse. Until two years ago, when he found out.",
            "He left without a word. Moved to the city. Abandoned you.",
            "Today, a crumpled newspaper blows past your shelter.",
            "'The Crossroads Bar - Grand Opening in Millbrook City'",
            "You recognize the name. You know who owns it.",
            "After weeks of walking, the city finally comes into view.",
            "Millbrook awaits. And so does your past."
        ];
        
        narrativeOverlay.classList.remove('hidden');
        
        for (const text of introTexts) {
            narrativeText.textContent = text;
            narrativeText.style.animation = 'none';
            narrativeText.offsetHeight; // Trigger reflow
            narrativeText.style.animation = 'fadeIn 0.5s ease';
            
            continueBtn.classList.remove('hidden');
            
            await new Promise(resolve => {
                const handler = () => {
                    this.audioManager.playSound('click');
                    continueBtn.removeEventListener('click', handler);
                    resolve();
                };
                continueBtn.addEventListener('click', handler);
            });
        }
        
        // Final controls info
        narrativeText.innerHTML = `
            <div style="text-align:left; max-width:600px; margin:0 auto;">
                <h2 style="color:#ff4444; margin-bottom:20px;">CONTROLS</h2>
                <p><strong>WASD</strong> - Move</p>
                <p><strong>Mouse</strong> - Look around</p>
                <p><strong>E</strong> - Interact / Talk / Pick up</p>
                <p><strong>C</strong> - Toggle camera (First/Third person)</p>
                <p><strong>Tab</strong> - Inventory</p>
                <p><strong>R</strong> - Crafting</p>
                <p><strong>H</strong> - Health status</p>
                <p><strong>J</strong> - Calendar</p>
                <p><strong>Escape</strong> - Pause</p>
                <p><strong>F3</strong> or <strong>\`</strong> - Debug menu</p>
                <br>
                <p style="color:#888;">Click to begin...</p>
            </div>
        `;
        
        await new Promise(resolve => {
            const handler = () => {
                this.audioManager.playSound('click');
                continueBtn.removeEventListener('click', handler);
                resolve();
            };
            continueBtn.addEventListener('click', handler);
        });
        
        narrativeOverlay.classList.add('hidden');
    }
    
    gameLoop() {
        if (!this.isRunning) return;
        
        requestAnimationFrame(() => this.gameLoop());
        
        this.deltaTime = Math.min(this.clock.getDelta(), 0.1); // Cap delta time
        
        if (this.isPaused) return;
        
        switch (this.gameState) {
            case 'playing':
                this.updatePlaying();
                break;
            case 'possession':
                this.updatePossession();
                break;
            case 'dialogue':
                this.renderer.render(this.sanitySystem.sanity);
                break;
        }
        
        // Update debug UI
        if (this.debugUI.isVisible) {
            this.debugUI.update();
        }
    }
    
    updatePlaying() {
        this.inputManager.update();
        
        // Update player
        this.player.update(this.deltaTime, this.inputManager);
        
        // Update NPCs
        this.npcManager.update(
            this.deltaTime,
            this.player.position,
            this.timeManager.currentPeriod
        );
        
        // Update time
        this.timeManager.update(this.deltaTime);
        
        // Update world (animate pickups, etc.)
        this.world.update(this.deltaTime, this.timeManager.normalizedTime);
        
        // Update lighting based on time
        this.world.updateLighting(this.timeManager.normalizedTime);
        
        // Update sanity
        this.sanitySystem.update(this.deltaTime, {
            playerPosition: this.player.position,
            nearbyNPCs: this.npcManager.getNearbyNPCs(this.player.position, 10),
            currentLocation: this.world.getCurrentLocation(this.player.position),
            timeOfDay: this.timeManager.currentPeriod
        });
        
        // Check for possession (unless disabled)
        if (!this.noPossession && !this.godMode) {
            if (this.possessionSystem.checkTrigger(
                this.sanitySystem.sanity,
                this.sanitySystem.comfort,
                this.timeManager.currentDay,
                this.world.getCurrentLocation(this.player.position)
            )) {
                this.startPossession();
            }
        }
        
        // Check for nearby interactables
        this.checkInteractables();
        
        // Update UI
        this.uiManager.update({
            sanity: this.sanitySystem.sanity,
            comfort: this.sanitySystem.comfort,
            day: this.calendarSystem.currentDay,
            week: this.calendarSystem.currentWeek,
            timePeriod: this.timeManager.periodName
        });
        
        // Render
        this.renderer.render(this.sanitySystem.sanity);
    }
    
    checkInteractables() {
        const nearby = this.world.getNearbyInteractables(this.player.position, 3);
        
        // Also check NPCs
        const nearbyNPC = this.npcManager.getInteractableNPC(this.player.position, 3);
        if (nearbyNPC) {
            nearby.push({
                type: 'npc',
                npc: nearbyNPC,
                distance: this.player.position.distanceTo(nearbyNPC.position),
                promptText: `Talk to ${nearbyNPC.name}`
            });
        }
        
        if (nearby.length > 0) {
            // Sort by distance and get closest
            nearby.sort((a, b) => a.distance - b.distance);
            this.currentInteractable = nearby[0];
            this.uiManager.showInteractionPrompt(this.currentInteractable.promptText);
        } else {
            this.currentInteractable = null;
            this.uiManager.hideInteractionPrompt();
        }
    }
    
    // In main.js, update the interact() method:
    interact() {
        if (!this.currentInteractable) return;
        
        const interactable = this.currentInteractable;
        
        switch (interactable.type) {
            case 'pickup':
                if (this.craftingSystem.addItem({ ...interactable.item })) {
                    this.world.removePickupItem(interactable);
                    this.uiManager.showNotification(`Picked up: ${interactable.item.name}`);
                    this.audioManager.playSound('click');
                } else {
                    this.uiManager.showNotification('Inventory full!', 'error');
                }
                break;
                
            case 'npc':
                console.log('Starting dialogue with NPC:', interactable.npc.name);
                this.startDialogue(interactable.npc);
                break;
                
            case 'door':
                this.handleDoorInteraction(interactable);
                break;
        }
    }

    handleDoorInteraction(door) {
        const location = door.locationId;
        const time = this.timeManager.normalizedTime;
        const isNight = time >= 0.75 || time < 0.25;
        
        // Bar is only open at night (after 6pm / 0.75)
        if (location === 'bar') {
            if (!isNight && this.calendarSystem.currentDay === 1) {
                this.uiManager.showNotification('The bar is closed. Come back tonight.');
                return;
            }
            
            // First visit to bar - trigger special dialogue
            if (!this.barVisited) {
                this.barVisited = true;
                // Determine personality result
                const personality = this.routeSystem.determinePersonality();
                
                // Find Marcus and start dialogue
                const marcus = this.npcManager.npcs.get('bar_owner');
                if (marcus) {
                    const dialogueKey = personality === 'good' ? 'firstVisit_good' : 'firstVisit_bad';
                    const dialogue = DIALOGUE_DATA.marcus[dialogueKey];
                    if (dialogue) {
                        this.uiManager.showDialogue(dialogue, marcus);
                        return;
                    }
                }
            }
            
            this.uiManager.showNotification('Entering The Crossroads Bar...');
            // Would trigger interior transition
        } else if (location === 'school') {
            // School requires the deal with Victor
            if (!this.hasSchoolAccess) {
                this.uiManager.showNotification('The academy is closed to visitors.');
                return;
            }
            this.uiManager.showNotification('Entering Millbrook Academy...');
        } else if (location === 'shop') {
            this.uiManager.showNotification("Entering Ada's General Store...");
            // Find Ada and start dialogue
            const ada = this.npcManager.npcs.get('shopkeeper');
            if (ada) {
                const dialogue = DIALOGUE_DATA.ada?.default;
                if (dialogue) {
                    this.uiManager.showDialogue(dialogue, ada);
                }
            }
        } else {
            this.uiManager.showNotification(`Entering ${location}...`);
        }
    }
    
    // Update startDialogue method in main.js:
    startDialogue(npc) {
        console.log('Starting dialogue system for:', npc.name);
        this.gameState = 'dialogue';
        document.exitPointerLock();
        
        const dialogue = this.dialogueSystem.startDialogue(npc);
        
        if (dialogue) {
            this.uiManager.showDialogue(dialogue, npc, () => {
                // Callback when dialogue ends
                this.gameState = 'playing';
                document.getElementById('game-canvas').requestPointerLock();
            });
        } else {
            // Fallback if no dialogue found
            this.uiManager.showNotification(`${npc.name} has nothing to say.`);
            this.gameState = 'playing';
            document.getElementById('game-canvas').requestPointerLock();
        }
    }
    
    updatePossession() {
        this.inputManager.update();
        this.possessionSystem.update(this.deltaTime, this.inputManager);
        
        if (this.possessionSystem.isComplete) {
            this.endPossession();
        }
        
        this.uiManager.updatePossessionUI(this.possessionSystem.getStatus());
        this.renderer.render(0); // Max distortion
    }
    
    startPossession() {
        console.log('Possession starting!');
        this.gameState = 'possession';
        
        this.uiManager.showPossessionWarning();
        
        this.possessionSystem.start({
            sanity: this.sanitySystem.sanity,
            location: this.world.getCurrentLocation(this.player.position),
            appliedTraps: this.craftingSystem.getAppliedTraps()
        });
        
        this.audioManager.playMusic('possession');
        document.getElementById('possession-ui').classList.remove('hidden');
    }
    
    endPossession() {
        console.log('Possession ended');
        this.gameState = 'playing';
        
        const aftermath = this.possessionSystem.getAftermath();
        
        // Apply effects
        if (aftermath.trapEffects?.length > 0) {
            aftermath.trapEffects.forEach(effect => {
                this.healthSystem.applyEffect(effect);
            });
        }
        
        if (aftermath.killedNPCs?.length > 0) {
            aftermath.killedNPCs.forEach(npc => {
                this.npcManager.killNPC(npc);
                this.routeSystem.recordNPCDeath();
            });
        }
        
        if (aftermath.remainingPhantom) {
            this.player.teleportTo(aftermath.remainingPhantom.position);
        }
        
        this.sanitySystem.onPossessionEnd(aftermath);
        
        document.getElementById('possession-ui').classList.add('hidden');
        this.uiManager.hidePossessionWarning();
        this.audioManager.playMusic('ambient');
    }
    
    togglePause() {
        this.isPaused = !this.isPaused;
        
        if (this.isPaused) {
            document.exitPointerLock();
            this.uiManager.showPauseMenu();
        } else {
            document.getElementById('game-canvas').requestPointerLock();
            this.uiManager.hidePauseMenu();
        }
    }
    
    saveGame() {
        const saveData = {
            player: this.player.serialize(),
            calendar: this.calendarSystem.serialize(),
            npcs: this.npcManager.serialize(),
            inventory: this.craftingSystem.serializeInventory(),
            health: this.healthSystem.serialize(),
            sanity: this.sanitySystem.serialize(),
            route: this.routeSystem.serialize(),
            world: this.world.serialize()
        };
        
        localStorage.setItem('checkered_save', JSON.stringify(saveData));
        this.uiManager.showNotification('Game saved!');
    }
    
    loadGame() {
        const saveData = JSON.parse(localStorage.getItem('checkered_save'));
        if (!saveData) return;
        
        this.player.deserialize(saveData.player);
        this.calendarSystem.deserialize(saveData.calendar);
        this.npcManager.deserialize(saveData.npcs);
        this.craftingSystem.deserializeInventory(saveData.inventory);
        this.healthSystem.deserialize(saveData.health);
        this.sanitySystem.deserialize(saveData.sanity);
        this.routeSystem.deserialize(saveData.route);
        this.world.deserialize(saveData.world);
        
        document.getElementById('title-screen').classList.add('hidden');
        document.getElementById('game-container').classList.remove('hidden');
        
        this.gameState = 'playing';
        this.isRunning = true;
        this.audioManager.playMusic('ambient');
        this.world.updateLighting(this.timeManager.normalizedTime);
        this.gameLoop();
        
        this.uiManager.showNotification('Game loaded!');
    }
}

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    window.game = new Game();
});