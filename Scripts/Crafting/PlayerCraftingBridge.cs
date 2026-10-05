using Godot;

namespace Crafting
{
    // Glue between YOUR player and the crafting systems. Auto-added by
    // CraftingRuntime, but you can add it yourself as a child of your player
    // (recommended — then the exports are editable in the inspector).
    [GlobalClass]
    public partial class PlayerCraftingBridge : Node3D, ICraftingPlayer
    {
        [Export] public NodePath CameraPath;   // leave empty = use current viewport camera
        [Export] public Vector3 HeadOffset = new(0, 1.95f, 0);
        [Export] public Vector3 TorsoOffset = new(0, 1.25f, 0.32f);
        [Export] public Vector3 BackOffset = new(0, 1.25f, -0.32f);
        [Export] public Vector3 HandLOffset = new(-0.5f, 1.05f, 0.1f);
        [Export] public Vector3 HandROffset = new(0.5f, 1.05f, 0.1f);

        public Camera3D Cam { get; private set; }
        public LimbRig Limbs { get; private set; }

        float _shakeT;

        public override void _Ready()
        {
            ResolveCamera();
            Limbs = new LimbRig();
            Limbs.ConfigureOffsets(HeadOffset, TorsoOffset, BackOffset, HandLOffset, HandROffset);
            AddChild(Limbs);
        }

        void ResolveCamera()
        {
            if (CameraPath != null && !CameraPath.IsEmpty)
                Cam = GetNodeOrNull<Camera3D>(CameraPath);
            Cam ??= GetViewport().GetCamera3D();
        }

        public override void _Process(double delta)
        {
            if (Cam == null) ResolveCamera();   // your camera may spawn later than the bridge
            if (_shakeT <= 0f || Cam == null) return;
            _shakeT -= (float)delta;
            Cam.HOffset = (float)GD.RandRange(-0.05, 0.05);
            Cam.VOffset = (float)GD.RandRange(-0.05, 0.05);
            if (_shakeT <= 0f) { Cam.HOffset = 0f; Cam.VOffset = 0f; }
        }

        public void ZapShake() => _shakeT = 0.7f;

        // the player's physics body, so rays can exclude it
        public Rid GetRid() => (GetParent() as CollisionObject3D)?.GetRid() ?? default;
    }
}