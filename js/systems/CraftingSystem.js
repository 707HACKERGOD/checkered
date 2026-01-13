// js/systems/CraftingSystem.js - FIXED addItem method
import { ITEMS } from '../data/items.js';
import { RECIPES } from '../data/recipes.js';

export class CraftingSystem {
    constructor() {
        this.inventory = [];
        this.maxInventorySize = 40;
        this.craftCount = 0;
        this.appliedTraps = [];
        this.discoveredRecipes = new Set();
    }
    
    addItem(item) {
        // Validate item
        if (!item) {
            console.error('Attempted to add null/undefined item');
            return false;
        }
        
        // Ensure item has required properties
        const itemToAdd = {
            id: item.id || `item_${Date.now()}`,
            name: item.name || 'Unknown Item',
            description: item.description || '',
            attributes: item.attributes || [],
            quantity: item.quantity || 1,
            stackable: item.stackable !== false, // Default to stackable
            ...item
        };
        
        console.log('Adding item to inventory:', itemToAdd.name);
        
        // Check if stackable and already exists
        if (itemToAdd.stackable) {
            const existing = this.inventory.find(i => i.id === itemToAdd.id);
            if (existing) {
                const maxStack = itemToAdd.maxStack || 99;
                const canAdd = Math.min(itemToAdd.quantity, maxStack - (existing.quantity || 1));
                if (canAdd > 0) {
                    existing.quantity = (existing.quantity || 1) + canAdd;
                    console.log(`Stacked ${canAdd} ${itemToAdd.name}, total: ${existing.quantity}`);
                    return true;
                } else {
                    console.log('Stack is full');
                }
            }
        }
        
        // Check inventory space
        if (this.inventory.length >= this.maxInventorySize) {
            console.log('Inventory full!');
            return false;
        }
        
        // Add as new item
        this.inventory.push(itemToAdd);
        console.log(`Added ${itemToAdd.name} to inventory. Total items: ${this.inventory.length}`);
        return true;
    }
    
    removeItem(itemId, quantity = 1) {
        const index = this.inventory.findIndex(i => i.id === itemId);
        if (index === -1) {
            console.log(`Item ${itemId} not found in inventory`);
            return false;
        }
        
        const item = this.inventory[index];
        if ((item.quantity || 1) > quantity) {
            item.quantity -= quantity;
            console.log(`Removed ${quantity} ${item.name}, ${item.quantity} remaining`);
        } else {
            this.inventory.splice(index, 1);
            console.log(`Removed all ${item.name} from inventory`);
        }
        return true;
    }
    
    getItem(itemId) {
        return this.inventory.find(i => i.id === itemId);
    }
    
    hasItem(itemId) {
        return this.inventory.some(i => i.id === itemId);
    }
    
    getItemCount(itemId) {
        const item = this.inventory.find(i => i.id === itemId);
        return item ? (item.quantity || 1) : 0;
    }
    
    craft(item1Id, item2Id = null, worldObject = null) {
        console.log(`Attempting to craft: ${item1Id} + ${item2Id || 'nothing'}`);
        
        const item1 = this.getItem(item1Id);
        const item2 = item2Id ? this.getItem(item2Id) : null;
        
        if (!item1) {
            console.log('Item 1 not found');
            return null;
        }
        
        // Check for predefined recipe
        const recipe = this.findRecipe(item1, item2, worldObject);
        
        let result;
        if (recipe) {
            console.log('Found predefined recipe:', recipe.id);
            result = this.executeRecipe(recipe, item1, item2);
            this.discoveredRecipes.add(recipe.id);
        } else {
            console.log('Generating procedural combination');
            result = this.generateCombination(item1, item2, worldObject);
        }
        
        if (result) {
            // Remove consumed items
            this.removeItem(item1Id);
            if (item2Id) {
                this.removeItem(item2Id);
            }
            
            // Add result
            this.addItem(result);
            this.craftCount++;
            
            console.log('Crafted:', result.name);
            return result;
        }
        
        return null;
    }
    
    findRecipe(item1, item2, worldObject) {
        if (!RECIPES) return null;
        
        for (const recipe of RECIPES) {
            const inputs = [item1.id];
            if (item2) inputs.push(item2.id);
            inputs.sort();
            
            const recipeInputs = recipe.inputs.slice().sort();
            
            if (JSON.stringify(inputs) === JSON.stringify(recipeInputs)) {
                if (recipe.requiresWorldObject && worldObject !== recipe.requiresWorldObject) {
                    continue;
                }
                return recipe;
            }
        }
        return null;
    }
    
