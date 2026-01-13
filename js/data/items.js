// js/data/items.js
export const ITEM_ATTRIBUTES = {
    flammable: { description: 'Can catch fire' },
    container: { description: 'Can hold liquids' },
    liquid: { description: 'Is a liquid substance' },
    sharp: { description: 'Can cut or pierce' },
    blunt: { description: 'Can bash or crush' },
    handle: { description: 'Has a grip for holding' },
    rope: { description: 'Can bind or tie' },
    heavy: { description: 'Weighs a lot' },
    conductive: { description: 'Conducts electricity' },
    insulating: { description: 'Blocks electricity' },
    explosive: { description: 'Can explode' },
    toxic: { description: 'Poisonous substance' },
    healing: { description: 'Can restore health' },
    fire_source: { description: 'Produces fire' },
    burning: { description: 'Currently on fire' },
    filled_container: { description: 'Contains a liquid' },
    weapon: { description: 'Can be used to attack' },
    trap_component: { description: 'Can be used in traps' },
    food: { description: 'Can be consumed' },
    cloth: { description: 'Made of fabric' },
    metal: { description: 'Made of metal' },
    wood: { description: 'Made of wood' },
    glass: { description: 'Made of glass, fragile' },
    electronic: { description: 'Contains electronics' },
    sticky: { description: 'Adheres to surfaces' },
    slippery: { description: 'Reduces friction' },
    cold: { description: 'Very cold' },
    mechanical: { description: 'Has moving parts' },
    luminous: { description: 'Emits light' },
    body_part: { description: 'Biological material' }
};

