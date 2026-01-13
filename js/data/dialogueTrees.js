// js/data/dialogueTrees.js - Unified dialogue structure
import { DIALOGUE_DATA } from './dialogue.js';
import { NPC_DATA } from './npcs.js';

export const DIALOGUE_TREES = {
    // Marcus (Bar Owner)
    marcus: {
        // First visit - determined by personality
        firstVisit_good: DIALOGUE_DATA.marcus.firstVisit_good || {
            text: "Syl... it really is you. After all these years.",
            choices: [
                { text: "You remember me?", next: 'remember_good' },
                { text: "Thank you, Marcus.", next: 'thanks', helpful: true }
            ]
        },
        
        firstVisit_bad: DIALOGUE_DATA.marcus.firstVisit_bad || {
            text: "You. I wasn't sure if I'd ever see you again. Or if I wanted to.",
            choices: [
                { text: "Things were complicated.", next: 'complicated' },
                { text: "I don't need your judgment.", next: 'judgment' },
                { text: "I need a place to stay.", next: 'need_stay' }
            ]
        },
        
        // Standard dialogue branches (based on relationship)
        default: DIALOGUE_DATA.marcus.default || NPC_DATA.barOwner.dialogueTree.default,
        relationship_friendly: DIALOGUE_DATA.marcus.return_friendly || NPC_DATA.barOwner.dialogueTree.relationship_friendly,
        relationship_trusted: NPC_DATA.barOwner.dialogueTree.relationship_trusted,
        
        // Additional dialogue nodes from DIALOGUE_DATA
        ...DIALOGUE_DATA.marcus
    },
    
    // Ada (Shopkeeper)
    ada: {
        default: DIALOGUE_DATA.ada.default || NPC_DATA.shopkeeper.dialogueTree.default,
        relationship_friendly: {
            text: "Back again? You're becoming a regular! Anything special you're looking for?",
            choices: [
                { text: "Just browsing.", next: 'browse' },
                { text: "Got any new gossip?", next: 'gossip' },
                { text: "Show me your wares.", next: 'shop', effect: { type: 'open_shop' } }
            ]
        }
    },
    
    // Victor
    victor: {
        default: DIALOGUE_DATA.victor.firstMeeting || NPC_DATA.students[1].dialogueTree.default,
        relationship_friendly: {
            text: "My stand-in! How goes the Academy? Not too boring, I hope.",
            choices: [
                { text: "It's fine. Your teachers suspect nothing.", next: 'teachers' },
                { text: "I need more money.", next: 'money' },
                { text: "This arrangement might not last.", next: 'warning' }
            ]
        }
    },
    
    // Elena
    elena: {
        default: NPC_DATA.students[0].dialogueTree.default,
        relationship_friendly: {
            text: "Oh, hi again! I was just studying for the mechanics exam. It's so hard!",
            choices: [
                { text: "Need any help?", next: 'help', helpful: true },
                { text: "Good luck with that.", next: 'luck' },
                { text: "I have to go.", next: 'leave' }
            ]
        }
    },
    
    // Generic student
    student_generic: {
        default: {
            text: "Hey. You new around here?",
            choices: [
                { text: "Yeah, just arrived.", next: 'new' },
                { text: "Just passing through.", next: 'passing' },
                { text: "What's there to do here?", next: 'activities' }
            ]
        }
    }
};