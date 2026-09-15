using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class CrystalLab : CanvasLayer
{
    private enum Tool { Select, Dope, Add, Remove }

    private class AtomVis
    {
        public MeshInstance3D Mesh;
        public int Site, Seed;
        public Vector3 Cell;      // integer repeat offset
        public Vector3 BasePos;   // un-jittered cart (picking)
        public Vector3 Wander;    // liquid drift
    }
    private class BondVis
    {
        public MeshInstance3D Mesh;
        public int I, J;
        public Vector3 Cell, Off;
    }

    private CrystalStructure _s;
    private MaterialStats _stats;
    private string _componentName = "";
    private System.Action _onClosed;

    private SubViewportContainer _vpContainer;
    private SubViewport _vp;
    private Camera3D _cam;
    private Node3D _atomRoot, _bondRoot, _cellRoot;
    private Control _root;

    private readonly List<AtomVis> _atoms = new();
    private readonly List<BondVis> _bonds = new();
    private List<Bond> _topology = new();
    private readonly Dictionary<int, Vector3> _wanderVel = new();
    private readonly System.Random _rng = new();

    private float _yaw = 0.8f, _pitch = 0.42f, _dist = 10f;
    private Vector3 _focus;
    private Basis _thermCell = Basis.Identity;
    private Vector3 _wanderLimit = Vector3.One;

    private float _tKelvin = 298f, _prevT = 298f, _meltBlend, _exaggerate = 20f, _readoutTimer, _fadeIn;

    private Tool _tool = Tool.Select;
    private string _pendingElement = "Si";
    private int _selectedAtom = -1;
    private int _dragSite = -1;
    private bool _dragging, _orbiting, _clickMoved;
    private Vector3 _dragPlaneN;
    private float _dragPlaneD;

    private Label _nameLabel, _formulaL, _densityL, _tmL, _condL, _hardL, _sgL, _cnL, _tempL, _hintL;
    private HSlider _tempSlider, _repeatSlider;
    private CheckButton _bondsCheck;
    private Button[] _toolButtons;
    private int _repeat = 1;

    private readonly SphereMesh _sphere = new() { Radius = 1f, Height = 2f, RadialSegments = 18, Rings = 12 };
    private readonly CylinderMesh _cyl = new() { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 1f, RadialSegments = 7 };
    private readonly Dictionary<string, StandardMaterial3D> _mats = new();
    private StandardMaterial3D _selMat, _bondMat, _cellMat;

    public void Open(CrystalStructure structure, string componentName, System.Action onClosed)
    {
        _componentName = componentName;
        _onClosed = onClosed;
        PlayerInputOverride.Set(true);
        BuildUi();
        LoadStructure(structure);
        UpdateToolButtons();
        UpdateHint();
    }

    // ================= UI construction =================

    private void BuildUi()
    {
        Layer = 60;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var bg = new ColorRect { Color = new Color(0.03f, 0.05f, 0.09f, 0.98f) };
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(bg);

        var header = new HBoxContainer();
        header.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        header.OffsetLeft = 16; header.OffsetTop = 8;
        _root.AddChild(header);

        _nameLabel = MakeLabel("—", 20, new Color(0.9f, 0.95f, 1f));
        header.AddChild(_nameLabel);
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var applyBtn = MakeButton("SAVE & SPAWN"); applyBtn.Pressed += OnApply;
        var closeBtn = MakeButton("CLOSE (Esc)"); closeBtn.Pressed += () => Close(false);
        header.AddChild(applyBtn); header.AddChild(closeBtn);

        var row = new HBoxContainer();
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        row.OffsetLeft = 16; row.OffsetRight = -16; row.OffsetTop = 52; row.OffsetBottom = -56;
        _root.AddChild(row);

        // left panel
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
        row.AddChild(left);
        left.AddChild(MakeLabel("TOOLS", 13, new Color(0.5f, 0.7f, 1f)));
        _toolButtons = new Button[4];
        string[] toolNames = { "1 · MOVE / INSPECT", "2 · DOPE", "3 · ADD ATOM", "4 · REMOVE" };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var b = MakeButton(toolNames[i]); b.ToggleMode = true;
            b.Pressed += () => { _tool = (Tool)idx; UpdateToolButtons(); UpdateHint(); };
            left.AddChild(b);
            _toolButtons[i] = b;
        }

        left.AddChild(MakeLabel("ELEMENT", 13, new Color(0.5f, 0.7f, 1f)));
        var grid = new GridContainer { Columns = 8 };
        left.AddChild(grid);
        foreach (var e in Elements.Palette)
        {
            var b = MakeButton(e.Symbol);
            b.CustomMinimumSize = new Vector2(36, 28);
            b.TooltipText = e.Name;
            b.AddThemeColorOverride("font_color", e.Color);
            b.AddThemeColorOverride("font_hover_color", e.Color);
            string sym = e.Symbol;
            b.Pressed += () =>
            {
                _pendingElement = sym;
                UpdatePendingHighlight();
                if (_selectedAtom >= 0 && _selectedAtom < _atoms.Count)
                    Dope(_atoms[_selectedAtom].Site, sym);
            };
            grid.AddChild(b);
        }

        left.AddChild(MakeLabel("TEMPERATURE", 13, new Color(0.5f, 0.7f, 1f)));
        _tempSlider = new HSlider { MinValue = 0, MaxValue = 3200, Step = 1, Value = 298,
            CustomMinimumSize = new Vector2(200, 20) };
        _tempSlider.ValueChanged += v => _tKelvin = (float)v;
        left.AddChild(_tempSlider);
        _tempL = MakeLabel("298 K — Solid");
        left.AddChild(_tempL);

        left.AddChild(MakeLabel("CELL REPEAT", 13, new Color(0.5f, 0.7f, 1f)));
        _repeatSlider = new HSlider { MinValue = 1, MaxValue = 3, Step = 1, Value = 1,
            CustomMinimumSize = new Vector2(200, 20) };
        _repeatSlider.ValueChanged += v =>
        {
            _repeat = (int)v;
            RebuildAtoms(); RebuildBonds(); RebuildCellFrame(); Select(-1); ResetCamera();
        };
        left.AddChild(_repeatSlider);

        _bondsCheck = new CheckButton { Text = "Bonds", ButtonPressed = true };
        left.AddChild(_bondsCheck);
        var exag = new CheckButton { Text = "Thermal ×20 (visual only)", ButtonPressed = true };
        exag.Toggled += on => _exaggerate = on ? 20f : 1f;
        left.AddChild(exag);

        // viewport
        _vpContainer = new SubViewportContainer
        {
            Stretch = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(_vpContainer);
        _vp = new SubViewport { RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _vp.OwnWorld3D = true;   // SubViewport uses its own isolated World3D
        _vpContainer.AddChild(_vp);

        var we = new WorldEnvironment();
        var env = new Environment
        {
            BackgroundMode = Environment.BGMode.Color,
            BackgroundColor = new Color(0.02f, 0.04f, 0.09f),
            AmbientLightSource = Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.5f, 0.6f, 0.8f),
            AmbientLightEnergy = 0.8f,
        };
        we.Environment = env;
        _vp.AddChild(we);

        var light = new DirectionalLight3D { LightEnergy = 1.2f, ShadowEnabled = true };
        light.LookAt(new Vector3(0.5f, -1f, 0.35f), Vector3.Up);
        _vp.AddChild(light);

        _cam = new Camera3D { Current = true, Fov = 50 };
        _vp.AddChild(_cam);
        _atomRoot = new Node3D(); _vp.AddChild(_atomRoot);
        _bondRoot = new Node3D(); _vp.AddChild(_bondRoot);
        _cellRoot = new Node3D(); _vp.AddChild(_cellRoot);

        // right panel
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(270, 0) };
        row.AddChild(right);
        right.AddChild(MakeLabel("PROPERTIES", 13, new Color(0.5f, 0.7f, 1f)));
        _formulaL = MakeLabel(""); _densityL = MakeLabel(""); _tmL = MakeLabel("");
        _condL = MakeLabel(""); _hardL = MakeLabel(""); _sgL = MakeLabel(""); _cnL = MakeLabel("CN: —");
        foreach (var l in new[] { _formulaL, _densityL, _tmL, _condL, _hardL, _sgL, _cnL }) right.AddChild(l);

        _hintL = MakeLabel("", 13, new Color(0.55f, 0.65f, 0.85f));
        _hintL.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _hintL.OffsetTop = -40; _hintL.OffsetLeft = 20;
        _root.AddChild(_hintL);

        _selMat = new StandardMaterial3D
        {
            AlbedoColor = Colors.White, Roughness = 0.3f,
            EmissionEnabled = true, Emission = new Color(0.6f, 0.8f, 1f), EmissionEnergyMultiplier = 1.2f,
        };
        _bondMat = new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.82f, 0.95f, 0.9f), Roughness = 0.5f };
        _cellMat = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.55f, 0.9f), Roughness = 0.6f };
    }

    private static Label MakeLabel(string txt, int size = 14, Color? c = null)
    {
        var l = new Label { Text = txt };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c ?? new Color(0.72f, 0.82f, 1f));
        return l;
    }
    private static Button MakeButton(string txt) =>
        new() { Text = txt, FocusMode = Control.FocusModeEnum.None };

    // ================= structure lifecycle =================

    private void LoadStructure(CrystalStructure s)
    {
        _s = s;
        _s.RebuildLattice();
        _meltBlend = 0; _wanderVel.Clear();
        _prevT = _tKelvin;
        _wanderLimit = new Vector3(_s.A, _s.B, _s.C);
        RebuildAtoms(); RebuildBonds(); RebuildCellFrame();
        RefreshReadout();
        ResetCamera();
    }

    private void ResetCamera()
    {
        _focus = _s.FracToCart(new Vector3(_repeat * 0.5f, _repeat * 0.5f, _repeat * 0.5f));
        float span = Mathf.Max(Mathf.Max(_s.A, _s.B), _s.C) * _repeat;
        _dist = Mathf.Clamp(span * 1.9f, 4f, 60f);
        _yaw = 0.8f; _pitch = 0.42f;
    }

    private void RebuildAtoms()
    {
        foreach (var a in _atoms) a.Mesh.QueueFree();
        _atoms.Clear();
        for (int i = 0; i < _s.Sites.Count; i++)
            for (int x = 0; x < _repeat; x++)
            for (int y = 0; y < _repeat; y++)
            for (int z = 0; z < _repeat; z++)
            {
                var cell = new Vector3(x, y, z);
                var m = new MeshInstance3D { Mesh = _sphere };
                m.MaterialOverride = MatFor(_s.Sites[i].Element);
                m.Scale = Vector3.One * Elements.Get(_s.Sites[i].Element).Radius;
                _atomRoot.AddChild(m);
                _atoms.Add(new AtomVis
                {
                    Mesh = m, Site = i, Cell = cell, Seed = SeedFor(i, cell),
                });
            }
        _selectedAtom = -1;
    }

    private void RebuildBonds()
    {
        _topology = _s.ComputeBonds();
        foreach (var b in _bonds) b.Mesh.QueueFree();
        _bonds.Clear();
        foreach (var t in _topology)
            for (int x = 0; x < _repeat; x++)
            for (int y = 0; y < _repeat; y++)
            for (int z = 0; z < _repeat; z++)
            {
                var m = new MeshInstance3D { Mesh = _cyl, MaterialOverride = _bondMat };
                _bondRoot.AddChild(m);
                _bonds.Add(new BondVis { Mesh = m, I = t.I, J = t.J, Cell = new Vector3(x, y, z), Off = t.Offset });
            }
    }

    private void RebuildCellFrame()
    {
        foreach (Node3D c in _cellRoot.GetChildren()) c.QueueFree();
        for (int i = 0; i <= _repeat; i++)
        for (int j = 0; j <= _repeat; j++)
        for (int k = 0; k <= _repeat; k++)
        {
            var p = _s.FracToCart(new Vector3(i, j, k));
            if (i < _repeat) AddEdge(p, _s.FracToCart(new Vector3(i + 1, j, k)));
            if (j < _repeat) AddEdge(p, _s.FracToCart(new Vector3(i, j + 1, k)));
            if (k < _repeat) AddEdge(p, _s.FracToCart(new Vector3(i, j, k + 1)));
        }
    }
    private void AddEdge(Vector3 a, Vector3 b)
    {
        var m = new MeshInstance3D { Mesh = _cyl, MaterialOverride = _cellMat };
        _cellRoot.AddChild(m);
        OrientCylinder(m, a, b);
    }

    private static void OrientCylinder(MeshInstance3D m, Vector3 a, Vector3 b)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 1e-4f) { m.Visible = false; return; }
        var dn = d / len;
        var up = Mathf.Abs(dn.Dot(Vector3.Up)) > 0.98f ? Vector3.Right : Vector3.Up;
        var xAxis = up.Cross(dn).Normalized();
        var zAxis = dn.Cross(xAxis);
        m.Transform = new Transform3D(new Basis(xAxis, dn * len, zAxis), (a + b) * 0.5f);
    }

    private StandardMaterial3D MatFor(string el)
    {
        if (_mats.TryGetValue(el, out var m)) return m;
        var e = Elements.Get(el);
        m = new StandardMaterial3D { AlbedoColor = e.Color, Roughness = 0.35f, Metallic = e.IsMetal ? 0.65f : 0f };
        _mats[el] = m;
        return m;
    }

    private static int SeedFor(int site, Vector3 cell) =>
        site * 7 + (int)(cell.X * 49 + cell.Y * 7 + cell.Z);

    private static Vector3 Jitter(int seed, float t) => new(
        Mathf.Sin(t * (3.1f + seed * 1.37f) + seed),
        Mathf.Sin(t * (2.6f + seed * 2.11f) + seed * 0.7f),
        Mathf.Sin(t * (3.9f + seed * 0.93f) + seed * 1.9f));

    // ================= per-frame =================

    public override void _Process(double delta)
    {
        if (_s == null) return;
        float dt = (float)delta;

        if (_fadeIn < 1f)
        {
            _fadeIn = Mathf.Min(_fadeIn + dt / 0.35f, 1f);
            _root.Modulate = new Color(1, 1, 1, _fadeIn);
        }
        var vpSize = new Vector2I((int)_vpContainer.Size.X, (int)_vpContainer.Size.Y);
        if (_vp.Size != vpSize) _vp.Size = vpSize;

        // camera
        var off = new Vector3(Mathf.Cos(_pitch) * Mathf.Cos(_yaw), Mathf.Sin(_pitch),
                              Mathf.Cos(_pitch) * Mathf.Sin(_yaw)) * _dist;
        _cam.GlobalPosition = _focus + off;
        _cam.LookAt(_focus, Vector3.Up);

        // melting / freezing
        float tm = _stats?.MeltingK ?? 1500;
        _meltBlend = Mathf.MoveToward(_meltBlend, _tKelvin >= tm ? 1f : 0f, dt * 0.8f);

        // phase transitions (unedited structures only)
        if (!_s.Edited && _tKelvin < tm)
        {
            var trs = StructureLibrary.MetaFor.GetValueOrDefault(_s.Id)?.Transitions;
            if (trs != null)
                foreach (var (tK, id, dir) in trs)
                {
                    bool cross = dir == Trig.Heat ? (_prevT < tK && _tKelvin >= tK)
                                                 : (_prevT > tK && _tKelvin <= tK);
                    if (cross && StructureLibrary.Prototypes.ContainsKey(id))
                    {
                        _prevT = _tKelvin;
                        LoadStructure(StructureLibrary.Prototypes[id].Clone());
                        _tempL.Text = $"{_tKelvin:F0} K — PHASE TRANSITION";
                        return;
                    }
                }
        }
        _prevT = _tKelvin;

        // thermal expansion
        var meta = StructureLibrary.MetaFor.GetValueOrDefault(_s.PrototypeId);
        float alpha = _exaggerate * (meta?.ThermalAlpha ?? 1.5e-5f);
        float therm = Mathf.Max(1f + alpha * (_tKelvin - 298f), 0.55f);
        var cb = _s.CellBasis;
        _thermCell = new Basis(cb.X * therm, cb.Y * therm, cb.Z * therm);
        _cellRoot.Scale = Vector3.One * therm;

        float time = (float)Time.GetTicksMsec() / 1000f;
        float amp = Mathf.Min(0.09f * Mathf.Sqrt(Mathf.Max(_tKelvin, 1f) / 298f), 0.5f) * (1f - _meltBlend);
        int cellCount = _repeat * _repeat * _repeat;

        // atoms
        for (int i = 0; i < _atoms.Count; i++)
        {
            var a = _atoms[i];
            var site = _s.Sites[a.Site];
            a.BasePos = _thermCell * (site.Frac + a.Cell);
            if (_meltBlend > 0.01f)
            {
                if (!_wanderVel.TryGetValue(i, out var v)) { v = RandDir() * 0.5f; _wanderVel[i] = v; }
                v += RandDir() * dt * 3f;
                v = v.LimitLength(1.2f);
                _wanderVel[i] = v;
                a.Wander += v * dt * _meltBlend;
                a.Wander = new Vector3(
                    Mathf.Clamp(a.Wander.X, -_wanderLimit.X, _wanderLimit.X),
                    Mathf.Clamp(a.Wander.Y, -_wanderLimit.Y, _wanderLimit.Y),
                    Mathf.Clamp(a.Wander.Z, -_wanderLimit.Z, _wanderLimit.Z));
            }
            else if (a.Wander != Vector3.Zero) a.Wander = Vector3.Zero;
            a.Mesh.Position = a.BasePos + Jitter(a.Seed, time) * amp + a.Wander * _meltBlend;
        }

        // bonds — live stretch & snap
        bool showBonds = _bondsCheck.ButtonPressed && _meltBlend < 0.5f;
        _bondRoot.Visible = showBonds;
        if (showBonds)
            foreach (var b in _bonds)
            {
                var fI = _s.Sites[b.I].Frac + b.Cell;
                var fJ = _s.Sites[b.J].Frac + b.Cell + b.Off;
                Vector3 w = Vector3.Zero;
                int idxI = b.I * cellCount + CellIndex(b.Cell);
                if (idxI >= 0 && idxI < _atoms.Count) w = _atoms[idxI].Wander * _meltBlend;
                var pi = _thermCell * fI + Jitter(SeedFor(b.I, b.Cell), time) * amp + w;
                var pj = _thermCell * fJ + Jitter(SeedFor(b.J, b.Cell + b.Off), time) * amp + w;
                float cut = CrystalStructure.PairCutoff(
                    Elements.Get(_s.Sites[b.I].Element), Elements.Get(_s.Sites[b.J].Element)) * 1.25f;
                bool vis = pi.DistanceTo(pj) < cut;
                b.Mesh.Visible = vis;
                if (vis) OrientCylinder(b.Mesh, pi, pj);
            }

        // throttled live readout (density drops as you heat — real physics)
        _readoutTimer += dt;
        if (_readoutTimer > 0.25f)
        {
            _readoutTimer = 0;
            float t3 = therm * therm * therm;
            _densityL.Text = $"Density: {_s.Density() / t3:F2} g/cm³";
            _tempL.Text = $"{_tKelvin:F0} K — {(_meltBlend > 0.5f ? "LIQUID" : _tKelvin >= tm ? "MELTING" : "Solid")}";
        }
    }

    private int CellIndex(Vector3 cell) =>
        (int)(cell.X * _repeat * _repeat + cell.Y * _repeat + cell.Z);

    private Vector3 RandDir()
    {
        float a = _rng.NextSingle() * Mathf.Tau, z = _rng.NextSingle() * 2f - 1f;
        float s = Mathf.Sqrt(1 - z * z);
        return new Vector3(s * Mathf.Cos(a), s * Mathf.Sin(a), z);
    }

    // ================= input =================

    public override void _Input(InputEvent @event)
    {
        if (_s == null) return;

        if (@event is InputEventKey k && k.Pressed && !k.Echo &&
            (k.Keycode == Key.Escape || k.Keycode == Key.Q))
        {
            Close(false);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventKey kt && kt.Pressed && !kt.Echo)
        {
            var newTool = kt.Keycode switch
            {
                Key.Key1 => Tool.Select, Key.Key2 => Tool.Dope,
                Key.Key3 => Tool.Add, Key.Key4 => Tool.Remove, _ => _tool,
            };
            if (newTool != _tool) { _tool = newTool; UpdateToolButtons(); UpdateHint(); }
        }

        if (@event is not InputEventMouse m) return;
        var rect = _vpContainer.GetGlobalRect();
        if (!rect.HasPoint(m.Position)) { _orbiting = false; return; }

        if (m is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Right)
            {
                _orbiting = mb.Pressed;
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            { _dist = Mathf.Max(_dist * 0.9f, 1.5f); GetViewport().SetInputAsHandled(); }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            { _dist = Mathf.Min(_dist * 1.1f, 80f); GetViewport().SetInputAsHandled(); }
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed) BeginPrimary(mb.Position);
                else EndPrimary();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (m is InputEventMouseMotion mm)
        {
            if (_orbiting)
            {
                _yaw -= mm.Relative.X * 0.005f;
                _pitch = Mathf.Clamp(_pitch + mm.Relative.Y * 0.005f, -1.4f, 1.4f);
                GetViewport().SetInputAsHandled();
            }
            else if (_dragging) { DragMove(mm.Position); _clickMoved = true; }
        }
    }

    private Vector2 VpLocal(Vector2 mouse) => mouse - _vpContainer.GetGlobalRect().Position;

    private void BeginPrimary(Vector2 mouse)
    {
        _clickMoved = false;
        int hit = PickAtom(mouse);
        switch (_tool)
        {
            case Tool.Select:
                if (hit >= 0)
                {
                    Select(hit);
                    _dragSite = _atoms[hit].Site;
                    _dragging = true;
                    StartDragPlane(mouse);
                }
                else { _dragging = false; Select(-1); }
                break;
            case Tool.Dope:
                if (hit >= 0) { Select(hit); Dope(_atoms[hit].Site, _pendingElement); }
                break;
            case Tool.Remove:
                if (hit >= 0) RemoveAtom(_atoms[hit].Site);
                break;
            case Tool.Add:
                AddAtomAt(mouse);
                break;
        }
    }

    private void EndPrimary()
    {
        if (_dragging) { _dragging = false; RebuildBonds(); RefreshReadout(); }
        _orbiting = false;
    }

    private int PickAtom(Vector2 mouse)
    {
        var local = VpLocal(mouse);
        var origin = _cam.ProjectRayOrigin(local);
        var dir = _cam.ProjectRayNormal(local);
        int best = -1; float bestT = float.MaxValue;
        for (int i = 0; i < _atoms.Count; i++)
        {
            var c = _atoms[i].BasePos;
            float t = (c - origin).Dot(dir);
            if (t <= 0 || t >= bestT) continue;
            float perp = (origin + dir * t - c).Length();
            float r = Mathf.Max(0.55f, Elements.Get(_s.Sites[_atoms[i].Site].Element).Radius * 1.15f);
            if (perp < r) { bestT = t; best = i; }
        }
        return best;
    }

    private void StartDragPlane(Vector2 mouse)
    {
        var dir = _cam.ProjectRayNormal(VpLocal(mouse));
        _dragPlaneN = -_cam.GlobalTransform.Basis.Z;
        _dragPlaneD = _dragPlaneN.Dot(_s.FracToCart(_s.Sites[_dragSite].Frac));
        _ = dir;
    }

    private void DragMove(Vector2 mouse)
    {
        var origin = _cam.ProjectRayOrigin(VpLocal(mouse));
        var dir = _cam.ProjectRayNormal(VpLocal(mouse));
        float denom = _dragPlaneN.Dot(dir);
        if (Mathf.Abs(denom) < 1e-5f) return;
        float t = (_dragPlaneD - _dragPlaneN.Dot(origin)) / denom;
        if (t < 0) return;
        var f = _s.CartToFrac(origin + dir * t);
        f = new Vector3(Mathf.Clamp(f.X, -0.3f, 1.3f),
                        Mathf.Clamp(f.Y, -0.3f, 1.3f),
                        Mathf.Clamp(f.Z, -0.3f, 1.3f));
        _s.Sites[_dragSite] = new AtomSite(_s.Sites[_dragSite].Element, f.X, f.Y, f.Z);
        MarkEdited();
    }

    // ================= editing tools =================

    private void Dope(int site, string el)
    {
        if (_s.Sites[site].Element == el) return;
        var f = _s.Sites[site].Frac;
        _s.Sites[site] = new AtomSite(el, f.X, f.Y, f.Z);
        MarkEdited();
        foreach (var a in _atoms)
            if (a.Site == site)
            {
                a.Mesh.MaterialOverride = MatFor(el);
                a.Mesh.Scale = Vector3.One * Elements.Get(el).Radius;
            }
        RebuildBonds(); RefreshReadout(); UpdateCnLabel();
    }

    private void RemoveAtom(int site)
    {
        if (site < 0 || site >= _s.Sites.Count) return;
        _s.Sites.RemoveAt(site);
        MarkEdited();
        RebuildAtoms(); RebuildBonds(); RebuildCellFrame(); RefreshReadout();
    }

    private void AddAtomAt(Vector2 mouse)
    {
        var origin = _cam.ProjectRayOrigin(VpLocal(mouse));
        var dir = _cam.ProjectRayNormal(VpLocal(mouse));
        var n = -_cam.GlobalTransform.Basis.Z;
        var center = _s.FracToCart(new Vector3(0.5f, 0.5f, 0.5f));
        float denom = n.Dot(dir);
        if (Mathf.Abs(denom) < 1e-5f) return;
        float t = (n.Dot(center) - n.Dot(origin)) / denom;
        if (t < 0) return;
        var f = _s.CartToFrac(origin + dir * t);
        f = new Vector3(Mathf.Clamp(f.X, 0f, 1f), Mathf.Clamp(f.Y, 0f, 1f), Mathf.Clamp(f.Z, 0f, 1f));
        _s.Sites.Add(new AtomSite(_pendingElement, f.X, f.Y, f.Z));
        MarkEdited();
        RebuildAtoms(); RebuildBonds(); RefreshReadout();
    }

    private void MarkEdited()
    {
        if (!_s.Edited)
        {
            _s.Edited = true;   // symmetry is broken the moment you touch an atom — honest P1
            _sgL.Text = SpaceGroupText();
        }
    }

    private void Select(int idx)
    {
        _selectedAtom = idx;
        for (int i = 0; i < _atoms.Count; i++)
        {
            var el = _s.Sites[_atoms[i].Site].Element;
            bool sel = i == idx;
            _atoms[i].Mesh.MaterialOverride = sel ? _selMat : MatFor(el);
            _atoms[i].Mesh.Scale = Vector3.One * Elements.Get(el).Radius * (sel ? 1.18f : 1f);
        }
        UpdateCnLabel();
    }

    private void UpdateCnLabel()
    {
        if (_selectedAtom < 0 || _selectedAtom >= _atoms.Count) { _cnL.Text = "CN: —"; return; }
        int site = _atoms[_selectedAtom].Site;
        int cn = _topology.Count(b => (b.I == site || b.J == site) && (b.I != b.J || b.I == site))
                 + _topology.Count(b => b.I == b.J && b.I == site); // self-image bonds count twice
        _cnL.Text = $"Selected: {_s.Sites[site].Element} · CN {cn}";
    }

    // ================= readout & apply =================

    private void RefreshReadout()
    {
        _stats = MaterialCompiler.Compile(_s);
        _nameLabel.Text = $"{_s.Name}  ({_componentName})" + (_s.Edited ? " *" : "");
        _formulaL.Text = "Formula: " + _stats.Formula;
        _densityL.Text = $"Density: {_stats.Density:F2} g/cm³";
        _tmL.Text = $"Melting: {_stats.MeltingK:F0} K";
        _condL.Text = "Conductivity: " + _stats.Conductivity;
        _hardL.Text = "Hardness: " + _stats.Hardness;
        _sgL.Text = SpaceGroupText();
        UpdateCnLabel();
    }

    private string SpaceGroupText() =>
        _s.Amorphous ? "Amorphous — no long-range order"
        : _s.SpaceGroup + (_s.Edited ? " → edited (P1)" : "");

    private void UpdateToolButtons()
    {
        for (int i = 0; i < _toolButtons.Length; i++)
            _toolButtons[i].ButtonPressed = (int)_tool == i;
    }

    private void UpdateHint() => _hintL.Text = _tool switch
    {
        Tool.Select => "LMB drag atom · RMB orbit · wheel zoom · keys 1-4 tools",
        Tool.Dope => "Select an atom, then click an element",
        Tool.Add => "LMB places the pending element on the mid-plane",
        _ => "LMB removes the atom under the cursor",
    };

    private void UpdatePendingHighlight()
    {
        foreach (var c in _vpContainer.GetParent().FindChildren("*", nameof(Button), false, false)) { }
        // element buttons highlight:
        foreach (var b in ElementButtons())
            b.Modulate = b.Text == _pendingElement ? Colors.White : new Color(0.55f, 0.55f, 0.55f);
    }
    private System.Collections.Generic.List<Button> ElementButtons()
    {
        var list = new System.Collections.Generic.List<Button>();
        var grid = _vpContainer.GetParent().FindChild("GridContainer", true, false) as GridContainer;
        if (grid == null)
        {
            // fallback: find our grid among left panel
            foreach (var c in _root.FindChildren("*", nameof(GridContainer), false, false))
                if (c is GridContainer gc) { grid = gc; break; }
        }
        if (grid != null)
            foreach (var c in grid.GetChildren())
                if (c is Button b) list.Add(b);
        return list;
    }

    private void OnApply()
    {
        RefreshReadout();
        int id = 900 + ItemRegistry.Items.Count;
        var item = MaterialCompiler.CreateItemData(_s, _stats, id);
        ItemRegistry.RegisterItem(item);   // see wiring note: make this method public
        SaveJson(id);
        SpawnSample(item);
        Close(true);
    }

    private void SaveJson(int itemId)
    {
        DirAccess.MakeDirRecursiveAbsolute("user://crystals");
        string fname = $"user_{itemId}_{(long)Time.GetUnixTimeFromSystem()}";
        var d = StructureLibrary.ToJson(_s, fname);
        using var f = FileAccess.Open($"user://crystals/{fname}.json", FileAccess.ModeFlags.Write);
        f?.StoreString(Json.Stringify(d));
    }

    private void SpawnSample(ItemData item)
    {
        var player = GetTree().Root.FindChild("Player", true, false) as Node3D;
        if (player == null) return;
        var fwd = -player.GlobalTransform.Basis.Z.Normalized();
        var it = new InteractableItem();
        GetTree().Root.AddChild(it);
        it.GlobalPosition = player.GlobalPosition + fwd * 2.5f + Vector3.Up * 0.6f;
        it.Initialize(item);
        it.AddToGroup("DroppedItems");
    }

    private void Close(bool saved)
    {
        PlayerInputOverride.Set(false);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        var cb = _onClosed;
        cb?.Invoke();
        QueueFree();
    }
}