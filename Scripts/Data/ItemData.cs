using Godot;
using System.Collections.Generic;

public class ItemData
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Abbreviation { get; set; }
    public string[] Properties { get; set; }
    public Color ThemeColor { get; set; }
    public bool IsSolid { get; set; } // true for crystallography, false for thermodynamics

    public ItemData(int id, string name, string abbr, string[] properties, bool isSolid = true)
    {
        Id = id;
        Name = name;
        Abbreviation = abbr;
        Properties = properties;
        IsSolid = isSolid;
        ThemeColor = new Color(GD.Randf(), GD.Randf(), GD.Randf(), 1.0f);
    }
}

public static class ItemRegistry
{
    public static readonly Dictionary<int, ItemData> Items = new Dictionary<int, ItemData>();
    public static readonly List<int> ItemKeys = new List<int>();

    static ItemRegistry()
    {
        RegisterItem(new ItemData(0, "Cloth Rag", "CL", new[] { "Cloth", "Flammable" }));
        RegisterItem(new ItemData(1, "Matches", "MA", new[] { "Fire source", "Flammable" }));
        RegisterItem(new ItemData(2, "Bandage", "BA", new[] { "Cloth", "Healing" }));
        RegisterItem(new ItemData(3, "Newspaper", "NW", new[] { "Flammable" }));
        RegisterItem(new ItemData(4, "Kitchen Knife", "KN", new[] { "Sharp", "Handle", "Metal" }));
        RegisterItem(new ItemData(5, "Glass Bottle", "GB", new[] { "Container", "Glass" }));
        RegisterItem(new ItemData(6, "Wooden Stick", "WS", new[] { "Wood", "Blunt", "Handle" }));
        RegisterItem(new ItemData(7, "Rope", "RO", new[] { "Rope", "Cloth" }));
        RegisterItem(new ItemData(8, "Metal Can", "MC", new[] { "Container", "Metal" }));
        RegisterItem(new ItemData(9, "Copper Wire", "CW", new[] { "Rope", "Metal", "Conductive" }));
    }

    private static void RegisterItem(ItemData item)
    {
        Items[item.Id] = item;
        ItemKeys.Add(item.Id);
    }

    public static ItemData GetRandomItem()
    {
        int randomIndex = GD.RandRange(0, ItemKeys.Count - 1);
        return Items[ItemKeys[randomIndex]];
    }
}