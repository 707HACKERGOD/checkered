using Godot;
using System.Collections.Generic;

public enum Trig { Heat, Cool }

public static class StructureLibrary
{
    public class Meta
    {
        public float MeltingK;
        public float ThermalAlpha = 1.5e-5f; // 1/K, linear
        public (float tK, string id, Trig dir)[] Transitions = new (float, string, Trig)[0];
    }

    public static readonly Dictionary<string, CrystalStructure> Prototypes = new();
    public static readonly Dictionary<string, Meta> MetaFor = new();

    static StructureLibrary()
    {
        void Reg(CrystalStructure s, float tmK, float alpha, params (float, string, Trig)[] tr)
        {
            Prototypes[s.Id] = s;
            MetaFor[s.Id] = new Meta { MeltingK = tmK, ThermalAlpha = alpha, Transitions = tr };
        }

        // metals
        Reg(CrystalStructure.Prototype("fe_bcc", "Iron (α)", "Im-3m (229)", 2.866f, 2.866f, 2.866f, 90, 90, 90, Centering.I, false, ("Fe", 0, 0, 0)),
            1811, 1.2e-5f, (1185, "fe_fcc", Trig.Heat), (1667, "fe_fcc", Trig.Cool));
        Reg(CrystalStructure.Prototype("fe_fcc", "Iron (γ)", "Fm-3m (225)", 3.59f, 3.59f, 3.59f, 90, 90, 90, Centering.F, false, ("Fe", 0, 0, 0)),
            1811, 1.2e-5f, (1185, "fe_bcc", Trig.Cool), (1667, "fe_bcc", Trig.Heat));
        Reg(CrystalStructure.Prototype("cu_fcc", "Copper", "Fm-3m (225)", 3.615f, 3.615f, 3.615f, 90, 90, 90, Centering.F, false, ("Cu", 0, 0, 0)), 1358, 1.7e-5f);
        Reg(CrystalStructure.Prototype("al_fcc", "Aluminum", "Fm-3m (225)", 4.049f, 4.049f, 4.049f, 90, 90, 90, Centering.F, false, ("Al", 0, 0, 0)), 933, 2.3e-5f);
        Reg(CrystalStructure.Prototype("au_fcc", "Gold", "Fm-3m (225)", 4.078f, 4.078f, 4.078f, 90, 90, 90, Centering.F, false, ("Au", 0, 0, 0)), 1337, 1.4e-5f);
        Reg(CrystalStructure.Prototype("pb_fcc", "Lead", "Fm-3m (225)", 4.95f, 4.95f, 4.95f, 90, 90, 90, Centering.F, false, ("Pb", 0, 0, 0)), 601, 2.9e-5f);
        Reg(CrystalStructure.Prototype("w_bcc", "Tungsten", "Im-3m (229)", 3.165f, 3.165f, 3.165f, 90, 90, 90, Centering.I, false, ("W", 0, 0, 0)), 3695, 4.5e-6f);
        Reg(CrystalStructure.Prototype("mg_hcp", "Magnesium", "P6₃/mmc (194)", 3.21f, 3.21f, 5.21f, 90, 90, 120, Centering.P, false,
            ("Mg", 1f / 3f, 2f / 3f, 0.25f), ("Mg", 2f / 3f, 1f / 3f, 0.75f)), 923, 2.6e-5f);
        Reg(CrystalStructure.Prototype("ti_hcp", "Titanium", "P6₃/mmc (194)", 2.95f, 2.95f, 4.68f, 90, 90, 120, Centering.P, false,
            ("Ti", 1f / 3f, 2f / 3f, 0.25f), ("Ti", 2f / 3f, 1f / 3f, 0.75f)), 1941, 8.6e-6f);

        // steel = bcc Fe + C interstitial (carbon content exaggerated for visibility — real max is ~2 wt%)
        Reg(CrystalStructure.Prototype("steel", "Steel (Fe + C)", "P1 (interstitial)", 2.866f, 2.866f, 2.866f, 90, 90, 90, Centering.P, false,
            ("Fe", 0, 0, 0), ("Fe", 0.5f, 0.5f, 0.5f), ("C", 0.5f, 0.5f, 0)), 1640, 1.3e-5f);

        // covalent
        Reg(CrystalStructure.Prototype("c_diamond", "Diamond", "Fd-3m (227)", 3.567f, 3.567f, 3.567f, 90, 90, 90, Centering.F, false,
            ("C", 0, 0, 0), ("C", 0.25f, 0.25f, 0.25f)), 3800, 1.0e-6f);
        Reg(CrystalStructure.Prototype("si_diamond", "Silicon", "Fd-3m (227)", 5.431f, 5.431f, 5.431f, 90, 90, 90, Centering.F, false,
            ("Si", 0, 0, 0), ("Si", 0.25f, 0.25f, 0.25f)), 1687, 2.6e-6f);
        Reg(CrystalStructure.Prototype("c_graphite", "Graphite", "P6₃/mmc (194)", 2.461f, 2.461f, 6.708f, 90, 90, 120, Centering.P, false,
            ("C", 0, 0, 0.25f), ("C", 1f / 3f, 2f / 3f, 0.25f),
            ("C", 2f / 3f, 1f / 3f, 0.75f), ("C", 0, 0, 0.75f)), 3900, 8e-6f);

        // ionic
        Reg(CrystalStructure.Prototype("nacl", "Halite (NaCl)", "Fm-3m (225)", 5.640f, 5.640f, 5.640f, 90, 90, 90, Centering.F, false,
            ("Na", 0, 0, 0), ("Cl", 0.5f, 0.5f, 0.5f)), 1074, 4.0e-5f);
        Reg(CrystalStructure.Prototype("cscl", "CsCl", "Pm-3m (221)", 4.114f, 4.114f, 4.114f, 90, 90, 90, Centering.P, false,
            ("Cs", 0, 0, 0), ("Cl", 0.5f, 0.5f, 0.5f)), 918, 5e-5f);
        Reg(CrystalStructure.Prototype("caf2", "Fluorite (CaF₂)", "Fm-3m (225)", 5.464f, 5.464f, 5.464f, 90, 90, 90, Centering.F, false,
            ("Ca", 0, 0, 0), ("F", 0.25f, 0.25f, 0.25f), ("F", 0.75f, 0.75f, 0.75f)), 1691, 1.9e-5f);
        Reg(CrystalStructure.Prototype("srtio3", "Perovskite (SrTiO₃)", "Pm-3m (221)", 3.905f, 3.905f, 3.905f, 90, 90, 90, Centering.P, false,
            ("Sr", 0, 0, 0), ("Ti", 0.5f, 0.5f, 0.5f),
            ("O", 0.5f, 0.5f, 0), ("O", 0.5f, 0, 0.5f), ("O", 0, 0.5f, 0.5f)), 2353, 1.1e-5f);

        // semiconductors
        Reg(CrystalStructure.Prototype("zns_blende", "Sphalerite (ZnS)", "F-43m (216)", 5.409f, 5.409f, 5.409f, 90, 90, 90, Centering.F, false,
            ("Zn", 0, 0, 0), ("S", 0.25f, 0.25f, 0.25f)), 2125, 3e-5f);
        Reg(CrystalStructure.Prototype("zns_wurtzite", "Wurtzite (ZnS)", "P6₃mc (186)", 3.82f, 3.82f, 6.26f, 90, 90, 120, Centering.P, false,
            ("Zn", 1f / 3f, 2f / 3f, 0), ("S", 1f / 3f, 2f / 3f, 0.375f),
            ("Zn", 2f / 3f, 1f / 3f, 0.5f), ("S", 2f / 3f, 1f / 3f, 0.875f)), 2125, 3e-5f);

        // quartz — O positions approximate for visuals; swap exact values from your tables
        Reg(CrystalStructure.Prototype("quartz_alpha", "Quartz (α)", "P3₁21 (152)", 4.914f, 4.914f, 5.405f, 90, 90, 120, Centering.P, false,
            ("Si", 0.4697f, 0, 0), ("Si", 0, 0.4697f, 1f / 3f), ("Si", 0.5303f, 0.5303f, 2f / 3f),
            ("O", 0.4135f, 0.2669f, 0.2145f), ("O", 0.7331f, 0.1466f, 0.5478f), ("O", 0.8534f, 0.5865f, 0.8812f),
            ("O", 0.2669f, 0.4135f, 0.7855f), ("O", 0.1466f, 0.7331f, 0.4522f), ("O", 0.5865f, 0.8534f, 0.1188f)),
            1986, 1.0e-5f);

        // amorphous (deterministic seeds)
        Reg(CrystalStructure.MakeAmorphous("amorph_sio2", "Glass (SiO₂)", 5.6f, 1, ("Si", 4), ("O", 8)), 1500, 8e-6f);
        Reg(CrystalStructure.MakeAmorphous("amorph_organic", "Organic (wood/cloth)", 8.0f, 2, ("C", 6), ("H", 10), ("O", 5)), 573, 1e-5f);
    }

