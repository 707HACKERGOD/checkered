using Godot;

namespace Crafting
{
    // Prototype scene root: test yard, sim, HUD, carry system, grid.
    public partial class CraftingLab : Node3D
    {
        public static CraftingLab Instance;

        CraftingSim _sim;
        LabPlayer _player;
        InventoryUi _invUi;
        CarrySystem _carry;
        GlobalGrid _grid;

        CanvasLayer _ui;
        Label _toast, _stats, _storm, _hint;
        ColorRect _flash;
        float _toastCd, _flashCd;
        Environment _env;
        DirectionalLight3D _sun;

        public override void _Ready()
        {
            Instance = this;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            CraftingHud.Toast += Toast;
            CraftingHud.SetHint += SetHint;
            CraftingHud.StormChanged += SetStorm;
            CraftingHud.ZapFx += Zap;

            _sim = new CraftingSim { Name = "CraftingSim" };
            AddChild(_sim);
            _sim.WorldRoot = this;

            RegisterCustomMeshes();
            BuildEnvironment();
            BuildGround();
            BuildHud();
            BuildPlayer();
            BuildDemoYard();
            PreStockInventory();
        }

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null;
            CraftingHud.Toast -= Toast;
            CraftingHud.SetHint -= SetHint;
            CraftingHud.StormChanged -= SetStorm;
            CraftingHud.ZapFx -= Zap;
        }

        void RegisterCustomMeshes()
        {
            CustomMeshes.RegisterDefaults();
        }

