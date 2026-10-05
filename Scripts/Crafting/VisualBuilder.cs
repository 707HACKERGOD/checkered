using Godot;
using System.Collections.Generic;

namespace Crafting
{
    // Builds meshes/colliders/materials for parts. Shared by world objects,
    // limb attachments and inventory thumbnails.
    public static class VisualBuilder
    {
        static readonly Dictionary<string, StandardMaterial3D> _mats = new();
        static readonly Dictionary<string, StandardMaterial3D> _glow = new();

        public static StandardMaterial3D MatFor(string materialId, bool frozen = false)
        {
            string key = materialId + (frozen ? "#ice" : "");
            if (_mats.TryGetValue(key, out var m)) return m;
            var def = MaterialLibrary.Get(materialId);
            Color c = def.Color;
            if (frozen) c = c.Lerp(new Color(0.85f, 0.92f, 1f), 0.6f);
            m = new StandardMaterial3D { AlbedoColor = c, Metallic = def.Metallic, Roughness = def.Roughness };
            if (materialId == "water") { m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha; m.AlbedoColor = new Color(c, 0.8f); }
            if (materialId == "glass") { m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha; m.AlbedoColor = new Color(c, 0.55f); }
            _mats[key] = m;
            return m;
        }

        public static StandardMaterial3D GlowFor(string materialId)
        {
            if (_glow.TryGetValue(materialId, out var g)) return g;
            var baseMat = MatFor(materialId);
            g = new StandardMaterial3D
            {
                AlbedoColor = baseMat.AlbedoColor, Metallic = baseMat.Metallic, Roughness = baseMat.Roughness,
                EmissionEnabled = true, Emission = new Color(1f, 0.42f, 0.1f), EmissionEnergyMultiplier = 1.4f
            };
            _glow[materialId] = g;
            return g;
        }

        public static List<MeshInstance3D> AttachPart(Node3D parent, Part p)
        {
            var result = new List<MeshInstance3D>();
            bool frozen = p.MaterialId == "water" && p.TempC < 0f;
            bool custom = CustomMeshes.Has(p.Shape.Kind, p.MaterialId);
            var mat = MatFor(p.MaterialId, frozen);
            foreach (var (mesh, xf) in p.Shape.BuildMeshes(p.MaterialId))
            {
                var mi = new MeshInstance3D { Mesh = mesh };
                if (!custom) mi.MaterialOverride = mat;   // custom GLBs keep their own materials
                parent.AddChild(mi);
                mi.Transform = p.Local * xf;
                result.Add(mi);
            }
            // visible fill line for liquid contents
            if (p.ContentsM3 > 0.0005f)
            {
                var inner = p.Shape.InnerAabb;
                if (inner.Size.X > 0.001f && inner.Size.Z > 0.001f)
                {
                    float fill = Mathf.Clamp(p.ContentsM3 / (inner.Size.X * inner.Size.Z), 0f, inner.Size.Y);
                    var liquid = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(inner.Size.X, fill, inner.Size.Z) } };
                    var lm = MatFor(p.Contents[0].MaterialId).Duplicate() as StandardMaterial3D;
                    if (lm != null) { lm.Transparency = BaseMaterial3D.TransparencyEnum.Alpha; lm.AlbedoColor = new Color(lm.AlbedoColor, 0.75f); }
                    liquid.MaterialOverride = lm;
                    parent.AddChild(liquid);
                    liquid.Transform = new Transform3D(p.Local.Basis,
                        p.Local.Origin + p.Local.Basis * new Vector3(0, inner.Position.Y + fill / 2f, 0));
                    result.Add(liquid);
                }
            }
            return result;
        }

        public static List<CollisionShape3D> AttachPartColliders(Node3D parent, Part p)
        {
            var result = new List<CollisionShape3D>();
            foreach (var (shape, xf) in p.Shape.BuildColliders())
            {
                var cs = new CollisionShape3D { Shape = shape };
                parent.AddChild(cs);
                cs.Transform = p.Local * xf;
                result.Add(cs);
            }
            return result;
        }

        public static Node3D BuildObjectVisual(CraftedObjectData d)
        {
            var root = new Node3D();
            foreach (var p in d.Parts) AttachPart(root, p);
            return root;
        }

        public static Aabb ComputeAabb(CraftedObjectData d)
        {
            var a = new Aabb(new Vector3(1, 1, 1) * 1000f, Vector3.Zero);
            foreach (var p in d.Parts)
            {
                var b = AabbUtil.OfShape(p.Shape, p.Local);
                var mn = new Vector3(Mathf.Min(a.Position.X, b.Position.X), Mathf.Min(a.Position.Y, b.Position.Y), Mathf.Min(a.Position.Z, b.Position.Z));
                var mx = new Vector3(Mathf.Max(a.End.X, b.End.X), Mathf.Max(a.End.Y, b.End.Y), Mathf.Max(a.End.Z, b.End.Z));
                a = new Aabb(mn, mx - mn);
            }
            return a;
        }
    }
}