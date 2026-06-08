using Godot;

public partial class WorldEnvironmentManager : Node
{
    [Export] private DirectionalLight3D _sun;
    [Export] private WorldEnvironment _worldEnvironment;
    [Export] private Gradient _sunColorGradient;

    private TimeManager _timeManager;

    public override void _Ready()
    {
        if (_sun == null)
            _sun = GetNodeOrNull<DirectionalLight3D>("../DirectionalLight3D")
                ?? GetTree()?.GetFirstNodeInGroup("sun") as DirectionalLight3D;

        if (_worldEnvironment == null)
            _worldEnvironment = GetNodeOrNull<WorldEnvironment>("../WorldEnvironment")
                ?? GetTree()?.GetFirstNodeInGroup("world_env") as WorldEnvironment;

        if (_sun == null)
        {
            GD.PrintErr("WorldEnvironmentManager: _sun is null.");
            return;
        }
        if (_sunColorGradient == null)
        {
            GD.PrintErr("WorldEnvironmentManager: No sun color gradient assigned!");
            return;
        }

        _timeManager = GetNodeOrNull<TimeManager>("/root/TimeManager");
        if (_timeManager != null)
            _timeManager.TimeUpdated += OnTimeUpdated;
        else
            GD.PrintErr("WorldEnvironmentManager: TimeManager not found.");
    }

    private void OnTimeUpdated(float timeOfDay)
    {
        if (_sun == null || _sunColorGradient == null) return;

        float hour = timeOfDay * 24f;
        float sunAngle = (hour - 6f) * (Mathf.Pi / 12f);

        // Match shader: rotated original formula
        Vector3 sunDir = new Vector3(
            Mathf.Cos(sunAngle),
            Mathf.Sin(sunAngle),
            0f
        ).Normalized();

        _sun.Transform = new Transform3D(Basis.LookingAt(-sunDir, Vector3.Up), _sun.Position);
        _sun.LightColor = _sunColorGradient.Sample(timeOfDay);
    }
}