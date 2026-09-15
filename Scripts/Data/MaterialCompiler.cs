using Godot;
using System.Linq;

public class MaterialStats
{
    public string Formula = "?";
    public float Density;      // g/cm³
    public float MeltingK;
    public string Conductivity = "Insulator";
    public string Hardness = "Soft";
    public string Bonding = "metallic";
    public float ForeignFraction;
    public LatticeType LegacyLattice = LatticeType.Amorphous;
}

public static class MaterialCompiler
{
    public static MaterialStats Compile(CrystalStructure s)
    {
        var st = new MaterialStats();
        if (s.Sites.Count == 0) return st;
        st.Formula = s.Formula();
        st.Density = s.Density();

        int total = s.Sites.Count;
        float metalFrac = s.Sites.Count(x => Elements.Get(x.Element).IsMetal) / (float)total;
        float organicFrac = s.Sites.Count(x =>
            x.Element is "C" or "H" or "O" or "N") / (float)total;
        bool hasH = s.Sites.Any(x => x.Element == "H");
        bool ionic = false;
        for (int i = 0; i < total && !ionic; i++)
            for (int j = i + 1; j < total; j++)
            {
                var a = Elements.Get(s.Sites[i].Element);
                var b = Elements.Get(s.Sites[j].Element);
                if (a.IsMetal != b.IsMetal && a.IonicRadius > 0 && b.IonicRadius > 0
                    && Mathf.Abs(a.Chi - b.Chi) >= 1.7f) { ionic = true; break; }
            }

        st.Bonding = ionic ? "ionic"
            : metalFrac >= 0.5f ? "metallic"
            : (organicFrac > 0.7f && hasH) ? "molecular"
            : "covalent";

        st.Conductivity = st.Bonding == "metallic" ? "Conductor"
            : SemiClass(s) ?? "Insulator";
        st.Hardness = st.Bonding switch
        {
            "covalent" => "Very hard",
            "ionic" => "Hard, brittle",
            "metallic" => "Malleable",
            _ => "Soft",
        };

        // melting point: inherit from prototype, weaken with defects (game heuristic, not CALPHAD)
        float baseTm = 1500;
        if (StructureLibrary.MetaFor.TryGetValue(s.PrototypeId, out var meta)) baseTm = meta.MeltingK;
        if (StructureLibrary.Prototypes.TryGetValue(s.PrototypeId, out var proto))
        {
            var baseComp = proto.Composition();
            int foreign = s.Sites.Count(x => !baseComp.ContainsKey(x.Element));
            float sizeDelta = Mathf.Abs(s.Sites.Count - proto.Sites.Count) / (float)proto.Sites.Count;
            st.ForeignFraction = foreign / (float)total;
            baseTm *= 1f - 0.55f * st.ForeignFraction - 0.4f * Mathf.Min(sizeDelta, 0.5f);
        }
        st.MeltingK = Mathf.Max(50, baseTm);

        bool cubic = s.Alpha == 90 && s.Beta == 90 && s.Gamma == 90 && s.A == s.B && s.B == s.C;
        st.LegacyLattice = s.Amorphous ? LatticeType.Amorphous
            : cubic ? (s.Centering == Centering.F ? LatticeType.FCC
                     : s.Centering == Centering.I ? LatticeType.BCC : LatticeType.SimpleCubic)
            : s.Gamma == 120 ? LatticeType.Hexagonal
            : LatticeType.SimpleCubic;
        return st;
    }

    static string SemiClass(CrystalStructure s)
    {
        int si = s.Sites.Count(x => x.Element is "Si" or "Ge");
        if (si < s.Sites.Count * 0.5f) return null;
        if (si >= s.Sites.Count * 0.9f) return "Semiconductor (intrinsic)";
        bool n = s.Sites.Any(x => x.Element is "P" or "As" or "Sb");
        bool p = s.Sites.Any(x => x.Element is "B" or "Al" or "Ga");
        return n ? "Semiconductor (n-type)" : p ? "Semiconductor (p-type)" : "Semiconductor";
    }

    // runtime artifact for your existing systems
    public static MaterialPhysics ToMaterialPhysics(MaterialStats st) =>
        new(st.LegacyLattice, 0.28f * st.MeltingK, 0.028f * st.MeltingK, st.MeltingK);

    public static ItemData CreateItemData(CrystalStructure s, MaterialStats st, int id)
    {
        var comp = s.Composition();
        var dom = comp.OrderByDescending(k => k.Value).First().Key;
        var props = ItemProperty.Blunt;
        if (st.Conductivity.StartsWith("Conductor")) props |= ItemProperty.Conductive;
        if (st.Bonding == "molecular") props |= ItemProperty.Flammable;
        if (s.Amorphous) props |= ItemProperty.Glass;
        if (st.Bonding == "metallic") props |= ItemProperty.Metal;
        if (st.Hardness == "Very hard") props |= ItemProperty.Sharp;

        var name = $"Custom {st.Formula}";
        var item = new ItemData(id, name, st.Formula.Length <= 2 ? st.Formula : st.Formula[..2],
            props, ToMaterialPhysics(st))
        { ThemeColor = Elements.Get(dom).Color };

        if (st.Hardness == "Very hard")
            item.AsWeapon(30, ImpactType.Pipe, 6, 0.06f, 0.12f, 0.9f);
        else
            item.AsWeapon(15, ImpactType.Fist, 3, 0.04f, 0.06f, 0.6f);
        return item;
    }
}