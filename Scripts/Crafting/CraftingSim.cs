using Godot;
using System.Linq;
using System.Collections.Generic;

namespace Crafting
{
    
    // Rigid weld between two SEPARATE WorldObjects. Used when gluing onto an
    // anchored/static object: the anchored one stays static, the glued-on one
    // stays a live rigid body, joint-locked at the socket point.
    public class CrossLink
    {
        public Generic6DofJoint3D Joint;
        public WorldObject A, B;
        public System.Guid PartA, PartB;
    }

    // Global simulation: thermal ticks, phase transitions, fluids, storm.
    // Add as autoload later; for the prototype the lab scene owns it.
    public partial class CraftingSim : Node
    {
        public static CraftingSim Instance;
        public readonly List<WorldObject> Objects = new();
        public readonly List<FluidBlob> Blobs = new();
        public readonly List<FluidCloud> Clouds = new();
        public readonly List<Rope> Ropes = new();
        public readonly List<CrossLink> Links = new();
        public readonly List<HeatEmitter> Emitters = new();
        public Node3D WorldRoot;
        public ICraftingPlayer Player;
        public bool Storm;
        public int Zaps;

        float _t; float _zapT = 4f;
        AudioStreamPlayer _rain;
        AudioStream _thunder;

        public override void _Ready()
        {
            Instance = this;
            _rain = new AudioStreamPlayer { Stream = UiKit.Load("res://Assets/Audio/rain.wav"), VolumeDb = -8 };
            if (_rain.Stream != null) AddChild(_rain);
            _thunder = UiKit.Load("res://Assets/Audio/thunder.wav");
        }

        public override void _ExitTree() { if (Instance == this) Instance = null; }

        public override void _PhysicsProcess(double delta)
        {
            for (int i = Links.Count - 1; i >= 0; i--)
            {
                var l = Links[i];
                if (!GodotObject.IsInstanceValid(l.A) || !GodotObject.IsInstanceValid(l.B) || !GodotObject.IsInstanceValid(l.Joint))
                {
                    if (GodotObject.IsInstanceValid(l.Joint)) l.Joint.QueueFree();
                    Links.RemoveAt(i);
                }
            }            
            float d = (float)delta;
            _t += d;
            if (_t >= 0.2f) { _t = 0f; ThermalTick(0.2f); }
            if (Storm)
            {
                _zapT -= d;
                if (_zapT <= 0f) { _zapT = (float)GD.RandRange(4.0, 9.0); }
            }
        }
        public static CrossLink Weld(WorldObject a, WorldObject b, Vector3 worldPos,
                                    System.Guid partA, System.Guid partB)
        {
            var j = new Generic6DofJoint3D();
            Instance.WorldRoot.AddChild(j);
            j.GlobalPosition = worldPos;
            j.NodeA = a.GetPath();
            j.NodeB = b.GetPath();

            // Enable the limit constraints on all six axes.
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            j.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
            j.SetFlagY(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
            j.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularLimit, true);

            // Lock all six degrees of freedom to zero = rigid weld.
            // Godot 4 API: SetParamX/Y/Z(Param, value). The old SetLowerLinearLimitX
            // and friends from Godot 3 no longer exist.
            void LockAll(Generic6DofJoint3D.Param lower, Generic6DofJoint3D.Param upper)
            {
                j.SetParamX(lower, 0f); j.SetParamX(upper, 0f);
                j.SetParamY(lower, 0f); j.SetParamY(upper, 0f);
                j.SetParamZ(lower, 0f); j.SetParamZ(upper, 0f);
            }
            LockAll(Generic6DofJoint3D.Param.LinearLowerLimit,
                    Generic6DofJoint3D.Param.LinearUpperLimit);
            LockAll(Generic6DofJoint3D.Param.AngularLowerLimit,
                    Generic6DofJoint3D.Param.AngularUpperLimit);

            var link = new CrossLink { Joint = j, A = a, B = b, PartA = partA, PartB = partB };
            Instance.Links.Add(link);
            return link;
        }

