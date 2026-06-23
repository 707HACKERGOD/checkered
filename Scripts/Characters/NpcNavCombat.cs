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
    private bool _isStunned = false;
    private float _stunTimer = 0f;
    private bool _hasCalledForHelp = false;

    public void SetStunned(float duration)
    {
        _isStunned = true;
        _stunTimer = duration;
    }

    private bool _inDialogue = false;
    public bool IsInDialogue => _inDialogue;

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
        if (_health.IsDead || _inDialogue) return;
        float dt = (float)delta;

        // Handle stun
        if (_isStunned)
        {
            _stunTimer -= dt;
            if (_stunTimer <= 0) _isStunned = false;
            _navAgent.MaxSpeed = 0;
            return; // Can't act while stunned
        }

        // Flee logic
        if ((_health.CurrentHealth / _health.MaxHealth) < FleeHealthFraction || _hasCalledForHelp)
        {
            FleeFromPlayer();
            
            // Call for help if not done already
            if (!_hasCalledForHelp)
            {
                _hasCalledForHelp = true;
                CallForHelp();
            }
            return;
        }

        // Chase logic
        _navAgent.SetNewTarget(_player.GlobalPosition);
        _navAgent.MaxSpeed = ChaseSpeed;

        if (_body.GlobalPosition.DistanceTo(_player.GlobalPosition) < AttackRange)
        {
            _attackTimer -= dt;
            if (_attackTimer <= 0f)
            {
                _attackTimer = AttackCooldown;
                PerformWildHaymaker();
            }
        }

        // ---- DIALOGUE MODE ----
        if (_inDialogue && !_health.IsDead)
        {
            // Face player smoothly
            if (_player != null)
            {
                Vector3 targetPos = new Vector3(_player.GlobalPosition.X, _body.GlobalPosition.Y, _player.GlobalPosition.Z);
                Vector3 direction = (targetPos - _body.GlobalPosition).Normalized();
                if (direction != Vector3.Zero)
                {
                    float targetYaw = Mathf.Atan2(direction.X, direction.Z);
                    Vector3 rot = _body.Rotation;
                    rot.Y = Mathf.LerpAngle(rot.Y, targetYaw, 10.0f * dt);
                    _body.Rotation = rot;
                }
            }
            _body.Velocity = Vector3.Zero;  // no movement
            return;
        }

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

    public void StartDialogue()
    {
        if (_health.IsDead) return;
        if (_inDialogue) return;
        _inDialogue = true;
        // Stop movement
        if (_navAgent != null)
            _navAgent.MaxSpeed = 0f;
        // Cancel current navigation
        _navAgent?.SetNewTarget(_body.GlobalPosition);
    }

    public void EndDialogue()
    {
        if (!_inDialogue) return;
        _inDialogue = false;
        if (_navAgent != null)
            _navAgent.MaxSpeed = ChaseSpeed;
        // Resume wandering if not possessed and not dead
        if (!_possessionActive && !_health.IsDead)
            PickWanderTarget();
    }

    private void PerformWildHaymaker()
    {
        // Telegraph heavily. Play "haymaker_windup" anim.
        // After 0.5s windup (handled via animation or timer), activate NPC's own MeleeHitbox
        GD.Print("NPC swings wildly!");
        // Face the player exactly
        Vector3 dir = (_player.GlobalPosition - _body.GlobalPosition).Normalized();
        _body.Rotation = new Vector3(0, Mathf.Atan2(-dir.X, -dir.Z), 0);
    }

    private void CallForHelp()
    {
        GD.Print("NPC shouts for help!");
        // Play shout animation
        // Find nearby neutral NPCs and set their persona to Strong/Aggressive
        foreach (Node node in GetTree().GetNodesInGroup("NPC"))
        {
            if (node is CharacterBody3D npc && npc != _body)
            {
                if (_body.GlobalPosition.DistanceTo(npc.GlobalPosition) < 15f)
                {
                    var combat = npc.GetNodeOrNull<NpcNavCombat>("NPCNavCombat");
                    if (combat != null && combat.Persona == AiPersona.Neutral)
                    {
                        combat.Persona = AiPersona.Strong; // Aggro nearby allies
                    }
                }
            }
        }
    }
}