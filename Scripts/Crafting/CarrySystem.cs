using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Crafting
{
    // In-world crafting: Portal-style carry, floor snapping (Ctrl+drop),
    // white-dot socket attachment, floor/wall anchoring (hold Ctrl while
    // carrying), rope handling, and Ctrl+E resize entry.
    public partial class CarrySystem : Node3D
    {
        enum State { None, Carrying, FloorSnap, SocketAttach, RopeHold }
        struct TargetHit { public WorldObject Obj; public Part Part; public SocketDef Sock; public Vector3 WorldPos; public Vector3 WorldNormal; public Vector3 WorldTangent; }

        [Export] public Key PickupKey = Key.E;
        [Export] public Key DropKey = Key.Q;
        [Export] public Key RollKey = Key.R;
        [Export] public Key GridKey = Key.G;
        [Export] public Key DetachKey = Key.X;
        [Export] public uint HeldCollisionMask = 2 | 4 | 8;   // what a held item collides with
        [Export] public uint StaticGlueMask = 2 | 4 | 8;      // floor + buildings + doors: anchor targets

        public uint FloorMask = 2;
        public float DotRadius = 5f;
        public float PickupRange = 3.6f;
        public static CarrySystem Instance { get; private set; }
        public bool IsBusy => _state != State.None;

        State _state = State.None;
        WorldObject _carried;
        float _holdDist = 2.2f;
        float _carryYaw;
        int _roll;
        int _orient;
        float _attachCd;
        uint _savedLayer, _savedMask;

        WorldObject _target;
        Part _targetPart;
        SocketDef _targetSocket;
        Vector3 _floorPoint, _floorNormal, _floorTangent;
        List<(Part Part, SocketDef Sock, Vector3 WorldPos)> _candidates = new();
        int _candIdx;

        Rope _heldRope;
        int _heldEnd;
        TargetHit? _ropeTarget;

        readonly List<MeshInstance3D> _dots = new();
        Material _dotMat, _dotHotMat;

        ICraftingPlayer P => CraftingSim.Instance?.Player;
        Vector3 CamPos => P.Cam.GlobalPosition;
        Vector3 CamDir => -P.Cam.GlobalTransform.Basis.Z;

        static readonly Vector3[] DownAxes =
        {
            Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward
        };

        public override void _Ready()
        {
            Instance = this;
            _dotMat = new StandardMaterial3D { AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = new Color(0.9f, 0.95f, 1f), EmissionEnergyMultiplier = 1.5f };
            _dotHotMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.6f, 0.2f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = new Color(1f, 0.55f, 0.15f), EmissionEnergyMultiplier = 2.2f };
            for (int i = 0; i < 128; i++)
            {
                var mi = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 8, Rings = 4 },
                    Visible = false,
                    MaterialOverride = _dotMat
                };
                AddChild(mi);
                _dots.Add(mi);
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float d = (float)delta;
            var p = P;
            if (p == null || !p.IsAlive()) return;
            if (ResizeEditor.Instance != null && ResizeEditor.Instance.Active) { HideDots(); return; }

            if (_attachCd > 0f) _attachCd -= d;
            if (_carried != null && !GodotObject.IsInstanceValid(_carried)) { _carried = null; _state = State.None; }
            if (_heldRope != null && !GodotObject.IsInstanceValid(_heldRope)) { _heldRope = null; _state = State.None; }

            switch (_state)
            {
                case State.Carrying: TickCarry(d); break;
                case State.FloorSnap: TickFloorSnap(d); break;
                case State.SocketAttach: TickAttach(d); break;
                case State.RopeHold: TickRopeHold(d); break;
                default: TickIdle(); break;
            }

            if (_state == State.Carrying || _state == State.SocketAttach || _state == State.RopeHold) RenderDots();
            else if (_state == State.None) RenderIdleRopeEnds();
            else HideDots();
        }

        void HideDots() { foreach (var dot in _dots) dot.Visible = false; }

        // ---------- idle ----------

        void TickIdle()
        {
            var (o, _) = RayObject(PickupRange);
            CraftingHud.SetHint?.Invoke(o != null ? $"{PickupKey} — pick up · {DetachKey} — detach part · Ctrl+{PickupKey} — resize" : "");
        }

        (WorldObject Obj, Vector3 Pos) RayObject(float dist)
        {
            var from = CamPos;
            var to = from + CamDir * dist;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to, uint.MaxValue, Exclude()));
            if (hit.Count > 0 && hit["collider"].Obj is WorldObject o) return (o, hit["position"].AsVector3());
            return (null, Vector3.Zero);
        }

        Godot.Collections.Array<Rid> Exclude()
        {
            var a = new Godot.Collections.Array<Rid> { P.GetRid() };
            if (_carried != null) a.Add(_carried.GetRid());
            return a;
        }

        // ---------- carrying ----------

        void TickCarry(float d)
        {
            CraftingHud.SetHint?.Invoke($"{PickupKey} — store · {DropKey} — drop · Ctrl+{DropKey} — floor snap · hold Ctrl + aim at ground — anchor · scroll — distance · {RollKey} — turn · aim at a white dot to attach");
            float yaw = P.GlobalTransform.Basis.GetEuler().Y + _carryYaw;
            var cur = _carried.GlobalTransform;
            var tgt = new Transform3D(new Basis(Vector3.Up, yaw), CamPos + CamDir * _holdDist + Vector3.Down * 0.35f);

            var desired = cur.InterpolateWith(tgt, Mathf.Min(1f, 14f * d));
            Vector3 delta = desired.Origin - cur.Origin;
            if (delta.LengthSquared() > 0.0000001f)
            {
                var pars = new PhysicsTestMotionParameters3D { From = cur, Motion = delta };
                pars.ExcludeBodies = new Godot.Collections.Array<Rid> { P.GetRid() };
                var res = new PhysicsTestMotionResult3D();
                if (PhysicsServer3D.BodyTestMotion(_carried.GetRid(), pars, res))
                    delta *= Mathf.Max(res.GetCollisionSafeFraction() - 0.02f, 0f);
            }
            _carried.GlobalTransform = new Transform3D(desired.Basis, cur.Origin + delta);

            if (_attachCd > 0f) return;
            var best = BestTargetSocket(Input.IsPhysicalKeyPressed(Key.Ctrl));
            if (best != null) EnterAttach(best.Value);
        }

        TargetHit? BestTargetSocket(bool includeStatic)
        {
            Vector3 origin = CamPos; Vector3 dir = CamDir;
            TargetHit best = default; float bestDist = 0.35f; bool found = false;
            foreach (var o in CraftingSim.Instance.Objects)
            {
                if (!GodotObject.IsInstanceValid(o) || o == _carried) continue;
                if (o.GlobalPosition.DistanceSquaredTo(P.GlobalPosition) > (DotRadius + 3f) * (DotRadius + 3f)) continue;
                foreach (var part in o.Data.Parts)
                {
                    var pw = o.GlobalTransform * part.Local;
                    foreach (var s in part.Shape.BuildSockets())
                    {
                        Vector3 wpos = pw.Origin + pw.Basis * s.Pos;
                        Vector3 to = wpos - origin;
                        float along = to.Dot(dir);
                        if (along < 0.35f) continue;
                        float rayDist = (to - dir * along).Length();
                        if (rayDist < bestDist)
                        {
                            bestDist = rayDist; found = true;
                            best = new TargetHit { Obj = o, Part = part, Sock = s, WorldPos = wpos, WorldNormal = (pw.Basis * s.Normal).Normalized(), WorldTangent = (pw.Basis * s.Tangent).Normalized() };
                        }
                    }
                }
            }
            if (includeStatic)
            {
                var sh = GetWorld3D().DirectSpaceState.IntersectRay(
                    PhysicsRayQueryParameters3D.Create(origin, origin + dir * PickupRange, StaticGlueMask, Exclude()));
                if (sh.Count > 0)
                {
                    float sd = sh["position"].AsVector3().DistanceTo(origin);
                    if (!found || sd < best.WorldPos.DistanceTo(origin))
                    {
                        Vector3 n = sh["normal"].AsVector3().Normalized();
                        Vector3 t = dir - n * dir.Dot(n);
                        if (t.LengthSquared() < 0.001f) t = Vector3.Right - n * Vector3.Right.Dot(n);
                        best = new TargetHit { Obj = null, Part = null, Sock = null, WorldPos = sh["position"].AsVector3(), WorldNormal = n, WorldTangent = t.Normalized() };
                        found = true;
                    }
                }
            }
            return found ? best : (TargetHit?)null;
        }

        // ---------- socket attach / anchor ----------

        void EnterAttach(TargetHit t)
        {
            _target = t.Obj; _targetPart = t.Part; _targetSocket = t.Sock;
            _floorPoint = t.WorldPos; _floorNormal = t.WorldNormal; _floorTangent = t.WorldTangent;
            _roll = 0;
            _candidates.Clear();
            foreach (var part in _carried.Data.Parts)
            {
                var pw = _carried.GlobalTransform * part.Local;
                foreach (var s in part.Shape.BuildSockets())
                    _candidates.Add((part, s, pw.Origin + pw.Basis * s.Pos));
            }
            _candidates.Sort((a, b) => a.WorldPos.DistanceSquaredTo(t.WorldPos).CompareTo(b.WorldPos.DistanceSquaredTo(t.WorldPos)));
            _candIdx = 0;
            _state = State.SocketAttach;
        }

        void TickAttach(float d)
        {
            Vector3 tPos; Vector3 tNorm; Vector3 tTan;

            if (_target == null)
            {
                // floor/wall anchor: re-raycast every frame
                var sh = GetWorld3D().DirectSpaceState.IntersectRay(
                    PhysicsRayQueryParameters3D.Create(CamPos, CamPos + CamDir * PickupRange, StaticGlueMask, Exclude()));
                if (sh.Count == 0) { BackToCarry(); return; }
                _floorPoint = sh["position"].AsVector3();
                _floorNormal = sh["normal"].AsVector3().Normalized();
                Vector3 t = CamDir - _floorNormal * CamDir.Dot(_floorNormal);
                if (t.LengthSquared() < 0.001f) t = Vector3.Right - _floorNormal * Vector3.Right.Dot(_floorNormal);
                _floorTangent = t.Normalized();
                tPos = _floorPoint; tNorm = _floorNormal; tTan = _floorTangent;
            }
            else
            {
                if (!GodotObject.IsInstanceValid(_target) || !_target.Data.Parts.Contains(_targetPart)) { BackToCarry(); return; }
                var pw = _target.GlobalTransform * _targetPart.Local;
                tPos = pw.Origin + pw.Basis * _targetSocket.Pos;
                tNorm = (pw.Basis * _targetSocket.Normal).Normalized();
                tTan = (pw.Basis * _targetSocket.Tangent).Normalized();
            }

            Vector3 to = tPos - CamPos;
            float along = to.Dot(CamDir);
            if (along < 0.3f || (to - CamDir * along).Length() > 0.45f) { BackToCarry(); return; }

            string what = _target == null ? "ground/wall" : "part";
            CraftingHud.SetHint?.Invoke($"scroll — which point · {RollKey} — roll 90° · {PickupKey} — glue to {what} · {DropKey} — cancel");

            var xf = CandidateTransform(tPos, tNorm, tTan);
            _carried.GlobalTransform = _carried.GlobalTransform.InterpolateWith(xf, Mathf.Min(1f, 22f * d));
        }

        Transform3D CandidateTransform(Vector3 tPos, Vector3 tNorm, Vector3 tTan)
        {
            var (part, sock, _) = _candidates[_candIdx];
            Basis S = part.Local.Basis;
            Vector3 sPosLocal = part.Local.Origin + S * sock.Pos;

            Vector3 wn = -tNorm;
            Vector3 wt = tTan - wn * tTan.Dot(wn);
            if (wt.LengthSquared() < 0.001f) wt = Vector3.Right - wn * Vector3.Right.Dot(wn);
            if (wt.LengthSquared() < 0.001f) wt = Vector3.Up - wn * Vector3.Up.Dot(wn);
            wt = wt.Rotated(wn, _roll * Mathf.Pi / 2f).Normalized();
            Vector3 wb = wn.Cross(wt);

            Basis F = new Basis(sock.Normal, sock.Tangent, sock.Normal.Cross(sock.Tangent));
            Basis W = new Basis(wn, wt, wb);
            Basis B = W * F.Inverse() * S.Inverse();
            return new Transform3D(B, tPos - B * sPosLocal);
        }

        void ConfirmAttach()
        {
            if (_target == null)
            {
                // anchor to the static world: becomes a frozen static body
                _carried.GlobalTransform = CandidateTransform(_floorPoint, _floorNormal, _floorTangent);
                _carried.Data.Anchored = true;
                _carried.IsCarried = false;
                _carried.CollisionLayer = _savedLayer;
                _carried.CollisionMask = _savedMask;
                _carried.Freeze = true;
                _carried.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
                _carried.LinearVelocity = Vector3.Zero;
                _carried = null; _state = State.None;
                CraftingHud.Toast?.Invoke("Anchored — static until you pick it up again.");
                return;
            }

            var (part, _, _) = _candidates[_candIdx];
            var pw = _target.GlobalTransform * _targetPart.Local;
            _carried.GlobalTransform = CandidateTransform(pw.Origin + pw.Basis * _targetSocket.Pos,
                (pw.Basis * _targetSocket.Normal).Normalized(), (pw.Basis * _targetSocket.Tangent).Normalized());

            if (_target.Freeze)
            {
                // Gluing onto an anchored/static object: carried stays its own
                // RIGID body, welded to the static one at the socket point.
                _carried.IsCarried = false;
                _carried.Freeze = false;
                _carried.CollisionLayer = _savedLayer;
                _carried.CollisionMask = _savedMask;
                CraftingSim.Weld(_target, _carried,
                    pw.Origin + pw.Basis * _targetSocket.Pos, _targetPart.Id, part.Id);
                _carried = null; _target = null; _state = State.None;
                CraftingHud.Toast?.Invoke("Welded on — rigid, target stays anchored.");
                return;
            }

            var bond = new Bond { A = _targetPart.Id, B = part.Id };
            _carried.IsCarried = false;
            _carried.Freeze = false;
            _carried.CollisionLayer = _savedLayer;
            _carried.CollisionMask = _savedMask;
            Structure.Merge(_target, _carried, bond);
            _carried = null; _target = null; _state = State.None;
            CraftingHud.Toast?.Invoke("Glued.");
        }
        void BackToCarry() { _state = State.Carrying; _attachCd = 0.45f; }

        // ---------- floor snap ----------

        void EnterFloorSnap()
        {
            float best = float.MaxValue; _orient = 0;
            for (int i = 0; i < DownAxes.Length; i++)
            {
                var b = OrientationBasis(i, 0);
                float err = (b * Vector3.Up - _carried.GlobalTransform.Basis * Vector3.Up).LengthSquared();
                if (err < best) { best = err; _orient = i; }
            }
            _roll = 0;
            _state = State.FloorSnap;
        }

        Basis OrientationBasis(int i, int rollSteps)
        {
            var b = new Basis(new Quaternion(DownAxes[i], Vector3.Down));
            return new Basis(Vector3.Up, rollSteps * Mathf.Pi / 2f) * b;
        }

        void TickFloorSnap(float d)
        {
            CraftingHud.SetHint?.Invoke($"scroll — orientation · {RollKey} — roll · {PickupKey} — place · {DropKey} — back to carrying");
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(
                PhysicsRayQueryParameters3D.Create(CamPos, CamPos + CamDir * 7f, FloorMask, Exclude()));
            if (hit.Count == 0) return;
            Vector3 pos = hit["position"].AsVector3();
            Basis b = OrientationBasis(_orient, _roll);
            float top = 0.25f;
            foreach (var part in _carried.Data.Parts)
                top = Mathf.Max(top, AabbUtil.OfShape(part.Shape, new Transform3D(b * part.Local.Basis, b * part.Local.Origin)).End.Y);
            var tgt = new Transform3D(b, pos + Vector3.Up * (top + 0.01f));
            _carried.GlobalTransform = _carried.GlobalTransform.InterpolateWith(tgt, Mathf.Min(1f, 25f * d));
        }

        void ConfirmFloor()
        {
            _carried.CollisionLayer = _savedLayer;
            _carried.CollisionMask = _savedMask;
            _carried.Freeze = false;
            _carried.IsCarried = false;
            _carried.LinearVelocity = Vector3.Zero;
            _carried = null;
            _state = State.None;
            CraftingHud.Toast?.Invoke("Placed.");
        }

        // ---------- rope ----------

        void TickRopeHold(float d)
        {
            CraftingHud.SetHint?.Invoke($"{PickupKey} — tie to aimed dot · {DropKey} — let go · scroll — rope length");
            _heldRope.SetHold(_heldEnd, CamPos + CamDir * _holdDist);
            _ropeTarget = BestTargetSocket(false);
        }

        void TryTieRope()
        {
            var best = BestTargetSocket(false);
            if (best == null) { CraftingHud.Toast?.Invoke("Aim at a white dot to tie the rope."); return; }
            _heldRope.Attach(_heldEnd, best.Value.Obj, best.Value.Part, best.Value.Sock);
            _heldRope = null; _state = State.None;
            CraftingHud.Toast?.Invoke("Tied. Grab the other end (E) to tie it somewhere else.");
        }

        // ---------- pickup / store / drop / detach / scoop ----------

        void TryPickup()
        {
            // rope ends first (they have no collider)
            Rope bestRope = null; int bestEnd = -1; float bd = 0.22f;
            foreach (var r in CraftingSim.Instance.Ropes)
            {
                if (!GodotObject.IsInstanceValid(r)) continue;
                if (r.GlobalPosition.DistanceSquaredTo(P.GlobalPosition) > PickupRange * PickupRange * 4f) continue;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 to = r.EndPosition(i) - CamPos;
                    float along = to.Dot(CamDir);
                    if (along < 0.2f || along > PickupRange) continue;
                    if ((to - CamDir * along).Length() < bd) { bd = (to - CamDir * along).Length(); bestRope = r; bestEnd = i; }
                }
            }
            if (bestRope != null)
            {
                _heldRope = bestRope; _heldEnd = bestEnd;
                bestRope.Grab(bestEnd);
                _holdDist = 2.0f;
                _state = State.RopeHold;
                return;
            }

            var (o, _) = RayObject(PickupRange);
            if (o != null)
            {
                _carried = o;
                o.IsCarried = true;
                o.Data.Anchored = false;   // picking it up un-anchors it
                CraftingSim.BreakLinks(o);   // picking it up detaches anything welded to it
                _savedLayer = o.CollisionLayer; _savedMask = o.CollisionMask;
                o.CollisionLayer = 0;
                o.CollisionMask = HeldCollisionMask;
                o.Freeze = true;
                o.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
                o.LinearVelocity = Vector3.Zero;
                _carryYaw = 0f; _holdDist = 2.2f;
                _state = State.Carrying;
                return;
            }
            TryScoop();
        }

        void Store()
        {
            var data = _carried.Data.Clone();
            data.Anchored = false;
            if (data.Name == "Object") data.Name = AutoName(data);
            var item = new InvItem { Kind = ItemKind.Object, Data = data, Name = data.Name };
            if (!Inventory.Add(item)) { CraftingHud.Toast?.Invoke("Inventory full — drop it instead."); return; }
            ThumbnailGen.Render(item);
            _carried.IsCarried = false;
            _carried.QueueFree();
            _carried = null;
            _state = State.None;
        }

        void Drop()
        {
            _carried.CollisionLayer = _savedLayer;
            _carried.CollisionMask = _savedMask;
            _carried.Freeze = false;
            _carried.IsCarried = false;
            _carried.LinearVelocity = CamDir * 2.5f;
            _carried = null;
            _state = State.None;
        }

        void DetachAimed()
        {
            var (o, pos) = RayObject(PickupRange);
            if (o == null) { CraftingHud.Toast?.Invoke("Nothing aimed at."); return; }
            var part = o.PartAtWorld(pos);
            if (part == null) return;
            CraftingSim.BreakLinks(o);
            var single = new CraftedObjectData { Name = $"{MaterialLibrary.Get(part.MaterialId).DisplayName} {part.Shape.Kind}" };
            var copy = part.Clone();
            copy.Local = Transform3D.Identity;
            single.Parts.Add(copy);
            var item = new InvItem { Kind = ItemKind.Object, Data = single, Name = single.Name };
            Structure.RemovePartDeferred(o, part.Id);
            if (Inventory.Add(item)) ThumbnailGen.Render(item);
            CraftingHud.Toast?.Invoke("Part detached to inventory.");
        }

        void TryScoop()
        {
            var sim = CraftingSim.Instance;
            FluidBlob best = null; float bd = 2.5f;
            foreach (var b in sim.Blobs)
            {
                if (!GodotObject.IsInstanceValid(b)) continue;
                float dist = b.GlobalPosition.DistanceTo(P.GlobalPosition + Vector3.Up);
                if (dist < bd) { bd = dist; best = b; }
            }
            if (best == null) { CraftingHud.Toast?.Invoke("Nothing to pick up."); return; }
            var c = P.Limbs.FindCarriedContainer();
            if (c.Part == null) { CraftingHud.Toast?.Invoke("Carry a container in a hand to scoop liquids."); return; }
            float free = c.Part.Shape.CapacityM3 - c.Part.ContentsM3;
            float take = Mathf.Min(free, best.M3);
            var s = c.Part.Contents.Find(x => x.MaterialId == best.MaterialId);
            if (s == null) { s = new SubstanceStack { MaterialId = best.MaterialId }; c.Part.Contents.Add(s); }
            s.M3 += take;
            best.M3 -= take;
            if (best.M3 <= 0.0005f) best.QueueFree();
            P.Limbs.RefreshVisual(c.Slot);
            CraftingHud.Toast?.Invoke($"Scooped {take * 1000:0.#} L.");
        }

        static string AutoName(CraftedObjectData d)
        {
            var p = d.Parts.FirstOrDefault();
            if (d.Parts.Count == 1 && p != null) return $"{MaterialLibrary.Get(p.MaterialId).DisplayName} {p.Shape.Kind}";
            return $"{d.Parts.Count}-part object";
        }

        // ---------- dots ----------

        void RenderDots()
        {
            int i = 0;
            Vector3 hotPos = Vector3.Zero; bool hasHot = false;
            if (_state == State.SocketAttach)
            {
                if (_target == null) { hotPos = _floorPoint; hasHot = true; }
                else if (GodotObject.IsInstanceValid(_target) && _targetPart != null)
                {
                    var pw = _target.GlobalTransform * _targetPart.Local;
                    hotPos = pw.Origin + pw.Basis * _targetSocket.Pos;
                    hasHot = true;
                }
            }
            else if (_state == State.RopeHold && _ropeTarget != null) { hotPos = _ropeTarget.Value.WorldPos; hasHot = true; }

            foreach (var o in CraftingSim.Instance.Objects)
            {
                if (i >= _dots.Count - 4) break;
                if (!GodotObject.IsInstanceValid(o) || o == _carried) continue;
                if (o.GlobalPosition.DistanceSquaredTo(P.GlobalPosition) > DotRadius * DotRadius) continue;
                foreach (var part in o.Data.Parts)
                {
                    if (i >= _dots.Count - 4) break;
                    var pw = o.GlobalTransform * part.Local;
                    foreach (var s in part.Shape.BuildSockets())
                    {
                        if (i >= _dots.Count - 4) break;
                        Vector3 wpos = pw.Origin + pw.Basis * s.Pos;
                        bool hot = hasHot && wpos.DistanceSquaredTo(hotPos) < 0.0004f;
                        var dot = _dots[i++];
                        dot.Visible = true;
                        dot.Position = wpos;
                        dot.Scale = hot ? new Vector3(1.9f, 1.9f, 1.9f) : Vector3.One;
                        dot.MaterialOverride = hot ? _dotHotMat : _dotMat;
                    }
                }
            }
            // rope ends
            foreach (var r in CraftingSim.Instance.Ropes)
            {
                if (i >= _dots.Count) break;
                if (!GodotObject.IsInstanceValid(r)) continue;
                if (r.GlobalPosition.DistanceSquaredTo(P.GlobalPosition) > DotRadius * DotRadius) continue;
                for (int e = 0; e < 2 && i < _dots.Count; e++)
                {
                    var dot = _dots[i++];
                    dot.Visible = true;
                    dot.Position = r.EndPosition(e);
                    dot.Scale = Vector3.One;
                    dot.MaterialOverride = _dotMat;
                }
            }
            // floor anchor point
            if (_state == State.SocketAttach && _target == null && i < _dots.Count)
            {
                var dot = _dots[i++];
                dot.Visible = true;
                dot.Position = _floorPoint;
                dot.Scale = new Vector3(1.9f, 1.9f, 1.9f);
                dot.MaterialOverride = _dotHotMat;
            }
            for (; i < _dots.Count; i++) _dots[i].Visible = false;
        }

        void RenderIdleRopeEnds()
        {
            int i = 0;
            foreach (var r in CraftingSim.Instance.Ropes)
            {
                if (i >= _dots.Count) break;
                if (!GodotObject.IsInstanceValid(r)) continue;
                if (r.GlobalPosition.DistanceSquaredTo(P.GlobalPosition) > DotRadius * DotRadius) continue;
                for (int e = 0; e < 2 && i < _dots.Count; e++)
                {
                    var dot = _dots[i++];
                    dot.Visible = true;
                    dot.Position = r.EndPosition(e);
                    dot.Scale = Vector3.One;
                    dot.MaterialOverride = _dotMat;
                }
            }
            for (; i < _dots.Count; i++) _dots[i].Visible = false;
        }

        // ---------- input ----------

        public override void _UnhandledInput(InputEvent e)
        {
            if (UiStack.Blocking) return;
            if (ResizeEditor.Instance != null && ResizeEditor.Instance.Active) return;
            var p = P;
            if (p == null || !p.IsAlive()) return;

            if (e is InputEventMouseButton mb && mb.Pressed)
            {
                int dir = mb.ButtonIndex == MouseButton.WheelUp ? -1 : mb.ButtonIndex == MouseButton.WheelDown ? 1 : 0;
                if (dir != 0) { GetViewport().SetInputAsHandled(); Scroll(dir); return; }
            }
            if (e is not InputEventKey k || !k.Pressed || k.Echo) return;

            var key = k.PhysicalKeycode;
            if (key == PickupKey)
            {
                GetViewport().SetInputAsHandled();
                if (k.CtrlPressed)
                {
                    if (_state == State.None)
                    {
                        var (o, _) = RayObject(PickupRange);
                        if (o != null && ResizeEditor.Instance != null) ResizeEditor.Instance.Begin(o);
                        else CraftingHud.Toast?.Invoke("Aim at an object to resize it (Ctrl+E).");
                    }
                    return;
                }
                if (_state == State.None) TryPickup();
                else if (_state == State.Carrying) Store();
                else if (_state == State.FloorSnap) ConfirmFloor();
                else if (_state == State.SocketAttach) ConfirmAttach();
                else if (_state == State.RopeHold) TryTieRope();
                return;
            }
            if (key == DropKey)
            {
                GetViewport().SetInputAsHandled();
                if (k.CtrlPressed) { if (_state == State.Carrying) EnterFloorSnap(); return; }
                if (_state == State.Carrying) Drop();
                else if (_state == State.FloorSnap) _state = State.Carrying;
                else if (_state == State.SocketAttach) BackToCarry();
                else if (_state == State.RopeHold) { _heldRope.Release(_heldEnd); _heldRope = null; _state = State.None; }
                return;
            }
            if (key == RollKey)
            {
                GetViewport().SetInputAsHandled();
                if (_state == State.Carrying) _carryYaw = Mathf.Wrap(_carryYaw + Mathf.Pi / 2f, -Mathf.Tau, Mathf.Tau);
                else _roll++;
                return;
            }
            if (key == GridKey) { GetViewport().SetInputAsHandled(); CraftingHud.ToggleGrid?.Invoke(); return; }
            if (key == DetachKey)
            {
                if (_state != State.None) return;
                GetViewport().SetInputAsHandled();
                DetachAimed();
            }
        }

        void Scroll(int dir)
        {
            switch (_state)
            {
                case State.Carrying:
                    _holdDist = Mathf.Clamp(_holdDist + dir * 0.25f, 1.2f, 3.5f);
                    break;
                case State.FloorSnap:
                    _orient = ((_orient + dir) % 6 + 6) % 6;
                    break;
                case State.SocketAttach:
                    if (_candidates.Count > 0) _candIdx = ((_candIdx + dir) % _candidates.Count + _candidates.Count) % _candidates.Count;
                    break;
                case State.RopeHold:
                    _heldRope.AdjustLength(dir * 0.25f);
                    break;
            }
        }
    }
}