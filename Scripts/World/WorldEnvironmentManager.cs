using Godot;

public partial class WorldEnvironmentManager : Node
{
    [Export] private DirectionalLight3D _sun;
    [Export] private WorldEnvironment _worldEnvironment;
    [Export] private Gradient _sunColorGradient;

    public override void _Ready()
    {
        // Auto-recover if export references were lost during scene reorganization
        if (_sun == null)
        {
            _sun = GetNodeOrNull<DirectionalLight3D>("../DirectionalLight3D")
                ?? GetTree()?.GetFirstNodeInGroup("sun") as DirectionalLight3D;
        }

        if (_worldEnvironment == null)
        {
            _worldEnvironment = GetNodeOrNull<WorldEnvironment>("../WorldEnvironment")
                ?? GetTree()?.GetFirstNodeInGroup("world_env") as WorldEnvironment;
        }

        if (_sun == null)
        {
            GD.PrintErr("WorldEnvironmentManager: _sun is null. Re-assign it in the Inspector.");
            return;
        }

        if (_sunColorGradient == null)
        {
            GD.PrintErr("WorldEnvironmentManager: No sun color gradient assigned!");
            return;
        }

        TimeManager timeManager = GetNodeOrNull<TimeManager>("/root/TimeManager");
        if (timeManager != null)
            timeManager.TimeUpdated += OnTimeUpdated;
        else
            GD.PrintErr("WorldEnvironmentManager: TimeManager autoload not found!");
    }

    private void OnTimeUpdated(float timeOfDay)
    {
        if (_sun == null || _sunColorGradient == null) return;

        float sunAngle = timeOfDay * 360.0f;
        _sun.RotationDegrees = new Vector3(sunAngle - 90.0f, -30.0f, 0.0f);
        _sun.LightColor = _sunColorGradient.Sample(timeOfDay);
    }
}