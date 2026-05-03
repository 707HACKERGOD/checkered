using Godot;
using System;

public partial class SanitySystem : Node
{
    [Signal] public delegate void SanityChangedEventHandler(float newValue);
    [Signal] public delegate void PossessionTriggeredEventHandler();

    public float Sanity = 100.0f;
    public float Comfort = 0.0f;
    
    // References
    private TimeManager _timeManager;
    private Node3D _player;

    public override void _Ready()
    {
        _timeManager = GetNode<TimeManager>("/root/TimeManager"); // Assuming Autoload name
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float sanityMod = 0;

        // Base decay
        sanityMod -= 0.5f * dt / 60.0f;

        // Apply
        ModifySanity(sanityMod);
    }

    public void ModifySanity(float amount)
    {
        Sanity = Mathf.Clamp(Sanity + amount, 0, 100);
        EmitSignal(SignalName.SanityChanged, Sanity);
        
        // Trigger visual effects based on Sanity here
        // e.g. GetNode<Camera3D>("Player/Camera").Attributes.Distortion = ...
    }
}