    public static string InferStructureId(ItemData d)
    {
        if (d?.Physics != null)
            switch (d.Physics.Lattice)
            {
                case LatticeType.BCC: return "fe_bcc";
                case LatticeType.FCC: return "cu_fcc";
                case LatticeType.Hexagonal: return "mg_hcp";
                case LatticeType.SimpleCubic: return "nacl";
            }
        var p = d?.Properties ?? ItemProperty.None;
        if (p.HasFlag(ItemProperty.Glass)) return "amorph_sio2";
        if (p.HasFlag(ItemProperty.Metal)) return "steel";
        if (p.HasFlag(ItemProperty.Wood) || p.HasFlag(ItemProperty.Cloth)) return "amorph_organic";
        return "amorph_sio2";
    }

    // ---- user materials -------------------------------------------------

    public static void LoadUserStructures()
    {
        DirAccess.MakeDirRecursiveAbsolute("user://crystals");
        using var dir = DirAccess.Open("user://crystals");
        if (dir == null) return;
        dir.ListDirBegin();
        string f = dir.GetNext();
        while (!string.IsNullOrEmpty(f))
        {
            if (f.EndsWith(".json"))
            {
                using var fa = FileAccess.Open($"user://crystals/{f}", FileAccess.ModeFlags.Read);
                var d = fa != null ? Json.ParseString(fa.GetAsText()).As<Godot.Collections.Dictionary>() : null;
                var s = FromJson(d);
                if (s != null)
                {
                    Prototypes[s.Id] = s;
                    MetaFor[s.Id] = new Meta { MeltingK = 1500, ThermalAlpha = 1.5e-5f };
                }
            }
            f = dir.GetNext();
        }
        dir.ListDirEnd();
    }

