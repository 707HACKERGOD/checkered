using Godot;

public partial class SanityCycle : Node
{
    [Signal] public delegate void SanityChangedEventHandler(float sanity);
    [Signal] public delegate void PossessionImminentEventHandler(float progress); // 0‑1
    [Signal] public delegate void PossessionStartedEventHandler();
    [Signal] public delegate void PossessionEndedEventHandler();

    public float Sanity { get; private set; } = 100f;
    public bool IsPossessed { get; private set; }
    public float CycleTimer { get; private set; } = 600f; // 10 minutes
    public float PossessionDuration { get; set; } = 30f;
    public float WarningDuration { get; set; } = 10f;
    public float CountdownProgress => 1f - (CycleTimer / WarningDuration);

    private bool _warningActive;

    public override void _Ready()
    {
        // No node lookup here – wait for main scene to load
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (IsPossessed) return;

        CycleTimer -= dt;

        if (CycleTimer <= WarningDuration && CycleTimer > 0 && !_warningActive)
        {
            _warningActive = true;
            EmitSignal(SignalName.PossessionImminent, CountdownProgress);
        }
        else if (_warningActive && CycleTimer > 0)
        {
            EmitSignal(SignalName.PossessionImminent, CountdownProgress);
        }
        else if (CycleTimer <= 0)
        {
            StartPossession();
        }
    }

    public void SetSanityDebug(float value)
    {
        Sanity = Mathf.Clamp(value, 0f, 100f);
        EmitSignal(SignalName.SanityChanged, Sanity);
    }

    public void StartPossession()
    {
        // Find the player and possession node on demand – safe now because
        // the main scene is already loaded by the time this method can be called
        var player = GetTree().Root.FindChild("Player", true, false) as Player;
        var playerPossession = player?.GetNodeOrNull<PlayerPossession>("PlayerPossession");

        if (playerPossession == null) return;

        _warningActive = false;
        IsPossessed = true;
        playerPossession.StartPossessionCountdown();
        EmitSignal(SignalName.PossessionStarted);
    }

    public void EndPossession()
    {
        IsPossessed = false;
        CycleTimer = 600f;
        EmitSignal(SignalName.PossessionEnded);
    }
}