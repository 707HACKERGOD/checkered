// js/data/npcs.js
export const NPC_DATA = {
    barOwner: {
        name: 'Marcus',
        personality: {
            friendly: true,
            mysterious: true,
            protective: true
        },
        appearance: {
            hairColor: 0x4a4a4a,
            skinColor: 0xd4a574,
            clothesColor: 0x2a1a1a
        },
        homeLocation: 'bar',
        workLocation: 'bar',
        schedule: {
            MORNING: { location: 'bar', activity: 'sleeping' },
            AFTERNOON: { location: 'bar', activity: 'preparing' },
            EVENING: { location: 'bar', activity: 'working' },
            NIGHT: { location: 'bar', activity: 'working' },
            NIGHT2: { location: 'bar', activity: 'closing' }
        },
        dialogueTree: {
            default: {
                text: "Welcome to The Crossroads. What can I get you?",
                choices: [
                    { text: "Just looking around.", next: 'looking' },
                    { text: "Do I... know you?", next: 'know_you' },
                    { text: "I need a place to stay.", next: 'stay' }
                ]
            },
            relationship_friendly: {
                text: "Syl! Good to see you. How are you holding up?",
                choices: [
                    { text: "I'm managing.", next: 'managing' },
                    { text: "The curse... it's getting worse.", next: 'curse' },
                    { text: "Thanks for everything.", next: 'thanks' }
                ]
            },
            relationship_trusted: {
                text: "My old friend. I've been worried about you.",
                choices: [
                    { text: "Tell me about the old days.", next: 'old_days' },
                    { text: "Is there any way to break this curse?", next: 'break_curse' },
                    { text: "I need your help.", next: 'help' }
                ]
            },
            know_you: {
                text: "...You don't remember? We were close, once. Before your self-imposed exile.",
                next: 'remember'
            },
            stay: {
                text: "There's an attic upstairs. It's not much, but it's yours if you need it.",
                effect: { type: 'unlock_attic' }
            }
        }
    },
    
    shopkeeper: {
        name: 'Ada',
        personality: {
            practical: true,
            nosy: true,
            helpful: true
        },
        appearance: {
            hairColor: 0x8b4513,
            skinColor: 0xffdbac,
            clothesColor: 0x556b2f
        },
        homeLocation: 'shop',
        workLocation: 'shop',
        schedule: {
            MORNING: { location: 'shop', activity: 'working' },
            AFTERNOON: { location: 'shop', activity: 'working' },
            EVENING: { location: 'shop', activity: 'closing' },
            NIGHT: { location: 'house2', activity: 'resting' },
            NIGHT2: { location: 'house2', activity: 'sleeping' }
        },
        dialogueTree: {
            default: {
                text: "Welcome to Ada's General Store. We've got a bit of everything.",
                choices: [
                    { text: "What do you have for sale?", next: 'shop' },
                    { text: "Know anything interesting happening in town?", next: 'gossip' },
                    { text: "Just browsing.", next: 'browse' }
                ]
            },
            shop: {
                text: "Take a look around. Tools, supplies, odds and ends.",
                effect: { type: 'open_shop' }
            },
            gossip: {
                text: "Well, that new bar opened up. Strange fellow running it. And there's been... incidents.",
                next: 'incidents'
            }
        }
    },
    
    students: [
        {
            name: 'Elena',
            personality: { curious: true, ambitious: true },
            appearance: {
                hairColor: 0x1a1a1a,
                skinColor: 0xffdbac,
                clothesColor: 0x4169e1
            },
            homeLocation: 'house1',
            workLocation: 'school',
            dialogueTree: {
                default: {
                    text: "Oh, you're new here! I'm Elena, studying at Millbrook Academy.",
                    choices: [
                        { text: "What do you study?", next: 'study' },
                        { text: "Nice to meet you.", next: 'nice' }
                    ]
                }
            }
        },
        {
            name: 'Victor',
            personality: { wealthy: true, arrogant: true, desperate: true },
            appearance: {
                hairColor: 0xdaa520,
                skinColor: 0xffefd5,
                clothesColor: 0x800020
            },
            homeLocation: 'house3',
            workLocation: 'school',
            dialogueTree: {
                default: {
                    text: "Hmm. You look like someone who could use some money. I have a... proposition.",
                    choices: [
                        { text: "I'm listening.", next: 'proposition' },
                        { text: "Not interested.", next: 'refuse' }
                    ]
                },
                proposition: {
                    text: "I need someone to infiltrate the Academy. Get me something from the headmaster's office. I'll pay well.",
                    choices: [
                        { text: "What's in it for me?", next: 'reward' },
                        { text: "What do you need?", next: 'details' },
                        { text: "Sounds illegal.", next: 'legal' }
                    ]
                },
                reward: {
                    text: "5000 credits. Plus, you'll have a friend in high places. The Ashford family has... connections.",
                    effect: { type: 'start_quest', quest: 'school_infiltration' }
                }
            }
        }
    ],
    
    keyCharacters: [
        // Week 1
        {
            name: 'Iris',
            personality: { gentle: true, psychic: true, isolated: true },
            appearance: {
                hairColor: 0xe6e6fa,
                skinColor: 0xfff0f5,
                clothesColor: 0x9370db
            },
            homeLocation: 'park',
            workLocation: 'park',
            specialAbility: 'sanity_boost',
            dialogueTree: {
                default: {
                    text: "...You're different. I can feel it. There's something inside you.",
                    choices: [
                        { text: "You can sense it?", next: 'sense' },
                        { text: "Stay away from me.", next: 'away' }
                    ]
                },
                sense: {
                    text: "I've always been... sensitive. To things others can't see. You carry a heavy burden.",
                    next: 'burden'
                }
            }
        },
        // Week 2
        {
            name: 'Jack',
            personality: { tough: true, scarred: true, loyal: true },
            appearance: {
                hairColor: 0x2f4f4f,
                skinColor: 0xd2691e,
                clothesColor: 0x696969
            },
            homeLocation: 'streets',
            workLocation: 'streets',
            specialAbility: 'combat_training',
            dialogueTree: {
                default: {
                    text: "You look like you've seen some things. Takes one to know one.",
                    choices: [
                        { text: "What happened to you?", next: 'story' },
                        { text: "I keep to myself.", next: 'alone' }
                    ]
                }
            }
        },
        // Week 3
        {
            name: 'Dr. Chen',
            personality: { scientific: true, obsessive: true, helpful: true },
            appearance: {
                hairColor: 0x1a1a1a,
                skinColor: 0xffdead,
                clothesColor: 0xf0f0f0
            },
            homeLocation: 'house2',
            workLocation: 'school',
            specialAbility: 'medical_knowledge',
            dialogueTree: {
                default: {
                    text: "Fascinating physiological indicators... Oh, excuse me. Dr. Chen, research physician.",
                    choices: [
                        { text: "What are you researching?", next: 'research' },
                        { text: "Can you help me?", next: 'help' }
                    ]
                }
            }
        },
        // Week 4
        {
            name: 'Raven',
            personality: { mysterious: true, artistic: true, nocturnal: true },
            appearance: {
                hairColor: 0x0a0a0a,
                skinColor: 0xfaf0e6,
                clothesColor: 0x0a0a0a
            },
            homeLocation: 'forest',
            workLocation: 'park',
            specialAbility: 'stealth_tips',
            dialogueTree: {
                default: {
                    text: "Another creature of the night. I see you wandering when others sleep.",
                    choices: [
                        { text: "You watch me?", next: 'watch' },
                        { text: "The night is... safer for me.", next: 'safer' }
                    ]
                }
            }
        },
        // Week 5
        {
            name: 'Old Tom',
            personality: { wise: true, cryptic: true, kind: true },
            appearance: {
                hairColor: 0xc0c0c0,
                skinColor: 0xdeb887,
                clothesColor: 0x8b4513
            },
            homeLocation: 'park',
            workLocation: 'park',
            specialAbility: 'lore_knowledge',
            dialogueTree: {
                default: {
                    text: "Sit, sit. These old bones have seen much. Your affliction... it's not the first of its kind.",
                    choices: [
                        { text: "You know about the curse?", next: 'curse' },
                        { text: "Tell me everything.", next: 'everything' }
                    ]
                }
            }
        },
        // Week 6
        {
            name: 'Mika',
            personality: { energetic: true, optimistic: true, brave: true },
            appearance: {
                hairColor: 0xff6347,
                skinColor: 0xffdbac,
                clothesColor: 0x32cd32
            },
            homeLocation: 'house1',
            workLocation: 'shop',
            specialAbility: 'morale_boost',
            dialogueTree: {
                default: {
                    text: "Hey! You're the new person everyone's whispering about! I'm Mika!",
                    choices: [
                        { text: "What are they saying?", next: 'rumors' },
                        { text: "Nice to meet you, Mika.", next: 'nice' }
                    ]
                }
            }
        },
        // Week 7
        {
            name: 'Father Gabriel',
            personality: { devout: true, conflicted: true, protective: true },
            appearance: {
                hairColor: 0x696969,
                skinColor: 0xfaebd7,
                clothesColor: 0x1a1a1a
            },
            homeLocation: 'house3',
            workLocation: 'streets',
            specialAbility: 'sanctuary',
            dialogueTree: {
                default: {
                    text: "My child... I sense a great darkness within you. But also, a fighting spirit.",
                    choices: [
                        { text: "Can faith help me?", next: 'faith' },
                        { text: "I'm beyond saving.", next: 'beyond' }
                    ]
                }
            }
        },
        // Week 8
        {
            name: 'Zero',
            personality: { hacker: true, paranoid: true, rebellious: true },
            appearance: {
                hairColor: 0x00ff00,
                skinColor: 0xfaf0e6,
                clothesColor: 0x0a0a0a
            },
            homeLocation: 'streets',
            workLocation: 'streets',
            specialAbility: 'information_network',
            dialogueTree: {
                default: {
                    text: "Yo. I know who you are. Well, what you are. The network sees everything.",
                    choices: [
                        { text: "What network?", next: 'network' },
                        { text: "Then you know to stay away.", next: 'away' }
                    ]
                }
            }
        },
        // Week 9
        {
            name: 'Nurse Helena',
            personality: { caring: true, secretive: true, experienced: true },
            appearance: {
                hairColor: 0x8b0000,
                skinColor: 0xffe4e1,
                clothesColor: 0xf0f0f0
            },
            homeLocation: 'house2',
            workLocation: 'school',
            specialAbility: 'healing_supplies',
            dialogueTree: {
                default: {
                    text: "Another patient who won't come to the clinic. Let me guess - unusual symptoms?",
                    choices: [
                        { text: "You could say that.", next: 'symptoms' },
                        { text: "I heal on my own.", next: 'heal' }
                    ]
                }
            }
        },
        // Week 10
        {
            name: 'The Stranger',
            personality: { enigmatic: true, powerful: true, testing: true },
            appearance: {
                hairColor: 0x4a0080,
                skinColor: 0xdcdcdc,
                clothesColor: 0x2f2f2f
            },
            homeLocation: 'forest',
            workLocation: 'forest',
            specialAbility: 'final_revelation',
            dialogueTree: {
                default: {
                    text: "We meet at last. I've been watching your journey with great interest.",
                    choices: [
                        { text: "Who are you?", next: 'identity' },
                        { text: "Are you the one who cursed me?", next: 'curse' }
                    ]
                },
                identity: {
                    text: "I am... a possibility. A future you might have. Or might prevent.",
                    next: 'future'
                }
            }
        }
    ]
};