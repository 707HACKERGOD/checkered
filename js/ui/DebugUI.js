// js/ui/DebugUI.js
export class DebugUI {
    constructor(game) {
        this.game = game;
        this.isVisible = false;
        this.panel = null;
        
        this.createPanel();
        this.setupKeyboardShortcut();
    }
    
    createPanel() {
        this.panel = document.createElement('div');
        this.panel.id = 'debug-panel';
        this.panel.innerHTML = `
            <div class="debug-header">
                <h3>DEBUG MENU</h3>
                <button class="debug-close">×</button>
            </div>
            <div class="debug-content">
                <div class="debug-section">
                    <h4>TIME & CALENDAR</h4>
                    <div class="debug-row">
                        <span>Day: <strong id="debug-day">1</strong></span>
                        <span>Week: <strong id="debug-week">1</strong></span>
                    </div>
                    <div class="debug-row">
                        <span>Time: <strong id="debug-time">0.25</strong></span>
                        <span>Period: <strong id="debug-period">Morning</strong></span>
                    </div>
                    <div class="debug-buttons">
                        <button id="debug-skip-hour">+1 Hour</button>
                        <button id="debug-skip-day">+1 Day</button>
                        <button id="debug-set-morning">Set Morning</button>
                        <button id="debug-set-night">Set Night</button>
                    </div>
                </div>
                
                <div class="debug-section">
                    <h4>PLAYER STATS</h4>
                    <div class="debug-row">
                        <label>Sanity:</label>
                        <input type="range" id="debug-sanity" min="0" max="100" value="100">
                        <span id="debug-sanity-val">100</span>
                    </div>
                    <div class="debug-row">
                        <label>Comfort:</label>
                        <input type="range" id="debug-comfort" min="0" max="100" value="0">
                        <span id="debug-comfort-val">0</span>
                    </div>
                    <div class="debug-row">
                        <span>Location: <strong id="debug-location">streets</strong></span>
                    </div>
                    <div class="debug-row">
                        <span>Position: <strong id="debug-position">0, 0, 0</strong></span>
                    </div>
                </div>
                
                <div class="debug-section">
                    <h4>SPAWN ITEMS</h4>
                    <select id="debug-item-select">
                        <option value="glass_bottle">Glass Bottle</option>
                        <option value="knife">Kitchen Knife</option>
                        <option value="rope">Rope</option>
                        <option value="matches">Matches</option>
                        <option value="gasoline">Gasoline</option>
                        <option value="bandage">Bandage</option>
                        <option value="cloth_rag">Cloth Rag</option>
                        <option value="metal_pipe">Metal Pipe</option>
                        <option value="wire">Wire</option>
                        <option value="battery">Battery</option>
                        <option value="duct_tape">Duct Tape</option>
                        <option value="crowbar">Crowbar</option>
                        <option value="chemicals">Chemicals</option>
                        <option value="nails">Box of Nails</option>
                        <option value="wooden_board">Wooden Board</option>
                    </select>
                    <button id="debug-give-item">Give Item</button>
                    <button id="debug-give-all">Give All Items</button>
                </div>
                
                <div class="debug-section">
                    <h4>POSSESSION</h4>
                    <button id="debug-trigger-possession">Trigger Possession</button>
                    <button id="debug-end-possession">End Possession</button>
                    <div class="debug-row">
                        <span>Grace Period: <strong id="debug-grace">0</strong>s remaining</span>
                    </div>
                    <div class="debug-row">
                        <span>Cooldown: <strong id="debug-possession-cooldown">0</strong>s remaining</span>
                    </div>
                    <div class="debug-row">
                        <span>Status: <strong id="debug-possession-status">Safe</strong></span>
                    </div>
                </div>
                
                <div class="debug-section">
                    <h4>TELEPORT</h4>
                    <div class="debug-buttons">
                        <button data-tp="bar">Bar</button>
                        <button data-tp="school">School</button>
                        <button data-tp="shop">Shop</button>
                        <button data-tp="park">Park</button>
                        <button data-tp="spawn">Spawn</button>
                    </div>
                </div>
                
                <div class="debug-section">
                    <h4>CHEATS</h4>
                    <div class="debug-buttons">
                        <button id="debug-god-mode">God Mode: OFF</button>
                        <button id="debug-no-possession">No Possession: OFF</button>
                        <button id="debug-fast-time">Fast Time: OFF</button>
                    </div>
                </div>
                
                <div class="debug-section">
                    <h4>NPC INFO</h4>
                    <div id="debug-npc-list"></div>
                </div>
            </div>
        `;
        
        // Add styles
        const style = document.createElement('style');
        style.textContent = `
            #debug-panel {
                position: fixed;
                top: 10px;
                right: 10px;
                width: 350px;
                max-height: 90vh;
                background: rgba(0, 0, 0, 0.95);
                border: 2px solid #00ff00;
                color: #00ff00;
                font-family: 'Courier New', monospace;
                font-size: 12px;
                z-index: 10000;
                overflow-y: auto;
            }
            #debug-panel.hidden { display: none; }
            .debug-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                padding: 10px;
                background: #003300;
                border-bottom: 1px solid #00ff00;
            }
            .debug-header h3 { margin: 0; }
            .debug-close {
                background: none;
                border: 1px solid #00ff00;
                color: #00ff00;
                cursor: pointer;
                font-size: 18px;
                width: 25px;
                height: 25px;
            }
            .debug-close:hover { background: #00ff00; color: #000; }
            .debug-content { padding: 10px; }
            .debug-section {
                margin-bottom: 15px;
                padding-bottom: 10px;
                border-bottom: 1px solid #003300;
            }
            .debug-section h4 {
                margin: 0 0 8px 0;
                color: #00ff00;
            }
            .debug-row {
                display: flex;
                justify-content: space-between;
                align-items: center;
                margin: 5px 0;
                gap: 10px;
            }
            .debug-row label { min-width: 60px; }
            .debug-row input[type="range"] { flex: 1; }
            .debug-buttons {
                display: flex;
                flex-wrap: wrap;
                gap: 5px;
                margin-top: 8px;
            }
            #debug-panel button {
                background: #001100;
                border: 1px solid #00ff00;
                color: #00ff00;
                padding: 5px 10px;
                cursor: pointer;
                font-family: inherit;
                font-size: 11px;
            }
            #debug-panel button:hover { background: #003300; }
            #debug-panel button.active { background: #00ff00; color: #000; }
            #debug-panel select {
                background: #001100;
                border: 1px solid #00ff00;
                color: #00ff00;
                padding: 5px;
                width: 100%;
                margin-bottom: 5px;
            }
            #debug-npc-list {
                max-height: 150px;
                overflow-y: auto;
            }
            .debug-npc-item {
                padding: 3px 0;
                border-bottom: 1px solid #002200;
            }
        `;
        document.head.appendChild(style);
        
        document.body.appendChild(this.panel);
        this.panel.classList.add('hidden');
        
        this.setupEventListeners();
    }
    
