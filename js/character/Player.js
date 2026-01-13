// js/character/Player.js - COMPLETE REWRITE for better controls
import * as THREE from 'three';

export class Player {
    constructor(scene, camera) {
        this.scene = scene;
        this.camera = camera;
        
        // Position and physics
        this.position = new THREE.Vector3(0, 1, 10); // Start position
        this.velocity = new THREE.Vector3();
        this.rotation = new THREE.Euler(0, 0, 0, 'YXZ');
        
        // Camera settings
        this.cameraMode = 'third'; // 'first' or 'third'
        this.thirdPersonDistance = 5;
        this.thirdPersonHeight = 2;
        this.cameraTargetOffset = new THREE.Vector3(0, 1.5, 0);
        
        // Camera smoothing
        this.cameraPosition = new THREE.Vector3();
        this.cameraLookAt = new THREE.Vector3();
        this.cameraSmoothness = 0.1;
        
        // Mouse look
        this.yaw = 0;
        this.pitch = 0;
        this.mouseSensitivity = 0.002;
        this.pitchLimit = Math.PI / 2 - 0.1;
        
        // Movement
        this.walkSpeed = 5;
        this.runSpeed = 10;
        this.jumpForce = 8;
        this.gravity = -25;
        this.isGrounded = true;
        
        // Character mesh
        this.mesh = null;
        
        // Interaction
        this.interactRange = 3;
        
        // Stats for route tracking
        this.totalDistance = 0;
        this.combatActions = 0;
        
        // Pointer lock state
        this.isPointerLocked = false;
    }
    
    async init() {
        this.createCharacterMesh();
        this.setupPointerLock();
        
        // Initial camera position
        this.cameraPosition.copy(this.position);
        this.updateCamera(0);
    }
    
    setupPointerLock() {
        const canvas = document.getElementById('game-canvas');
        
        // Request pointer lock on click
        canvas.addEventListener('click', () => {
            if (!this.isPointerLocked && document.pointerLockElement !== canvas) {
                canvas.requestPointerLock();
            }
        });
        
        // Track pointer lock state
        document.addEventListener('pointerlockchange', () => {
            this.isPointerLocked = document.pointerLockElement === canvas;
        });
        
        // Mouse movement for camera
        document.addEventListener('mousemove', (e) => {
            if (this.isPointerLocked) {
                this.yaw -= e.movementX * this.mouseSensitivity;
                this.pitch -= e.movementY * this.mouseSensitivity;
                
                // Clamp pitch
                this.pitch = Math.max(-this.pitchLimit, Math.min(this.pitchLimit, this.pitch));
            }
        });
    }
    
