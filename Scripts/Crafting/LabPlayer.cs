using Godot;

namespace Crafting
{
    // Chunky test dummy: movement, mouse look, shove, heat/freeze rays.
    // All crafting interaction (E/Q/Ctrl+Q/scroll/dots) lives in CarrySystem.
    public partial class LabPlayer : CharacterBody3D, ICraftingPlayer
    {
        public LimbRig Limbs { get; private set; }
        public Camera3D Cam => _cam;

        const float BaseSpeed = 4.3f;
        const float CarryLimitKg = 60f;

        Camera3D _cam;
        float _pitch, _vy, _shakeT;

        public override void _Ready()
        {
            var col = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.95f } };
            col.Position = new Vector3(0, 0.98f, 0);
            CollisionLayer = 1; CollisionMask = 127;
            AddChild(col);
            BuildDummy();

            _cam = new Camera3D { Current = true, Fov = 70f };
            _cam.Position = new Vector3(0, 1.7f, 0);
            AddChild(_cam);

            Limbs = new LimbRig();
            AddChild(Limbs);
        }

        void BuildDummy()
        {
            var coat = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.19f, 0.24f), Roughness = 0.95f };
            var skin = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.62f, 0.5f), Roughness = 1f };
            var pants = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.25f, 0.3f), Roughness = 1f };

            void Piece(Mesh m, Vector3 pos, Material mat)
            {
                var mi = new MeshInstance3D { Mesh = m, MaterialOverride = mat };
                AddChild(mi);
                mi.Position = pos;
            }
            Piece(new BoxMesh { Size = new Vector3(0.46f, 0.7f, 0.64f) }, new Vector3(0, 1.2f, 0), coat);
            Piece(new SphereMesh { Radius = 0.15f, Height = 0.3f }, new Vector3(0, 1.8f, 0), skin);
            Piece(new BoxMesh { Size = new Vector3(0.17f, 0.8f, 0.17f) }, new Vector3(-0.12f, 0.45f, 0), pants);
            Piece(new BoxMesh { Size = new Vector3(0.17f, 0.8f, 0.17f) }, new Vector3(0.12f, 0.45f, 0), pants);
            Piece(new BoxMesh { Size = new Vector3(0.14f, 0.6f, 0.14f) }, new Vector3(-0.34f, 1.22f, 0), coat);
            Piece(new BoxMesh { Size = new Vector3(0.14f, 0.6f, 0.14f) }, new Vector3(0.34f, 1.22f, 0), coat);
        }

        public float SpeedPercent()
        {
            float load = Limbs?.TotalMassKg() ?? 0f;
            return Mathf.Clamp(1f - (load / CarryLimitKg) * 0.6f, 0.35f, 1f);
        }

        public void ZapShake() => _shakeT = 0.7f;

        static bool Held(Key k) => Input.IsPhysicalKeyPressed(k);

        public override void _PhysicsProcess(double delta)
        {
            float d = (float)delta;

            if (_shakeT > 0f)
            {
                _shakeT -= d;
                _cam.HOffset = (float)GD.RandRange(-0.05, 0.05);
                _cam.VOffset = (float)GD.RandRange(-0.05, 0.05);
                if (_shakeT <= 0f) { _cam.HOffset = 0f; _cam.VOffset = 0f; }
            }

            bool ui = UiStack.Blocking;

            if (IsOnFloor()) { _vy = -0.5f; if (!ui && Held(Key.Space)) _vy = 4.4f; }
            else _vy -= 9.8f * d;

            Vector3 horiz = Vector3.Zero;
            if (!ui)
            {
                var fwd = -GlobalTransform.Basis.Z;
                var right = GlobalTransform.Basis.X;
                var move = Vector3.Zero;
                if (Held(Key.W)) move += fwd;
                if (Held(Key.S)) move -= fwd;
                if (Held(Key.D)) move += right;
                if (Held(Key.A)) move -= right;
                if (move != Vector3.Zero) horiz = move.Normalized() * (BaseSpeed * SpeedPercent());

                if (Input.IsMouseButtonPressed(MouseButton.Left)) RayHeat(70f * d);
                if (Input.IsMouseButtonPressed(MouseButton.Right)) RayHeat(-70f * d);
            }

            Velocity = new Vector3(horiz.X, _vy, horiz.Z);
            MoveAndSlide();
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (UiStack.Blocking) return;

            if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                RotateY(-mm.Relative.X * 0.0025f);
                _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * 0.0025f, -1.35f, 1.35f);
                _cam.Rotation = new Vector3(_pitch, 0, 0);
                return;
            }
            if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
            if (k.PhysicalKeycode == Key.T)
            {
                GetViewport().SetInputAsHandled();
                Shove();
            }
        }

        void Shove()
        {
            var from = _cam.GlobalPosition;
            var to = from + -_cam.GlobalTransform.Basis.Z * 4f;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
            if (hit.Count == 0 || hit["collider"].Obj is not WorldObject obj) return;
            var dir = (-GlobalTransform.Basis.Z + Vector3.Up * 0.15f).Normalized();
            obj.ApplyCentralImpulse(dir * (3f + obj.Mass * 4f));
        }

        void RayHeat(float dC)
        {
            var from = _cam.GlobalPosition;
            var to = from + -_cam.GlobalTransform.Basis.Z * 8f;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
            if (hit.Count == 0 || hit["collider"].Obj is not WorldObject obj) return;
            obj.HeatPart(obj.PartAtWorld(hit["position"].AsVector3()), dC);
        }
    }
}