        void BuildEnvironment()
        {
            _env = new Environment
            {
                BackgroundMode = Environment.BGMode.Color,
                BackgroundColor = new Color(0.55f, 0.65f, 0.8f),
                AmbientLightSource = Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.75f, 0.8f, 0.9f),
                AmbientLightEnergy = 0.9f,
                FogEnabled = true,
                FogLightColor = new Color(0.6f, 0.7f, 0.85f),
                FogDensity = 0.005f
            };
            AddChild(new WorldEnvironment { Environment = _env });
            _sun = new DirectionalLight3D { RotationDegrees = new Vector3(-50, -35, 0), ShadowEnabled = true, LightEnergy = 1.1f };
            AddChild(_sun);
        }

        void BuildGround()
        {
            var ground = new StaticBody3D { Name = "Ground" };
            // layer 1 keeps default physics; layer 2 marks it "floor" for Ctrl+Q snapping
            ground.CollisionLayer = 1u | 2u;
            ground.AddChild(new CollisionShape3D { Shape = new WorldBoundaryShape3D() });
            AddChild(ground);

            var mesh = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(120, 120) } };
            var mat = new StandardMaterial3D { Roughness = 1f };
            if (ResourceLoader.Exists("res://Assets/textures/grass_1.png"))
            {
                mat.AlbedoTexture = ResourceLoader.Load<Texture2D>("res://Assets/textures/grass_1.png");
                mat.Uv1Scale = new Vector3(30, 1, 30);
                mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            }
            else
            {
                mat.AlbedoTexture = UiKit.GridTexture();
                mat.Uv1Scale = new Vector3(60, 1, 60);
            }
            mesh.MaterialOverride = mat;
            AddChild(mesh);
        }

        void BuildHud()
        {
            _ui = new CanvasLayer { Layer = 10 };
            AddChild(_ui);

            var cross = new ColorRect { Color = new Color(1, 1, 1, 0.7f), Size = new Vector2(4, 4), MouseFilter = Control.MouseFilterEnum.Ignore };
            cross.SetAnchorsPreset(Control.LayoutPreset.Center);
            _ui.AddChild(cross);

            _toast = UiKit.Label("", 17);
            _toast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _toast.OffsetTop = 90; _toast.OffsetLeft = -400; _toast.OffsetRight = 400;
            _toast.HorizontalAlignment = HorizontalAlignment.Center;
            _toast.Modulate = Colors.Transparent;
            _ui.AddChild(_toast);

            _storm = UiKit.Label("[ THUNDERSTORM ]  conductive material above your head is a lightning rod", 15, new Color(0.8f, 0.85f, 1f));
            _storm.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _storm.OffsetTop = 40; _storm.OffsetLeft = -500; _storm.OffsetRight = 500;
            _storm.HorizontalAlignment = HorizontalAlignment.Center;
            _storm.Visible = false;
            _ui.AddChild(_storm);

            _hint = UiKit.Label("", 16);
            _hint.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            _hint.OffsetLeft = -420; _hint.OffsetRight = 420; _hint.OffsetTop = -56; _hint.OffsetBottom = -28;
            _hint.HorizontalAlignment = HorizontalAlignment.Center;
            _ui.AddChild(_hint);

            _stats = UiKit.Label("", 14, UiKit.Dim);
            _stats.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
            _stats.OffsetLeft = 16; _stats.OffsetTop = -64; _stats.OffsetBottom = -36;
            _ui.AddChild(_stats);

            var help = UiKit.Label(
                "E pick up/store · Q drop/cancel · Ctrl+Q floor-snap · R turn/roll · scroll cycle\n" +
                "G grid · X detach part · 1-5 spawn parts · TAB inventory\n" +
                "T shove · hold LMB heat · hold RMB freeze · K storm · WASD/SPACE move", 13, UiKit.Dim);
            help.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            help.OffsetRight = -16; help.OffsetTop = -80; help.OffsetBottom = -16;
            help.HorizontalAlignment = HorizontalAlignment.Right;
            _ui.AddChild(help);

            _flash = new ColorRect { Color = new Color(1, 0.4f, 0.4f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
            _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _flash.Visible = false;
            _ui.AddChild(_flash);

            _invUi = new InventoryUi();
            _ui.AddChild(_invUi);
        }

        void BuildPlayer()
        {
            _player = new LabPlayer { Position = new Vector3(0, 1.2f, 7f) };
            AddChild(_player);
            _sim.Player = _player;

            _carry = new CarrySystem { Name = "CarrySystem" };
            AddChild(_carry);
            AddChild(new ResizeEditor { Name = "ResizeEditor" });
            _grid = new GlobalGrid();
            AddChild(_grid);
        }

        static CraftedObjectData Single(string name, string mat, ShapeKind kind, Vector3 size)
        {
            var d = new CraftedObjectData { Name = name };
            d.Parts.Add(new Part { MaterialId = mat, Shape = new ShapeDef { Kind = kind, Size = size } });
            return d;
        }

        void BuildDemoYard()
        {
            // campfire: static fixture with a heat emitter (stove behavior)
            var fire = new CraftedObjectData { Name = "Campfire" };
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.Tau;
                fire.Parts.Add(new Part
                {
                    MaterialId = "stone",
                    Shape = new ShapeDef { Kind = ShapeKind.Sphere, Size = new Vector3(0.24f, 0.24f, 0.24f) },
                    Local = new Transform3D(Basis.Identity, new Vector3(Mathf.Cos(a) * 0.45f, 0.12f, Mathf.Sin(a) * 0.45f))
                });
            }
            var rod = new ShapeDef { Kind = ShapeKind.Rod, Size = new Vector3(0.09f, 0.7f, 0.09f) };
            fire.Parts.Add(new Part { MaterialId = "wood", Shape = rod.Clone(), Local = new Transform3D(new Basis(Vector3.Back, 0.6f), new Vector3(0, 0.24f, 0)) });
            fire.Parts.Add(new Part { MaterialId = "wood", Shape = rod.Clone(), Local = new Transform3D(new Basis(Vector3.Back, -0.6f), new Vector3(0, 0.24f, 0)) });
            fire.BondAllAdjacent();
            var fireObj = CraftingSim.SpawnObject(fire, new Vector3(3f, 0.05f, -3f), frozen: true);
            fireObj.AddChild(new HeatEmitter { Radius = 3f, Power = 24f, Position = new Vector3(0, 0.25f, 0) });

            // iron pot of water on the fire: boils away as steam
            var pot = Single("Iron pot", "iron", ShapeKind.OpenBox, new Vector3(0.42f, 0.32f, 0.42f));
            pot.Parts[0].Shape.Wall = 0.03f;
            pot.Parts[0].Contents.Add(new SubstanceStack { MaterialId = "water", M3 = 0.008f });
            CraftingSim.SpawnObject(pot, new Vector3(3f, 0.75f, -3f));

            // coldstone: static fixture with a negative emitter (fridge behavior)
            var cold = Single("Coldstone", "iron", ShapeKind.Sphere, new Vector3(0.5f, 0.5f, 0.5f));
            cold.Parts[0].TempC = -30f;
            var coldObj = CraftingSim.SpawnObject(cold, new Vector3(-3.5f, 0.27f, -3f), frozen: true);
            coldObj.AddChild(new HeatEmitter { Radius = 2.6f, Power = -20f, Position = new Vector3(0, 0.3f, 0) });

            // starter kit: the table-build materials
            CraftingSim.SpawnObject(Single("Leg plank", "wood", ShapeKind.Box, new Vector3(0.10f, 0.55f, 0.10f)), new Vector3(1.6f, 0.35f, 2.2f));
            CraftingSim.SpawnObject(Single("Leg plank", "wood", ShapeKind.Box, new Vector3(0.10f, 0.55f, 0.10f)), new Vector3(1.9f, 0.35f, 2.5f));
            CraftingSim.SpawnObject(Single("Leg plank", "wood", ShapeKind.Box, new Vector3(0.10f, 0.55f, 0.10f)), new Vector3(2.2f, 0.35f, 2.2f));
            CraftingSim.SpawnObject(Single("Leg plank", "wood", ShapeKind.Box, new Vector3(0.10f, 0.55f, 0.10f)), new Vector3(2.5f, 0.35f, 2.5f));
            CraftingSim.SpawnObject(Single("Tabletop", "wood", ShapeKind.Box, new Vector3(1.20f, 0.06f, 0.70f)), new Vector3(1.9f, 0.06f, 3.2f));
            CraftingSim.SpawnObject(Single("Stone", "stone", ShapeKind.Sphere, new Vector3(0.3f, 0.3f, 0.3f)), new Vector3(0.8f, 0.2f, 2.8f));
            CraftingSim.SpawnObject(Single("Bucket", "iron", ShapeKind.OpenBox, new Vector3(0.32f, 0.30f, 0.32f)), new Vector3(0.4f, 0.18f, 2.4f));
        }

        void PreStockInventory()
        {
            void Add(string name, string mat, ShapeKind kind, Vector3 size)
            {
                var item = new InvItem { Kind = ItemKind.Object, Data = Single(name, mat, kind, size), Name = name };
                if (Inventory.Add(item)) ThumbnailGen.Render(item);
            }
            Add("Copper rod", "copper", ShapeKind.Rod, new Vector3(0.07f, 1.0f, 0.07f));
            Add("Rubber ball", "rubber", ShapeKind.Sphere, new Vector3(0.3f, 0.3f, 0.3f));
        }

        public override void _Process(double delta)
        {
            float d = (float)delta;
            if (_toastCd > 0f)
            {
                _toastCd -= d;
                if (_toastCd < 0.6f) _toast.Modulate = new Color(1, 1, 1, Mathf.Max(_toastCd / 0.6f, 0f));
            }
            if (_flashCd > 0f)
            {
                _flashCd -= d;
                _flash.Visible = true;
                _flash.Color = new Color(1f, 0.5f, 0.5f, Mathf.Clamp(_flashCd / 0.35f, 0f, 1f) * 0.55f);
                if (_flashCd <= 0f) _flash.Visible = false;
            }
            if (_player != null && _sim != null)
            {
                float load = _player.Limbs.TotalMassKg();
                bool bait = _sim.Storm && _player.Limbs.HighestConductiveY() > _player.GlobalPosition.Y + 1.6f;
                _stats.Text = $"CARRY {load:0.0} kg   SPEED {_player.SpeedPercent():0%}   ZAPS {_sim.Zaps}" +
                              (bait ? "\n> conductive item overhead — lightning bait" : "");
            }
        }

        public void Toast(string msg) { _toast.Text = msg; _toast.Modulate = Colors.White; _toastCd = 2.6f; }
        public void SetHint(string text) => _hint.Text = text;
        public void ToggleGrid() => _grid?.Toggle();
        public void Zap() { _flashCd = 0.35f; Toast("ZAPPED — conductive material overhead in a storm."); }

        public void SetStorm(bool on)
        {
            _storm.Visible = on;
            _env.BackgroundColor = on ? new Color(0.2f, 0.22f, 0.3f) : new Color(0.55f, 0.65f, 0.8f);
            _env.AmbientLightEnergy = on ? 0.45f : 0.9f;
            _env.FogLightColor = on ? new Color(0.25f, 0.28f, 0.38f) : new Color(0.6f, 0.7f, 0.85f);
            _sun.LightEnergy = on ? 0.5f : 1.1f;
        }

        public void OpenInventoryUi() => _invUi.Open();

        public void DropItem(InvItem it)
        {
            if (it.Kind == ItemKind.Object)
            {
                var fwd = -_player.GlobalTransform.Basis.Z;
                CraftingSim.SpawnObject(it.Data, _player.GlobalPosition + fwd * 1.6f + Vector3.Up * 1.1f);
            }
            else SpawnSubstance(it);
        }

        public void PourItem(InvItem it) => SpawnSubstance(it);

        void SpawnSubstance(InvItem it)
        {
            var fwd = -_player.GlobalTransform.Basis.Z;
            _sim.SpawnBlob(it.MaterialId, it.M3, 20f, _player.GlobalPosition + fwd * 1.4f + Vector3.Up * 1.2f);
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
            var key = k.PhysicalKeycode;
            if (key == Key.Tab)
            {
                if (UiStack.Blocking) return;
                GetViewport().SetInputAsHandled();
                _invUi.Open();
                return;
            }
            if (UiStack.Blocking) return;
            if (key == Key.K) { GetViewport().SetInputAsHandled(); _sim.ToggleStorm(); return; }
            if (key == Key.Key6)
            {
                GetViewport().SetInputAsHandled();
                var p6 = _player;
                if (p6?.Cam == null || p6 is not Node3D node6) return;
                var fwd6 = -p6.Cam.GlobalTransform.Basis.Z;
                Rope.Spawn(node6.GlobalPosition + fwd6 * 1.7f + Vector3.Up * 0.6f);
                Toast("Rope — grab an end with E, tie it to a white dot.");
                return;
            }            

            int spawn = key switch { Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, Key.Key5 => 4, _ => -1 };
            if (spawn >= 0 && spawn < PartCatalog.All.Count)
            {
                GetViewport().SetInputAsHandled();
                var c = PartCatalog.All[spawn];
                var fwd = -_player.GlobalTransform.Basis.Z;
                var jitter = new Vector3((float)GD.RandRange(-0.3, 0.3), (float)GD.RandRange(0.0, 0.2), (float)GD.RandRange(-0.3, 0.3));
                CraftingSim.SpawnObject(Single(c.Name, c.MaterialId, c.Kind, c.DefaultSize),
                                        _player.GlobalPosition + fwd * 1.7f + Vector3.Up * 1.1f + jitter);
                Toast($"Spawned {c.Name}.");
            }
        }
    }
}