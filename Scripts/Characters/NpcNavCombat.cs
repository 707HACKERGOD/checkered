using Godot;
using System;

public enum AiPersona { Strong, Weak, Neutral }

public partial class NpcNavCombat : Node
{
    [Export] public AiPersona Persona = AiPersona.Neutral;
    [Export] public float ChaseSpeed = 4.0f;
    [Export] public float FleeDistance = 15.0f;
    [Export] public float AttackRange = 2.0f;
    [Export] public float AttackCooldown = 1.0f;
    [Export] public float WanderRadius = 10.0f;
    [Export] public float WanderInterval = 3.0f;
    [Export] public float FleeHealthFraction = 0.5f;

    private NavAgentNPC _navAgent;          // sibling NavigationAgent3D
    private CharacterBody3D _body;          // root CharacterBody3D
    private Health _health;
    private Node3D _player;
    private bool _possessionActive;
    private float _wanderTimer;
    private float _attackTimer;

    public override void _Ready()
    {
        _body = GetParent<CharacterBody3D>();
        _navAgent = _body.GetNode<NavAgentNPC>("NavAgentNPC");
        _health = _body.GetNode<Health>("Health");
        _player = GetTree().Root.FindChild("Player", true, false) as Node3D;

        PlayerPossession.PossessionStateChanged += OnPossessionChanged;
        _wanderTimer = 0f;
        PickWanderTarget();
    }

    public override void _ExitTree()
    {
        PlayerPossession.PossessionStateChanged -= OnPossessionChanged;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_health.IsDead) return;

        float dt = (float)delta;

        if (!_possessionActive)
        {
            // Wander, ignore player
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                PickWanderTarget();
                _wanderTimer = WanderInterval;
            }
            return;   // NavAgentNPC handles movement
        }

        // ---- Possession active – choose behaviour ----
        switch (Persona)
        {
            case AiPersona.Strong:
                ChaseAndAttack(dt);
                break;
            case AiPersona.Weak:
                FleeFromPlayer();
                break;
            case AiPersona.Neutral:
                if ((_health.CurrentHealth / _health.MaxHealth) < FleeHealthFraction)
                    FleeFromPlayer();
                else
                    ChaseAndAttack(dt);
                break;
        }
    }

    private void ChaseAndAttack(float dt)
    {
        if (_player == null) return;
        _navAgent.SetNewTarget(_player.GlobalPosition);
        _navAgent.MaxSpeed = ChaseSpeed;

        if (_body.GlobalPosition.DistanceTo(_player.GlobalPosition) < AttackRange)
        {
            _attackTimer -= dt;
            if (_attackTimer <= 0f)
            {
                _attackTimer = AttackCooldown;
                GD.Print($"[{Name}] Attacks player!");   // placeholder
                // TODO: add actual combat move like pushback or punch animation
            }
        }
    }

    private void FleeFromPlayer()
    {
        if (_player == null) return;
        Vector3 away = (_body.GlobalPosition - _player.GlobalPosition).Normalized();
        _navAgent.SetNewTarget(_body.GlobalPosition + away * FleeDistance);
        _navAgent.MaxSpeed = ChaseSpeed;   // could be a faster flee speed
    }

    private void PickWanderTarget()
    {
        if (_health.IsDead) return;   // never wander when dead
        float angle = (float)GD.RandRange(0, Mathf.Tau);
        float dist = (float)GD.RandRange(WanderRadius * 0.5f, WanderRadius);
        Vector3 destination = _body.GlobalPosition + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
        _navAgent.SetNewTarget(destination);
        _navAgent.MaxSpeed = ChaseSpeed;
    }

    private void OnPossessionChanged(bool active)
    {
        _possessionActive = active;
        _attackTimer = 0f;
        if (!active && !_health.IsDead)   // return to wandering only if alive
            PickWanderTarget();
    }
}