    static float F(Godot.Collections.Dictionary d, string k, float def) => d.ContainsKey(k) ? d[k].AsSingle() : def;
    static string S(Godot.Collections.Dictionary d, string k, string def) => d.ContainsKey(k) ? d[k].AsString() : def;

    public static CrystalStructure FromJson(Godot.Collections.Dictionary d)
    {
        if (d == null) return null;
        var s = new CrystalStructure
        {
            Id = S(d, "id", ""), Name = S(d, "name", "Custom"), SpaceGroup = S(d, "sg", "P1"),
            PrototypeId = S(d, "proto", ""), Edited = d.ContainsKey("edited") && d["edited"].AsBool(),
            A = F(d, "a", 1), B = F(d, "b", 1), C = F(d, "c", 1),
            Alpha = F(d, "alpha", 90), Beta = F(d, "beta", 90), Gamma = F(d, "gamma", 90),
        };
        if (d.ContainsKey("sites") && d["sites"].VariantType == Variant.Type.Array)
            foreach (var sv in d["sites"].As<Godot.Collections.Array>())
            {
                var sd = sv.As<Godot.Collections.Dictionary>();
                s.Sites.Add(new AtomSite(S(sd, "el", "C"), F(sd, "x", 0), F(sd, "y", 0), F(sd, "z", 0)));
            }
        s.RebuildLattice();
        return s.Id.Length > 0 && s.Sites.Count > 0 ? s : null;
    }

    public static Godot.Collections.Dictionary ToJson(CrystalStructure s, string id)
    {
        var sites = new Godot.Collections.Array();
        foreach (var a in s.Sites)
            sites.Add(new Godot.Collections.Dictionary { ["el"] = a.Element, ["x"] = a.Frac.X, ["y"] = a.Frac.Y, ["z"] = a.Frac.Z });
        return new Godot.Collections.Dictionary
        {
            ["id"] = id, ["name"] = s.Name, ["sg"] = s.SpaceGroup, ["proto"] = s.PrototypeId,
            ["edited"] = s.Edited, ["a"] = s.A, ["b"] = s.B, ["c"] = s.C,
            ["alpha"] = s.Alpha, ["beta"] = s.Beta, ["gamma"] = s.Gamma, ["sites"] = sites,
        };
    }
}