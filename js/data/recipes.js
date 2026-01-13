// js/data/recipes.js
export const RECIPES = [
    // Molotov Cocktail variations
    {
        id: 'molotov_1',
        inputs: ['glass_bottle', 'gasoline'],
        output: 'molotov_cocktail',
        outputName: 'Molotov Cocktail',
        outputDescription: 'Glass bottle filled with gasoline. Add a rag to complete.'
    },
    {
        id: 'molotov_2',
        inputs: ['glass_bottle', 'alcohol'],
        requiresAdditional: 'cloth_rag',
        output: 'molotov_cocktail',
        outputName: 'Alcohol Molotov',
        outputDescription: 'An improvised incendiary using alcohol.'
    },
    
    // Weapons
    {
        id: 'nail_bat',
        inputs: ['wooden_board', 'nails'],
        output: 'nail_bat',
        outputName: 'Nail Bat',
        outputDescription: 'A wooden board with nails hammered through it. Brutal.'
    },
    {
        id: 'glass_knife',
        inputs: ['glass_shard', 'duct_tape'],
        output: 'glass_knife',
        outputName: 'Glass Shiv',
        outputDescription: 'A glass shard wrapped with tape for a handle.',
        customItem: {
            attributes: ['sharp', 'glass', 'handle', 'weapon'],
            damage: 18
        }
    },
    {
        id: 'pipe_mace',
        inputs: ['metal_pipe', 'chain'],
        output: 'pipe_mace',
        outputName: 'Chain Mace',
        outputDescription: 'A pipe with chain wrapped around the end.',
        customItem: {
            attributes: ['blunt', 'metal', 'heavy', 'weapon'],
            damage: 20
        }
    },
    {
        id: 'torch',
        inputs: ['wooden_stick', 'cloth_rag'],
        requiresAdditional: 'oil',
        output: 'torch',
        outputName: 'Makeshift Torch',
        outputDescription: 'A stick with an oil-soaked rag. Light it up.',
        customItem: {
            attributes: ['wood', 'flammable', 'handle', 'luminous'],
            damage: 8
        }
    },
    
    // Traps
    {
        id: 'restraint_trap',
        inputs: ['rope', 'zip_ties'],
        output: 'restraint_trap',
        outputName: 'Leg Restraint',
        outputDescription: 'A trap that binds legs together, slowing movement.'
    },
    {
        id: 'restraint_trap_2',
        inputs: ['chain', 'metal_pipe'],
        output: 'restraint_trap',
        outputName: 'Chain Restraint',
        outputDescription: 'Heavy chains that severely limit mobility.'
    },
    {
        id: 'spike_trap',
        inputs: ['wooden_board', 'nails'],
        requiresWorldObject: 'floor',
        output: 'spike_trap',
        outputName: 'Nail Board Trap',
        outputDescription: 'A board of nails placed on the ground.'
    },
    {
        id: 'spike_trap_2',
        inputs: ['metal_sheet', 'glass_shard'],
        output: 'spike_trap',
        outputName: 'Glass Spike Trap',
        outputDescription: 'Sharpened metal with glass shards attached.'
    },
    {
        id: 'electric_trap',
        inputs: ['battery', 'wire'],
        output: 'electric_trap',
        outputName: 'Shock Wire',
        outputDescription: 'Electrified wire that stuns on contact.'
    },
    {
        id: 'electric_trap_2',
        inputs: ['battery', 'chain'],
        output: 'electric_trap',
        outputName: 'Electric Chain',
        outputDescription: 'An electrified chain trap.'
    },
    {
        id: 'tripwire',
        inputs: ['wire', 'alarm_clock'],
        output: 'tripwire_alarm',
        outputName: 'Tripwire Alarm',
        outputDescription: 'A wire that triggers an alarm when tripped.',
        customItem: {
            attributes: ['trap_component', 'mechanical'],
            canBePlaced: true,
            trapEffect: 'alert'
        }
    },
    
    // Explosives
    {
        id: 'pipe_bomb',
        inputs: ['metal_pipe', 'chemicals'],
        requiresAdditional: 'battery',
        output: 'pipe_bomb',
        outputName: 'Pipe Bomb',
        outputDescription: 'A crude but effective explosive device.'
    },
    {
        id: 'smoke_bomb',
        inputs: ['metal_can', 'chemicals'],
        output: 'smoke_bomb',
        outputName: 'Smoke Bomb',
        outputDescription: 'Creates a thick cloud of smoke when ignited.'
    },
    
    // Healing items
    {
        id: 'medkit',
        inputs: ['bandage', 'alcohol'],
        output: 'medkit',
        outputName: 'Improvised Medkit',
        outputDescription: 'Basic first aid supplies bundled together.'
    },
    {
        id: 'medkit_2',
        inputs: ['bandage', 'medicine'],
        output: 'medkit',
        outputName: 'Medicine Kit',
        outputDescription: 'Bandages combined with pain medication.'
    },
    {
        id: 'splint',
        inputs: ['wooden_stick', 'bandage'],
        output: 'splint',
        outputName: 'Makeshift Splint',
        outputDescription: 'For immobilizing broken limbs.',
        customItem: {
            attributes: ['healing', 'wood'],
            healAmount: 30,
            healType: 'limb'
        }
    },
    
    // Utility items
    {
        id: 'lantern',
        inputs: ['jar', 'candle'],
        output: 'lantern',
        outputName: 'Jar Lantern',
        outputDescription: 'A protected light source.',
        customItem: {
            attributes: ['luminous', 'glass', 'handle'],
            lightRadius: 8
        }
    },
    {
        id: 'oil_lantern',
        inputs: ['metal_can', 'oil'],
        requiresAdditional: 'cloth_rag',
        output: 'oil_lantern',
        outputName: 'Oil Lantern',
        outputDescription: 'A longer-lasting light source.',
        customItem: {
            attributes: ['luminous', 'metal', 'handle', 'flammable'],
            lightRadius: 12
        }
    },
    {
        id: 'lockpick',
        inputs: ['wire', 'screwdriver'],
        output: 'lockpick',
        outputName: 'Improvised Lockpick',
        outputDescription: 'For opening locked doors.',
        customItem: {
            attributes: ['metal', 'mechanical'],
            canUnlock: true
        }
    },
    
    // Filled containers (liquid + container combinations)
    {
        id: 'bottle_gasoline',
        inputs: ['glass_bottle', 'gasoline'],
        output: 'bottle_gasoline',
        outputName: 'Bottle of Gasoline',
        outputDescription: 'A glass bottle filled with gasoline.',
        customItem: {
            attributes: ['container', 'glass', 'flammable', 'liquid', 'filled_container'],
            contents: 'gasoline'
        }
    },
    {
        id: 'bottle_water',
        inputs: ['glass_bottle', 'water'],
        output: 'bottle_water',
        outputName: 'Bottle of Water',
        outputDescription: 'Clean drinking water.',
        customItem: {
            attributes: ['container', 'glass', 'liquid', 'filled_container'],
            contents: 'water'
        }
    },
    {
        id: 'bottle_acid',
        inputs: ['glass_bottle', 'acid'],
        output: 'bottle_acid',
        outputName: 'Bottle of Acid',
        outputDescription: 'Handle with extreme care.',
        customItem: {
            attributes: ['container', 'glass', 'toxic', 'liquid', 'filled_container'],
            contents: 'acid',
            damage: 30
        }
    },
    
    // Gory combinations (using body parts)
    {
        id: 'blood_ritual',
        inputs: ['blood_vial', 'candle'],
        output: 'blood_candle',
        outputName: 'Blood Candle',
        outputDescription: 'A candle infused with blood. Unsettling.',
        customItem: {
            attributes: ['body_part', 'luminous', 'flammable'],
            gory: true,
            sanityEffect: -5
        }
    },
    {
        id: 'finger_necklace',
        inputs: ['finger', 'rope'],
        output: 'finger_necklace',
        outputName: 'Gruesome Necklace',
        outputDescription: 'A necklace made of fingers. Why would you make this?',
        customItem: {
            attributes: ['body_part', 'rope'],
            gory: true,
            sanityEffect: -10,
            npcReaction: 'horror'
        }
    },
    
    // Complex multi-step crafting
    {
        id: 'advanced_trap',
        inputs: ['electric_trap', 'restraint_trap'],
        output: 'shock_restraint',
        outputName: 'Shock Restraint System',
        outputDescription: 'Binds and continuously shocks the victim.',
        customItem: {
            attributes: ['trap_component', 'electronic', 'rope'],
            canBePlaced: true,
            trapEffect: ['slow', 'stun', 'damage'],
            damage: 10,
            continuous: true
        }
    },
    {
        id: 'fire_trap',
        inputs: ['molotov_cocktail', 'tripwire_alarm'],
        output: 'fire_trap',
        outputName: 'Tripwire Fire Trap',
        outputDescription: 'Ignites when triggered.',
        customItem: {
            attributes: ['trap_component', 'explosive', 'flammable'],
            canBePlaced: true,
            trapEffect: 'fire',
            damage: 40,
            areaEffect: true
        }
    }
];

