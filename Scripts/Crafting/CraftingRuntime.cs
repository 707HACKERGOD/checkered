using Godot;

namespace Crafting
{
    // Integration node for your real game scene. Add as a child of the scene
    // root, set PlayerPath to your player, play. CraftingLab stays the
    // standalone F6 sandbox — never put both in one scene.
    [GlobalClass]
    public partial class CraftingRuntime : Node3D
    {
        [Export] public NodePath PlayerPath;
        [Export] public NodePath WorldRootPath;             // default: this node's parent
        [Export] public bool AddHud = true;
        [Export] public bool AddOwnGrid = true;             // false if you use your Terrain3D grid
        [Export] public bool EnableCraftingInventory = true;
        [Export] public Key InventoryKey = Key.Tab;
        [Export] public bool DebugKeys = false;             // K = storm, 1-5 = spawn parts

        public CraftingSim Sim => _sim;
        public CarrySystem Carry => _carry;

        CraftingSim _sim;
        CarrySystem _carry;
        GlobalGrid _grid;
        InventoryUi _invUi;
        CanvasLayer _hud;
        Label _toast, _hint, _stats;
        float _toastCd;

        public override void _Ready()
        {
            CustomMeshes.RegisterDefaults();

            _sim = new CraftingSim { Name = "CraftingSim" };
            AddChild(_sim);
            Node3D worldRoot = this;
            if (WorldRootPath != null && !WorldRootPath.IsEmpty) worldRoot = GetNodeOrNull<Node3D>(WorldRootPath) ?? this;
            else if (GetParent() is Node3D par) worldRoot = par;
            _sim.WorldRoot = worldRoot;

            _carry = new CarrySystem { Name = "CarrySystem" };
            AddChild(_carry);
            if (AddOwnGrid) { _grid = new GlobalGrid(); AddChild(_grid); }

            CraftingHud.Toast += Toast;
            CraftingHud.SetHint += SetHint;
            CraftingHud.StormChanged += OnStorm;
            CraftingHud.ZapFx += OnZap;
            if (_grid != null) CraftingHud.ToggleGrid += ToggleGrid;

            var player = (PlayerPath != null && !PlayerPath.IsEmpty)
                ? GetNodeOrNull<Node3D>(PlayerPath)
                : GetTree().Root.FindChild("Player", true, false) as Node3D;
            if (player != null) RebindPlayer(player);
            else GD.PushWarning("CraftingRuntime: player not found — set the PlayerPath export.");
            // end of CraftingRuntime._Ready(), after RebindPlayer(...)
            Crafting.UiStack.ExternalBlocking = () => PlayerInputOverride.Active;
            Crafting.UiStack.OpenChanged += open =>
            {
                if (open) PlayerInputOverride.Begin();
                else      PlayerInputOverride.End();
            };
        }

        public override void _ExitTree()
        {
            CraftingHud.Toast -= Toast;
            CraftingHud.SetHint -= SetHint;
            CraftingHud.StormChanged -= OnStorm;
            CraftingHud.ZapFx -= OnZap;
            if (_grid != null) CraftingHud.ToggleGrid -= ToggleGrid;
        }

        public void RebindPlayer(Node3D player)
        {
            if (player == null) return;
            var bridge = player.GetNodeOrNull<PlayerCraftingBridge>("PlayerCraftingBridge");
            if (bridge == null) { bridge = new PlayerCraftingBridge { Name = "PlayerCraftingBridge" }; player.AddChild(bridge); }
            _sim.Player = bridge;
        }

        void BuildHud()
        {
            _hud = new CanvasLayer { Layer = 10 };
            AddChild(_hud);

            _toast = UiKit.Label("", 17);
            _toast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _toast.OffsetTop = 90; _toast.OffsetLeft = -400; _toast.OffsetRight = 400;
            _toast.HorizontalAlignment = HorizontalAlignment.Center;
            _toast.Modulate = Colors.Transparent;
            _hud.AddChild(_toast);

            _hint = UiKit.Label("", 16);
            _hint.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            _hint.OffsetLeft = -420; _hint.OffsetRight = 420; _hint.OffsetTop = -56; _hint.OffsetBottom = -28;
            _hint.HorizontalAlignment = HorizontalAlignment.Center;
            _hud.AddChild(_hint);

            _stats = UiKit.Label("", 14, UiKit.Dim);
            _stats.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            _stats.OffsetLeft = 16; _stats.OffsetTop = -64; _stats.OffsetBottom = -36;
            _hud.AddChild(_stats);
        }

        public override void _Process(double delta)
        {
            if (_toastCd > 0f)
            {
                _toastCd -= (float)delta;
                if (_toastCd < 0.6f) _toast.Modulate = new Color(1, 1, 1, Mathf.Max(_toastCd / 0.6f, 0f));
            }
            var p = _sim?.Player;
            if (p != null && _stats != null)
                _stats.Text = $"CARRY {p.Limbs.TotalMassKg():0.0} kg";
        }

        public void Toast(string msg) { if (_toast == null) return; _toast.Text = msg; _toast.Modulate = Colors.White; _toastCd = 2.6f; }
        public void SetHint(string text) { if (_hint != null) _hint.Text = text; }
        public void ToggleGrid() { if (_grid != null) _grid.Visible = !_grid.Visible; }
        void OnStorm(bool on) => Toast(on ? "Thunderstorm ON" : "Thunderstorm off");
        void OnZap() => Toast("ZAPPED — conductive material overhead in a storm.");

        public void OpenCraftingInventory()
        {
            if (_invUi == null)
            {
                if (_hud == null) BuildHud();
                _invUi = new InventoryUi();
                _hud.AddChild(_invUi);
            }
            _invUi.Open();
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
            var key = k.PhysicalKeycode;
            if (key == InventoryKey)
            {
                if (!EnableCraftingInventory || UiStack.Blocking) return;
                GetViewport().SetInputAsHandled();
                OpenCraftingInventory();
                return;
            }
            if (UiStack.Blocking || !DebugKeys) return;
            if (key == Key.K) { GetViewport().SetInputAsHandled(); _sim.ToggleStorm(); return; }
            int spawn = key switch { Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, Key.Key5 => 4, _ => -1 };
            if (spawn >= 0 && spawn < PartCatalog.All.Count)
            {
                var p = _sim.Player;
                if (p?.Cam == null || p is not Node3D node) return;
                GetViewport().SetInputAsHandled();
                var c = PartCatalog.All[spawn];
                var fwd = -p.Cam.GlobalTransform.Basis.Z;
                var d = new CraftedObjectData { Name = c.Name };
                d.Parts.Add(new Part { MaterialId = c.MaterialId, Shape = new ShapeDef { Kind = c.Kind, Size = c.DefaultSize } });
                CraftingSim.SpawnObject(d, node.GlobalPosition + fwd * 1.7f + Vector3.Up * 1.1f);
                Toast($"Spawned {c.Name}.");
            }
        }
    }
}