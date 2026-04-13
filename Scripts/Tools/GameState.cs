using Godot;

public partial class GameState : Node
{
    public static GameState Instance { get; private set; }
    public float GameSpeed { get; set; } = 1.0f;
    public bool AutoRunActive { get; set; } = false;
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