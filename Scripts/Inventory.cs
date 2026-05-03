using Godot;
using System.Collections.Generic;

public partial class Inventory : Node
{
    [Signal] public delegate void SlotUpdatedEventHandler(int slotIndex);
    [Signal] public delegate void InventoryChangedEventHandler();

    public const int SLOT_COUNT = 20;
    private Dictionary<int, ItemData> _slots = new Dictionary<int, ItemData>();

    public ItemData GetSlot(int index) => _slots.TryGetValue(index, out var item) ? item : null;

    public bool AddItem(ItemData item)
    {
        // Try stacking with existing item of same type and identical modifiers
        foreach (var (idx, existing) in _slots)
        {
            if (existing.Id == item.Id && ModifiersEqual(existing, item) && existing.Quantity < 99)
            {
                existing.Quantity += item.Quantity;
                EmitSignal(SignalName.SlotUpdated, idx);
                return true;
            }
        }

        // Find first empty slot
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (!_slots.ContainsKey(i))
            {
                _slots[i] = item;
                EmitSignal(SignalName.SlotUpdated, i);
                GD.Print($"Added item {item.Name} (Qty {item.Quantity}) to slot {i}");
                return true;
            }
        }

        return false; // inventory full
    }

    private bool ModifiersEqual(ItemData a, ItemData b)
    {
        if (a.Modifiers.Count != b.Modifiers.Count) return false;
        foreach (var kvp in a.Modifiers)
        {
            if (!b.Modifiers.TryGetValue(kvp.Key, out var value) || !value.Equals(kvp.Value))
                return false;
        }
        return true;
    }

    public bool RemoveItem(int slot, int quantity = 1)
    {
        if (_slots.TryGetValue(slot, out var item))
        {
            item.Quantity -= quantity;
            if (item.Quantity <= 0)
                _slots.Remove(slot);
            EmitSignal(SignalName.SlotUpdated, slot);
            return true;
        }
        return false;
    }
}