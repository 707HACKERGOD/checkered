using Godot;

public partial class MindEyeMode : Node
{
    public static MindEyeMode Instance { get; private set; }
    public static bool ActiveNow => Instance != null && Instance.IsActive;

    public const float ScanSeconds = 2.0f;
    public bool IsActive { get; private set; }
    public bool LabOpen { get; set; }

    private Camera3D _cam;
    private Node3D _player;
    private readonly Godot.Collections.Array<Rid> _exclude = new();
    private float _dbg;
    private CanvasLayer _overlay;
    private ColorRect _fx;
    private ShaderMaterial _fxMat;
    private EyeIcon _eye;
    private ScanTooltip _tooltip;
    private MaterialComposition _target;
    private float _scan, _fxLevel;
    private int _sel;
    private bool _listShown;

    public override void _Ready()
    {
        Instance = this;
        StructureLibrary.LoadUserStructures(); // custom materials become scannable
    }
    public override void _ExitTree() { if (Instance == this) Instance = null; }

    public void Toggle()
    {
        if (LabOpen) return;
        if (IsActive) Deactivate(); else Activate();
    }

    private void Activate()
    {
        IsActive = true;
        PlayerInputOverride.MovementLock = true;

        if (_player == null)
        {
            _player = GetTree().Root.FindChild("Player", true, false) as Node3D;
            if (_player != null)
            {
                // exclude EVERY physics object on the player — capsule, InterestArea, hitboxes.
                // Your InterestArea sphere sits right on the camera→target ray path.
                _exclude.Clear();
                if (_player is CollisionObject3D self)
                    _exclude.Add(self.GetRid());
                foreach (var c in _player.FindChildren("*", "CollisionObject3D", true, false))
                    if (c is CollisionObject3D co)
                        _exclude.Add(co.GetRid());
            }
            else
                GD.PrintErr("[MindEye] Player node not found by name 'Player'.");
        }

        _cam = ResolveCamera();

        if (_overlay == null) BuildOverlay();
        _overlay.Visible = true;
        _target = null; _scan = 0; _listShown = false;
        _tooltip.Reset();
        GD.Print($"[MindEye] ON — cam='{_cam?.Name}' excluded={_exclude.Count} colliders");
    }

    private Camera3D ResolveCamera()
    {
        if (_player != null)
        {
            var cam = _player.Get("PlayerCamera").As<Camera3D>();   // your exported field
            if (cam != null) return cam;
            foreach (var c in _player.FindChildren("*", "Camera3D", true, false))
                if (c is Camera3D c3) return c3;
        }
        return GetViewport().GetCamera3D();
    }

    private Godot.Collections.Dictionary RaycastCenter(float dist)
    {
        var cam = _cam ?? ResolveCamera();
        if (cam == null) return new Godot.Collections.Dictionary();

        // query the PLAYER's world, not the autoload's viewport —
        // correct even if the game renders through a SubViewport
        var world = _player?.GetWorld3D() ?? GetViewport().World3D;
        if (world == null) return new Godot.Collections.Dictionary();

        var from = cam.GlobalPosition;
        var dir = -cam.GlobalTransform.Basis.Z;
        var q = PhysicsRayQueryParameters3D.Create(from, from + dir.Normalized() * dist);
        q.CollisionMask = 0xFFFFFFFF;
        q.CollideWithAreas = true;
        q.CollideWithBodies = true;
        if (_exclude.Count > 0)
            q.Exclude = _exclude;
        return world.DirectSpaceState.IntersectRay(q);
    }

    private void Deactivate()
    {
        IsActive = false;
        PlayerInputOverride.MovementLock = false;
        _tooltip?.Reset();
        if (_overlay != null) _overlay.Visible = false;
        _target = null; _listShown = false;
    }

    private void BuildOverlay()
    {
        _overlay = new CanvasLayer { Layer = 40 };
        AddChild(_overlay);

        _fx = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
        _fx.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _fxMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/BlueprintOverlay.gdshader") };
        _fx.Material = _fxMat;
        _fxMat.SetShaderParameter("intensity", 0.0f);
        _overlay.AddChild(_fx);

        _eye = new EyeIcon { Position = new Vector2(24, 18) };
        _overlay.AddChild(_eye);

        _tooltip = new ScanTooltip();
        _overlay.AddChild(_tooltip);
    }