    createCharacterMesh() {
        const group = new THREE.Group();
        
        // === SYL CHARACTER MODEL ===
        
        // Body / Torso with dark vest
        const torsoGeo = new THREE.BoxGeometry(0.5, 0.65, 0.3);
        const vestMat = new THREE.MeshBasicMaterial({ color: 0x1a1a1a, flatShading: true });
        const torso = new THREE.Mesh(torsoGeo, vestMat);
        torso.position.y = 1.1;
        torso.castShadow = true;
        group.add(torso);
        
        // Brown jacket (outer layer)
        const jacketGeo = new THREE.BoxGeometry(0.58, 0.68, 0.35);
        const jacketMat = new THREE.MeshBasicMaterial({ color: 0x5a3a2a, flatShading: true });
        const jacket = new THREE.Mesh(jacketGeo, jacketMat);
        jacket.position.y = 1.1;
        jacket.position.z = -0.02;
        jacket.castShadow = true;
        group.add(jacket);
        
        // Head
        const headGeo = new THREE.BoxGeometry(0.35, 0.4, 0.35);
        const skinMat = new THREE.MeshBasicMaterial({ color: 0xffdbac, flatShading: true });
        const head = new THREE.Mesh(headGeo, skinMat);
        head.position.y = 1.65;
        head.castShadow = true;
        group.add(head);
        
        // White spiky hair
        const hairMat = new THREE.MeshBasicMaterial({ color: 0xeeeeee, flatShading: true });
        
        // Multiple hair spikes
        const spikes = [
            { x: 0, z: -0.1, rotX: -0.3, rotZ: 0 },
            { x: -0.1, z: 0, rotX: 0, rotZ: 0.4 },
            { x: 0.1, z: 0, rotX: 0, rotZ: -0.4 },
            { x: 0, z: 0.1, rotX: 0.3, rotZ: 0 },
            { x: 0.08, z: -0.08, rotX: -0.2, rotZ: -0.2 },
            { x: -0.08, z: -0.08, rotX: -0.2, rotZ: 0.2 },
        ];
        
        spikes.forEach(spike => {
            const spikeGeo = new THREE.ConeGeometry(0.08, 0.25, 4);
            const spikeMesh = new THREE.Mesh(spikeGeo, hairMat);
            spikeMesh.position.set(spike.x, 1.92, spike.z);
            spikeMesh.rotation.set(spike.rotX, 0, spike.rotZ);
            group.add(spikeMesh);
        });
        
        // Red scarf
        const scarfMat = new THREE.MeshBasicMaterial({ color: 0xcc2222, flatShading: true });
        
        // Scarf wrap
        const scarfWrapGeo = new THREE.BoxGeometry(0.52, 0.15, 0.38);
        const scarfWrap = new THREE.Mesh(scarfWrapGeo, scarfMat);
        scarfWrap.position.y = 1.45;
        group.add(scarfWrap);
        
        // Scarf tail (hanging)
        const scarfTailGeo = new THREE.BoxGeometry(0.12, 0.45, 0.08);
        const scarfTail = new THREE.Mesh(scarfTailGeo, scarfMat);
        scarfTail.position.set(-0.22, 1.15, 0.15);
        scarfTail.rotation.z = -0.2;
        group.add(scarfTail);
        
        // Left eye area (missing - eye patch/scar)
        const eyePatchGeo = new THREE.BoxGeometry(0.1, 0.06, 0.02);
        const eyePatchMat = new THREE.MeshBasicMaterial({ color: 0x222222 });
        const eyePatch = new THREE.Mesh(eyePatchGeo, eyePatchMat);
        eyePatch.position.set(-0.08, 1.7, 0.18);
        group.add(eyePatch);
        
        // Right eye (blue)
        const eyeGeo = new THREE.BoxGeometry(0.05, 0.04, 0.02);
        const eyeMat = new THREE.MeshBasicMaterial({ color: 0x4488cc });
        const rightEye = new THREE.Mesh(eyeGeo, eyeMat);
        rightEye.position.set(0.08, 1.7, 0.18);
        group.add(rightEye);
        
        // Arms
        const armGeo = new THREE.BoxGeometry(0.14, 0.5, 0.14);
        
        const leftArm = new THREE.Mesh(armGeo, jacketMat);
        leftArm.position.set(-0.35, 1.0, 0);
        leftArm.castShadow = true;
        group.add(leftArm);
        
        const rightArm = new THREE.Mesh(armGeo, jacketMat);
        rightArm.position.set(0.35, 1.0, 0);
        rightArm.castShadow = true;
        group.add(rightArm);
        
        // Hands
        const handGeo = new THREE.BoxGeometry(0.1, 0.12, 0.1);
        
        const leftHand = new THREE.Mesh(handGeo, skinMat);
        leftHand.position.set(-0.35, 0.7, 0);
        group.add(leftHand);
        
        const rightHand = new THREE.Mesh(handGeo, skinMat);
        rightHand.position.set(0.35, 0.7, 0);
        group.add(rightHand);
        
        // Legs
        const legGeo = new THREE.BoxGeometry(0.16, 0.55, 0.16);
        const pantsMat = new THREE.MeshBasicMaterial({ color: 0x1a1a1a, flatShading: true });
        
        const leftLeg = new THREE.Mesh(legGeo, pantsMat);
        leftLeg.position.set(-0.12, 0.47, 0);
        leftLeg.castShadow = true;
        group.add(leftLeg);
        
        const rightLeg = new THREE.Mesh(legGeo, pantsMat);
        rightLeg.position.set(0.12, 0.47, 0);
        rightLeg.castShadow = true;
        group.add(rightLeg);
        
        // Boots
        const bootGeo = new THREE.BoxGeometry(0.18, 0.15, 0.22);
        const bootMat = new THREE.MeshBasicMaterial({ color: 0x2a2a2a, flatShading: true });
        
        const leftBoot = new THREE.Mesh(bootGeo, bootMat);
        leftBoot.position.set(-0.12, 0.12, 0.02);
        group.add(leftBoot);
        
        const rightBoot = new THREE.Mesh(bootGeo, bootMat);
        rightBoot.position.set(0.12, 0.12, 0.02);
        group.add(rightBoot);
        
        this.mesh = group;
        this.mesh.position.copy(this.position);
        this.mesh.position.y = 0;
        this.scene.add(this.mesh);
    }
    