        public static void BreakLinks(WorldObject o)
        {
            for (int i = Instance.Links.Count - 1; i >= 0; i--)
            {
                var l = Instance.Links[i];
                if (l.A == o || l.B == o)
                {
                    if (GodotObject.IsInstanceValid(l.Joint)) l.Joint.QueueFree();
                    Instance.Links.RemoveAt(i);
                }
            }
        }
        void ThermalTick(float h)
        {
            foreach (var em in Emitters.ToArray())
            {
                if (!IsInstanceValid(em)) continue;
                foreach (var o in Objects.ToArray())
                {
                    if (!IsInstanceValid(o)) continue;
                    if (o.GlobalPosition.DistanceSquaredTo(em.GlobalPosition) > em.Radius * em.Radius) continue;
                    foreach (var p in o.Data.Parts.ToArray())
                    {
                        float dist = (o.GlobalTransform * p.Local).Origin.DistanceTo(em.GlobalPosition);
                        if (dist < em.Radius) o.HeatPart(p, em.Power * h * (1f - dist / em.Radius));
                    }
                }
                foreach (var b in Blobs.ToArray())
                    if (IsInstanceValid(b) && b.GlobalPosition.DistanceTo(em.GlobalPosition) < em.Radius) b.TempC += em.Power * h * 0.5f;
                foreach (var c in Clouds.ToArray())
                    if (IsInstanceValid(c) && c.GlobalPosition.DistanceTo(em.GlobalPosition) < em.Radius) c.TempC += em.Power * h * 0.5f;
            }

            foreach (var o in Objects.ToArray()) if (IsInstanceValid(o)) o.ThermalTick(h);
            foreach (var b in Blobs.ToArray()) { if (!IsInstanceValid(b)) continue; b.TempC += (Settings.AmbientC - b.TempC) * h * 0.002f; b.PhaseCheck(); }
            foreach (var c in Clouds.ToArray()) { if (!IsInstanceValid(c)) continue; c.TempC += (Settings.AmbientC - c.TempC) * h * 0.02f; c.PhaseCheck(); }
        }

        public void SpawnFluidFromPart(WorldObject o, System.Guid partId, Phase ph)
        {
            var p = o.Data?.Parts.Find(x => x.Id == partId);
            if (p == null) return;
            var xf = o.WorldXfOf(p);
            float v = p.Shape.VolumeM3;
            if (ph == Phase.Gas) SpawnCloud(p.MaterialId, v, p.TempC, xf.Origin);
            else SpawnBlob(p.MaterialId, v, p.TempC, xf.Origin);
            Structure.RemovePartDeferred(o, partId);
        }

        public FluidBlob SpawnBlob(string mat, float m3, float temp, Vector3 pos) => FluidBlob.Spawn(mat, m3, temp, pos);
        public FluidCloud SpawnCloud(string mat, float m3, float temp, Vector3 pos)
        {
            var def = MaterialLibrary.Get(mat);
            return FluidCloud.Spawn(mat, m3, Mathf.Max(temp, def.BoilC + 60f), pos);
        }