    // physics-space raycasts must run in _PhysicsProcess
    public override void _PhysicsProcess(double delta)
    {
        if (!IsActive || LabOpen) return;
        var hit = RaycastCenter(8f);
        _dbg -= (float)delta;
        if (_dbg <= 0f)
        {
            _dbg = 0.5f;
            if (_cam == null) GD.PrintErr("[MindEye] no camera resolved");
            else if (hit.Count == 0) GD.Print("[MindEye] ray: NO HIT within 8 m of crosshair");
            else
            {
                var node = hit["collider"].AsGodotObject() as Node;
                GD.Print($"[MindEye] ray hit '{node?.Name}'  scan={_scan:F2}  listed={_listShown}");
            }
        }
        MaterialComposition comp = null;
        Vector3 hitPoint = default;
        if (hit.Count > 0)
        {
            comp = MaterialComposition.FindOrCreate(hit["collider"].As<Node>());
            hitPoint = hit["position"].AsVector3();
        }
        if (comp != _target)
        {
            _target = comp; _scan = 0; _listShown = false; _sel = 0;
            if (comp == null) _tooltip.Reset();
        }
        if (_target == null) return;

        if (!_listShown)
        {
            _scan += (float)delta / ScanSeconds;
            _tooltip.Scan(_target.OwnerName, Mathf.Min(_scan, 1f));
            PinTooltip(hitPoint);
            if (_scan >= 1f) { _listShown = true; _tooltip.List(_target, _sel); PinTooltip(hitPoint); }
        }
        else PinTooltip(hitPoint);
    }

    public override void _Process(double delta)
    {
        if (_fxMat == null) return;
        float targetFx = IsActive && !LabOpen ? 0.55f : 0f;
        _fxLevel = Mathf.MoveToward(_fxLevel, targetFx, (float)delta * 1.8f);
        _fxMat.SetShaderParameter("intensity", _fxLevel);
    }

    public override void _Input(InputEvent @event)
    {
        if (PlayerInputOverride.Active) return;

        // must be ABOVE the IsActive check — this is the only way IN
        if (@event.IsActionPressed("mind_eye"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!IsActive || LabOpen) return;

        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape)
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_target == null || !_listShown) return;

        var comps = _target.Components;
        if (comps.Length == 0) return;
        bool changed = false;

        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            { _sel = (_sel + comps.Length - 1) % comps.Length; changed = true; }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            { _sel = (_sel + 1) % comps.Length; changed = true; }
            else if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                OpenLab(comps[Mathf.Clamp(_sel, 0, comps.Length - 1)]);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        else if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            for (int i = 0; i < Mathf.Min(9, comps.Length); i++)
                if (key.Keycode == (Key)((int)Key.Key1 + i)) { _sel = i; changed = true; }
        }
        if (changed) { _tooltip.List(_target, _sel); GetViewport().SetInputAsHandled(); }
    }
    private void OpenLab(MaterialComponent c)
    {
        if (!StructureLibrary.Prototypes.TryGetValue(c.StructureId, out var proto)) return;
        LabOpen = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _overlay.Visible = false;
        var lab = new CrystalLab();
        GetTree().Root.AddChild(lab);
        lab.Open(proto.Clone(), c.Name, OnLabClosed);
    }

    private void OnLabClosed()
    {
        LabOpen = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Deactivate();
    }

    private void PinTooltip(Vector3 world)
    {
        if (_cam == null || _cam.IsPositionBehind(world)) { _tooltip.Visible = false; return; }
        _tooltip.Visible = true;
        var p = _cam.UnprojectPosition(world) + new Vector2(24, -80);
        var size = GetViewport().GetVisibleRect().Size;
        p = p.Clamp(Vector2.Zero, size - _tooltip.CustomMinimumSize);
        _tooltip.Position = p;
    }
}

