using System;

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