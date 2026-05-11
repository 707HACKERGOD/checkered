using Godot;
public partial class NavAgentNPC : NavigationAgent3D
{
    [Export] public float MoveSpeed = 3.0f;
    [Export] public float TurnSpeed = 10.0f;

    private CharacterBody3D _owner;

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
        if (IsNavigationFinished()) return;

        Vector3 nextPos = GetNextPathPosition();
        Vector3 dir = _owner.GlobalPosition.DirectionTo(nextPos);
        _owner.Velocity = new Vector3(dir.X * MoveSpeed, _owner.Velocity.Y, dir.Z * MoveSpeed);
        _owner.MoveAndSlide();

        // Smooth rotation
        Vector3 hv = new Vector3(_owner.Velocity.X, 0, _owner.Velocity.Z);
        if (hv.Length() > 0.1f)
        {
            Vector3 moveDir = hv.Normalized();
            Vector3 forward = _owner.GlobalTransform.Basis.Z;
            float angle = forward.AngleTo(moveDir);
            if (angle > 0.001f)
            {
                float turn = TurnSpeed * (float)delta;
                float fraction = Mathf.Clamp(turn / angle, 0f, 1f);
                _owner.Quaternion = _owner.Quaternion.Slerp(new Quaternion(Vector3.Back, moveDir), fraction);
            }
        }
    }

    public void SetNewTarget(Vector3 destination) => TargetPosition = destination;
}