    executeRecipe(recipe, item1, item2) {
        // Get base item if it exists
        const baseItem = ITEMS ? ITEMS[recipe.output] : null;
        
        // Use custom item if provided, otherwise use base
        const itemData = recipe.customItem || baseItem || {};
        
        return {
            id: recipe.output || `crafted_${Date.now()}`,
            name: recipe.outputName || itemData.name || 'Crafted Item',
            description: recipe.outputDescription || itemData.description || '',
            attributes: itemData.attributes || [],
            damage: itemData.damage,
            healAmount: itemData.healAmount,
            canBePlaced: itemData.canBePlaced,
            trapEffect: itemData.trapEffect,
            stackable: false,
            craftedFrom: [item1.id, item2?.id].filter(Boolean),
            quantity: 1
        };
    }
    
    generateCombination(item1, item2, worldObject) {
        const attrs1 = item1.attributes || [];
        const attrs2 = item2 ? (item2.attributes || []) : [];
        
        // Combine attributes
        const combined = this.combineAttributes(attrs1, attrs2);
        
        return {
            id: `crafted_${Date.now()}`,
            name: this.generateItemName(item1, item2, combined),
            description: this.generateItemDescription(item1, item2, combined),
            attributes: combined,
            stackable: false,
            craftedFrom: [item1.id, item2?.id].filter(Boolean),
            procedural: true,
            quantity: 1
        };
    }
    
    combineAttributes(attrs1, attrs2) {
        const combined = new Set([...attrs1, ...attrs2]);
        
        // Attribute interactions
        if (combined.has('flammable') && combined.has('fire_source')) {
            combined.delete('flammable');
            combined.add('burning');
        }
        
        if (combined.has('container') && combined.has('liquid')) {
            combined.add('filled_container');
        }
        
        if (combined.has('sharp') && combined.has('handle')) {
            combined.add('weapon');
        }
        
        if (combined.has('rope') && combined.has('heavy')) {
            combined.add('trap_component');
        }
        
        if (combined.has('metal') && combined.has('conductive') && combined.has('electronic')) {
            combined.add('trap_component');
        }
        
        return Array.from(combined);
    }
    
    generateItemName(item1, item2, attributes) {
        if (!item2) {
            return `Modified ${item1.name}`;
        }
        
        if (attributes.includes('filled_container')) {
            return `${item1.name} with ${item2.name}`;
        }
        
        if (attributes.includes('weapon')) {
            return `${item1.name}-${item2.name} Weapon`;
        }
        
        if (attributes.includes('burning')) {
            return `Burning ${item1.name}`;
        }
        
        if (attributes.includes('trap_component')) {
            return `${item1.name} Trap Device`;
        }
        
        return `${item1.name} & ${item2.name}`;
    }
    
    generateItemDescription(item1, item2, attributes) {
        const attrList = attributes.join(', ');
        return `A combination of ${item1.name}${item2 ? ` and ${item2.name}` : ''}. [${attrList}]`;
    }
    
    applyTrapToSelf(itemId) {
        const item = this.getItem(itemId);
        if (!item || !item.canBePlaced) {
            console.log('Cannot apply trap - item not found or not placeable');
            return false;
        }
        
        this.appliedTraps.push({ ...item });
        this.removeItem(itemId);
        console.log('Applied trap to self:', item.name);
        return true;
    }
    
    getAppliedTraps() {
        return this.appliedTraps;
    }
    
    clearAppliedTraps() {
        this.appliedTraps = [];
    }
    
    previewCraft(item1Id, item2Id = null, worldObject = null) {
        const item1 = this.getItem(item1Id);
        const item2 = item2Id ? this.getItem(item2Id) : null;
        
        if (!item1) return null;
        
        const recipe = this.findRecipe(item1, item2, worldObject);
        
        if (recipe) {
            const baseItem = ITEMS ? ITEMS[recipe.output] : null;
            const itemData = recipe.customItem || baseItem || {};
            
            return {
                name: recipe.outputName || itemData.name || 'Crafted Item',
                description: recipe.outputDescription || itemData.description || 'A crafted item.',
                isKnownRecipe: this.discoveredRecipes.has(recipe.id),
                predictedAttributes: itemData.attributes
            };
        }
        
        // Preview procedural
        const attrs1 = item1.attributes || [];
        const attrs2 = item2 ? (item2.attributes || []) : [];
        const combinedAttrs = this.combineAttributes(attrs1, attrs2);
        
        return {
            name: this.generateItemName(item1, item2, combinedAttrs),
            description: 'Unknown combination - try it!',
            isKnownRecipe: false,
            predictedAttributes: combinedAttrs
        };
    }
    
    serializeInventory() {
        return {
            inventory: JSON.parse(JSON.stringify(this.inventory)),
            craftCount: this.craftCount,
            discoveredRecipes: Array.from(this.discoveredRecipes),
            appliedTraps: JSON.parse(JSON.stringify(this.appliedTraps))
        };
    }
    
    deserializeInventory(data) {
        this.inventory = data.inventory || [];
        this.craftCount = data.craftCount || 0;
        this.discoveredRecipes = new Set(data.discoveredRecipes || []);
        this.appliedTraps = data.appliedTraps || [];
    }
}