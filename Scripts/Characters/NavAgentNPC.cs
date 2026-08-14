using Godot;

public partial class NavAgentNPC : NavigationAgent3D
{
    [Export] public float MoveSpeed = 3.0f;
    [Export] public float TurnSpeed = 10.0f;
    [Export] public bool FacesPositiveZ = false; // Set to false if your model faces -Z (Godot standard)

    [Export] public Vector3 KnockbackVelocity;

    private CharacterBody3D _owner;
    private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        _owner = GetParent<CharacterBody3D>();
        MaxSpeed = MoveSpeed;
        TargetDesiredDistance = 1.0f;
        PathDesiredDistance = 1.0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_owner == null) return;
        // Skip if NpcController is handling physics (stumble/dead)
        var npcCtrl = _owner.GetNodeOrNull<NpcController>(".");
        if (npcCtrl != null && (npcCtrl.IsStumbling || npcCtrl.IsDead))
            return;
            
        float dt = (float)delta;
        Vector3 velocity = _owner.Velocity;

        // FIX: APPLY GRAVITY! This stops them from flying away on stairs/slopes.
        if (!_owner.IsOnFloor())
        {
            velocity.Y -= _gravity * dt;
        }

        if (KnockbackVelocity.LengthSquared() > 0.01f)
        {
            velocity.X = KnockbackVelocity.X;
            velocity.Z = KnockbackVelocity.Z;
            // Apply upward component when airborne or launching
            if (!_owner.IsOnFloor() || KnockbackVelocity.Y > 0.1f)
                velocity.Y = KnockbackVelocity.Y;
            KnockbackVelocity = KnockbackVelocity.Lerp(Vector3.Zero, dt * 5f);
        }
        else if (!IsNavigationFinished())
        {
            Vector3 nextPos = GetNextPathPosition();
            Vector3 dir = _owner.GlobalPosition.DirectionTo(nextPos);
            velocity.X = dir.X * MoveSpeed;
            velocity.Z = dir.Z * MoveSpeed;
        }
        else
        {
            velocity.X = 0;
            velocity.Z = 0;
        }

        _owner.Velocity = velocity;
        _owner.MoveAndSlide();

        // Smooth rotation
        Vector3 hv = new Vector3(velocity.X, 0, velocity.Z);
        if (hv.Length() > 0.1f)
        {
            Vector3 moveDir = hv.Normalized();
            // FIX: Dynamic Atan2 based on which way the model faces
            float targetYaw = FacesPositiveZ 
                ? Mathf.Atan2(moveDir.X, moveDir.Z) 
                : Mathf.Atan2(-moveDir.X, -moveDir.Z);
                
            float currentYaw = _owner.Rotation.Y;
            _owner.Rotation = new Vector3(0, Mathf.LerpAngle(currentYaw, targetYaw, TurnSpeed * dt), 0);
        }
    }

    public void SetNewTarget(Vector3 destination) => TargetPosition = destination;

    public void ApplyKnockback(Vector3 direction, float force)
    {
        direction.Y = 0.3f; 
        KnockbackVelocity = direction.Normalized() * force;
    }
}