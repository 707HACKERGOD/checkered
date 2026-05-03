using Godot;
using System.Collections.Generic;

// Add this enum near the top
public enum LatticeType
{
    Amorphous,   // Glass, cloth, wood
    SimpleCubic,
    BCC,         // Body-Centered Cubic (iron)
    FCC,         // Face-Centered Cubic (copper, aluminum)
    Hexagonal    // Hexagonal Close-Packed (zinc)
}

// Optional: a container for physics properties
public class MaterialPhysics
{
    public LatticeType Lattice { get; set; }
    public float Enthalpy { get; set; }   // H – stored energy
    public float Entropy { get; set; }    // S – disorder
    public float MeltingPoint { get; set; }

    public MaterialPhysics(LatticeType lattice, float enthalpy, float entropy, float meltingPoint)
    {
        Lattice = lattice;
        Enthalpy = enthalpy;
        Entropy = entropy;
        MeltingPoint = meltingPoint;
    }
}

public class ItemData
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Abbreviation { get; set; }
    public ItemProperty Properties { get; set; }
    public Color ThemeColor { get; set; }
    public bool IsSolid { get; set; }
    public int Quantity { get; set; } = 1;
    public Dictionary<string, object> Modifiers { get; set; }

    // --- NEW: Material physics ---
    public MaterialPhysics Physics { get; set; }

    // Constructor updated to accept physics data (optional, defaults to null)
    public ItemData(int id, string name, string abbr, ItemProperty properties,
                    MaterialPhysics physics = null, bool isSolid = true)
    {
        Id = id;
        Name = name;
        Abbreviation = abbr;
        Properties = properties;
        IsSolid = isSolid;
        ThemeColor = new Color(GD.Randf(), GD.Randf(), GD.Randf(), 1.0f);
        Modifiers = new Dictionary<string, object>();
        Physics = physics;
    }
}

public static class ItemRegistry
{
    public static readonly Dictionary<int, ItemData> Items = new();
    public static readonly List<int> ItemKeys = new();

    static ItemRegistry()
    {
        // Now include physics data when registering items
        RegisterItem(new ItemData(0, "Cloth Rag", "CL",
            ItemProperty.Cloth | ItemProperty.Flammable,
            new MaterialPhysics(LatticeType.Amorphous, 200f, 100f, 300f)));

        RegisterItem(new ItemData(1, "Matches", "MA",
            ItemProperty.FireSource | ItemProperty.Flammable,
            new MaterialPhysics(LatticeType.Amorphous, 400f, 150f, 200f)));

        RegisterItem(new ItemData(2, "Bandage", "BA",
            ItemProperty.Cloth | ItemProperty.Healing,
            new MaterialPhysics(LatticeType.Amorphous, 100f, 80f, 250f)));

        RegisterItem(new ItemData(3, "Newspaper", "NW",
            ItemProperty.Flammable,
            new MaterialPhysics(LatticeType.Amorphous, 150f, 90f, 220f)));

        RegisterItem(new ItemData(4, "Kitchen Knife", "KN",
            ItemProperty.Sharp | ItemProperty.Handle | ItemProperty.Metal,
            new MaterialPhysics(LatticeType.FCC, 500f, 50f, 1500f)));

        RegisterItem(new ItemData(5, "Glass Bottle", "GB",
            ItemProperty.Container | ItemProperty.Glass,
            new MaterialPhysics(LatticeType.Amorphous, 300f, 30f, 1000f)));

        RegisterItem(new ItemData(6, "Wooden Stick", "WS",
            ItemProperty.Wood | ItemProperty.Blunt | ItemProperty.Handle,
            new MaterialPhysics(LatticeType.Amorphous, 100f, 70f, 400f)));

        RegisterItem(new ItemData(7, "Rope", "RO",
            ItemProperty.Rope | ItemProperty.Cloth,
            new MaterialPhysics(LatticeType.Amorphous, 80f, 110f, 250f)));

        RegisterItem(new ItemData(8, "Metal Can", "MC",
            ItemProperty.Container | ItemProperty.Metal,
            new MaterialPhysics(LatticeType.FCC, 450f, 40f, 1200f)));

        RegisterItem(new ItemData(9, "Copper Wire", "CW",
            ItemProperty.Rope | ItemProperty.Metal | ItemProperty.Conductive,
            new MaterialPhysics(LatticeType.FCC, 420f, 35f, 1350f)));
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