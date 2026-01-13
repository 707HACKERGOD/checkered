// js/engine/AudioManager.js
export class AudioManager {
    constructor() {
        this.audioContext = null;
        this.masterVolume = 0.7;
        this.musicVolume = 0.5;
        this.sfxVolume = 0.8;
        
        this.currentMusic = null;
        this.musicGain = null;
        this.sfxGain = null;
        
        // Sound buffers cache
        this.buffers = new Map();
        
        // Music tracks (using Web Audio API oscillators for prototype)
        this.musicTracks = {
            menu: { baseFreq: 110, type: 'ambient' },
            ambient: { baseFreq: 80, type: 'ambient' },
            possession: { baseFreq: 150, type: 'intense' },
            combat: { baseFreq: 200, type: 'intense' }
        };
        
        this.init();
    }
    
    init() {
        // Initialize on first user interaction
        document.addEventListener('click', () => this.initContext(), { once: true });
        document.addEventListener('keydown', () => this.initContext(), { once: true });
    }
    
    initContext() {
        if (this.audioContext) return;
        
        this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
        
        // Create master gain
        this.masterGain = this.audioContext.createGain();
        this.masterGain.gain.value = this.masterVolume;
        this.masterGain.connect(this.audioContext.destination);
        
        // Create music gain
        this.musicGain = this.audioContext.createGain();
        this.musicGain.gain.value = this.musicVolume;
        this.musicGain.connect(this.masterGain);
        
        // Create SFX gain
        this.sfxGain = this.audioContext.createGain();
        this.sfxGain.gain.value = this.sfxVolume;
        this.sfxGain.connect(this.masterGain);
        
        console.log('Audio context initialized');
    }
    
    playMusic(trackName) {
        if (!this.audioContext) return;
        
        // Stop current music
        this.stopMusic();
        
        const track = this.musicTracks[trackName];
        if (!track) return;
        
        // Create procedural music based on track type
        this.currentMusic = this.createProceduralMusic(track);
    }
    
    createProceduralMusic(track) {
        const nodes = [];
        
        if (track.type === 'ambient') {
            // Create dark ambient drone
            const drone = this.audioContext.createOscillator();
            drone.type = 'sawtooth';
            drone.frequency.value = track.baseFreq;
            
            const droneGain = this.audioContext.createGain();
            droneGain.gain.value = 0.1;
            
            const filter = this.audioContext.createBiquadFilter();
            filter.type = 'lowpass';
            filter.frequency.value = 200;
            
            drone.connect(filter);
            filter.connect(droneGain);
            droneGain.connect(this.musicGain);
            
            drone.start();
            nodes.push(drone);
            
            // Add subtle LFO modulation
            const lfo = this.audioContext.createOscillator();
            lfo.type = 'sine';
            lfo.frequency.value = 0.1;
            
            const lfoGain = this.audioContext.createGain();
            lfoGain.gain.value = 10;
            
            lfo.connect(lfoGain);
            lfoGain.connect(drone.frequency);
            
            lfo.start();
            nodes.push(lfo);
            
            // Add occasional low rumble
            const rumbleInterval = setInterval(() => {
                if (!this.currentMusic) {
                    clearInterval(rumbleInterval);
                    return;
                }
                this.playRumble();
            }, 8000 + Math.random() * 4000);
            
        } else if (track.type === 'intense') {
            // Create intense pulsing music
            const pulse = this.audioContext.createOscillator();
            pulse.type = 'square';
            pulse.frequency.value = track.baseFreq;
            
            const pulseGain = this.audioContext.createGain();
            pulseGain.gain.value = 0;
            
            const filter = this.audioContext.createBiquadFilter();
            filter.type = 'lowpass';
            filter.frequency.value = 400;
            
            pulse.connect(filter);
            filter.connect(pulseGain);
            pulseGain.connect(this.musicGain);
            
            pulse.start();
            nodes.push(pulse);
            
            // Rhythmic gain modulation
            const rhythm = this.audioContext.createOscillator();
            rhythm.type = 'square';
            rhythm.frequency.value = 2; // 120 BPM feel
            
            const rhythmGain = this.audioContext.createGain();
            rhythmGain.gain.value = 0.15;
            
            rhythm.connect(rhythmGain);
            rhythmGain.connect(pulseGain.gain);
            
            rhythm.start();
            nodes.push(rhythm);
            
            // Bass drone
            const bass = this.audioContext.createOscillator();
            bass.type = 'sine';
            bass.frequency.value = track.baseFreq / 2;
            
            const bassGain = this.audioContext.createGain();
            bassGain.gain.value = 0.2;
            
            bass.connect(bassGain);
            bassGain.connect(this.musicGain);
            
            bass.start();
            nodes.push(bass);
        }
        
        return { nodes, track };
    }
    
    playRumble() {
        if (!this.audioContext) return;
        
        const rumble = this.audioContext.createOscillator();
        rumble.type = 'sine';
        rumble.frequency.value = 30 + Math.random() * 20;
        
        const rumbleGain = this.audioContext.createGain();
        rumbleGain.gain.value = 0.3;
        
        rumble.connect(rumbleGain);
        rumbleGain.connect(this.musicGain);
        
        rumble.start();
        
        // Fade out
        rumbleGain.gain.exponentialRampToValueAtTime(0.001, this.audioContext.currentTime + 3);
        
        setTimeout(() => {
            rumble.stop();
        }, 3000);
    }
    
    stopMusic() {
        if (this.currentMusic) {
            this.currentMusic.nodes.forEach(node => {
                try {
                    node.stop();
                } catch (e) {
                    // Already stopped
                }
            });
            this.currentMusic = null;
        }
    }
    