export const ITEMS = {
    // Containers
    glass_bottle: {
        id: 'glass_bottle',
        name: 'Glass Bottle',
        description: 'An empty glass bottle. Can hold liquids.',
        attributes: ['container', 'glass'],
        stackable: true,
        maxStack: 5
    },
    metal_can: {
        id: 'metal_can',
        name: 'Metal Can',
        description: 'A sturdy metal container.',
        attributes: ['container', 'metal'],
        stackable: true,
        maxStack: 10
    },
    bucket: {
        id: 'bucket',
        name: 'Bucket',
        description: 'A large bucket for carrying things.',
        attributes: ['container', 'metal', 'handle'],
        stackable: false
    },
    jar: {
        id: 'jar',
        name: 'Glass Jar',
        description: 'A jar with a lid.',
        attributes: ['container', 'glass'],
        stackable: true,
        maxStack: 5
    },
    
    // Liquids
    gasoline: {
        id: 'gasoline',
        name: 'Gasoline',
        description: 'Highly flammable fuel.',
        attributes: ['liquid', 'flammable', 'toxic'],
        stackable: false,
        requiresContainer: true
    },
    water: {
        id: 'water',
        name: 'Water',
        description: 'Clean drinking water.',
        attributes: ['liquid'],
        stackable: false,
        requiresContainer: true
    },
    oil: {
        id: 'oil',
        name: 'Motor Oil',
        description: 'Slippery lubricant.',
        attributes: ['liquid', 'flammable', 'slippery'],
        stackable: false,
        requiresContainer: true
    },
    alcohol: {
        id: 'alcohol',
        name: 'Rubbing Alcohol',
        description: 'Disinfectant, also flammable.',
        attributes: ['liquid', 'flammable', 'healing'],
        stackable: false,
        requiresContainer: true
    },
    acid: {
        id: 'acid',
        name: 'Battery Acid',
        description: 'Corrosive and dangerous.',
        attributes: ['liquid', 'toxic', 'conductive'],
        stackable: false,
        requiresContainer: true
    },
    
    // Tools
    knife: {
        id: 'knife',
        name: 'Kitchen Knife',
        description: 'A sharp kitchen knife.',
        attributes: ['sharp', 'handle', 'metal'],
        damage: 15,
        stackable: false
    },
    hammer: {
        id: 'hammer',
        name: 'Hammer',
        description: 'A sturdy claw hammer.',
        attributes: ['blunt', 'handle', 'metal', 'heavy'],
        damage: 12,
        stackable: false
    },
    screwdriver: {
        id: 'screwdriver',
        name: 'Screwdriver',
        description: 'A flathead screwdriver.',
        attributes: ['sharp', 'handle', 'metal', 'conductive'],
        damage: 8,
        stackable: false
    },
    wrench: {
        id: 'wrench',
        name: 'Wrench',
        description: 'An adjustable wrench.',
        attributes: ['blunt', 'handle', 'metal', 'heavy'],
        damage: 10,
        stackable: false
    },
    scissors: {
        id: 'scissors',
        name: 'Scissors',
        description: 'Sharp cutting scissors.',
        attributes: ['sharp', 'handle', 'metal'],
        damage: 8,
        stackable: false
    },
    crowbar: {
        id: 'crowbar',
        name: 'Crowbar',
        description: 'Heavy iron crowbar.',
        attributes: ['blunt', 'sharp', 'handle', 'metal', 'heavy'],
        damage: 18,
        stackable: false
    },
    
    // Rope and binding
    rope: {
        id: 'rope',
        name: 'Rope',
        description: 'Strong hemp rope, about 10 meters.',
        attributes: ['rope', 'cloth'],
        stackable: true,
        maxStack: 3
    },
    duct_tape: {
        id: 'duct_tape',
        name: 'Duct Tape',
        description: 'Industrial strength tape.',
        attributes: ['sticky', 'rope', 'cloth'],
        stackable: true,
        maxStack: 5
    },
    zip_ties: {
        id: 'zip_ties',
        name: 'Zip Ties',
        description: 'Plastic cable ties.',
        attributes: ['rope'],
        stackable: true,
        maxStack: 20
    },
    chain: {
        id: 'chain',
        name: 'Metal Chain',
        description: 'Heavy metal chain.',
        attributes: ['rope', 'metal', 'heavy', 'conductive'],
        stackable: false
    },
    wire: {
        id: 'wire',
        name: 'Copper Wire',
        description: 'Coil of copper wire.',
        attributes: ['rope', 'metal', 'conductive'],
        stackable: true,
        maxStack: 5
    },
    
    // Fire-related
    matches: {
        id: 'matches',
        name: 'Matches',
        description: 'A box of matches.',
        attributes: ['fire_source', 'flammable'],
        stackable: true,
        maxStack: 10
    },
    lighter: {
        id: 'lighter',
        name: 'Lighter',
        description: 'A disposable lighter.',
        attributes: ['fire_source', 'flammable'],
        stackable: false
    },
    candle: {
        id: 'candle',
        name: 'Candle',
        description: 'A wax candle.',
        attributes: ['flammable', 'luminous'],
        stackable: true,
        maxStack: 10
    },
    
    // Cloth and fabric
    cloth_rag: {
        id: 'cloth_rag',
        name: 'Cloth Rag',
        description: 'A piece of torn cloth.',
        attributes: ['cloth', 'flammable'],
        stackable: true,
        maxStack: 10
    },
    bandage: {
        id: 'bandage',
        name: 'Bandage',
        description: 'Medical bandage wrap.',
        attributes: ['cloth', 'healing'],
        stackable: true,
        maxStack: 10,
        healAmount: 15
    },
    blanket: {
        id: 'blanket',
        name: 'Blanket',
        description: 'A wool blanket.',
        attributes: ['cloth', 'insulating', 'flammable'],
        stackable: false
    },
    
    // Electronics
    battery: {
        id: 'battery',
        name: 'Battery',
        description: 'A 9V battery.',
        attributes: ['electronic', 'conductive'],
        stackable: true,
        maxStack: 10
    },
    flashlight: {
        id: 'flashlight',
        name: 'Flashlight',
        description: 'A handheld flashlight.',
        attributes: ['electronic', 'luminous', 'handle', 'blunt'],
        stackable: false
    },
    radio: {
        id: 'radio',
        name: 'Portable Radio',
        description: 'A small transistor radio.',
        attributes: ['electronic', 'mechanical'],
        stackable: false
    },
    alarm_clock: {
        id: 'alarm_clock',
        name: 'Alarm Clock',
        description: 'A wind-up alarm clock.',
        attributes: ['mechanical', 'metal'],
        stackable: false
    },
    
    // Wood items
    wooden_board: {
        id: 'wooden_board',
        name: 'Wooden Board',
        description: 'A sturdy wooden plank.',
        attributes: ['wood', 'blunt', 'flammable'],
        damage: 8,
        stackable: true,
        maxStack: 5
    },
    wooden_stick: {
        id: 'wooden_stick',
        name: 'Wooden Stick',
        description: 'A thick wooden stick.',
        attributes: ['wood', 'blunt', 'handle', 'flammable'],
        damage: 6,
        stackable: true,
        maxStack: 10
    },
    
    // Metal items
    nails: {
        id: 'nails',
        name: 'Box of Nails',
        description: 'Assorted metal nails.',
        attributes: ['metal', 'sharp'],
        stackable: true,
        maxStack: 20
    },
    metal_pipe: {
        id: 'metal_pipe',
        name: 'Metal Pipe',
        description: 'A section of steel pipe.',
        attributes: ['metal', 'blunt', 'heavy', 'handle'],
        damage: 14,
        stackable: false
    },
    metal_sheet: {
        id: 'metal_sheet',
        name: 'Metal Sheet',
        description: 'A thin sheet of metal.',
        attributes: ['metal', 'sharp'],
        stackable: true,
        maxStack: 3
    },
    
    // Food and consumables
    canned_food: {
        id: 'canned_food',
        name: 'Canned Food',
        description: 'A can of preserved food.',
        attributes: ['food', 'metal', 'container'],
        stackable: true,
        maxStack: 10
    },
    medicine: {
        id: 'medicine',
        name: 'Medicine Bottle',
        description: 'Pain relievers.',
        attributes: ['healing', 'container'],
        healAmount: 25,
        stackable: true,
        maxStack: 5
    },
    energy_drink: {
        id: 'energy_drink',
        name: 'Energy Drink',
        description: 'Caffeine boost.',
        attributes: ['food', 'liquid', 'container'],
        stackable: true,
        maxStack: 5
    },
    
    // Misc
    brick: {
        id: 'brick',
        name: 'Brick',
        description: 'A heavy clay brick.',
        attributes: ['blunt', 'heavy'],
        damage: 10,
        stackable: true,
        maxStack: 5
    },
    glass_shard: {
        id: 'glass_shard',
        name: 'Glass Shard',
        description: 'A sharp piece of broken glass.',
        attributes: ['glass', 'sharp'],
        damage: 12,
        stackable: true,
        maxStack: 10
    },
    newspaper: {
        id: 'newspaper',
        name: 'Newspaper',
        description: 'Today\'s local paper.',
        attributes: ['flammable'],
        stackable: true,
        maxStack: 10
    },
    plastic_bag: {
        id: 'plastic_bag',
        name: 'Plastic Bag',
        description: 'A large plastic bag.',
        attributes: ['container'],
        stackable: true,
        maxStack: 10
    },
    mirror_shard: {
        id: 'mirror_shard',
        name: 'Mirror Shard',
        description: 'A piece of broken mirror.',
        attributes: ['glass', 'sharp', 'luminous'],
        stackable: true,
        maxStack: 5
    },
    chemicals: {
        id: 'chemicals',
        name: 'Cleaning Chemicals',
        description: 'Various household chemicals.',
        attributes: ['toxic', 'liquid'],
        requiresContainer: true,
        stackable: false
    },
    
    // Crafted items (predefined outputs)
    molotov_cocktail: {
        id: 'molotov_cocktail',
        name: 'Molotov Cocktail',
        description: 'An improvised incendiary device.',
        attributes: ['explosive', 'flammable', 'glass'],
        damage: 40,
        areaEffect: true,
        stackable: false
    },
    nail_bat: {
        id: 'nail_bat',
        name: 'Nail Bat',
        description: 'A wooden board studded with nails.',
        attributes: ['wood', 'sharp', 'blunt', 'weapon'],
        damage: 22,
        stackable: false
    },
    pipe_bomb: {
        id: 'pipe_bomb',
        name: 'Pipe Bomb',
        description: 'A crude explosive device.',
        attributes: ['explosive', 'metal'],
        damage: 60,
        areaEffect: true,
        stackable: false
    },
    restraint_trap: {
        id: 'restraint_trap',
        name: 'Restraint Trap',
        description: 'A trap that binds the victim.',
        attributes: ['trap_component', 'rope'],
        canBePlaced: true,
        trapEffect: 'slow',
        stackable: false
    },
    electric_trap: {
        id: 'electric_trap',
        name: 'Electric Trap',
        description: 'A trap that shocks the victim.',
        attributes: ['trap_component', 'electronic', 'conductive'],
        canBePlaced: true,
        trapEffect: 'stun',
        damage: 15,
        stackable: false
    },
    spike_trap: {
        id: 'spike_trap',
        name: 'Spike Trap',
        description: 'A trap with sharp spikes.',
        attributes: ['trap_component', 'sharp', 'metal'],
        canBePlaced: true,
        trapEffect: 'damage',
        damage: 25,
        stackable: false
    },
    smoke_bomb: {
        id: 'smoke_bomb',
        name: 'Smoke Bomb',
        description: 'Creates a cloud of smoke.',
        attributes: ['explosive', 'toxic'],
        areaEffect: true,
        stackable: true,
        maxStack: 3
    },
    medkit: {
        id: 'medkit',
        name: 'Improvised Medkit',
        description: 'A makeshift first aid kit.',
        attributes: ['healing'],
        healAmount: 50,
        stackable: false
    },
    
    // Body parts (for gory crafting)
    left_eye: {
        id: 'left_eye',
        name: 'Left Eye',
        description: 'Syl\'s missing left eye. It never regenerates.',
        attributes: ['body_part'],
        bodyPart: true,
        unique: true,
        cannotObtain: true // Player starts without this
    },
    finger: {
        id: 'finger',
        name: 'Severed Finger',
        description: 'A human finger.',
        attributes: ['body_part'],
        bodyPart: true,
        stackable: true,
        maxStack: 10
    },
    blood_vial: {
        id: 'blood_vial',
        name: 'Vial of Blood',
        description: 'Fresh blood in a vial.',
        attributes: ['body_part', 'liquid', 'container'],
        bodyPart: true,
        stackable: true,
        maxStack: 5
    }
};

// Starting inventory for new game
export const STARTING_INVENTORY = [
    { id: 'cloth_rag', quantity: 3 },
    { id: 'matches', quantity: 1 },
    { id: 'wooden_stick', quantity: 2 },
    { id: 'bandage', quantity: 2 },
    { id: 'newspaper', quantity: 1 }
];