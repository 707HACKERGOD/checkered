using Godot;
using System;

public partial class FootprintSpawner : Node3D
{
    [Export] private PackedScene _footprintScene;          // FootprintDecal.tscn
    [Export] private float _stepDistance = 0.7f;           // Distance between steps
    [Export] private float _sideOffset = 0.15f;            // Left/right offset from center
    [Export] private float _lifetime = 5.0f;               // How long footprints last

    [Export] private AudioStreamPlayer3D _footstepPlayer;  // For footstep sounds
    [Export] private AudioStream[] _footstepSnow;          // Snow footstep sounds
    [Export] private AudioStream[] _footstepLeaves;        // Autumn leaf footstep sounds
    // Add more arrays for other ground types as needed

    private CharacterBody3D _player;
    private Vector3 _lastPos;
    private bool _nextIsLeft = true;
    private TimeManager _timeManager;                       // Correct field name

    public override void _Ready()
    {
        _player = GetParent<CharacterBody3D>();
        _lastPos = _player.GlobalPosition;
        _timeManager = TimeManager.Instance;                // Assign TimeManager
    }

    public override void _PhysicsProcess(double delta)
    {
        // Only spawn footprints in winter for now
        if (_timeManager?.CurrentSeason != Season.WINTER)
            return;

        if (_player.IsOnFloor() && _player.Velocity.Length() > 0.5f)
        {
            if (_player.GlobalPosition.DistanceTo(_lastPos) >= _stepDistance)
            {
                SpawnFootprint();
                PlayFootstep();                              // Play sound
                _lastPos = _player.GlobalPosition;
                _nextIsLeft = !_nextIsLeft;
            }
        }
    }

    private void SpawnFootprint()
    {
        if (_footprintScene == null) return;

        Decal footprint = _footprintScene.Instantiate<Decal>();
        GetTree().Root.AddChild(footprint);

        // Position relative to player
        Vector3 forward = -_player.GlobalTransform.Basis.Z;
        Vector3 right = _player.GlobalTransform.Basis.X;
        float side = _nextIsLeft ? -1 : 1;
        Vector3 pos = _player.GlobalPosition
                    + forward * 0.3f
                    + right * (_sideOffset * side);
        pos.Y += 0.05f;
        footprint.GlobalPosition = pos;

        // Random rotation (±10°)
        footprint.Rotation = new Vector3(
            Mathf.DegToRad(-90),
            0,
            (float)GD.RandRange(-0.2f, 0.2f)
        );

        // Start fade if the footprint has the script
        if (footprint is FootprintDecal fd)
            fd.StartFade(_lifetime);
        else
            footprint.GetTree().CreateTimer(_lifetime).Timeout += footprint.QueueFree;
    }

    private void PlayFootstep()
    {
        if (_footstepPlayer == null) return;

        // Choose a random footstep sound based on current season/ground
        AudioStream[] streams = _timeManager.CurrentSeason switch
        {
            Season.WINTER => _footstepSnow,
            Season.AUTUMN => _footstepLeaves,
            // Add other seasons when you have sounds
            _ => _footstepSnow // fallback
        };

        if (streams != null && streams.Length > 0)
        {
            _footstepPlayer.Stream = streams[GD.RandRange(0, streams.Length - 1)];
            _footstepPlayer.Play();
        }
    }
}