    setupKeyboardShortcut() {
        document.addEventListener('keydown', (e) => {
            // F3 or backtick to toggle debug
            if (e.code === 'F3' || e.code === 'Backquote') {
                e.preventDefault();
                this.toggle();
            }
        });
    }
    
    setupEventListeners() {
        // Close button
        this.panel.querySelector('.debug-close').addEventListener('click', () => this.hide());
        
        // Time controls
        document.getElementById('debug-skip-hour')?.addEventListener('click', () => {
            this.game.timeManager.currentTime += 1/24;
            if (this.game.timeManager.currentTime >= 1) {
                this.game.timeManager.currentTime -= 1;
                this.game.calendarSystem.currentDay++;
            }
        });
        
        document.getElementById('debug-skip-day')?.addEventListener('click', () => {
            this.game.calendarSystem.currentDay++;
            this.game.timeManager.currentTime = 0.25;
        });
        
        document.getElementById('debug-set-morning')?.addEventListener('click', () => {
            this.game.timeManager.currentTime = 0.3;
        });
        
        document.getElementById('debug-set-night')?.addEventListener('click', () => {
            this.game.timeManager.currentTime = 0.9;
        });
        
        // Sanity slider
        document.getElementById('debug-sanity')?.addEventListener('input', (e) => {
            this.game.sanitySystem.sanity = parseInt(e.target.value);
            document.getElementById('debug-sanity-val').textContent = e.target.value;
        });
        
        // Comfort slider
        document.getElementById('debug-comfort')?.addEventListener('input', (e) => {
            this.game.sanitySystem.comfort = parseInt(e.target.value);
            document.getElementById('debug-comfort-val').textContent = e.target.value;
        });
        
        // Give item
        document.getElementById('debug-give-item')?.addEventListener('click', () => {
            const itemId = document.getElementById('debug-item-select').value;
            this.giveItem(itemId);
        });
        
        // Give all items
        document.getElementById('debug-give-all')?.addEventListener('click', () => {
            this.giveAllItems();
        });
        
        // Possession controls
        document.getElementById('debug-trigger-possession')?.addEventListener('click', () => {
            this.game.startPossession();
        });
        
        document.getElementById('debug-end-possession')?.addEventListener('click', () => {
            if (this.game.possessionSystem.isActive) {
                this.game.possessionSystem.isComplete = true;
            }
        });
        
        // Teleport buttons
        this.panel.querySelectorAll('[data-tp]').forEach(btn => {
            btn.addEventListener('click', () => {
                this.teleportTo(btn.dataset.tp);
            });
        });
        
        // Cheats
        document.getElementById('debug-god-mode')?.addEventListener('click', (e) => {
            this.game.godMode = !this.game.godMode;
            e.target.textContent = `God Mode: ${this.game.godMode ? 'ON' : 'OFF'}`;
            e.target.classList.toggle('active', this.game.godMode);
        });
        
        document.getElementById('debug-no-possession')?.addEventListener('click', (e) => {
            this.game.noPossession = !this.game.noPossession;
            e.target.textContent = `No Possession: ${this.game.noPossession ? 'ON' : 'OFF'}`;
            e.target.classList.toggle('active', this.game.noPossession);
        });
        
        document.getElementById('debug-fast-time')?.addEventListener('click', (e) => {
            this.game.timeManager.timeScale = this.game.timeManager.timeScale === 1 ? 10 : 1;
            const isFast = this.game.timeManager.timeScale > 1;
            e.target.textContent = `Fast Time: ${isFast ? 'ON' : 'OFF'}`;
            e.target.classList.toggle('active', isFast);
        });
    }
    