// World objects that can be used in crafting without picking up
export const WORLD_CRAFTING_OBJECTS = {
    puddle: {
        id: 'puddle',
        name: 'Water Puddle',
        attributes: ['liquid', 'water'],
        canExtract: ['water'],
        requiresContainer: true
    },
    oil_spill: {
        id: 'oil_spill',
        name: 'Oil Spill',
        attributes: ['liquid', 'oil', 'slippery', 'flammable'],
        canExtract: ['oil'],
        requiresContainer: true
    },
    campfire: {
        id: 'campfire',
        name: 'Campfire',
        attributes: ['fire_source', 'burning'],
        canIgnite: true
    },
    electrical_outlet: {
        id: 'electrical_outlet',
        name: 'Electrical Outlet',
        attributes: ['electronic', 'conductive'],
        canCharge: true,
        dangerous: true
    },
    floor: {
        id: 'floor',
        name: 'Floor',
        attributes: [],
        canPlaceTraps: true
    },
    wall: {
        id: 'wall',
        name: 'Wall',
        attributes: [],
        canMount: true
    },
    tree: {
        id: 'tree',
        name: 'Tree',
        attributes: ['wood', 'flammable'],
        canHarvest: ['wooden_stick', 'wooden_board']
    },
    dumpster: {
        id: 'dumpster',
        name: 'Dumpster',
        attributes: ['container', 'metal'],
        canSearch: true,
        lootTable: ['cloth_rag', 'newspaper', 'metal_can', 'glass_bottle', 'plastic_bag']
    }
};