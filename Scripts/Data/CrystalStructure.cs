using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum Centering { P, C, I, F }

public struct AtomSite
{
    public string Element;
    public Vector3 Frac;
    public AtomSite(string el, float x, float y, float z) { Element = el; Frac = new Vector3(x, y, z); }
}

public struct Bond
{
    public int I, J;
    public Vector3 Offset; // integer cell offset from I to J
}

public class CrystalStructure
{
    public string Id = "", Name = "", SpaceGroup = "P1", PrototypeId = "";
    public float A = 1, B = 1, C = 1;
    public float Alpha = 90, Beta = 90, Gamma = 90;   // degrees
    public Centering Centering;
    public bool Amorphous;
    public bool Edited;                               // set by the lab; symmetry claim downgrades to P1

    public List<AtomSite> Sites = new();

    private Basis _cell, _cellInv;
    public Basis CellBasis => _cell;

    public void RebuildLattice()
    {
        var ga = Mathf.DegToRad(Gamma);
        var al = Mathf.DegToRad(Alpha);
        var be = Mathf.DegToRad(Beta);
        var av = new Vector3(A, 0, 0);
        var bv = new Vector3(B * Mathf.Cos(ga), B * Mathf.Sin(ga), 0);
        float cx = C * Mathf.Cos(be);
        float cy = Mathf.Abs(Mathf.Sin(ga)) < 1e-5f ? 0
            : C * (Mathf.Cos(al) - Mathf.Cos(be) * Mathf.Cos(ga)) / Mathf.Sin(ga);
        float cz2 = C * C - cx * cx - cy * cy;
        var cv = new Vector3(cx, cy, cz2 > 0 ? Mathf.Sqrt(cz2) : 0);
        _cell = new Basis(av, bv, cv);
        _cellInv = _cell.Inverse();
    }

    public Vector3 FracToCart(Vector3 f) => _cell * f;
    public Vector3 CartToFrac(Vector3 p) => _cellInv * p;
    public float CellVolume() => Mathf.Abs(_cell.X.Cross(_cell.Y).Dot(_cell.Z));

    public CrystalStructure Clone()
    {
        var s = (CrystalStructure)MemberwiseClone();
        s.Sites = Sites.ToList();
        return s;
    }

    // ---- construction -------------------------------------------------

    static Vector3[] CenteringOffsets(Centering c) => c switch
    {
        Centering.I => new[] { Vector3.Zero, new Vector3(.5f, .5f, .5f) },
        Centering.F => new[] { Vector3.Zero, new Vector3(.5f, .5f, 0), new Vector3(.5f, 0, .5f), new Vector3(0, .5f, .5f) },
        Centering.C => new[] { Vector3.Zero, new Vector3(.5f, .5f, 0) },
        _ => new[] { Vector3.Zero },
    };

    static float Wrap(float v) => v - Mathf.Floor(v);

    public static CrystalStructure Prototype(string id, string name, string sg,
        float a, float b, float c, float al, float be, float ga, Centering cent, bool amorphous,
        params (string el, float x, float y, float z)[] motif)
    {
        var s = new CrystalStructure
        {
            Id = id, Name = name, SpaceGroup = sg, PrototypeId = id,
            A = a, B = b, C = c, Alpha = al, Beta = be, Gamma = ga,
            Centering = cent, Amorphous = amorphous,
        };
        foreach (var m in motif)
            foreach (var o in CenteringOffsets(cent))
                s.Sites.Add(new AtomSite(m.el, Wrap(m.x + o.X), Wrap(m.y + o.Y), Wrap(m.z + o.Z)));
        s.RebuildLattice();
        return s;
    }

