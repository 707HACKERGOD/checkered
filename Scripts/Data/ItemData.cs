using Godot;
using System;
using System.Collections.Generic;

public enum LatticeType
{
    Amorphous,
    SimpleCubic,
    BCC,
    FCC,
    Hexagonal
}

public class MaterialPhysics
{
    public LatticeType Lattice { get; set; }
    public float Enthalpy { get; set; }
    public float Entropy { get; set; }
    public float MeltingPoint { get; set; }

    public MaterialPhysics(LatticeType lattice, float enthalpy, float entropy, float meltingPoint)
    {
        Lattice = lattice;
        Enthalpy = enthalpy;
        Entropy = entropy;
        MeltingPoint = meltingPoint;
    }
}

public enum ImpactType { Fist, Pipe, Chair }

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
    public MaterialPhysics Physics { get; set; }

    // Weapon stats (null if not a weapon)
    public float? Damage { get; set; }
    public float? KnockbackForce { get; set; }
    public float? HitstopDuration { get; set; }
    public float? CameraShake { get; set; }
    public float? StumbleDuration { get; set; }
    public float? AttackCooldown { get; set; }
    public float? AttackRange { get; set; }
    public ImpactType? ImpactType { get; set; }
    public float? SwingRadius { get; set; }

    public bool IsWeapon => Damage.HasValue;

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

    public ItemData AsWeapon(float damage, ImpactType impact, float knockback,
        float hitstop, float shake, float radius,
        float stumbleDuration = 0.3f, float attackCooldown = 0.4f, float attackRange = 1.5f)
    {
        Damage = damage;
        KnockbackForce = knockback;
        HitstopDuration = hitstop;
        CameraShake = shake;
        ImpactType = impact;
        SwingRadius = radius;
        StumbleDuration = stumbleDuration;
        AttackCooldown = attackCooldown;
        AttackRange = attackRange;
        return this;
    }
}

[Flags]
public enum ItemProperty : uint
{
    None = 0,
    Cloth = 1 << 0,
    Flammable = 1 << 1,
    FireSource = 1 << 2,
    Healing = 1 << 3,
    Sharp = 1 << 4,
    Handle = 1 << 5,
    Metal = 1 << 6,
    Container = 1 << 7,
    Glass = 1 << 8,
    Wood = 1 << 9,
    Blunt = 1 << 10,
    Rope = 1 << 11,
    Conductive = 1 << 12,
}

public static class ItemRegistry
{
    public static readonly Dictionary<int, ItemData> Items = new();
    public static readonly List<int> ItemKeys = new();

    static ItemRegistry()
    {
        RegisterItem(new ItemData(0, "Fist", "FI",
            ItemProperty.Blunt,
            new MaterialPhysics(LatticeType.Amorphous, 0f, 0f, 0f))
            .AsWeapon(
                damage: 10f,
                impact: ImpactType.Fist,
                knockback: 2f,
                hitstop: 0.04f,
                shake: 0.05f,
                radius: 0.6f,
                stumbleDuration: 0.2f,
                attackCooldown: 0.3f,
                attackRange: 0.8f
            ));

        RegisterItem(new ItemData(1, "Pipe", "PI",
            ItemProperty.Metal | ItemProperty.Blunt | ItemProperty.Handle,
            new MaterialPhysics(LatticeType.FCC, 500f, 50f, 1500f))
            .AsWeapon(
                damage: 20f,
                impact: ImpactType.Pipe,
                knockback: 5f,
                hitstop: 0.06f,
                shake: 0.12f,
                radius: 0.9f,
                stumbleDuration: 0.3f,
                attackCooldown: 0.4f,
                attackRange: 1.2f
            ));

        RegisterItem(new ItemData(2, "Chair", "CH",
            ItemProperty.Wood | ItemProperty.Blunt,
            new MaterialPhysics(LatticeType.Amorphous, 100f, 70f, 400f))
            .AsWeapon(
                damage: 35f,
                impact: ImpactType.Chair,
                knockback: 8f,
                hitstop: 0.1f,
                shake: 0.2f,
                radius: 1.2f,
                stumbleDuration: 0.5f,
                attackCooldown: 0.6f,
                attackRange: 1.5f
            ));

        RegisterItem(new ItemData(100, "Cloth Rag", "CL",
            ItemProperty.Cloth | ItemProperty.Flammable,
            new MaterialPhysics(LatticeType.Amorphous, 200f, 100f, 300f)));

        RegisterItem(new ItemData(101, "Matches", "MA",
            ItemProperty.FireSource | ItemProperty.Flammable,
            new MaterialPhysics(LatticeType.Amorphous, 400f, 150f, 200f)));

        RegisterItem(new ItemData(102, "Bandage", "BA",
            ItemProperty.Cloth | ItemProperty.Healing,
            new MaterialPhysics(LatticeType.Amorphous, 100f, 80f, 250f)));

        RegisterItem(new ItemData(103, "Newspaper", "NW",
            ItemProperty.Flammable,
            new MaterialPhysics(LatticeType.Amorphous, 150f, 90f, 220f)));

        RegisterItem(new ItemData(104, "Kitchen Knife", "KN",
            ItemProperty.Sharp | ItemProperty.Handle | ItemProperty.Metal,
            new MaterialPhysics(LatticeType.FCC, 500f, 50f, 1500f)));

        RegisterItem(new ItemData(105, "Glass Bottle", "GB",
            ItemProperty.Container | ItemProperty.Glass,
            new MaterialPhysics(LatticeType.Amorphous, 300f, 30f, 1000f)));

        RegisterItem(new ItemData(106, "Wooden Stick", "WS",
            ItemProperty.Wood | ItemProperty.Blunt | ItemProperty.Handle,
            new MaterialPhysics(LatticeType.Amorphous, 100f, 70f, 400f)));

        RegisterItem(new ItemData(107, "Rope", "RO",
            ItemProperty.Rope | ItemProperty.Cloth,
            new MaterialPhysics(LatticeType.Amorphous, 80f, 110f, 250f)));

        RegisterItem(new ItemData(108, "Metal Can", "MC",
            ItemProperty.Container | ItemProperty.Metal,
            new MaterialPhysics(LatticeType.FCC, 450f, 40f, 1200f)));

        RegisterItem(new ItemData(109, "Copper Wire", "CW",
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

    public static ItemData GetWeapon(ImpactType type)
    {
        foreach (var item in Items.Values)
        {
            if (item.ImpactType == type)
                return item;
        }
        return null;
    }
}