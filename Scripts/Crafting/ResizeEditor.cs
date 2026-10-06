using Godot;
using System.Collections.Generic;

namespace Crafting
{
    // Ctrl+E while aiming at an object: white outline, white arrows on the
    // faces the shape is allowed to grow along (one-sided), center dot for
    // the authored "core" scale (thickness/scale/diameter). Grab with LMB,
    // drag (snaps to 5 cm), release. E/Q applies, Esc reverts.
    public partial class ResizeEditor : Node3D
    {
        public static ResizeEditor Instance { get; private set; }
        public static bool DragActive { get; private set; }

        public class ResizeAxis
        {
            public Vector3 Dir; public bool Symmetric; public string Label; public bool Center;
            public ResizeAxis(Vector3 dir, bool sym, string label, bool center = false)
            { Dir = dir; Symmetric = sym; Label = label; Center = center; }
        }

        class Handle
        {
            public Part Part; public ResizeAxis Def; public Basis LocalBasis;
            public Node3D Root; public List<MeshInstance3D> Meshes = new();
        }

        WorldObject _obj;
        bool _wasFrozen;
        readonly List<Handle> _handles = new();
        readonly List<(Part Part, ShapeDef Shape, Transform3D Local)> _backup = new();
        readonly List<MeshInstance3D> _outline = new();
        Handle _hot, _grab;
        float _dragMeters, _applied;
        Material _mat, _hotMat;
        ShaderMaterial _outlineMat;

        ICraftingPlayer P => CraftingSim.Instance?.Player;
        public bool Active => _obj != null;

        const string OutlineCode = @"
shader_type spatial;
render_mode cull_front, unshaded;
uniform float width = 0.012;
void vertex() { VERTEX += NORMAL * width; }
void fragment() { ALBEDO = vec3(0.95); }
";

        public override void _Ready()
        {
            Instance = this;
            _mat = new StandardMaterial3D { AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = new Color(0.9f, 0.95f, 1f), EmissionEnergyMultiplier = 1.2f };
            _hotMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.6f, 0.2f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = new Color(1f, 0.55f, 0.15f), EmissionEnergyMultiplier = 2f };
            _outlineMat = new ShaderMaterial { Shader = new Shader { Code = OutlineCode } };
        }

        public static List<ResizeAxis> AxesFor(ShapeKind k)
        {
            switch (k)
            {
                case ShapeKind.Box or ShapeKind.Plate:
                    return new()
                    {
                        new ResizeAxis(Vector3.Back, false, "length"),
                        new ResizeAxis(Vector3.Forward, false, "length"),
                        new ResizeAxis(Vector3.Right, false, "width"),
                        new ResizeAxis(Vector3.Left, false, "width"),
                        new ResizeAxis(Vector3.Up, true, "thickness", center: true),
                    };
                case ShapeKind.Rod:
                    return new()
                    {
                        new ResizeAxis(Vector3.Up, false, "length"),
                        new ResizeAxis(Vector3.Down, false, "length"),
                        new ResizeAxis(Vector3.Up, true, "thickness", center: true),
                    };
                case ShapeKind.Sphere:
                    return new() { new ResizeAxis(Vector3.Up, true, "scale", center: true) };
                case ShapeKind.Wheel:
                    return new() { new ResizeAxis(Vector3.Up, true, "diameter", center: true) };
                case ShapeKind.OpenBox:
                    return new()
                    {
                        new ResizeAxis(Vector3.Up, false, "height"),
                        new ResizeAxis(Vector3.Right, true, "width"),
                        new ResizeAxis(Vector3.Left, true, "width"),
                        new ResizeAxis(Vector3.Back, true, "depth"),
                        new ResizeAxis(Vector3.Forward, true, "depth"),
                    };
                default: return new();
            }
        }

