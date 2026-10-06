using Godot;
using System;
using System.Collections.Generic;

namespace Crafting
{
    // Verlet rope: cheap, looks good, "infinite" (auto-extends up to 250
    // segments). Ends can be held, tied to part sockets, and tightened.
    // When both ends are tied to rigid objects it also acts as a leash:
    // stretched beyond rest length it pulls the two bodies together.
    public partial class Rope : Node3D
    {
        public float Segment = 0.15f;
        public float Radius = 0.02f;
        public uint CollisionMask = 2 | 4 | 8 | 64;   // floor, buildings, doors, items
        public int MaxSegments = 250;

        public class EndAnchor
        {
            public WorldObject Obj; public Guid PartId; public Vector3 LocalPos;
            public bool Held; public Vector3 HoldPoint;
            public bool Attached => Obj != null && !Held;

            public Vector3 World(Vector3 fallback)
            {
                if (Held) return HoldPoint;
                if (Obj != null && GodotObject.IsInstanceValid(Obj))
                {
                    var part = Obj.Data.Parts.Find(p => p.Id == PartId);
                    if (part != null)
                    {
                        var xf = Obj.GlobalTransform * part.Local;
                        return xf.Origin + xf.Basis * LocalPos;
                    }
                }
                return fallback;
            }
        }

        public readonly EndAnchor[] Ends = { new EndAnchor(), new EndAnchor() };

        readonly List<Vector3> _pos = new();
        bool _inited;
        readonly List<Vector3> _prev = new();
        readonly List<MeshInstance3D> _segs = new();
        CylinderMesh _unit;
        StandardMaterial3D _mat;

        public float Length => (_pos.Count - 1) * Segment;

        public static Rope Spawn(Vector3 pos)
        {
            var r = new Rope();
            CraftingSim.Instance.WorldRoot.AddChild(r);   // _Ready fires here
            r.GlobalPosition = pos;                        // position becomes real
            r.ResetPoints(pos + Vector3.Up * 0.6f, pos);   // build chain AFTER
            r._inited = true;
            CraftingSim.Instance.Ropes.Add(r);
            return r;
        }

        public override void _ExitTree() { if (CraftingSim.Instance != null) CraftingSim.Instance.Ropes.Remove(this); }

        public override void _Ready()
        {
            _mat = new StandardMaterial3D { AlbedoColor = MaterialLibrary.Get("rope").Color, Roughness = 1f };
            _unit = new CylinderMesh { TopRadius = Radius, BottomRadius = Radius, Height = 1f, RadialSegments = 6 };
            // NOTE: no ResetPoints here — _Ready runs inside AddChild, before
            // Spawn has assigned GlobalPosition. InitPoints is called after.
        }

        public void ResetPoints(Vector3 a, Vector3 b)
        {
            _pos.Clear(); _prev.Clear();
            int n = Mathf.Max(3, (int)(a.DistanceTo(b) / Segment) + 1);
            for (int i = 0; i < n; i++)
            {
                var p = a.Lerp(b, i / (float)(n - 1));
                _pos.Add(p); _prev.Add(p);
            }
            RebuildSegs();
        }

