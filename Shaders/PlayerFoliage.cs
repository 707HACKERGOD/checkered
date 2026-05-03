using Godot;

public partial class PlayerFoliage : GpuParticles3D
{
    private CharacterBody3D _player;
    private bool _wasOnFloor = true;
    private float _burstTimer = 0.0f;

    public override void _Ready()
    {
        _player = GetParent() as CharacterBody3D;
        OneShot = false;
        Emitting = false;
        Amount = 64;
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;

        // Season check
        bool isAutumn = TimeManager.Instance?.CurrentSeason == Season.AUTUMN;
        if (!isAutumn)
        {
            Emitting = false;
            _wasOnFloor = _player.IsOnFloor();
            return;
        }

        bool isOnFloor = _player.IsOnFloor();
        float speed = _player.Velocity.Length();

        ShaderMaterial mat = ProcessMaterial as ShaderMaterial;

        // FIX Bug 3: Burst fires on LANDING (was airborne, now on floor)
        // This matches the visual expectation: you kick leaves when you land, not when you jump.
        if (isOnFloor && !_wasOnFloor)
        {
            _burstTimer = 0.25f;

            if (mat != null) mat.SetShaderParameter("spawn_mode", 1); // Land burst

            Explosiveness = 0.95f;
            Restart();
        }

        _wasOnFloor = isOnFloor;

        // FIX Bug 3: Walk scatter ONLY when on floor
        if (_burstTimer <= 0.0f)
        {
            // Only emit walking particles when actually on the ground and moving
            if (isOnFloor && speed > 0.5f)
            {
                Emitting = true;
                Explosiveness = 0.0f;
                if (mat != null) mat.SetShaderParameter("spawn_mode", 0); // Walk kick
            }
            else
            {
                Emitting = false;
            }
        }
        else
        {
            _burstTimer -= (float)delta;
            // Keep burst particles running during burst timer
            if (_burstTimer <= 0.0f)
            {
                Emitting = false; // Stop after burst
            }
        }
    }
}