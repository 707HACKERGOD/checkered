using Godot;

public partial class WeatherSystem : Node3D
{
    // In the editor, drag your player node here.
    [Export]
    private Node3D _target;

    public override void _Process(double delta)
    {
        if (_target != null)
        {
            // Match the weather system's position to the player's position every frame.
            GlobalPosition = _target.GlobalPosition;
        }
    }
}
