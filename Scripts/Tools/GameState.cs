using Godot;
using System;

public partial class GameState : Node
{
    public static GameState Instance { get; private set; }
    
    public static event Action<float> TimeScaleChanged;

    private float _gameSpeed = 1.0f;
    public float GameSpeed
    {
        get => _gameSpeed;
        set
        {
            _gameSpeed = value;
            Engine.TimeScale = value;
            TimeScaleChanged?.Invoke(value);
        }
    }

    public bool AutoRunActive { get; set; } = false;
    public bool AutoRunSprinting { get; set; } = false;

    public Vector2 AutoRunDirection { get; set; } = Vector2.Zero;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }
    
}