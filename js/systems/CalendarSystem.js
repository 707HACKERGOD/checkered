// js/systems/CalendarSystem.js
export class CalendarSystem {
    constructor(timeManager) {
        this.timeManager = timeManager;
        
        // Calendar state
        this.currentDay = 1;
        this.currentWeek = 1;
        this.totalDays = 70; // 10 weeks
        
        // Events tracking
        this.events = new Map(); // day -> array of events
        this.scheduledEvents = [];
        
        // Special dates
        this.keyCharacterDays = [1, 8, 15, 22, 29, 36, 43, 50, 57, 64]; // When key chars appear
        
        // Subscribe to time changes
        if (timeManager) {
            timeManager.onDayChange = (day) => this.onDayChange(day);
        }
        
        this.initializeEvents();
    }
    
    initializeEvents() {
        // Mark key character appearance days
        this.keyCharacterDays.forEach((day, index) => {
            this.addEvent(day, {
                type: 'key_character',
                description: `New character may appear (Week ${index + 1})`,
                important: true
            });
        });
        
        // Final day event
        this.addEvent(70, {
            type: 'ending',
            description: 'The final day approaches...',
            important: true
        });
    }
    
    onDayChange(newDay) {
        this.currentDay = newDay;
        this.currentWeek = Math.ceil(newDay / 7);
        
        // Check for scheduled events
        this.checkScheduledEvents(newDay);
        
        // Trigger day change event
        const event = new CustomEvent('dayChanged', { 
            detail: { 
                day: newDay, 
                week: this.currentWeek,
                remaining: this.totalDays - newDay
            } 
        });
        document.dispatchEvent(event);
    }
    
    addEvent(day, event) {
        if (!this.events.has(day)) {
            this.events.set(day, []);
        }
        this.events.get(day).push({
            ...event,
            id: `event_${day}_${Date.now()}`,
            timestamp: Date.now()
        });
    }
    
    getEventsForDay(day) {
        return this.events.get(day) || [];
    }
    
    recordPossession(day, location, casualties) {
        this.addEvent(day, {
            type: 'possession',
            description: `Possession occurred at ${location}. ${casualties} casualties.`,
            location: location,
            casualties: casualties
        });
    }
    
    recordNPCDeath(day, npcName) {
        this.addEvent(day, {
            type: 'death',
            description: `${npcName} died.`,
            npcName: npcName
        });
    }
    
    recordKeyMoment(day, description) {
        this.addEvent(day, {
            type: 'story',
            description: description,
            important: true
        });
    }
    
    scheduleEvent(day, callback) {
        this.scheduledEvents.push({ day, callback, executed: false });
    }
    
    checkScheduledEvents(currentDay) {
        this.scheduledEvents.forEach(event => {
            if (event.day === currentDay && !event.executed) {
                event.callback();
                event.executed = true;
            }
        });
    }
    
    getDaysRemaining() {
        return Math.max(0, this.totalDays - this.currentDay);
    }
    
    getWeeksRemaining() {
        return Math.max(0, 10 - this.currentWeek);
    }
    
    isLastWeek() {
        return this.currentWeek === 10;
    }
    
    isLastDay() {
        return this.currentDay === this.totalDays;
    }
    
    getProgress() {
        return this.currentDay / this.totalDays;
    }
    
    serialize() {
        return {
            currentDay: this.currentDay,
            currentWeek: this.currentWeek,
            events: Object.fromEntries(this.events),
            scheduledEvents: this.scheduledEvents.map(e => ({
                day: e.day,
                executed: e.executed
            }))
        };
    }
    
    deserialize(data) {
        this.currentDay = data.currentDay;
        this.currentWeek = data.currentWeek;
        this.events = new Map(Object.entries(data.events).map(([k, v]) => [parseInt(k), v]));
    }
}