        public void Begin(WorldObject obj)
        {
            if (!GodotObject.IsInstanceValid(obj) || obj.IsCarried || Active) return;
            _obj = obj;
            _wasFrozen = obj.Freeze;
            obj.Freeze = true;
            obj.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
            obj.LinearVelocity = Vector3.Zero;
            obj.AngularVelocity = Vector3.Zero;
            _backup.Clear();
            foreach (var p in obj.Data.Parts) _backup.Add((p, p.Shape.Clone(), p.Local));
            BuildHandles();
            BuildOutline();
            UpdateHint();
            CraftingHud.InputLocked = true;
        }

        public void End(bool apply)
        {
            if (_obj == null) return;
            if (!apply)
                foreach (var (p, shape, local) in _backup) { p.Shape = shape; p.Local = local; }
            ClearHandles();
            ClearOutline();
            var obj = _obj;
            _obj = null; _grab = null; _hot = null; DragActive = false;
            obj.RebuildBody();
            obj.Freeze = obj.Data.Anchored || _wasFrozen;
            if (obj.Freeze) obj.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
            CraftingHud.InputLocked = false;
            CraftingHud.LookLocked = false;
            CraftingHud.SetHint?.Invoke("");
        }

        // ---------- handles ----------

        void BuildHandles()
        {
            ClearHandles();
            IEnumerable<Part> parts = _obj.Data.Parts;
            if (_obj.Data.Parts.Count > 8)
            {
                // big assemblies: only the part nearest the aim ray gets handles
                var cam = P?.Cam;
                Part nearest = null; float bd = 1.5f;
                if (cam != null)
                {
                    Vector3 o = cam.GlobalPosition; Vector3 dir = -cam.GlobalTransform.Basis.Z;
                    foreach (var p in _obj.Data.Parts)
                    {
                        Vector3 to = (_obj.GlobalTransform * p.Local).Origin - o;
                        float along = to.Dot(dir);
                        if (along < 0.2f) continue;
                        float rd = (to - dir * along).Length();
                        if (rd < bd) { bd = rd; nearest = p; }
                    }
                }
                parts = nearest != null ? new[] { nearest } : System.Array.Empty<Part>();
                CraftingHud.Toast?.Invoke("Large object — editing the aimed part only.");
            }
            foreach (var part in parts)
                foreach (var def in AxesFor(part.Shape.Kind))
                {
                    var h = new Handle { Part = part, Def = def, LocalBasis = def.Center ? Basis.Identity : BasisFromY(def.Dir) };
                    var root = new Node3D();
                    AddChild(root);
                    h.Root = root;
                    if (def.Center)
                    {
                        var dot = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 10, Rings = 6 } };
                        root.AddChild(dot); h.Meshes.Add(dot);
                    }
                    else
                    {
                        var shaft = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = 0.14f, RadialSegments = 8 } };
                        shaft.Position = new Vector3(0, 0.09f, 0);
                        var tip = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.002f, BottomRadius = 0.038f, Height = 0.08f, RadialSegments = 8 } };
                        tip.Position = new Vector3(0, 0.19f, 0);
                        root.AddChild(shaft); root.AddChild(tip);
                        h.Meshes.Add(shaft); h.Meshes.Add(tip);
                    }
                    foreach (var m in h.Meshes) { m.MaterialOverride = _mat; m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
                    _handles.Add(h);
                }
            RefreshHandles();
        }

        void ClearHandles()
        {
            foreach (var h in _handles) if (GodotObject.IsInstanceValid(h.Root)) h.Root.QueueFree();
            _handles.Clear(); _hot = null; _grab = null;
        }

        static Vector3 HandleLocalPos(Part part, ResizeAxis def)
        {
            if (def.Center) return Vector3.Zero;
            var half = part.Shape.Size * 0.5f;
            return new Vector3(def.Dir.X * half.X, def.Dir.Y * half.Y, def.Dir.Z * half.Z);
        }

        void RefreshHandles()
        {
            if (_obj == null) return;
            foreach (var h in _handles)
            {
                var worldXf = _obj.GlobalTransform * h.Part.Local;
                h.Root.GlobalTransform = worldXf * new Transform3D(h.LocalBasis, HandleLocalPos(h.Part, h.Def));
            }
        }

        static Basis BasisFromY(Vector3 y)
        {
            y = y.Normalized();
            Vector3 x = Mathf.Abs(y.Dot(Vector3.Up)) < 0.9f ? y.Cross(Vector3.Up) : y.Cross(Vector3.Right);
            x = x.Normalized();
            return new Basis(x, y, x.Cross(y));
        }

        // ---------- outline ----------

        void BuildOutline()
        {
            ClearOutline();
            if (_obj == null) return;
            foreach (var c in _obj.GetChildren())
                if (c is MeshInstance3D mi && mi.Mesh != null && mi.Visible)
                {
                    var o = new MeshInstance3D { Mesh = mi.Mesh, MaterialOverride = _outlineMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                    _obj.AddChild(o);
                    o.Transform = mi.Transform;
                    _outline.Add(o);
                }
        }

        void ClearOutline()
        {
            foreach (var o in _outline) if (GodotObject.IsInstanceValid(o)) o.QueueFree();
            _outline.Clear();
        }

        // ---------- drag ----------

        void StartGrab(Handle h)
        {
            _grab = h; _dragMeters = 0f; _applied = 0f;
            DragActive = true;
            RefreshHotVisuals();
            UpdateHint();
            CraftingHud.LookLocked = true;
        }

        void EndGrab()
        {
            _grab = null;
            DragActive = false;
            CraftingHud.LookLocked = false;
            UpdateHint();
        }

        void OnDragMotion(Vector2 relative)
        {
            if (_grab == null) return;
            var cam = P?.Cam;
            if (cam == null || _obj == null) return;
            var part = _grab.Part;
            var worldXf = _obj.GlobalTransform * part.Local;
            Vector3 anchor = worldXf.Origin + worldXf.Basis * HandleLocalPos(part, _grab.Def);
            Vector3 worldDir = (worldXf.Basis * _grab.Def.Dir).Normalized();

            var p1 = cam.UnprojectPosition(anchor);
            var p2 = cam.UnprojectPosition(anchor + worldDir * 0.5f);
            var sdir = p2 - p1;
            if (sdir.LengthSquared() < 1f) return;     // axis points at the camera
            sdir = sdir.Normalized();
            float metersPerPx = 2f * cam.GlobalPosition.DistanceTo(anchor)
                * Mathf.Tan(Mathf.DegToRad(cam.Fov * 0.5f))
                / (float)cam.GetViewport().GetVisibleRect().Size.Y;

            _dragMeters += relative.Dot(sdir) * metersPerPx;
            float snapped = Mathf.Round(_dragMeters / 0.05f) * 0.05f;
            if (Mathf.IsEqualApprox(snapped, _applied)) return;
            ApplyDelta(_grab, snapped - _applied);
            _applied = snapped;
        }

        void ApplyDelta(Handle h, float d)
        {
            var part = h.Part; var def = h.Def; var shape = part.Shape;
            float ax = Mathf.Abs(def.Dir.X), ay = Mathf.Abs(def.Dir.Y), az = Mathf.Abs(def.Dir.Z);
            int comp = (ax >= ay && ax >= az) ? 0 : (ay >= az ? 1 : 2);
            float grow = def.Symmetric ? d * 2f : d;

            void Set(int c, float v)
            {
                v = ClampSize(shape.Kind, c, v);
                var s = shape.Size;
                if (c == 0) s.X = v; else if (c == 1) s.Y = v; else s.Z = v;
                shape.Size = s;
            }
            float Get(int c) { var s = shape.Size; return c == 0 ? s.X : c == 1 ? s.Y : s.Z; }

            if (shape.Kind == ShapeKind.Sphere && def.Label == "scale")
            { Set(0, Get(0) + grow); Set(1, Get(0)); Set(2, Get(0)); }
            else if (shape.Kind == ShapeKind.Rod && def.Label == "thickness")
            { Set(0, Get(0) + grow); Set(2, Get(0)); }
            else if (shape.Kind == ShapeKind.Wheel && def.Label == "diameter")
            { Set(1, Get(1) + grow); Set(2, Get(1)); }
            else
                Set(comp, Get(comp) + grow);

            // one-sided: keep the opposite face put
            if (!def.Symmetric)
                part.Local = new Transform3D(part.Local.Basis, part.Local.Origin + part.Local.Basis * (def.Dir * (d * 0.5f)));

            _obj.RebuildBody();
            RefreshHandles();
            ClearOutline();
            BuildOutline();
            UpdateHint();
        }

        static float ClampSize(ShapeKind k, int comp, float v)
        {
            float mn = 0.02f, mx = 6f;
            if (k == ShapeKind.Sphere) { mn = 0.05f; mx = 3f; }
            if (k == ShapeKind.OpenBox) { mn = 0.10f; mx = 1.6f; }
            if (k == ShapeKind.Wheel) { mn = 0.05f; mx = 3f; }
            if (k == ShapeKind.Rod && comp != 1) { mn = 0.01f; mx = 1f; }
            return Mathf.Clamp(v, mn, mx);
        }

        // ---------- frame & input ----------

        public override void _Process(double delta)
        {
            if (_obj == null) return;
            if (!GodotObject.IsInstanceValid(_obj)) { _obj = null; ClearHandles(); ClearOutline(); DragActive = false; return; }
            RefreshHandles();
            UpdateHot();
        }

        void UpdateHot()
        {
            if (_grab != null) return;
            var cam = P?.Cam;
            if (cam == null) { _hot = null; return; }
            Vector3 origin = cam.GlobalPosition; Vector3 dir = -cam.GlobalTransform.Basis.Z;
            Handle best = null; float bd = 0.15f;
            foreach (var h in _handles)
            {
                Vector3 to = h.Root.GlobalPosition - origin;
                float along = to.Dot(dir);
                if (along < 0.2f) continue;
                float rd = (to - dir * along).Length();
                if (rd < bd) { bd = rd; best = h; }
            }
            if (best != _hot) { _hot = best; RefreshHotVisuals(); UpdateHint(); }
        }

        void RefreshHotVisuals()
        {
            foreach (var h in _handles)
            {
                bool hot = h == _hot || h == _grab;
                h.Root.Scale = hot ? new Vector3(1.25f, 1.25f, 1.25f) : Vector3.One;
                foreach (var m in h.Meshes) m.MaterialOverride = hot ? _hotMat : _mat;
            }
        }

        void UpdateHint()
        {
            if (_grab != null)
                CraftingHud.SetHint?.Invoke($"{_grab.Def.Label.ToUpper()} — {_grab.Part.Shape.Describe()} — release to set · E/Q done · Esc revert");
            else
                CraftingHud.SetHint?.Invoke("RESIZE — aim an arrow, hold LMB and drag · E/Q done · Esc revert");
        }

        public override void _Input(InputEvent e)
        {
            if (!Active) return;
            if (e is InputEventMouseMotion mm)
            {
                if (_grab != null) { GetViewport().SetInputAsHandled(); OnDragMotion(mm.Relative); }
                return;
            }
            if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                GetViewport().SetInputAsHandled();
                if (mb.Pressed) { if (_hot != null) StartGrab(_hot); }
                else EndGrab();
                return;
            }
            if (e is InputEventKey k && k.Pressed && !k.Echo)
            {
                var key = k.PhysicalKeycode;
                if (key == Key.Escape) { GetViewport().SetInputAsHandled(); End(false); }
                else if (key == Key.E || key == Key.Q) { GetViewport().SetInputAsHandled(); End(true); }
            }
        }
    }
}