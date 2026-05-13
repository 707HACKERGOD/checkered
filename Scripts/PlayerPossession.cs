using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerPossession : Node
{
    [Export] public float PathUpdateInterval = 0.5f;
    [Export] private float _attackRange = 2.5f;
    [Export] private float _attackCooldown = 0.8f;
    [Export] private float _countdownDuration = 10f;   // seconds of warning before possession

    // Signals for the HUD to show countdown bar
    [Signal] public delegate void PossessionCountdownChangedEventHandler(float progress, bool active);

    private Player _player;
    private NavigationAgent3D _navAgent;
    private Node3D _currentTarget;
    private float _pathTimer;
    private bool _possessed;
    private float _attackTimer;

    // Countdown state
    private bool _countdownActive;
    private float _countdownTimer;

    public bool IsPossessed => _possessed;
    public bool IsCountdownActive => _countdownActive;

    public static event Action<bool> PossessionStateChanged;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        _navAgent = _player.GetNode<NavigationAgent3D>("NavAgentPlayer");
        _navAgent.MaxSpeed = _player.RunSpeed;
    }

    public void StartPossession()
    {
        _possessed = true;
        _pathTimer = 0f;
        PossessionStateChanged?.Invoke(true);
    }

    public void StartPossessionCountdown()
    {
        if (_possessed || _countdownActive) return;
        _countdownActive = true;
        _countdownTimer = _countdownDuration;

        // Freeze player movement during countdown
        _player.Velocity = Vector3.Zero;
        _navAgent.TargetPosition = _player.GlobalPosition;

        EmitSignal(SignalName.PossessionCountdownChanged, 0f, true);
    }

    public void StopPossession()
    {
        _possessed = false;
        _player.Velocity = Vector3.Zero;
        _navAgent.TargetPosition = _player.GlobalPosition;
        PossessionStateChanged?.Invoke(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // --- COUNTDOWN LOGIC (runs even if not possessed) ---
        if (_countdownActive)
        {
            _countdownTimer -= dt;
            float progress = 1f - (_countdownTimer / _countdownDuration);
            EmitSignal(SignalName.PossessionCountdownChanged, progress, true);

            if (_countdownTimer <= 0f)
            {
                _countdownActive = false;
                EmitSignal(SignalName.PossessionCountdownChanged, 1f, false);
                StartPossession();
            }
            return;   // skip everything else during countdown
        }

        // --- POSSESSION LOGIC ---
        if (!_possessed) return;

        _pathTimer -= dt;
        if (_pathTimer <= 0f)
        {
            _pathTimer = PathUpdateInterval;
            UpdateTarget();
        }

        if (_currentTarget == null)
        {
            _player.Velocity = Vector3.Zero;
            return;
        }

        _navAgent.TargetPosition = _currentTarget.GlobalPosition;
        if (_navAgent.IsNavigationFinished()) return;

        Vector3 nextPos = _navAgent.GetNextPathPosition();
        Vector3 dir = _player.GlobalPosition.DirectionTo(nextPos);
        _player.Velocity = new Vector3(dir.X * _player.RunSpeed, _player.Velocity.Y, dir.Z * _player.RunSpeed);
        _player.MoveAndSlide();

        // Rotate toward movement (-Z forward)
        Vector3 horizontalVelocity = new Vector3(_player.Velocity.X, 0, _player.Velocity.Z);
        if (horizontalVelocity.Length() > 0.1f)
        {
            Vector3 moveDir = horizontalVelocity.Normalized();
            float targetAngle = Mathf.Atan2(-moveDir.X, -moveDir.Z);
            float currentAngle = _player.Rotation.Y;
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, _player.TurnSpeed * dt);
            _player.Rotation = new Vector3(0, newAngle, 0);
        }

        // Auto‑attack
        if (_currentTarget != null && _player.GlobalPosition.DistanceTo(_currentTarget.GlobalPosition) < _attackRange)
        {
            _attackTimer -= dt;
            if (_attackTimer <= 0f)
            {
                _attackTimer = _attackCooldown;
                _player.PerformAttack();
            }
        }
    }

    private void UpdateTarget()
    {
        _currentTarget = FindClosestLivingNpc();
    }

    private Node3D FindClosestLivingNpc()
    {
        var npcs = GetTree().GetNodesInGroup("NPC");
        float minDist = float.MaxValue;
        Node3D closest = null;

        foreach (Node node in npcs)
        {
            Node3D body = node as CharacterBody3D ?? node.GetParent() as CharacterBody3D;
            if (body == null) continue;

            var health = body.GetNodeOrNull<Health>("Health");
            if (health != null && !health.IsDead)
            {
                float d = _player.GlobalPosition.DistanceTo(body.GlobalPosition);
                if (d < minDist)
                {
                    minDist = d;
                    closest = body;
                }
            }
        }
        return closest;
    }
}