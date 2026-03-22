using Godot;
using System;

public partial class WorldEnvironmentManager : Node
{
    // We need references to the nodes we want to control.
    // The [Export] tag lets us drag-and-drop them in the Godot editor.
    [Export] private DirectionalLight3D _sun;
    [Export] private WorldEnvironment _worldEnvironment;

    // We'll use a Gradient to define the sun's color throughout the day.
    [Export] private Gradient _sunColorGradient;

    public override void _Ready()
    {
        // Connect to the TimeManager's signal when the game starts.
        GetNode<TimeManager>("/root/TimeManager").TimeUpdated += OnTimeUpdated;
    }

    // This function will be called automatically every frame by the signal.
    private void OnTimeUpdated(float timeOfDay)
    {
        // --- Rotate the Sun ---
        // We'll rotate it around the X-axis. 360 degrees * timeOfDay.
        float sunAngle = timeOfDay * 360.0f;
        _sun.RotationDegrees = new Vector3(sunAngle - 90, -30, 0); // -90 to make noon be overhead, -30 for a slight tilt.

        // --- Update Sun Color ---
        // The gradient's Sample method takes a value from 0.0 to 1.0.
        _sun.LightColor = _sunColorGradient.Sample(timeOfDay);

        // --- Update Sky ---
        // (We can add sky color changes here later in the same way)
    }
}