    // amorphous: rejection-sampled random network in a box (deterministic seed)
    public static CrystalStructure MakeAmorphous(string id, string name, float box, int seed,
        params (string el, int n)[] comp)
    {
        var s = new CrystalStructure
        {
            Id = id, Name = name, SpaceGroup = "amorphous", PrototypeId = id,
            A = box, B = box, C = box, Amorphous = true,
        };
        s.RebuildLattice();
        var rng = new System.Random(seed);
        foreach (var (el, n) in comp)
            for (int placed = 0, tries = 0; placed < n && tries < 8000; tries++)
            {
                var f = new Vector3(0.08f + 0.84f * rng.NextSingle(),
                                    0.08f + 0.84f * rng.NextSingle(),
                                    0.08f + 0.84f * rng.NextSingle());
                var p = f * box;
                float r = Elements.Get(el).Radius + 0.55f;
                bool ok = true;
                foreach (var o in s.Sites)
                {
                    var d = p - o.Frac * box;
                    d = new Vector3(MinAx(d.X, box), MinAx(d.Y, box), MinAx(d.Z, box));
                    if (d.Length() < r) { ok = false; break; }
                }
                if (ok) { s.Sites.Add(new AtomSite(el, f.X, f.Y, f.Z)); placed++; }
            }
        return s;
    }
    static float MinAx(float d, float box) { d = Mathf.Abs(d); return Mathf.Min(d, box - d); }

    // ---- bonding -------------------------------------------------------

    // covalent radii for covalent/metallic pairs, ionic radii when Δχ is large & one partner is a nonmetal
    public static float PairCutoff(ElementInfo a, ElementInfo b)
    {
        bool ionic = (a.IsMetal != b.IsMetal) && Mathf.Abs(a.Chi - b.Chi) >= 1.7f
                     && a.IonicRadius > 0 && b.IonicRadius > 0;
        float ra = ionic ? a.IonicRadius : a.Radius;
        float rb = ionic ? b.IonicRadius : b.Radius;
        return ra + rb + 0.30f;
    }

    public List<Bond> ComputeBonds()
    {
        var res = new List<Bond>();
        var cart = new Vector3[Sites.Count];
        for (int i = 0; i < Sites.Count; i++) cart[i] = FracToCart(Sites[i].Frac);
        var cache = new Dictionary<(string, string), float>();

        for (int i = 0; i < Sites.Count; i++)
        for (int j = i; j < Sites.Count; j++)
        for (int ox = -1; ox <= 1; ox++)
        for (int oy = -1; oy <= 1; oy++)
        for (int oz = -1; oz <= 1; oz++)
        {
            if (i == j && (ox == 0 && oy == 0 && oz == 0)) continue;
            if (i == j && LexNeg(ox, oy, oz)) continue; // dedupe self-image bonds
            var off = new Vector3(ox, oy, oz);
            var d = cart[j] + _cell * off - cart[i];
            if (d.LengthSquared() > 25f) continue; // 5 Å early-out
            var key = (Sites[i].Element, Sites[j].Element);
            if (!cache.TryGetValue(key, out var cut))
            {
                cut = PairCutoff(Elements.Get(key.Item1), Elements.Get(key.Item2));
                cache[key] = cut;
            }
            if (d.LengthSquared() < cut * cut)
                res.Add(new Bond { I = i, J = j, Offset = off });
        }
        return res;
    }
    static bool LexNeg(int x, int y, int z) => x < 0 || (x == 0 && y < 0) || (x == 0 && y == 0 && z < 0);

    // ---- stats ---------------------------------------------------------

    public Dictionary<string, int> Composition()
    {
        var d = new Dictionary<string, int>();
        foreach (var s in Sites) d[s.Element] = d.TryGetValue(s.Element, out var v) ? v + 1 : 1;
        return d;
    }

    public float MolarMass() => Sites.Sum(s => Elements.Get(s.Element).Mass);

    public float Density() // g/cm³ — exact: ΣM / (N_A · V)
    {
        var v = CellVolume();
        return Sites.Count > 0 && v > 1e-6f ? MolarMass() / (0.602214f * v) : 0f;
    }

    public string Formula()
    {
        if (Sites.Count == 0) return "?";
        var d = Composition();
        int g = 0;
        foreach (var v in d.Values) g = Gcd(g, v);
        if (g == 0) g = 1;
        var keys = d.Keys.ToList();
        if (d.ContainsKey("C")) // Hill order: C, H, then alphabetical
            keys = keys.OrderBy(k => k == "C" ? 0 : (k == "H" ? 1 : 2)).ThenBy(k => k).ToList();
        else
            keys.Sort();
        var sb = new StringBuilder();
        foreach (var k in keys)
        {
            sb.Append(k);
            int n = d[k] / g;
            if (n > 1) sb.Append(n);
        }
        return sb.ToString();
    }
    static int Gcd(int a, int b) { while (b != 0) (a, b) = (b, a % b); return a; }
}