    update(deltaTime, input) {
        // Skip if paused or in menu
        if (!input) return;
        
        // Get movement direction based on camera yaw
        const moveDir = new THREE.Vector3();
        const forward = new THREE.Vector3(0, 0, -1);
        const right = new THREE.Vector3(1, 0, 0);
        
        // Rotate directions by camera yaw
        forward.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
        right.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
        
        // Get input
        const movement = input.getMovementInput();
        const speed = input.isSprintPressed() ? this.runSpeed : this.walkSpeed;
        
        // Calculate movement
        if (movement.x !== 0 || movement.z !== 0) {
            moveDir.addScaledVector(forward, -movement.z);
            moveDir.addScaledVector(right, movement.x);
            moveDir.normalize();
            
            this.velocity.x = moveDir.x * speed;
            this.velocity.z = moveDir.z * speed;
            
            // Track distance
            this.totalDistance += speed * deltaTime;
            
            // Rotate character to face movement direction (third person only)
            if (this.cameraMode === 'third' && this.mesh) {
                const targetRotation = Math.atan2(moveDir.x, moveDir.z);
                this.mesh.rotation.y = THREE.MathUtils.lerp(
                    this.mesh.rotation.y,
                    targetRotation,
                    0.15
                );
            }
        } else {
            // Friction
            this.velocity.x *= 0.85;
            this.velocity.z *= 0.85;
        }
        
        // Jump
        if (input.isJumpPressed() && this.isGrounded) {
            this.velocity.y = this.jumpForce;
            this.isGrounded = false;
        }
        
        // Gravity
        if (!this.isGrounded) {
            this.velocity.y += this.gravity * deltaTime;
        }
        
        // Apply velocity
        this.position.x += this.velocity.x * deltaTime;
        this.position.y += this.velocity.y * deltaTime;
        this.position.z += this.velocity.z * deltaTime;
        
        // Ground collision
        if (this.position.y <= 1) {
            this.position.y = 1;
            this.velocity.y = 0;
            this.isGrounded = true;
        }
        
        // Update mesh position
        if (this.mesh) {
            this.mesh.position.x = this.position.x;
            this.mesh.position.z = this.position.z;
            this.mesh.position.y = this.position.y - 1;
            
            // In first person, rotate mesh with camera
            if (this.cameraMode === 'first') {
                this.mesh.rotation.y = this.yaw;
            }
        }
        
        // Update camera
        this.updateCamera(deltaTime);
    }
    
    updateCamera(deltaTime) {
        if (this.cameraMode === 'first') {
            // First person camera
            const targetPos = this.position.clone();
            targetPos.y += 0.6; // Eye level
            
            this.camera.position.copy(targetPos);
            
            // Set camera rotation from yaw/pitch
            this.camera.rotation.order = 'YXZ';
            this.camera.rotation.y = this.yaw;
            this.camera.rotation.x = this.pitch;
            
            // Hide player mesh
            if (this.mesh) this.mesh.visible = false;
            
        } else {
            // Third person camera
            // Calculate desired camera position
            const cameraOffset = new THREE.Vector3(0, this.thirdPersonHeight, this.thirdPersonDistance);
            
            // Apply yaw rotation
            cameraOffset.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
            
            // Apply pitch (limited)
            const pitchAxis = new THREE.Vector3(1, 0, 0);
            pitchAxis.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
            cameraOffset.applyAxisAngle(pitchAxis, -this.pitch * 0.5);
            
            const targetCameraPos = this.position.clone().add(cameraOffset);
            
            // Smooth camera movement
            this.camera.position.lerp(targetCameraPos, this.cameraSmoothness);
            
            // Look at player
            const lookTarget = this.position.clone().add(this.cameraTargetOffset);
            this.camera.lookAt(lookTarget);
            
            // Show player mesh
            if (this.mesh) this.mesh.visible = true;
        }
    }
    
    toggleCameraMode() {
        this.cameraMode = this.cameraMode === 'first' ? 'third' : 'first';
        console.log('Camera mode:', this.cameraMode);
    }
    
    teleportTo(position) {
        this.position.copy(position);
        this.position.y = 1;
        this.velocity.set(0, 0, 0);
        
        if (this.mesh) {
            this.mesh.position.copy(position);
            this.mesh.position.y = 0;
        }
    }
    
    getForwardDirection() {
        const forward = new THREE.Vector3(0, 0, -1);
        forward.applyAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
        return forward;
    }
    
    serialize() {
        return {
            position: this.position.toArray(),
            yaw: this.yaw,
            pitch: this.pitch,
            cameraMode: this.cameraMode,
            totalDistance: this.totalDistance,
            combatActions: this.combatActions
        };
    }
    
    deserialize(data) {
        this.position.fromArray(data.position);
        this.yaw = data.yaw || 0;
        this.pitch = data.pitch || 0;
        this.cameraMode = data.cameraMode || 'third';
        this.totalDistance = data.totalDistance || 0;
        this.combatActions = data.combatActions || 0;
        
        if (this.mesh) {
            this.mesh.position.copy(this.position);
            this.mesh.position.y = 0;
        }
    }
}