    giveItem(itemId) {
        const { ITEMS } = window.gameData || {};
        
        // Fallback item data
        const items = {
            glass_bottle: { id: 'glass_bottle', name: 'Glass Bottle', attributes: ['container', 'glass'] },
            knife: { id: 'knife', name: 'Kitchen Knife', attributes: ['sharp', 'handle', 'metal'], damage: 15 },
            rope: { id: 'rope', name: 'Rope', attributes: ['rope', 'cloth'] },
            matches: { id: 'matches', name: 'Matches', attributes: ['fire_source', 'flammable'] },
            gasoline: { id: 'gasoline', name: 'Gasoline', attributes: ['liquid', 'flammable', 'toxic'] },
            bandage: { id: 'bandage', name: 'Bandage', attributes: ['cloth', 'healing'], healAmount: 15 },
            cloth_rag: { id: 'cloth_rag', name: 'Cloth Rag', attributes: ['cloth', 'flammable'] },
            metal_pipe: { id: 'metal_pipe', name: 'Metal Pipe', attributes: ['metal', 'blunt', 'heavy', 'handle'], damage: 14 },
            wire: { id: 'wire', name: 'Copper Wire', attributes: ['rope', 'metal', 'conductive'] },
            battery: { id: 'battery', name: 'Battery', attributes: ['electronic', 'conductive'] },
            duct_tape: { id: 'duct_tape', name: 'Duct Tape', attributes: ['sticky', 'rope', 'cloth'] },
            crowbar: { id: 'crowbar', name: 'Crowbar', attributes: ['blunt', 'sharp', 'handle', 'metal', 'heavy'], damage: 18 },
            chemicals: { id: 'chemicals', name: 'Cleaning Chemicals', attributes: ['toxic', 'liquid'] },
            nails: { id: 'nails', name: 'Box of Nails', attributes: ['metal', 'sharp'] },
            wooden_board: { id: 'wooden_board', name: 'Wooden Board', attributes: ['wood', 'blunt', 'flammable'], damage: 8 }
        };
        
        const item = items[itemId] || { id: itemId, name: itemId, attributes: [] };
        
        if (this.game.craftingSystem.addItem(item)) {
            this.game.uiManager.showNotification(`Added: ${item.name}`);
        } else {
            this.game.uiManager.showNotification('Inventory full!', 'error');
        }
    }
    
