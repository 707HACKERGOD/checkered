using Godot;
using System.Collections.Generic;

namespace Crafting
{
    // Liquid phase: falls, rests as a puddle, fills containers, freezes back into a Part.
    public partial class FluidBlob : Node3D
    {
        public string MaterialId = "water";
        public float M3 = 0.01f;
        public float TempC = 20f;

        Vector3 _vel;
        bool _resting;
        float _containCd;
        MeshInstance3D _mi;

        public static FluidBlob Spawn(string mat, float m3, float temp, Vector3 pos)
        {
            // FIX (P10, correct location): spawn with a working window above melt point,
            // otherwise ambient cooling re-solidifies fresh liquid in ~1 second.
            var b = new FluidBlob { MaterialId = mat, M3 = m3, TempC = Mathf.Max(temp, MaterialLibrary.Get(mat).MeltC + 45f) };
            CraftingSim.Instance.WorldRoot.AddChild(b);
            b.GlobalPosition = pos;
            CraftingSim.Instance.Blobs.Add(b);
            return b;
        }

        public override void _ExitTree() { if (CraftingSim.Instance != null) CraftingSim.Instance.Blobs.Remove(this); }

        public override void _Ready()
        {
            float r = Mathf.Clamp(Mathf.Pow(3f * M3 / (4f * Mathf.Pi), 1f / 3f) * 1.6f, 0.06f, 0.25f);
            _mi = new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 10, Rings = 6 } };
            var c = MaterialLibrary.Get(MaterialId).Color;
            _mi.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(c, 0.75f), Roughness = 0.15f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            AddChild(_mi);
        }

        public override void _PhysicsProcess(double delta)
        {
            float d = (float)delta;
            if (!_resting)
            {
                _vel += Vector3.Down * 9.8f * d;
                var next = GlobalPosition + _vel * d;
                var hit = GetWorld3D().DirectSpaceState.IntersectRay(
                    PhysicsRayQueryParameters3D.Create(GlobalPosition, next + Vector3.Down * 0.05f));
                if (hit.Count > 0)
                {
                    GlobalPosition = hit["position"].AsVector3() + Vector3.Up * 0.03f;
                    _resting = true; _vel = Vector3.Zero;
                    _mi.Scale = new Vector3(1.4f, 0.35f, 1.4f);
                }
                else GlobalPosition = next;
            }
            _containCd -= d;
            if (_containCd <= 0) { _containCd = 0.2f; TryContain(); }
            if (GlobalPosition.Y < -30f) QueueFree();
        }

        void TryContain()
        {
            foreach (var o in CraftingSim.Instance.Objects)
            {
                if (!IsInstanceValid(o)) continue;
                foreach (var p in o.Data.Parts)
                {
                    if (!p.IsContainer) continue;
                    var wx = o.GlobalTransform * p.Local;
                    var worldInner = AabbUtil.OfShape(new ShapeDef { Kind = ShapeKind.Box, Size = p.Shape.InnerAabb.Size },
                                                      new Transform3D(wx.Basis, wx.Origin + wx.Basis * p.Shape.InnerAabb.Position));
                    if (!worldInner.HasPoint(GlobalPosition)) continue;
                    float free = p.Shape.CapacityM3 - p.ContentsM3;
                    if (free <= 0.0001f) continue;
                    float take = Mathf.Min(free, M3);
                    var s = p.Contents.Find(x => x.MaterialId == MaterialId);
                    if (s == null) { s = new SubstanceStack { MaterialId = MaterialId }; p.Contents.Add(s); }
                    s.M3 += take; M3 -= take;
                    o.RebuildBody();
                    if (M3 <= 0.0005f) { QueueFree(); return; }
                }
            }
        }

        public void PhaseCheck()
        {
            var mat = MaterialLibrary.Get(MaterialId);
            if (TempC > mat.BoilC - 1)
            {
                CraftingSim.Instance.SpawnCloud(MaterialId, M3, TempC, GlobalPosition);
                QueueFree();
            }
            else if (TempC < mat.MeltC - 1) FreezeSolid();
        }

        void FreezeSolid()
        {
            float a = Mathf.Clamp(Mathf.Pow(M3, 1f / 3f), 0.08f, 1f);
            var data = new CraftedObjectData { Name = $"{MaterialLibrary.Get(MaterialId).DisplayName} block" };
            data.Parts.Add(new Part { MaterialId = MaterialId, TempC = TempC, Shape = new ShapeDef { Kind = ShapeKind.Box, Size = new Vector3(a, a, a) } });
            CraftingSim.SpawnObject(data, GlobalPosition + Vector3.Up * 0.1f);
            QueueFree();
        }
    }

    // Gas phase: rises, drifts, can be condensed (cooled) back to liquid or frozen straight to solid.
    // NOTE (v0.1): dissipates after 90s — a conservation ledger is on the roadmap.
    public partial class FluidCloud : Node3D
    {
        public string MaterialId = "water";
        public float M3 = 0.01f;
        public float TempC = 110f;

        float _r; float _life;
        Vector3 _drift;

        public float Radius => _r * (1f + _life * 0.05f);

        public static FluidCloud Spawn(string mat, float m3, float temp, Vector3 pos)
        {
            var c = new FluidCloud { MaterialId = mat, M3 = m3, TempC = temp };
            CraftingSim.Instance.WorldRoot.AddChild(c);
            c.GlobalPosition = pos;
            CraftingSim.Instance.Clouds.Add(c);
            return c;
        }

        public override void _ExitTree() { if (CraftingSim.Instance != null) CraftingSim.Instance.Clouds.Remove(this); }

        public override void _Ready()
        {
            _r = Mathf.Clamp(Mathf.Pow(3f * M3 / (4f * Mathf.Pi), 1f / 3f) * 2.2f, 0.15f, 0.7f);
            var mesh = new MeshInstance3D { Mesh = new SphereMesh { Radius = _r, Height = _r * 2, RadialSegments = 10, Rings = 6 } };
            var c = MaterialLibrary.Get(MaterialId).Color.Lerp(new Color(1, 1, 1), 0.5f);
            mesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(c, 0.3f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            AddChild(mesh);
            _drift = new Vector3((float)GD.RandRange(-0.2, 0.2), 0, (float)GD.RandRange(-0.2, 0.2));
        }

        public override void _PhysicsProcess(double delta)
        {
            float d = (float)delta;
            _life += d;
            if (GlobalPosition.Y < 12f)
                GlobalPosition += (Vector3.Up * 0.45f + _drift + new Vector3(Mathf.Sin(_life * 1.7f) * 0.1f, 0, 0)) * d;
            Scale = Vector3.One * Mathf.Min(1f + _life * 0.05f, 2.5f);
            if (_life > 90f) QueueFree();
        }

        public void PhaseCheck()
        {
            var mat = MaterialLibrary.Get(MaterialId);
            if (TempC < mat.MeltC - 1) // supercooled: straight back to solid
            {
                float a = Mathf.Clamp(Mathf.Pow(M3, 1f / 3f), 0.08f, 1f);
                var data = new CraftedObjectData { Name = $"{mat.DisplayName} block" };
                data.Parts.Add(new Part { MaterialId = MaterialId, TempC = TempC, Shape = new ShapeDef { Kind = ShapeKind.Box, Size = new Vector3(a, a, a) } });
                CraftingSim.SpawnObject(data, GlobalPosition);
                QueueFree();
            }
            else if (TempC < mat.BoilC - 1)
            {
                // FIX: stray "var b = new FluidBlob {...}" line removed — it was P10
                // pasted into the wrong method and referenced m3/temp, which don't
                // exist in this scope. Condensation is just this:
                FluidBlob.Spawn(MaterialId, M3, TempC, GlobalPosition);
                QueueFree();
            }
        }
    }
}