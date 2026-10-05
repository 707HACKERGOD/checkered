using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Crafting
{
    public enum Phase { Solid, Liquid, Gas }

    public static class Settings
    {
        public const float Lattice = 0.25f;   // Mind's Eye grid step (m)
        public static float AmbientC = 12f;   // world temperature
    }
    // Anything that can carry items and be struck by lightning. LabPlayer and
    // PlayerCraftingBridge both implement it; satisfied members (GlobalPosition,
    // GlobalTransform, GetRid) come from their Node3D/CollisionObject3D bases.
    public interface ICraftingPlayer
    {
        Camera3D Cam { get; }
        LimbRig Limbs { get; }
        Vector3 GlobalPosition { get; }
        Transform3D GlobalTransform { get; }
        Rid GetRid();
        void ZapShake();
    }
    public static class CraftingPlayerExt
    {
        // Both ICraftingPlayer implementations are GodotObjects, but the
        // interface hides that from the compiler — clean validity check for
        // every call site (also returns false for null).
        public static bool IsAlive(this ICraftingPlayer p) =>
            p is GodotObject go && GodotObject.IsInstanceValid(go);
    }

    [GlobalClass]
    public partial class MaterialDef : Resource
    {
        [Export] public string Id = "wood";
        [Export] public string DisplayName = "Wood";
        [Export] public Color Color = new(0.47f, 0.32f, 0.16f);
        [Export] public float Density = 600f;        // kg/m^3
        [Export] public float MeltC = 300f;
        [Export] public float BoilC = 2500f;
        [Export] public float Conductivity = 0.08f;  // thermal 0..1
        [Export] public float Electrical = 0.02f;    // 0..1
        [Export] public float Hardness = 0.3f;       // 0..1
        [Export] public float Metallic = 0f;
        [Export] public float Roughness = 0.9f;

        public Phase PhaseAt(float c) =>
            c >= BoilC ? Phase.Gas : c >= MeltC ? Phase.Liquid : Phase.Solid;
    }

    public static class MaterialLibrary
    {
        static readonly Dictionary<string, MaterialDef> _all = new();
        public static IEnumerable<MaterialDef> All => _all.Values;

        static MaterialLibrary()
        {
            Register(new MaterialDef { Id = "wood",   DisplayName = "Wood",   Color = new Color(0.47f, 0.32f, 0.16f), Density = 600,  MeltC = 300,  BoilC = 600,  Conductivity = 0.06f, Electrical = 0.01f, Hardness = 0.3f });
            Register(new MaterialDef { Id = "stone",  DisplayName = "Stone",  Color = new Color(0.5f, 0.5f, 0.53f),  Density = 2600, MeltC = 1500, BoilC = 2800, Conductivity = 0.10f, Electrical = 0.01f, Hardness = 0.8f });
            Register(new MaterialDef { Id = "iron",   DisplayName = "Iron",   Color = new Color(0.55f, 0.56f, 0.60f), Density = 7870, MeltC = 1538, BoilC = 2862, Conductivity = 0.55f, Electrical = 0.80f, Hardness = 0.85f, Metallic = 0.85f, Roughness = 0.45f });
            Register(new MaterialDef { Id = "copper", DisplayName = "Copper", Color = new Color(0.72f, 0.45f, 0.31f), Density = 8960, MeltC = 1085, BoilC = 2562, Conductivity = 0.90f, Electrical = 0.98f, Hardness = 0.45f, Metallic = 0.95f, Roughness = 0.35f });
            Register(new MaterialDef { Id = "gold",   DisplayName = "Gold",   Color = new Color(1.0f, 0.81f, 0.36f), Density = 19300, MeltC = 1064, BoilC = 2856, Conductivity = 0.80f, Electrical = 0.90f, Hardness = 0.30f, Metallic = 1.0f, Roughness = 0.25f });
            Register(new MaterialDef { Id = "glass",  DisplayName = "Glass",  Color = new Color(0.65f, 0.80f, 0.85f), Density = 2500, MeltC = 1500, BoilC = 2200, Conductivity = 0.05f, Electrical = 0.00f, Hardness = 0.20f, Roughness = 0.10f });
            Register(new MaterialDef { Id = "rubber", DisplayName = "Rubber", Color = new Color(0.15f, 0.15f, 0.17f), Density = 1100, MeltC = 200,  BoilC = 500,  Conductivity = 0.02f, Electrical = 0.00f, Hardness = 0.20f, Roughness = 0.95f });
            Register(new MaterialDef { Id = "water",  DisplayName = "Water",  Color = new Color(0.35f, 0.55f, 0.85f), Density = 1000, MeltC = 0,    BoilC = 100,  Conductivity = 0.30f, Electrical = 0.05f, Hardness = 0.05f, Roughness = 0.10f });
        }
        public static void Register(MaterialDef m) => _all[m.Id] = m;
        public static MaterialDef Get(string id) => _all.TryGetValue(id, out var m) ? m : _all["wood"];
    }

    public enum ShapeKind { Box, Rod, Plate, Sphere, Wheel, OpenBox }

    public class ShapeDef
    {
        public ShapeKind Kind = ShapeKind.Box;
        public Vector3 Size = new(0.5f, 0.5f, 0.5f);
        public float Wall = 0.05f; // OpenBox wall thickness

        public ShapeDef Clone() => new() { Kind = Kind, Size = Size, Wall = Wall };

        public Aabb LocalAabb => new(Size * -0.5f, Size);

        public float VolumeM3 => Kind switch
        {
            ShapeKind.Box or ShapeKind.Plate => Size.X * Size.Y * Size.Z,
            ShapeKind.Rod => Mathf.Pi * Mathf.Pow(Size.X * 0.5f, 2) * Size.Y,
            ShapeKind.Sphere => 4f / 3f * Mathf.Pi * Mathf.Pow(Size.X * 0.5f, 3),
            ShapeKind.Wheel => Mathf.Pi * Mathf.Pow(Size.Y * 0.5f, 2) * Size.X,
            ShapeKind.OpenBox => Mathf.Max(Size.X * Size.Y * Size.Z - CapacityM3, 0f),
            _ => 0f
        };

        public float CapacityM3 => Kind == ShapeKind.OpenBox
            ? Mathf.Max((Size.X - 2 * Wall) * (Size.Z - 2 * Wall) * (Size.Y - Wall), 0f) : 0f;

        public Aabb InnerAabb => Kind == ShapeKind.OpenBox
            ? new Aabb(new Vector3(-(Size.X / 2 - Wall), -Size.Y / 2 + Wall, -(Size.Z / 2 - Wall)),
                       new Vector3(Size.X - 2 * Wall, Size.Y - Wall, Size.Z - 2 * Wall))
            : default;

        public float LargestFace => Kind switch
        {
            ShapeKind.Box or ShapeKind.Plate or ShapeKind.OpenBox =>
                Mathf.Max(Mathf.Max(Size.X * Size.Z, Size.X * Size.Y), Size.Y * Size.Z),
            _ => Mathf.Pi * Mathf.Pow(Mathf.Max(Size.X, Size.Y) * 0.5f, 2)
        };

        public string Describe() => Kind switch
        {
            ShapeKind.Box => $"Box {Size.X:0.00}x{Size.Y:0.00}x{Size.Z:0.00}m",
            ShapeKind.Rod => $"Rod d{Size.X:0.00} x {Size.Y:0.00}m",
            ShapeKind.Plate => $"Plate {Size.X:0.00}x{Size.Z:0.00}m",
            ShapeKind.Sphere => $"Sphere d{Size.X:0.00}m",
            ShapeKind.Wheel => $"Wheel d{Size.Y:0.00} x {Size.X:0.00}m",
            ShapeKind.OpenBox => $"Container {Size.X:0.00}x{Size.Y:0.00}x{Size.Z:0.00}m ({CapacityM3 * 1000:0.#}L)",
            _ => "?"
        };

        public List<SocketDef> BuildSockets()
        {
            var l = new List<SocketDef>();
            void Add(Vector3 p, Vector3 n, Vector3 t, string tag) =>
                l.Add(new SocketDef { Pos = p, Normal = n, Tangent = t, Tag = tag });
            Vector3 h = Size * 0.5f;
            switch (Kind)
            {
                case ShapeKind.Box or ShapeKind.Plate:
                    Add(new Vector3(0, h.Y, 0), Vector3.Up, Vector3.Right, "top");
                    Add(new Vector3(0, -h.Y, 0), Vector3.Down, Vector3.Right, "bottom");
                    Add(new Vector3(h.X, 0, 0), Vector3.Right, Vector3.Back, "side+");
                    Add(new Vector3(-h.X, 0, 0), Vector3.Left, Vector3.Back, "side-");
                    Add(new Vector3(0, 0, h.Z), Vector3.Back, Vector3.Right, "end+");
                    Add(new Vector3(0, 0, -h.Z), Vector3.Forward, Vector3.Right, "end-");
                    break;
                case ShapeKind.Rod:
                    Add(new Vector3(0, h.Y, 0), Vector3.Up, Vector3.Right, "end+");
                    Add(new Vector3(0, -h.Y, 0), Vector3.Down, Vector3.Right, "end-");
                    break;
                case ShapeKind.Sphere:
                    Add(new Vector3(0, -h.Y, 0), Vector3.Down, Vector3.Right, "seat"); // built-in preferred point
                    break;
                case ShapeKind.OpenBox:
                    Add(new Vector3(0, h.Y + 0.06f, 0), Vector3.Up, Vector3.Right, "handle");
                    Add(new Vector3(0, h.Y, 0), Vector3.Up, Vector3.Right, "rim");
                    Add(new Vector3(0, -h.Y, 0), Vector3.Down, Vector3.Right, "base");
                    break;
                case ShapeKind.Wheel:
                    Add(new Vector3(h.X, 0, 0), Vector3.Right, Vector3.Up, "hub+");
                    Add(new Vector3(-h.X, 0, 0), Vector3.Left, Vector3.Up, "hub-");
                    break;
            }
            return l;
        }

        // Runtime shape modification: adjust the primary dimension by one lattice-ish step.
        public void AdjustPrimary(float step)
        {
            switch (Kind)
            {
                case ShapeKind.Box or ShapeKind.Rod or ShapeKind.Plate or ShapeKind.OpenBox:
                    Size = new Vector3(Size.X, Mathf.Clamp(Size.Y + step, 0.05f, 4f), Size.Z);
                    break;
                case ShapeKind.Sphere:
                    float d = Mathf.Clamp(Size.X + step, 0.10f, 4f);
                    Size = new Vector3(d, d, d);
                    break;
                case ShapeKind.Wheel:
                    float w = Mathf.Clamp(Size.Y + step, 0.10f, 4f);
                    Size = new Vector3(Size.X, w, w);
                    break;
            }
        }

        public List<(Mesh Mesh, Transform3D Xf)> BuildMeshes(string materialId = null)
        {
            var custom = CustomMeshes.Get(Kind, Size, materialId);
            if (custom != null) return custom;
            var list = new List<(Mesh, Transform3D)>();
            switch (Kind)
            {
                case ShapeKind.Box:
                case ShapeKind.Plate:
                    list.Add((new BoxMesh { Size = Size }, Transform3D.Identity));
                    break;
                case ShapeKind.Rod:
                    list.Add((new CylinderMesh { TopRadius = Size.X / 2, BottomRadius = Size.X / 2, Height = Size.Y, RadialSegments = 12 }, Transform3D.Identity));
                    break;
                case ShapeKind.Sphere:
                    list.Add((new SphereMesh { Radius = Size.X / 2, Height = Size.X, RadialSegments = 12, Rings = 8 }, Transform3D.Identity));
                    break;
                case ShapeKind.Wheel:
                    list.Add((new CylinderMesh { TopRadius = Size.Y / 2, BottomRadius = Size.Y / 2, Height = Size.X, RadialSegments = 14 },
                              new Transform3D(new Basis(Vector3.Back, Mathf.Pi / 2f), Vector3.Zero)));
                    break;
                case ShapeKind.OpenBox:
                    float x = Size.X, y = Size.Y, z = Size.Z, t = Mathf.Min(Wall, Mathf.Min(x, Mathf.Min(y, z)) / 3f);
                    void Slab(Vector3 sz, Vector3 pos) => list.Add((new BoxMesh { Size = sz }, new Transform3D(Basis.Identity, pos)));
                    Slab(new Vector3(x, t, z), new Vector3(0, -y / 2 + t / 2, 0));
                    Slab(new Vector3(t, y, z), new Vector3(-x / 2 + t / 2, 0, 0));
                    Slab(new Vector3(t, y, z), new Vector3(x / 2 - t / 2, 0, 0));
                    Slab(new Vector3(x, y, t), new Vector3(0, 0, -z / 2 + t / 2));
                    Slab(new Vector3(x, y, t), new Vector3(0, 0, z / 2 - t / 2));
                    break;
            }
            return list;
        }

        public List<(Shape3D Shape, Transform3D Xf)> BuildColliders()
        {
            var list = new List<(Shape3D, Transform3D)>();
            switch (Kind)
            {
                case ShapeKind.Box:
                case ShapeKind.Plate:
                    list.Add((new BoxShape3D { Size = Size }, Transform3D.Identity));
                    break;
                case ShapeKind.Rod:
                    list.Add((new CylinderShape3D { Radius = Size.X / 2, Height = Size.Y }, Transform3D.Identity));
                    break;
                case ShapeKind.Sphere:
                    list.Add((new SphereShape3D { Radius = Size.X / 2 }, Transform3D.Identity));
                    break;
                case ShapeKind.Wheel:
                    list.Add((new CylinderShape3D { Radius = Size.Y / 2, Height = Size.X },
                              new Transform3D(new Basis(Vector3.Back, Mathf.Pi / 2f), Vector3.Zero)));
                    break;
                case ShapeKind.OpenBox:
                    float x = Size.X, y = Size.Y, z = Size.Z, t = Mathf.Min(Wall, Mathf.Min(x, Mathf.Min(y, z)) / 3f);
                    void Slab(Vector3 sz, Vector3 pos) => list.Add((new BoxShape3D { Size = sz }, new Transform3D(Basis.Identity, pos)));
                    Slab(new Vector3(x, t, z), new Vector3(0, -y / 2 + t / 2, 0));
                    Slab(new Vector3(t, y, z), new Vector3(-x / 2 + t / 2, 0, 0));
                    Slab(new Vector3(t, y, z), new Vector3(x / 2 - t / 2, 0, 0));
                    Slab(new Vector3(x, y, t), new Vector3(0, 0, -z / 2 + t / 2));
                    Slab(new Vector3(x, y, t), new Vector3(0, 0, z / 2 - t / 2));
                    break;
            }
            return list;
        }
    }

    public class SubstanceStack
    {
        public string MaterialId = "water";
        public float M3 = 0.005f;
    }

    public class SocketDef
    {
        public Vector3 Pos;      // part-local, includes current Size
        public Vector3 Normal;
        public Vector3 Tangent;
        public string Tag = "";
    }

    public class CatalogEntry
    {
        public ShapeKind Kind; public string Name; public string MaterialId;
        public Vector3 DefaultSize, MinSize, MaxSize;
    }

    public static class PartCatalog
    {
        public static readonly List<CatalogEntry> All = new()
        {
            new CatalogEntry { Kind = ShapeKind.Box,     Name = "Plank (long)",  MaterialId = "wood",  DefaultSize = new Vector3(0.20f, 0.05f, 1.20f), MinSize = new Vector3(0.02f, 0.02f, 0.05f), MaxSize = new Vector3(2f, 0.5f, 4f) },
            new CatalogEntry { Kind = ShapeKind.Box,     Name = "Plank (leg)",   MaterialId = "wood",  DefaultSize = new Vector3(0.10f, 0.55f, 0.10f), MinSize = new Vector3(0.02f, 0.02f, 0.02f), MaxSize = new Vector3(2f, 2f, 2f) },
            new CatalogEntry { Kind = ShapeKind.Box,     Name = "Plank (table)", MaterialId = "wood",  DefaultSize = new Vector3(1.20f, 0.06f, 0.70f), MinSize = new Vector3(0.1f, 0.02f, 0.1f),  MaxSize = new Vector3(3f, 0.5f, 3f) },
            new CatalogEntry { Kind = ShapeKind.Sphere,  Name = "Stone",         MaterialId = "stone", DefaultSize = new Vector3(0.30f, 0.30f, 0.30f), MinSize = new Vector3(0.08f, 0.08f, 0.08f), MaxSize = new Vector3(1.2f, 1.2f, 1.2f) },
            new CatalogEntry { Kind = ShapeKind.OpenBox, Name = "Bucket",        MaterialId = "iron",  DefaultSize = new Vector3(0.32f, 0.30f, 0.32f), MinSize = new Vector3(0.15f, 0.15f, 0.15f), MaxSize = new Vector3(0.8f, 0.8f, 0.8f) },
        };
    }

    // Drop-in custom meshes: a GLB per ShapeKind (+ optional material binding).
    // BuildMeshes uses it, scaled from BaseSize to the part's Size.
    public static class CustomMeshes
    {
        public class Entry { public string Path; public string MaterialId; public List<(Mesh Mesh, Transform3D Xf)> Cached; public Vector3 BaseSize = Vector3.One; }

        public static readonly Dictionary<ShapeKind, Entry> Map = new();

        public static void Register(ShapeKind kind, string path, string materialId = null) =>
            Map[kind] = new Entry { Path = path, MaterialId = materialId };

        public static void RegisterDefaults()
        {
            void Try(ShapeKind k, string p, string m) { if (ResourceLoader.Exists(p)) Register(k, p, m); }
            Try(ShapeKind.Box, "res://Assets/Crafting/plank.glb", "wood");
            Try(ShapeKind.Sphere, "res://Assets/Crafting/stone.glb", "stone");
            Try(ShapeKind.OpenBox, "res://Assets/Crafting/bucket.glb", "iron");
        }

        public static bool Has(ShapeKind kind, string materialId) =>
            Map.TryGetValue(kind, out var e) && ResourceLoader.Exists(e.Path)
            && (e.MaterialId == null || e.MaterialId == materialId);

        public static List<(Mesh, Transform3D)> Get(ShapeKind kind, Vector3 targetSize, string materialId)
        {
            if (!Map.TryGetValue(kind, out var e) || !ResourceLoader.Exists(e.Path)) return null;
            if (e.MaterialId != null && e.MaterialId != materialId) return null;
            if (e.Cached == null)
            {
                e.Cached = new List<(Mesh, Transform3D)>();
                if (ResourceLoader.Load(e.Path) is PackedScene ps && ps.Instantiate() is Node3D inst)
                {
                    Collect(inst, inst, e.Cached);
                    inst.Free();
                }
                if (e.Cached.Count == 0) return null;
                e.BaseSize = Measure(e.Cached);   // auto-fit: model at ANY size works
            }
            Vector3 s = new(
                Mathf.Max(targetSize.X / Mathf.Max(e.BaseSize.X, 0.001f), 0.001f),
                Mathf.Max(targetSize.Y / Mathf.Max(e.BaseSize.Y, 0.001f), 0.001f),
                Mathf.Max(targetSize.Z / Mathf.Max(e.BaseSize.Z, 0.001f), 0.001f));
            var scale = Basis.FromScale(s);
            return e.Cached.ConvertAll(c => (c.Mesh, new Transform3D(scale * c.Xf.Basis, scale * c.Xf.Origin)));
        }

        static Vector3 Measure(List<(Mesh Mesh, Transform3D Xf)> list)
        {
            var mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var (mesh, xf) in list)
            {
                var b = mesh.GetAabb();
                for (int i = 0; i < 8; i++)
                {
                    var c = b.Position + new Vector3((i & 1) > 0 ? b.Size.X : 0, (i & 2) > 0 ? b.Size.Y : 0, (i & 4) > 0 ? b.Size.Z : 0);
                    var p = xf.Basis * c + xf.Origin;
                    mn = new Vector3(Mathf.Min(mn.X, p.X), Mathf.Min(mn.Y, p.Y), Mathf.Min(mn.Z, p.Z));
                    mx = new Vector3(Mathf.Max(mx.X, p.X), Mathf.Max(mx.Y, p.Y), Mathf.Max(mx.Z, p.Z));
                }
            }
            var size = mx - mn;
            return new Vector3(Mathf.Max(size.X, 0.01f), Mathf.Max(size.Y, 0.01f), Mathf.Max(size.Z, 0.01f));
        }

        static void Collect(Node3D node, Node3D stop, List<(Mesh, Transform3D)> list)
        {
            foreach (var c in node.GetChildren())
            {
                if (c is MeshInstance3D mi && mi.Mesh != null)
                    list.Add((mi.Mesh, Accumulate(mi, stop)));
                if (c is Node3D cn) Collect(cn, stop, list);
            }
        }

        static Transform3D Accumulate(Node3D n, Node3D stop)
        {
            var t = Transform3D.Identity;
            var cur = n;
            while (cur != null && cur != stop) { t = cur.Transform * t; cur = cur.GetParent() as Node3D; }
            return t;
        }
    }

    public class Part
    {
        public Guid Id = Guid.NewGuid();
        public string MaterialId = "wood";
        public ShapeDef Shape = new();
        public Transform3D Local = Transform3D.Identity;
        public float TempC = 20f;
        public List<SubstanceStack> Contents = new();

        public bool IsContainer => Shape.CapacityM3 > 0.001f;
        public float ContentsM3 => Contents.Sum(c => c.M3);
        public float MassKg => Shape.VolumeM3 * MaterialLibrary.Get(MaterialId).Density
                             + Contents.Sum(c => c.M3 * MaterialLibrary.Get(c.MaterialId).Density);

        public Part Clone() => new()
        {
            Id = Id, MaterialId = MaterialId, Shape = Shape.Clone(), Local = Local, TempC = TempC,
            Contents = Contents.Select(c => new SubstanceStack { MaterialId = c.MaterialId, M3 = c.M3 }).ToList()
        };
    }

    public class Bond
    {
        public Guid A, B;
        public float Strength = 600f;
    }

    public class CraftedObjectData
    {
        public string Name = "Object";
        public List<Part> Parts = new();
        public List<Bond> Bonds = new();

        public float MassKg => Parts.Sum(p => p.MassKg);

        public Vector3 Center()
        {
            float m = 0; var c = Vector3.Zero;
            foreach (var p in Parts) { float pm = p.MassKg; m += pm; c += p.Local.Origin * pm; }
            return m > 0 ? c / m : Vector3.Zero;
        }

        public void Recenter()
        {
            var c = Center();
            foreach (var p in Parts) p.Local = new Transform3D(p.Local.Basis, p.Local.Origin - c);
        }

        public int BondCount(Guid partId) => Bonds.Count(b => b.A == partId || b.B == partId);

        public void BondAllAdjacent()
        {
            for (int i = 0; i < Parts.Count; i++)
                for (int j = i + 1; j < Parts.Count; j++)
                {
                    var a = Parts[i]; var b = Parts[j];
                    if (Bonds.Any(x => (x.A == a.Id && x.B == b.Id) || (x.B == a.Id && x.A == b.Id))) continue;
                    if (AabbUtil.Touches(AabbUtil.OfShape(a.Shape, a.Local), AabbUtil.OfShape(b.Shape, b.Local)))
                        Bonds.Add(new Bond { A = a.Id, B = b.Id });
                }
        }

        public CraftedObjectData Clone() => new()
        {
            Name = Name,
            Parts = Parts.Select(p => p.Clone()).ToList(),
            Bonds = Bonds.Select(b => new Bond { A = b.A, B = b.B, Strength = b.Strength }).ToList()
        };
    }

    public static class AabbUtil
    {
        public static Aabb OfShape(ShapeDef s, Transform3D xf)
        {
            var b = s.LocalAabb;
            var mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var c = b.Position + new Vector3((i & 1) > 0 ? b.Size.X : 0, (i & 2) > 0 ? b.Size.Y : 0, (i & 4) > 0 ? b.Size.Z : 0);
                var p = xf.Basis * c + xf.Origin;
                mn = new Vector3(Mathf.Min(mn.X, p.X), Mathf.Min(mn.Y, p.Y), Mathf.Min(mn.Z, p.Z));
                mx = new Vector3(Mathf.Max(mx.X, p.X), Mathf.Max(mx.Y, p.Y), Mathf.Max(mx.Z, p.Z));
            }
            return new Aabb(mn, mx - mn);
        }

        public static bool Touches(Aabb a, Aabb b, float slop = 0.03f) => a.Grow(slop).Intersects(b);
        public static bool OverlapsSolid(Aabb a, Aabb b, float shrink = 0.02f) => a.Grow(-shrink).Intersects(b.Grow(-shrink));
    }

    // Structural mutations: partition the part graph into connected components,
    // each becomes its own rigid body positioned at its own center of mass.
    public static class Structure
    {
        public static void RemovePartDeferred(WorldObject obj, Guid id) =>
            Callable.From(() => RemovePart(obj, id)).CallDeferred();

        public static void AddPartDeferred(WorldObject obj, Part part) =>
            Callable.From(() => AddPart(obj, part)).CallDeferred();

        public static void RemovePart(WorldObject obj, Guid id)
        {
            if (!GodotObject.IsInstanceValid(obj)) return;
            var snap = obj.SnapshotPartWorld();
            obj.Data.Parts.RemoveAll(p => p.Id == id);
            obj.Data.Bonds.RemoveAll(b => b.A == id || b.B == id);
            RebuildFromWorld(obj, snap);
        }

        public static void AddPart(WorldObject obj, Part part)
        {
            if (!GodotObject.IsInstanceValid(obj)) return;
            var snap = obj.SnapshotPartWorld();
            obj.Data.Parts.Add(part);
            RebuildFromWorld(obj, snap);
        }

        public static void RebuildFromWorld(WorldObject obj, Dictionary<Guid, Transform3D> world)
        {
            if (!GodotObject.IsInstanceValid(obj)) return;
            var data = obj.Data;
            if (data.Parts.Count == 0) { obj.QueueFree(); return; }
            var basis = obj.GlobalTransform.Basis;
            var parent = obj.GetParent();
            var lv = obj.LinearVelocity;
            var comps = Partition(data);
        }
        // Fuses source into target (source is freed). Position source first.
        public static WorldObject Merge(WorldObject target, WorldObject source, Bond newBond = null)
        {
            if (!GodotObject.IsInstanceValid(target) || !GodotObject.IsInstanceValid(source) || target == source) return target;
            var worldB = source.SnapshotPartWorld();
            var inv = target.GlobalTransform.AffineInverse();
            var data = target.Data;
            foreach (var p in source.Data.Parts)
            {
                p.Local = inv * worldB[p.Id];
                data.Parts.Add(p);
            }
            data.Bonds.AddRange(source.Data.Bonds);
            if (newBond != null) data.Bonds.Add(newBond);
            source.Data.Parts.Clear();
            source.QueueFree();
            target.RebuildBody();
            return target;
        }

        public static List<List<Part>> PartitionData(CraftedObjectData d)
        {
            var index = new Dictionary<Guid, int>();
            for (int i = 0; i < d.Parts.Count; i++) index[d.Parts[i].Id] = i;
            var par = new int[d.Parts.Count];
            for (int i = 0; i < par.Length; i++) par[i] = i;
            int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
            foreach (var b in d.Bonds)
                if (index.TryGetValue(b.A, out var i) && index.TryGetValue(b.B, out var j))
                    par[Find(i)] = Find(j);
            return d.Parts.Select((p, i) => (p, r: Find(i))).GroupBy(t => t.r)
                          .Select(g => g.Select(t => t.p).ToList()).ToList();
        }

        static List<List<Part>> Partition(CraftedObjectData d) => PartitionData(d);
    }
}