        // Splits data into one body per bonded component (so unbonded parts fall apart).
        public static WorldObject SpawnObject(CraftedObjectData d, Vector3 pos, float yawRad = 0f, bool frozen = false)
        {
            pos = LiftAboveGround(d, pos);
            WorldObject first = null;
            var rot = new Basis(Vector3.Up, yawRad);
            foreach (var comp in Structure.PartitionData(d))
            {
                float m = 0; var c = Vector3.Zero;
                foreach (var p in comp) { float pm = p.MassKg; m += pm; c += p.Local.Origin * pm; }
                c /= Mathf.Max(m, 0.001f);
                var nd = new CraftedObjectData { Name = d.Name };
                foreach (var p in comp) { p.Local = new Transform3D(p.Local.Basis, p.Local.Origin - c); nd.Parts.Add(p); }
                nd.Bonds.AddRange(d.Bonds.FindAll(b => comp.Any(p => p.Id == b.A) && comp.Any(p => p.Id == b.B)));
                var o = WorldObject.Spawn(nd, new Transform3D(rot, pos + rot * c), Instance.WorldRoot, frozen);
                first ??= o;
            }
            return first;
        }
        // Never spawn inside terrain: on slopes the ground can be higher than
        // the requested point — lift the spawn to sit on top of it instead.
        static Vector3 LiftAboveGround(CraftedObjectData d, Vector3 pos)
        {
            var space = Instance?.WorldRoot?.GetWorld3D().DirectSpaceState;
            if (space == null) return pos;
            float half = 0.1f;
            foreach (var p in d.Parts) half = Mathf.Max(half, p.Shape.Size.Y * 0.5f);

            // 1) Surface below (terrain, floors, other items). Never looks above,
            //    so roofs/ceilings can't capture the spawn.
            var down = space.IntersectRay(PhysicsRayQueryParameters3D.Create(pos, pos + Vector3.Down * 3f, 126));
            if (down.Count > 0)
            {
                float minY = down["position"].AsVector3().Y + half + 0.02f;
                return pos.Y < minY ? new Vector3(pos.X, minY, pos.Z) : pos;
            }
            // 2) Buried (aimed into a slope, spawn point underground): surface
            //    above with an UPWARD normal = terrain, not a ceiling — pop out on it.
            var up = space.IntersectRay(PhysicsRayQueryParameters3D.Create(pos, pos + Vector3.Up * (half + 3f), 126));
            if (up.Count > 0 && up["normal"].AsVector3().Y > 0.5f)
                return new Vector3(pos.X, up["position"].AsVector3().Y + half + 0.02f, pos.Z);
            return pos;
        }

        public static WorldObject SpawnObjectAtXf(CraftedObjectData d, Transform3D xf, bool frozen = false) =>
            WorldObject.Spawn(d, xf, Instance.WorldRoot, frozen);

        public void ToggleStorm()
        {
            Storm = !Storm;
            if (_rain?.Stream != null) { if (Storm) _rain.Play(); else _rain.Stop(); }
            CraftingHud.StormChanged?.Invoke(Storm);
        }

        void TryZap()
        {
            if (Player == null || !Player.IsAlive()) return;
            float y = Player.Limbs.HighestConductiveY();
            if (y > Player.GlobalPosition.Y + 1.6f)
            {
                Zaps++;
                if (_thunder != null)
                {
                    var s = new AudioStreamPlayer { Stream = _thunder };
                    AddChild(s);
                    s.Finished += s.QueueFree;
                    s.Play();
                }
                CraftingHud.ZapFx?.Invoke();
                Player.ZapShake();
            }
        }
        // Shared inventory→world actions, usable in any scene.
        public static void DropItem(InvItem it)
        {
            var sim = Instance;
            var p = sim?.Player;
            if (p == null || p.Cam == null || it == null) return;
            var node = (Node3D)p;
            var fwd = -p.Cam.GlobalTransform.Basis.Z;
            if (it.Kind == ItemKind.Object)
                SpawnObject(it.Data, node.GlobalPosition + fwd * 1.6f + Vector3.Up * 1.1f);
            else
                sim.SpawnBlob(it.MaterialId, it.M3, 20f, node.GlobalPosition + fwd * 1.4f + Vector3.Up * 1.2f);
        }

        public static void PourItem(InvItem it)
        {
            var sim = Instance;
            var p = sim?.Player;
            if (p == null || p.Cam == null || it == null) return;
            var node = (Node3D)p;
            var fwd = -p.Cam.GlobalTransform.Basis.Z;
            sim.SpawnBlob(it.MaterialId, it.M3, 20f, node.GlobalPosition + fwd * 1.4f + Vector3.Up * 1.2f);
        }
    }
}