        void RebuildSegs()
        {
            while (_segs.Count < _pos.Count - 1)
            {
                var mi = new MeshInstance3D { Mesh = _unit, MaterialOverride = _mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(mi);
                _segs.Add(mi);
            }
            while (_segs.Count > _pos.Count - 1) { _segs[^1].QueueFree(); _segs.RemoveAt(_segs.Count - 1); }
        }

        public void Grab(int i)
        {
            Ends[i].HoldPoint = EndPosition(i);   // anchor the hold where the end actually is
            Ends[i].Held = true;
            Ends[i].Obj = null;
        }
        public void Release(int i) => Ends[i].Held = false;
        public void SetHold(int i, Vector3 wp) => Ends[i].HoldPoint = wp;
        public Vector3 EndPosition(int i) => i == 0 ? _pos[0] : _pos[^1];

        public void Attach(int i, WorldObject o, Part p, SocketDef s)
        {
            Ends[i].Obj = o; Ends[i].PartId = p.Id; Ends[i].LocalPos = s.Pos; Ends[i].Held = false;
        }

        public void AdjustLength(float meters)
        {
            int target = Mathf.Clamp(_pos.Count + (int)(meters / Segment), 3, MaxSegments);
            while (_pos.Count < target) { _pos.Add(_pos[^1]); _prev.Add(_pos[^1]); }
            while (_pos.Count > target) { _pos.RemoveAt(_pos.Count - 1); _prev.RemoveAt(_prev.Count - 1); }
            RebuildSegs();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_inited) return;
            float dt = (float)delta;

            // auto-extend while anchors stretch it
            var a = Ends[0].World(_pos[0]);
            var b = Ends[1].World(_pos[^1]);
            float need = a.DistanceTo(b) * 1.1f + Segment;
            if (need > Length && _pos.Count < MaxSegments) AdjustLength(need - Length);

            bool pinA = Ends[0].Held || Ends[0].Attached;
            bool pinB = Ends[1].Held || Ends[1].Attached;

            // verlet integration
            for (int i = 0; i < _pos.Count; i++)
            {
                if ((i == 0 && pinA) || (i == _pos.Count - 1 && pinB)) continue;
                var cur = _pos[i];
                var vel = (cur - _prev[i]) * 0.985f;
                _prev[i] = cur;
                _pos[i] = cur + vel + Vector3.Down * 9.8f * dt * dt;
            }

            // distance constraints
            for (int it = 0; it < 3; it++)
                for (int i = 0; i < _pos.Count - 1; i++)
                {
                    var d = _pos[i + 1] - _pos[i];
                    float len = d.Length();
                    if (len < 0.0001f) continue;
                    var dir = d / len;
                    float err = len - Segment;
                    float w0 = (i == 0 && pinA) ? 0f : 0.5f;
                    float w1 = (i == _pos.Count - 1 && pinB) ? 0f : 0.5f;
                    _pos[i] += dir * err * w0;
                    _pos[i + 1] -= dir * err * w1;
                }

            // collision: push points out of statics + items
            var space = GetWorld3D().DirectSpaceState;
            for (int i = 1; i < _pos.Count - 1; i++)
            {
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(_prev[i], _pos[i], CollisionMask));
                if (hit.Count > 0)
                    _pos[i] = hit["position"].AsVector3() + hit["normal"].AsVector3() * Radius;
            }

            // pin ends
            _pos[0] = Ends[0].World(_pos[0]);
            _pos[^1] = Ends[1].World(_pos[^1]);

            // leash: both ends tied to rigid bodies
            if (Ends[0].Attached && Ends[1].Attached
                && GodotObject.IsInstanceValid(Ends[0].Obj) && GodotObject.IsInstanceValid(Ends[1].Obj))
            {
                float d = _pos[0].DistanceTo(_pos[^1]);
                if (d > Length)
                {
                    var dir = (_pos[^1] - _pos[0]).Normalized();
                    float excess = Mathf.Min(d - Length, 1.5f);
                    Ends[0].Obj.ApplyCentralForce(dir * excess * 60f);
                    Ends[1].Obj.ApplyCentralForce(-dir * excess * 60f);
                }
            }

            Render();
        }

        void Render()
        {
            for (int i = 0; i < _segs.Count; i++)
            {
                var a = _pos[i]; var b = _pos[i + 1];
                var y = b - a;
                float len = y.Length();
                if (len < 0.0005f) { _segs[i].Visible = false; continue; }
                y /= len;
                Vector3 x = Mathf.Abs(y.Dot(Vector3.Up)) < 0.9f ? y.Cross(Vector3.Up) : y.Cross(Vector3.Right);
                x = x.Normalized();
                var z = x.Cross(y);
                _segs[i].Visible = true;
                _segs[i].GlobalTransform = new Transform3D(new Basis(x, y * len, z), (a + b) * 0.5f);
            }
        }
    }
}