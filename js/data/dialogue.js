// js/data/dialogue.js - CORRECTED LORE
export const DIALOGUE_DATA = {
    greetings: {
        neutral: [
            "Hello there.",
            "Can I help you?",
            "What do you need?"
        ],
        friendly: [
            "Good to see you!",
            "Hey! How are you?",
            "Always nice to see a familiar face."
        ],
        suspicious: [
            "What do you want?",
            "I've heard things about you...",
            "Make it quick."
        ],
        fearful: [
            "P-please... don't...",
            "Stay back!",
            "I don't want any trouble..."
        ]
    },
    
    // Bar Owner - Marcus (key NPC with history)
    marcus: {
        firstVisit_good: {
            text: "...Syl? Is that really you?",
            speaker: "Marcus",
            choices: [
                { text: "It's me, Marcus.", next: "marcus_recognize_good" },
                { text: "You remember.", next: "marcus_recognize_good" },
                { text: "I saw the newspaper.", next: "marcus_newspaper" }
            ]
        },
        marcus_recognize_good: {
            text: "After two years... I didn't think you'd come. I didn't think you could. Look, about how I left—",
            speaker: "Marcus",
            choices: [
                { text: "You found out what I am.", next: "marcus_curse" },
                { text: "You abandoned me.", next: "marcus_abandoned" },
                { text: "I'm not here to talk about that.", next: "marcus_business" }
            ]
        },
        marcus_curse: {
            text: "The Chichiru. Yes. I saw what happened in the woods that night. The... the blood. But you were fighting it, Syl. I saw you fighting. I was just... scared. I'm sorry.",
            speaker: "Marcus",
            choices: [
                { text: "I understand.", next: "marcus_forgive", helpful: true, relationshipEffect: 10 },
                { text: "Being scared doesn't excuse leaving.", next: "marcus_notexcused" },
                { text: "I need a place to stay.", next: "marcus_stay" }
            ]
        },
        marcus_abandoned: {
            text: "I know. I know I did. You took me in when I had nothing, and when things got hard, I ran. There's no excuse. But I want to make it right.",
            speaker: "Marcus",
            choices: [
                { text: "How?", next: "marcus_makeitright", helpful: true },
                { text: "Words are cheap.", next: "marcus_cheap" },
                { text: "Just give me a room.", next: "marcus_stay" }
            ]
        },
        marcus_stay: {
            text: "The attic. It's not much—just a bed and some storage—but it's yours. For as long as you need. No questions asked.",
            speaker: "Marcus",
            effect: { type: "unlock_attic" },
            choices: [
                { text: "Thank you, Marcus.", next: "marcus_end", helpful: true, relationshipEffect: 5 },
                { text: "I won't forget this.", next: "marcus_end" }
            ]
        },
        marcus_newspaper: {
            text: "The grand opening? Ha. It's just a bar in a bigger city. I thought... I thought if I started over somewhere new, I could forget. But I couldn't stop thinking about you out there, alone.",
            speaker: "Marcus",
            choices: [
                { text: "I've been alone my whole life.", next: "marcus_alone" },
                { text: "You could have written.", next: "marcus_written" },
                { text: "I need a place to stay.", next: "marcus_stay" }
            ]
        },
        
        firstVisit_bad: {
            text: "You. I heard someone matching your description arrived in town. Causing trouble already.",
            speaker: "Marcus",
            choices: [
                { text: "Nice to see you too, Marcus.", next: "marcus_sarcastic" },
                { text: "I need a room.", next: "marcus_room_bad" },
                { text: "I'm not here for a lecture.", next: "marcus_lecture" }
            ]
        },
        marcus_room_bad: {
            text: "After everything? You've got nerve showing up here. ...Fine. There's an attic. But if you bring trouble to my doorstep, you're out. Understood?",
            speaker: "Marcus",
            effect: { type: "unlock_attic" },
            choices: [
                { text: "Crystal clear.", next: "marcus_end" },
                { text: "I don't need your charity.", next: "marcus_charity" }
            ]
        },
        marcus_end: {
            text: "The stairs are in the back. Try to stay out of trouble, Syl.",
            speaker: "Marcus"
        },
        
        // Return visits
        return_friendly: {
            text: "Syl. How are you holding up? The curse... is it getting any easier?",
            speaker: "Marcus",
            choices: [
                { text: "Some days are better than others.", next: "marcus_days" },
                { text: "I'm learning to control it.", next: "marcus_control" },
                { text: "I need supplies.", next: "marcus_supplies" }
            ]
        },
        return_neutral: {
            text: "What'll it be?",
            speaker: "Marcus",
            choices: [
                { text: "Just checking in.", next: "marcus_checkin" },
                { text: "Any work available?", next: "marcus_work" },
                { text: "Nothing. Just passing through.", next: "marcus_end" }
            ]
        }
    },
    
    // Victor - Rich student who offers the school infiltration deal
    victor: {
        firstMeeting: {
            text: "Well, well. You look like someone who's fallen on hard times. Or perhaps... someone who never had them good to begin with. I'm Victor. Victor Ashford.",
            speaker: "Victor",
            choices: [
                { text: "What do you want?", next: "victor_want" },
                { text: "I don't need your pity.", next: "victor_pity" },
                { text: "Ashford? The rich family?", next: "victor_family" }
            ]
        },
        victor_want: {
            text: "Straight to business. I like that. Here's the situation: I'm enrolled at Millbrook Trade Academy. Family insists. But I have... other commitments. I need someone to attend in my place.",
            speaker: "Victor",
            choices: [
                { text: "Attend school for you?", next: "victor_school" },
                { text: "What's in it for me?", next: "victor_reward" },
                { text: "Not interested.", next: "victor_refuse" }
            ]
        },
        victor_school: {
            text: "Think of it as... an arrangement. You get meals, shelter, education. I get freedom. The teachers barely pay attention anyway. Just show up, don't cause trouble, and we both win.",
            speaker: "Victor",
            choices: [
                { text: "Why a trade school? You're rich.", next: "victor_whytrade" },
                { text: "Deal.", next: "victor_accept", effect: { type: "start_quest", quest: "school_infiltration" } },
                { text: "I need to think about it.", next: "victor_think" }
            ]
        },
        victor_whytrade: {
            text: "Father's idea of 'building character.' Learning how the common folk live. As if I'd ever need to fix a pipe or wire a house. The irony isn't lost on me.",
            speaker: "Victor",
            choices: [
                { text: "Must be nice to have options.", next: "victor_options" },
                { text: "Fine. I'll do it.", next: "victor_accept", effect: { type: "start_quest", quest: "school_infiltration" } }
            ]
        },
        victor_reward: {
            text: "Smart. I'll cover your basic expenses—food, lodging. And if you do well? Let's say the Ashford family has ways of making problems... disappear. Useful connections for someone like you.",
            speaker: "Victor",
            choices: [
                { text: "Someone like me?", next: "victor_someonelikeme" },
                { text: "Deal.", next: "victor_accept", effect: { type: "start_quest", quest: "school_infiltration" } }
            ]
        },
        victor_accept: {
            text: "Excellent. Report to the Academy tomorrow morning. They'll be expecting 'Victor Ashford.' Try not to embarrass the name too badly.",
            speaker: "Victor",
            effect: { type: "start_quest", quest: "school_infiltration" }
        },
        victor_refuse: {
            text: "Suit yourself. But if you change your mind, I'm usually around the plaza. The offer stands.",
            speaker: "Victor"
        }
    },
    
    // Ada - Shopkeeper
    ada: {
        default: {
            text: "Welcome to my shop! You're new in town, aren't you? I'm Ada. We've got a bit of everything here—tools, supplies, odds and ends.",
            speaker: "Ada",
            choices: [
                { text: "What do you have for sale?", next: "ada_shop", effect: { type: "open_shop" } },
                { text: "Know anything about this town?", next: "ada_gossip" },
                { text: "Just looking around.", next: "ada_looking" }
            ]
        },
        ada_gossip: {
            text: "Millbrook? It's quiet, mostly. Though lately... there's been talk. People going missing at night. The authorities say wild animals, but between you and me, that's nonsense. Animals don't pick locks.",
            speaker: "Ada",
            choices: [
                { text: "Missing people?", next: "ada_missing" },
                { text: "I'll be careful.", next: "ada_careful" },
                { text: "Show me your wares.", next: "ada_shop", effect: { type: "open_shop" } }
            ]
        },
        ada_missing: {
            text: "Three this month alone. A dockworker, a student, and old Mrs. Chen from the bakery. No bodies, no witnesses. Just... gone. Started about two years ago, actually. Around when that new bar opened.",
            speaker: "Ada",
            choices: [
                { text: "The Crossroads Bar?", next: "ada_bar" },
                { text: "That's concerning.", next: "ada_concerning" },
                { text: "Thanks for the warning.", next: "ada_end" }
            ]
        },
        ada_bar: {
            text: "That's the one. The owner keeps to himself. Seems nice enough, but... there's something sad about him. Like he's waiting for someone. Or hiding from something.",
            speaker: "Ada",
            choices: [
                { text: "I see. Thanks for the information.", next: "ada_end" },
                { text: "I might know him.", next: "ada_knowhim" }
            ]
        },
        ada_end: {
            text: "Stay safe out there. And come back if you need anything!",
            speaker: "Ada"
        }
    },
    
    // Key Characters dialogue stubs (to be expanded)
    keyCharacters: {
        // Week 1 - Iris (psychic/sensitive)
        iris: {
            firstMeeting: {
                text: "...You carry something dark with you. I can feel it pressing against you, trying to break free. But you're fighting it. That takes strength.",
                speaker: "Iris",
                choices: [
                    { text: "You can sense the Chichiru?", next: "iris_sense" },
                    { text: "Stay away from me. For your own safety.", next: "iris_away" },
                    { text: "How do you know that?", next: "iris_how" }
                ]
            }
        },
        
        // Week 7 - Dr. Mira (substitute psychiatrist)
        dr_mira: {
            firstMeeting: {
                text: "You must be the new transfer student. I'm Dr. Mira, filling in for the regular counselor. I've noticed some... interesting patterns in your behavior during class. Would you like to talk?",
                speaker: "Dr. Mira",
                choices: [
                    { text: "What kind of patterns?", next: "mira_patterns" },
                    { text: "I'd rather not.", next: "mira_refuse" },
                    { text: "...Maybe that would help.", next: "mira_accept", helpful: true }
                ]
            },
            mira_patterns: {
                text: "The way you watch the exits. How you tense up when too many people get close. The moments where you seem to... drift. Like you're fighting something inside. I've seen trauma before, but yours is different.",
                speaker: "Dr. Mira",
                choices: [
                    { text: "You don't know what you're talking about.", next: "mira_dismiss" },
                    { text: "It's complicated.", next: "mira_complicated" },
                    { text: "Can you help me?", next: "mira_help", helpful: true }
                ]
            }
        }
    }
};