    playSound(soundName) {
        if (!this.audioContext) return;
        
        // Create procedural sounds
        switch (soundName) {
            case 'click':
                this.playClick();
                break;
            case 'craft':
                this.playCraft();
                break;
            case 'hit':
                this.playHit();
                break;
            case 'footstep':
                this.playFootstep();
                break;
            case 'whisper':
                this.playWhisper();
                break;
            case 'possession_warning':
                this.playPossessionWarning();
                break;
        }
    }
    
    playClick() {
        const osc = this.audioContext.createOscillator();
        osc.type = 'square';
        osc.frequency.value = 800;
        
        const gain = this.audioContext.createGain();
        gain.gain.value = 0.1;
        
        osc.connect(gain);
        gain.connect(this.sfxGain);
        
        osc.start();
        gain.gain.exponentialRampToValueAtTime(0.001, this.audioContext.currentTime + 0.05);
        
        setTimeout(() => osc.stop(), 50);
    }
    
    playCraft() {
        // Metallic clang sound
        const osc1 = this.audioContext.createOscillator();
        osc1.type = 'triangle';
        osc1.frequency.value = 400;
        
        const osc2 = this.audioContext.createOscillator();
        osc2.type = 'square';
        osc2.frequency.value = 600;
        
        const gain = this.audioContext.createGain();
        gain.gain.value = 0.15;
        
        osc1.connect(gain);
        osc2.connect(gain);
        gain.connect(this.sfxGain);
        
        osc1.start();
        osc2.start();
        
        gain.gain.exponentialRampToValueAtTime(0.001, this.audioContext.currentTime + 0.3);
        
        setTimeout(() => {
            osc1.stop();
            osc2.stop();
        }, 300);
    }
    
    playHit() {
        // Impact sound
        const noise = this.audioContext.createBufferSource();
        const buffer = this.audioContext.createBuffer(1, this.audioContext.sampleRate * 0.1, this.audioContext.sampleRate);
        const data = buffer.getChannelData(0);
        
        for (let i = 0; i < data.length; i++) {
            data[i] = (Math.random() * 2 - 1) * Math.exp(-i / 1000);
        }
        
        noise.buffer = buffer;
        
        const filter = this.audioContext.createBiquadFilter();
        filter.type = 'lowpass';
        filter.frequency.value = 1000;
        
        const gain = this.audioContext.createGain();
        gain.gain.value = 0.3;
        
        noise.connect(filter);
        filter.connect(gain);
        gain.connect(this.sfxGain);
        
        noise.start();
    }
    
    playFootstep() {
        const noise = this.audioContext.createBufferSource();
        const buffer = this.audioContext.createBuffer(1, this.audioContext.sampleRate * 0.05, this.audioContext.sampleRate);
        const data = buffer.getChannelData(0);
        
        for (let i = 0; i < data.length; i++) {
            data[i] = (Math.random() * 2 - 1) * Math.exp(-i / 500);
        }
        
        noise.buffer = buffer;
        
        const filter = this.audioContext.createBiquadFilter();
        filter.type = 'lowpass';
        filter.frequency.value = 500;
        
        const gain = this.audioContext.createGain();
        gain.gain.value = 0.1;
        
        noise.connect(filter);
        filter.connect(gain);
        gain.connect(this.sfxGain);
        
        noise.start();
    }
    
    playWhisper() {
        // Creepy whisper effect for sanity cues
        const noise = this.audioContext.createBufferSource();
        const buffer = this.audioContext.createBuffer(1, this.audioContext.sampleRate * 2, this.audioContext.sampleRate);
        const data = buffer.getChannelData(0);
        
        for (let i = 0; i < data.length; i++) {
            data[i] = (Math.random() * 2 - 1) * 0.5 * (1 + Math.sin(i / 1000));
        }
        
        noise.buffer = buffer;
        
        const filter = this.audioContext.createBiquadFilter();
        filter.type = 'bandpass';
        filter.frequency.value = 2000;
        filter.Q.value = 5;
        
        const gain = this.audioContext.createGain();
        gain.gain.value = 0.05;
        
        noise.connect(filter);
        filter.connect(gain);
        gain.connect(this.sfxGain);
        
        noise.start();
        
        gain.gain.exponentialRampToValueAtTime(0.001, this.audioContext.currentTime + 2);
    }
    
    playPossessionWarning() {
        // Heartbeat-like sound
        const playBeat = (delay) => {
            setTimeout(() => {
                const osc = this.audioContext.createOscillator();
                osc.type = 'sine';
                osc.frequency.value = 60;
                
                const gain = this.audioContext.createGain();
                gain.gain.value = 0.4;
                
                osc.connect(gain);
                gain.connect(this.sfxGain);
                
                osc.start();
                gain.gain.exponentialRampToValueAtTime(0.001, this.audioContext.currentTime + 0.3);
                
                setTimeout(() => osc.stop(), 300);
            }, delay);
        };
        
        // Double beat pattern
        playBeat(0);
        playBeat(200);
    }
    
    setMasterVolume(value) {
        this.masterVolume = value;
        if (this.masterGain) {
            this.masterGain.gain.value = value;
        }
    }
    
    setMusicVolume(value) {
        this.musicVolume = value;
        if (this.musicGain) {
            this.musicGain.gain.value = value;
        }
    }
    
    setSFXVolume(value) {
        this.sfxVolume = value;
        if (this.sfxGain) {
            this.sfxGain.gain.value = value;
        }
    }
}