    giveAllItems() {
        const items = ['glass_bottle', 'knife', 'rope', 'matches', 'gasoline', 'bandage', 
                       'cloth_rag', 'metal_pipe', 'wire', 'battery', 'duct_tape', 'crowbar',
                       'chemicals', 'nails', 'wooden_board'];
        
        items.forEach(id => this.giveItem(id));
        this.game.uiManager.showNotification('All items added!');
    }
    
    teleportTo(location) {
        const positions = {
            bar: { x: 0, z: -20 },
            school: { x: -50, z: -45 },
            shop: { x: 25, z: 20 },
            park: { x: 0, z: 35 },
            spawn: { x: 0, z: 10 }
        };
        
        const pos = positions[location];
        if (pos) {
            this.game.player.teleportTo(new THREE.Vector3(pos.x, 1, pos.z));
            this.game.uiManager.showNotification(`Teleported to ${location}`);
        }
    }
    
    toggle() {
        this.isVisible = !this.isVisible;
        this.panel.classList.toggle('hidden', !this.isVisible);
        
        if (this.isVisible) {
            this.update();
        }
    }
    
    show() {
        this.isVisible = true;
        this.panel.classList.remove('hidden');
        this.update();
    }
    
    hide() {
        this.isVisible = false;
        this.panel.classList.add('hidden');
    }
    
    update() {
        if (!this.isVisible) return;
        
        // Update time info
        const tm = this.game.timeManager;
        const cal = this.game.calendarSystem;
        
        document.getElementById('debug-day').textContent = cal?.currentDay || 1;
        document.getElementById('debug-week').textContent = cal?.currentWeek || 1;
        document.getElementById('debug-time').textContent = tm?.currentTime?.toFixed(2) || '0.25';
        document.getElementById('debug-period').textContent = tm?.periodName || 'Morning';
        
        // Update player stats
        document.getElementById('debug-sanity').value = this.game.sanitySystem?.sanity || 100;
        document.getElementById('debug-sanity-val').textContent = Math.round(this.game.sanitySystem?.sanity || 100);
        document.getElementById('debug-comfort').value = this.game.sanitySystem?.comfort || 0;
        document.getElementById('debug-comfort-val').textContent = Math.round(this.game.sanitySystem?.comfort || 0);
        
        // Update location
        const location = this.game.world?.getCurrentLocation(this.game.player.position) || 'unknown';
        document.getElementById('debug-location').textContent = location;
        
        // Update position
        const pos = this.game.player.position;
        document.getElementById('debug-position').textContent = 
            `${pos.x.toFixed(1)}, ${pos.y.toFixed(1)}, ${pos.z.toFixed(1)}`;
        
        // Update possession cooldown
        const poss = this.game.possessionSystem;
        const cooldown = Math.max(0, (poss?.currentCooldown || 0) - 
            (Date.now() - (poss?.lastPossessionTime || 0)) / 1000);
        document.getElementById('debug-possession-cooldown').textContent = Math.round(cooldown);
        
        // Update possession info
        const graceRemaining = Math.max(0, poss.gracePeriod - (Date.now() - poss.gameStartTime) / 1000);
        document.getElementById('debug-grace').textContent = Math.round(graceRemaining);

        const cooldownRemaining = poss.getTimeUntilPossessionPossible();
        document.getElementById('debug-possession-cooldown').textContent = Math.round(cooldownRemaining);

        let status = 'Safe';
        if (poss.isActive) {
            status = `Active (${poss.phase})`;
        } else if (cooldownRemaining <= 0) {
            status = 'VULNERABLE';
        }
        document.getElementById('debug-possession-status').textContent = status;

        // Update NPC list
        this.updateNPCList();
    }
    
    updateNPCList() {
        const list = document.getElementById('debug-npc-list');
        if (!list || !this.game.npcManager) return;
        
        list.innerHTML = '';
        
        this.game.npcManager.npcs.forEach((npc, id) => {
            const item = document.createElement('div');
            item.className = 'debug-npc-item';
            item.innerHTML = `
                <strong>${npc.name}</strong> (${npc.type})
                <br>Relationship: ${npc.relationship} (${npc.relationshipValue})
                <br>State: ${npc.state} | Alive: ${npc.isAlive}
            `;
            list.appendChild(item);
        });
    }
}

// Make THREE available globally for teleport
import * as THREE from 'three';
window.THREE = THREE;