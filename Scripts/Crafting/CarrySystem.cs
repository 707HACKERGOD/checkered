using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Crafting
{
    // In-world crafting: Portal-style carry, floor snapping (Ctrl+Q),
    // white-dot socket attachment with scroll cycling. States:
    // None -> Carrying -> (FloorSnap | SocketAttach) -> back.
    public partial class CarrySystem : Node3D
    {
        enum State { None, Carrying, FloorSnap, SocketAttach }
        struct TargetHit { public WorldObject Obj; public Part Part; public SocketDef Sock; public Vector3 WorldPos; public Vector3 WorldNormal; public Vector3 WorldTangent; }

        public uint FloorMask = 2;        // floor = collision layer 2 (bit value 2)
        public float DotRadius = 5f;
        public float PickupRange = 3.6f;
        // Remappable keys (inspector) so they never fight your real game binds.
        [Export] public Key PickupKey = Key.E;
        [Export] public Key DropKey = Key.Q;
        [Export] public Key RollKey = Key.R;
        [Export] public Key GridKey = Key.G;
        [Export] public Key DetachKey = Key.X;

        public static CarrySystem Instance { get; private set; }
        public bool IsBusy => _state != State.None;

        State _state = State.None;
        WorldObject _carried;
        float _holdDist = 2.2f;
        float _carryYaw;
        int _roll;
        int _orient;
        float _attachCd;

        WorldObject _target;
        Part _targetPart;
        SocketDef _targetSocket;
        List<(Part Part, SocketDef Sock, Vector3 WorldPos)> _candidates = new();
        int _candIdx;

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
            if (_attachCd > 0f) _attachCd -= d;
            if (_carried != null && !GodotObject.IsInstanceValid(_carried)) { _carried = null; _state = State.None; }

            switch (_state)
            {
                case State.Carrying: TickCarry(d); break;
                case State.FloorSnap: TickFloorSnap(d); break;
                case State.SocketAttach: TickAttach(d); break;
                default: TickIdle(); break;
            }

            if (_state == State.Carrying || _state == State.SocketAttach) RenderDots();
            else foreach (var dot in _dots) dot.Visible = false;
        }

        // ---------- idle ----------

        void TickIdle()
        {
            var (o, _) = RayObject(PickupRange);
            CraftingHud.SetHint?.Invoke(o != null ? "E — pick up · X — detach part · Ctrl+E — resize (v0.2b)" : "");
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
            CraftingHud.SetHint?.Invoke("E — store · Q — drop · Ctrl+Q — floor snap · scroll — hold distance · R — turn · aim at a white dot to attach");
            float yaw = P.GlobalTransform.Basis.GetEuler().Y + _carryYaw;
            var tgt = new Transform3D(new Basis(Vector3.Up, yaw), CamPos + CamDir * _holdDist + Vector3.Down * 0.35f);
            _carried.GlobalTransform = _carried.GlobalTransform.InterpolateWith(tgt, Mathf.Min(1f, 18f * d));

            if (_attachCd > 0f) return;
            var best = BestTargetSocket();
            if (best != null) EnterAttach(best.Value.Obj, best.Value.Part, best.Value.Sock, best.Value.WorldPos);
        }

        TargetHit? BestTargetSocket()
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
            return found ? best : (TargetHit?)null;
        }

        // ---------- socket attach ----------

        void EnterAttach(WorldObject t, Part tp, SocketDef ts, Vector3 tWorldPos)
        {
            _target = t; _targetPart = tp; _targetSocket = ts; _roll = 0;
            _candidates.Clear();
            foreach (var part in _carried.Data.Parts)
            {
                var pw = _carried.GlobalTransform * part.Local;
                foreach (var s in part.Shape.BuildSockets())
                    _candidates.Add((part, s, pw.Origin + pw.Basis * s.Pos));
            }
            _candidates.Sort((a, b) => a.WorldPos.DistanceSquaredTo(tWorldPos).CompareTo(b.WorldPos.DistanceSquaredTo(tWorldPos)));
            _candIdx = 0;
            _state = State.SocketAttach;
        }

        void TickAttach(float d)
        {
            if (!GodotObject.IsInstanceValid(_target) || !_target.Data.Parts.Contains(_targetPart)) { BackToCarry(); return; }
            var pw = _target.GlobalTransform * _targetPart.Local;
            Vector3 tPos = pw.Origin + pw.Basis * _targetSocket.Pos;

            Vector3 to = tPos - CamPos;
            float along = to.Dot(CamDir);
            if (along < 0.3f || (to - CamDir * along).Length() > 0.45f) { BackToCarry(); return; }

            CraftingHud.SetHint?.Invoke("scroll — which point · R — roll 90° · E — glue · Q — cancel");

            var xf = CandidateTransform(tPos, (pw.Basis * _targetSocket.Normal).Normalized(), (pw.Basis * _targetSocket.Tangent).Normalized());
            _carried.GlobalTransform = _carried.GlobalTransform.InterpolateWith(xf, Mathf.Min(1f, 22f * d));
        }

        // Full constraint solve: source socket at target socket, normals opposed,
        // tangents auto-aligned (this is what makes a correct-size tabletop land
        // square on the legs), plus manual roll in 90° steps.
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
            var (part, _, _) = _candidates[_candIdx];
            var pw = _target.GlobalTransform * _targetPart.Local;
            _carried.GlobalTransform = CandidateTransform(pw.Origin + pw.Basis * _targetSocket.Pos,
                (pw.Basis * _targetSocket.Normal).Normalized(), (pw.Basis * _targetSocket.Tangent).Normalized());

            var bond = new Bond { A = _targetPart.Id, B = part.Id };
            _carried.IsCarried = false;
            _carried.Freeze = false;
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
            CraftingHud.SetHint?.Invoke("scroll — orientation · R — roll · E — place · Q — back to carrying");
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
            _carried.Freeze = false;
            _carried.IsCarried = false;
            _carried.LinearVelocity = Vector3.Zero;
            _carried = null;
            _state = State.None;
            CraftingHud.Toast?.Invoke("Placed.");
        }

        // ---------- pickup / store / drop / detach / scoop ----------

        void TryPickup()
        {
            var (o, _) = RayObject(PickupRange);
            if (o != null)
            {
                _carried = o;
                o.IsCarried = true;
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
            if (data.Name == "Object") data.Name = AutoName(data);
            var item = new InvItem { Kind = ItemKind.Object, Data = data, Name = data.Name };
            if (!Inventory.Add(item)) { CraftingHud.Toast?.Invoke("Inventory full — Q drops it instead."); return; }
            ThumbnailGen.Render(item);
            _carried.IsCarried = false;
            _carried.QueueFree();
            _carried = null;
            _state = State.None;
        }

        void Drop()
        {
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
            if (c.Part == null) { CraftingHud.Toast?.Invoke("Carry a container in a hand (TAB → item → 4/5) to scoop liquids."); return; }
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
            Vector3 tPos = Vector3.Zero; bool hasTarget = false;
            if (_state == State.SocketAttach && GodotObject.IsInstanceValid(_target) && _targetPart != null)
            {
                var pw = _target.GlobalTransform * _targetPart.Local;
                tPos = pw.Origin + pw.Basis * _targetSocket.Pos;
                hasTarget = true;
            }
            foreach (var o in CraftingSim.Instance.Objects)
            {
                if (i >= _dots.Count - 1) break;
                if (!GodotObject.IsInstanceValid(o) || o == _carried) continue;
                if (o.GlobalPosition.DistanceSquaredTo(P.GlobalPosition) > DotRadius * DotRadius) continue;
                foreach (var part in o.Data.Parts)
                {
                    if (i >= _dots.Count - 1) break;
                    var pw = o.GlobalTransform * part.Local;
                    foreach (var s in part.Shape.BuildSockets())
                    {
                        if (i >= _dots.Count - 1) break;
                        Vector3 wpos = pw.Origin + pw.Basis * s.Pos;
                        bool hot = hasTarget && wpos.DistanceSquaredTo(tPos) < 0.0004f;
                        var dot = _dots[i++];
                        dot.Visible = true;
                        dot.Position = wpos;
                        dot.Scale = hot ? new Vector3(1.9f, 1.9f, 1.9f) : Vector3.One;
                        dot.MaterialOverride = hot ? _dotHotMat : _dotMat;
                    }
                }
            }
            for (; i < _dots.Count; i++) _dots[i].Visible = false;
        }

        // ---------- input ----------

        public override void _UnhandledInput(InputEvent e)
        {
            if (UiStack.Blocking) return;
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
                    if (_state == State.None) CraftingHud.Toast?.Invoke("In-world resize editor lands in v0.2b — spawn sizes with the catalog or 1-5 for now.");
                    return;
                }
                if (_state == State.None) TryPickup();
                else if (_state == State.Carrying) Store();
                else if (_state == State.FloorSnap) ConfirmFloor();
                else if (_state == State.SocketAttach) ConfirmAttach();
                return;
            }
            if (key == DropKey)
            {
                GetViewport().SetInputAsHandled();
                if (k.CtrlPressed) { if (_state == State.Carrying) EnterFloorSnap(); return; }
                if (_state == State.Carrying) Drop();
                else if (_state == State.FloorSnap) _state = State.Carrying;
                else if (_state == State.SocketAttach) BackToCarry();
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
            }
        }
    }
}