public partial class EyeIcon : Control
{
    public EyeIcon() { CustomMinimumSize = new Vector2(48, 32); MouseFilter = Control.MouseFilterEnum.Ignore; }

    public override void _Process(double delta)
    {
        if (Mathf.PosMod(Time.GetTicksMsec() / 1000f, 3.5f) < 0.2f || IsVisibleInTree())
            QueueRedraw();
    }

    public override void _Draw()
    {
        var c = new Vector2(24, 16);
        float t = Time.GetTicksMsec() / 1000f;
        float blink = Mathf.PosMod(t, 3.5f) < 0.15f ? 0.12f : 1f;
        var pts = new Vector2[25];
        for (int i = 0; i <= 24; i++)
        {
            float a = i / 24f * Mathf.Tau;
            pts[i] = c + new Vector2(Mathf.Cos(a) * 20, Mathf.Sin(a) * 10 * blink);
        }
        DrawPolyline(pts, new Color(1, 1, 1, 0.9f), 2f);
        DrawCircle(c, 5, new Color(1, 1, 1, 0.95f));
    }
}

public partial class ScanTooltip : Control
{
    private readonly Label _title, _scanLabel;
    private readonly ScanBar _bar;
    private readonly VBoxContainer _list;

    public ScanTooltip()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(320, 60);
        var pc = new PanelContainer();
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.08f, 0.16f, 0.88f),
            BorderColor = new Color(0.35f, 0.65f, 1.0f, 0.9f),
        };
        sb.SetBorderWidthAll(1);
        sb.ContentMarginLeft = 10; sb.ContentMarginRight = 10;
        sb.ContentMarginTop = 6; sb.ContentMarginBottom = 8;
        pc.AddThemeStyleboxOverride("panel", sb);
        AddChild(pc);

        var box = new VBoxContainer();
        pc.AddChild(box);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 16);
        _title.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1f));
        box.AddChild(_title);
        _scanLabel = new Label { Text = "ANALYSING…" };
        box.AddChild(_scanLabel);
        _bar = new ScanBar { CustomMinimumSize = new Vector2(300, 6) };
        box.AddChild(_bar);
        _list = new VBoxContainer();
        box.AddChild(_list);
        Reset();
    }

    public void Reset()
    {
        Visible = false;
        _title.Text = ""; _scanLabel.Visible = false; _bar.Visible = false; _bar.P = 0;
        foreach (var c in _list.GetChildren()) c.QueueFree();
    }

    public void Scan(string obj, float p)
    {
        Visible = true;
        _title.Text = "◆ " + obj.ToUpper();
        _scanLabel.Visible = p < 1f;
        _bar.Visible = p < 1f;
        _bar.P = p;
    }

    public void List(MaterialComposition mc, int sel)
    {
        Visible = true;
        _scanLabel.Visible = false; _bar.Visible = false;
        foreach (var c in _list.GetChildren()) c.QueueFree();
        for (int i = 0; i < mc.Components.Length; i++)
        {
            var c = mc.Components[i];
            string sub = "";
            if (StructureLibrary.Prototypes.TryGetValue(c.StructureId, out var st))
                sub = $" — {st.Formula()} · {st.Density():F2} g/cm³";
            var l = new Label { Text = $"{(i == sel ? "▶" : " ")} {i + 1}. {c.Name}{sub}" };
            l.AddThemeColorOverride("font_color",
                i == sel ? new Color(1f, 1f, 1f) : new Color(0.65f, 0.8f, 1f));
            _list.AddChild(l);
        }
        var hint = new Label { Text = "wheel / 1-9 select · LMB open lab · Q exit" };
        hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.6f, 0.8f));
        hint.AddThemeFontSizeOverride("font_size", 12);
        _list.AddChild(hint);
    }
}

public partial class ScanBar : Control
{
    private float _p;
    public float P { get => _p; set { _p = value; QueueRedraw(); } }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.1f, 0.2f, 0.35f));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * P, Size.Y)), new Color(0.4f, 0.8f, 1f));
    }
}