// js/engine/InputManager.js
export class InputManager {
    constructor() {
        this.keys = {};
        this.mouse = {
            x: 0,
            y: 0,
            deltaX: 0,
            deltaY: 0,
            buttons: {}
        };
        this.gamepad = null;
        
        this.sensitivity = 0.002;
        this.isPointerLocked = false;
        
        this.setupEventListeners();
    }
    
    setupEventListeners() {
        // Keyboard events
        document.addEventListener('keydown', (e) => {
            this.keys[e.code] = true;
        });
        
        document.addEventListener('keyup', (e) => {
            this.keys[e.code] = false;
        });
        
        // Mouse events
        document.addEventListener('mousemove', (e) => {
            if (this.isPointerLocked) {
                this.mouse.deltaX = e.movementX;
                this.mouse.deltaY = e.movementY;
            }
        });
        
        document.addEventListener('mousedown', (e) => {
            this.mouse.buttons[e.button] = true;
        });
        
        document.addEventListener('mouseup', (e) => {
            this.mouse.buttons[e.button] = false;
        });
        
        // Pointer lock change
        document.addEventListener('pointerlockchange', () => {
            this.isPointerLocked = document.pointerLockElement !== null;
        });
        
        // Gamepad events
        window.addEventListener('gamepadconnected', (e) => {
            this.gamepad = e.gamepad;
            console.log('Gamepad connected:', e.gamepad.id);
        });
        
        window.addEventListener('gamepaddisconnected', () => {
            this.gamepad = null;
            console.log('Gamepad disconnected');
        });
    }
    
    update() {
        // Reset mouse delta
        this.mouse.deltaX = 0;
        this.mouse.deltaY = 0;
        
        // Update gamepad state
        if (this.gamepad) {
            const gamepads = navigator.getGamepads();
            this.gamepad = gamepads[this.gamepad.index];
        }
    }
    
    isKeyPressed(code) {
        return this.keys[code] === true;
    }
    
    isMouseButtonPressed(button) {
        return this.mouse.buttons[button] === true;
    }
    
    getMovementInput() {
        let x = 0;
        let z = 0;
        
        // Keyboard input
        if (this.keys['KeyW'] || this.keys['ArrowUp']) z -= 1;
        if (this.keys['KeyS'] || this.keys['ArrowDown']) z += 1;
        if (this.keys['KeyA'] || this.keys['ArrowLeft']) x -= 1;
        if (this.keys['KeyD'] || this.keys['ArrowRight']) x += 1;
        
        // Gamepad input (left stick)
        if (this.gamepad) {
            const deadzone = 0.15;
            const lx = this.gamepad.axes[0];
            const ly = this.gamepad.axes[1];
            
            if (Math.abs(lx) > deadzone) x += lx;
            if (Math.abs(ly) > deadzone) z += ly;
        }
        
        // Normalize diagonal movement
        const length = Math.sqrt(x * x + z * z);
        if (length > 1) {
            x /= length;
            z /= length;
        }
        
        return { x, z };
    }
    
    getLookInput() {
        let x = this.mouse.deltaX * this.sensitivity;
        let y = this.mouse.deltaY * this.sensitivity;
        
        // Gamepad input (right stick)
        if (this.gamepad) {
            const deadzone = 0.15;
            const rx = this.gamepad.axes[2];
            const ry = this.gamepad.axes[3];
            
            if (Math.abs(rx) > deadzone) x += rx * 0.05;
            if (Math.abs(ry) > deadzone) y += ry * 0.05;
        }
        
        return { x, y };
    }
    
    isJumpPressed() {
        return this.keys['Space'] || (this.gamepad && this.gamepad.buttons[0].pressed);
    }
    
    isSprintPressed() {
        return this.keys['ShiftLeft'] || (this.gamepad && this.gamepad.buttons[10].pressed);
    }
    
    isInteractPressed() {
        return this.keys['KeyE'] || (this.gamepad && this.gamepad.buttons[2].pressed);
    }
    
    isAttackPressed() {
        return this.mouse.buttons[0] || (this.gamepad && this.gamepad.buttons[5].pressed);
    }
    
    setSensitivity(value) {
        this.sensitivity = value * 0.001;
    }
}