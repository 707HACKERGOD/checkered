using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Crafting
{
    // One rigid body = one connected component of a part graph.
    public partial class WorldObject : RigidBody3D
    {
        public CraftedObjectData Data = new();
        public bool IsCarried;
        public Dictionary<Guid, List<MeshInstance3D>> PartMeshes = new();

        readonly List<Node> _built = new();
        readonly Dictionary<Guid, bool> _glowing = new();
        float _breakCd;

        public override void _ExitTree() { if (CraftingSim.Instance != null) CraftingSim.Instance.Objects.Remove(this); }

        public static WorldObject Spawn(CraftedObjectData data, Transform3D worldXf, Node parent, bool frozen = false)
        {
            var o = new WorldObject { Data = data };
            parent.AddChild(o);
            o.GlobalTransform = worldXf;
            o.RebuildBody();
            if (frozen) { o.Freeze = true; o.FreezeMode = FreezeModeEnum.Static; }
            return o;
        }

        public void RebuildBody()
        {
            foreach (var n in _built) { if (IsInstanceValid(n)) { RemoveChild(n); n.QueueFree(); } }
            _built.Clear(); PartMeshes.Clear();
            float mass = 0; var com = Vector3.Zero;
            foreach (var p in Data.Parts)
            {
                var mis = VisualBuilder.AttachPart(this, p);
                _built.AddRange(mis); PartMeshes[p.Id] = mis;
                _built.AddRange(VisualBuilder.AttachPartColliders(this, p));
                float m = p.MassKg; mass += m; com += p.Local.Origin * m;
            }
            Mass = Mathf.Max(mass, 0.05f);
            CenterOfMassMode = CenterOfMassModeEnum.Custom;
            CenterOfMass = mass > 0 ? com / mass : Vector3.Zero;
            ContactMonitor = true; MaxContactsReported = 6;
            LinearDamp = 0.05f; AngularDamp = 0.6f;
            CollisionLayer = 1u << 6;   // your layer 7 = Items
            CollisionMask = 127;        // collides with layers 1-7 (player, floor, buildings, doors, NPC, vehicles, items)
        }

        public Transform3D WorldXfOf(Part p) => GlobalTransform * p.Local;

        public Part PartAtWorld(Vector3 wp) => PartNearestLocal(ToLocal(wp));

        public Part PartNearestLocal(Vector3 lp)
        {
            Part best = null; float bd = 1.2f;
            foreach (var p in Data.Parts)
            {
                if (p.Shape.LocalAabb.HasPoint(lp)) return p;
                float d = p.Local.Origin.DistanceTo(lp);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public void HeatPart(Part p, float dC)
        {
            if (p == null) return;
            p.TempC = Mathf.Clamp(p.TempC + dC * (0.25f + MaterialLibrary.Get(p.MaterialId).Conductivity), -270f, 4000f);
        }

        public Dictionary<Guid, Transform3D> SnapshotPartWorld() =>
            Data.Parts.ToDictionary(p => p.Id, p => GlobalTransform * p.Local);

        public override void _EnterTree()
        {
            if (CraftingSim.Instance != null) CraftingSim.Instance.Objects.Add(this);
            BodyEntered += OnBodyEntered;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_breakCd > 0f) _breakCd -= (float)delta;
        }

        // Impact = relative momentum. BodyEntered + velocities is stable across versions.
        private void OnBodyEntered(Node body)
        {
            if (_breakCd > 0f || Data == null || Data.Parts.Count < 2) return; // single parts have no bonds
            if (IsCarried || (body is WorldObject cw && cw.IsCarried)) return;   // carried items never break glue
            if (body is CharacterBody3D) return;                    // player bumps never break glue
            if (body is not CollisionObject3D other) return;

            var otherVel = other is RigidBody3D rb ? rb.LinearVelocity : Vector3.Zero;
            float impact = (otherVel - LinearVelocity).Length()
                        * (other is RigidBody3D rb2 && rb2.Mass > 0f ? rb2.Mass : Mass); // statics/frozen: our own mass
            if (impact < 120f) return;

            var p = PartNearestLocal(ToLocal((other.GlobalPosition + GlobalPosition) * 0.5f));
            if (p == null) return;
            _breakCd = 0.5f;
            BreakWeakestBondOf(p);
        }

        void BreakWeakestBondOf(Part p)
        {
            Bond weakest = null;
            foreach (var b in Data.Bonds)
                if ((b.A == p.Id || b.B == p.Id) && (weakest == null || b.Strength < weakest.Strength)) weakest = b;
            if (weakest == null) return;
            Data.Bonds.Remove(weakest);
            var snap = SnapshotPartWorld();
            Callable.From(() => { if (IsInstanceValid(this)) Structure.RebuildFromWorld(this, snap); }).CallDeferred();
        }

        public void ThermalTick(float h)
        {
            var byId = new Dictionary<Guid, Part>();
            foreach (var p in Data.Parts) byId[p.Id] = p;

            foreach (var b in Data.Bonds)
            {
                if (!byId.TryGetValue(b.A, out var pa) || !byId.TryGetValue(b.B, out var pb)) continue;
                float k = (MaterialLibrary.Get(pa.MaterialId).Conductivity + MaterialLibrary.Get(pb.MaterialId).Conductivity) * 0.5f
                        * 90f * Mathf.Min(pa.Shape.LargestFace, pb.Shape.LargestFace);
                float q = k * (pb.TempC - pa.TempC) * h;
                pa.TempC += q / (pa.MassKg * 500f + 1f);
                pb.TempC -= q / (pb.MassKg * 500f + 1f);
            }

            List<(WorldObject o, Guid id, Phase ph)> phase = null;
            foreach (var p in Data.Parts)
            {
                p.TempC += (Settings.AmbientC - p.TempC) * h * 0.004f;
                var mat = MaterialLibrary.Get(p.MaterialId);
                if (p.TempC > mat.BoilC + 1) (phase ??= new()).Add((this, p.Id, Phase.Gas));
                else if (p.TempC > mat.MeltC + 1) (phase ??= new()).Add((this, p.Id, Phase.Liquid));
                if (p.IsContainer) TickContents(p);
            }
            if (phase != null)
                foreach (var (o, id, ph) in phase) CraftingSim.Instance.SpawnFluidFromPart(o, id, ph);

            UpdateGlow();
        }

        void TickContents(Part p)
        {
            for (int i = p.Contents.Count - 1; i >= 0; i--)
            {
                var s = p.Contents[i];
                var sm = MaterialLibrary.Get(s.MaterialId);
                if (p.TempC > sm.BoilC - 1)
                {
                    float evap = Mathf.Min(s.M3, 0.0015f);
                    s.M3 -= evap;
                    CraftingSim.Instance.SpawnCloud(s.MaterialId, evap, p.TempC, WorldXfOf(p).Origin + Vector3.Up * 0.25f);
                    if (s.M3 <= 0.0006f) p.Contents.RemoveAt(i);
                    RebuildBody();
                }
                else if (p.TempC < sm.MeltC - 1 && s.M3 > 0.0006f)
                {
                    FreezeContent(p, i);
                }
            }
        }

        void FreezeContent(Part p, int idx)
        {
            var s = p.Contents[idx];
            var inner = p.Shape.InnerAabb;
            float ix = inner.Size.X, iz = inner.Size.Z, iy = inner.Size.Y;
            if (ix <= 0 || iz <= 0) { p.Contents.RemoveAt(idx); return; }
            float hgt = Mathf.Clamp(s.M3 / (ix * iz), 0.03f, iy);
            var ice = new Part
            {
                MaterialId = s.MaterialId,
                TempC = p.TempC,
                Shape = new ShapeDef { Kind = ShapeKind.Box, Size = new Vector3(ix, hgt, iz) },
                Local = new Transform3D(Basis.Identity,
                        p.Local.Origin + p.Local.Basis * new Vector3(0, inner.Position.Y + hgt / 2f, 0))
            };
            p.Contents.RemoveAt(idx);
            Data.Bonds.Add(new Bond { A = p.Id, B = ice.Id });
            Structure.AddPartDeferred(this, ice);
            CraftingHud.Toast?.Invoke($"{MaterialLibrary.Get(s.MaterialId).DisplayName} froze solid — it's a real part now!");
        }

        void UpdateGlow()
        {
            foreach (var p in Data.Parts)
            {
                bool hot = p.TempC > 260f;
                if (_glowing.TryGetValue(p.Id, out var was) && was == hot) continue;
                _glowing[p.Id] = hot;
                if (PartMeshes.TryGetValue(p.Id, out var mis))
                    foreach (var mi in mis) mi.MaterialOverride = hot ? VisualBuilder.GlowFor(p.MaterialId) : null;
            }
        }
    }
}