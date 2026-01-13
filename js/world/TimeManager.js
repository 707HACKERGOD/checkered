// js/world/TimeManager.js
export class TimeManager {
    constructor() {
        // Time settings
        this.dayDuration = 600; // 10 minutes real time = 1 game day
        this.currentTime = 0.25; // Start at 6 AM (0.25 of day)
        
        // Day tracking
        this.currentDay = 1;
        this.totalDays = 70; // 10 weeks
        
        // Time periods
        this.periods = {
            NIGHT: { start: 0, end: 0.25, name: 'Night' },
            MORNING: { start: 0.25, end: 0.42, name: 'Morning' },
            AFTERNOON: { start: 0.42, end: 0.67, name: 'Afternoon' },
            EVENING: { start: 0.67, end: 0.83, name: 'Evening' },
            NIGHT2: { start: 0.83, end: 1.0, name: 'Night' }
        };
        
        this.currentPeriod = 'MORNING';
        this.timeScale = 1; // Can speed up/slow down time
        
        // Callbacks
        this.onPeriodChange = null;
        this.onDayChange = null;
    }
    
    update(deltaTime) {
        // Advance time
        const timeAdvance = (deltaTime / this.dayDuration) * this.timeScale;
        this.currentTime += timeAdvance;
        
        // Check for day rollover
        if (this.currentTime >= 1.0) {
            this.currentTime -= 1.0;
            this.currentDay++;
            
            if (this.onDayChange) {
                this.onDayChange(this.currentDay);
            }
        }
        
        // Update current period
        const newPeriod = this.getPeriodFromTime(this.currentTime);
        if (newPeriod !== this.currentPeriod) {
            this.currentPeriod = newPeriod;
            
            if (this.onPeriodChange) {
                this.onPeriodChange(this.currentPeriod);
            }
        }
    }
    
    getPeriodFromTime(time) {
        for (const [period, data] of Object.entries(this.periods)) {
            if (time >= data.start && time < data.end) {
                return period;
            }
        }
        return 'NIGHT';
    }
    
    get normalizedTime() {
        return this.currentTime;
    }
    
    get periodName() {
        return this.periods[this.currentPeriod]?.name || 'Night';
    }
    
    get currentWeek() {
        return Math.ceil(this.currentDay / 7);
    }
    
    get dayOfWeek() {
        return ((this.currentDay - 1) % 7) + 1;
    }
    
    get remainingDays() {
        return Math.max(0, this.totalDays - this.currentDay);
    }
    
    // Skip to specific time
    skipTo(period) {
        const periodData = this.periods[period];
        if (periodData) {
            this.currentTime = periodData.start;
            this.currentPeriod = period;
        }
    }
    
    // Sleep until next morning
    sleep() {
        const wasNight = this.currentTime > 0.83 || this.currentTime < 0.25;
        
        this.currentTime = 0.25; // Morning
        
        if (wasNight) {
            // Same day, just slept until morning
        } else {
            // Slept into next day
            this.currentDay++;
            if (this.onDayChange) {
                this.onDayChange(this.currentDay);
            }
        }
        
        this.currentPeriod = 'MORNING';
    }
    
    // Get formatted time string
    getTimeString() {
        const hours = Math.floor(this.currentTime * 24);
        const minutes = Math.floor((this.currentTime * 24 * 60) % 60);
        return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}`;
    }
    
    serialize() {
        return {
            currentTime: this.currentTime,
            currentDay: this.currentDay,
            timeScale: this.timeScale
        };
    }
    
    deserialize(data) {
        this.currentTime = data.currentTime;
        this.currentDay = data.currentDay;
        this.timeScale = data.timeScale;
        this.currentPeriod = this.getPeriodFromTime(this.currentTime);
    }
}