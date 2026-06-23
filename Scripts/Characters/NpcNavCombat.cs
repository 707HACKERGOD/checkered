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
    [Export] public bool FacesPositiveZ = false; // Match this with NavAgentNPC!

    private NavAgentNPC _navAgent;          
    private CharacterBody3D _body;          
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

        if (_isStunned)
        {
            _stunTimer -= dt;
            if (_stunTimer <= 0) _isStunned = false;
            _navAgent.MaxSpeed = 0;
            return; 
        }

        if (!_possessionActive)
        {
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                PickWanderTarget();
                _wanderTimer = WanderInterval;
            }
            return;   
        }

        if ((_health.CurrentHealth / _health.MaxHealth) < FleeHealthFraction || _hasCalledForHelp)
        {
            FleeFromPlayer();
            if (!_hasCalledForHelp)
            {
                _hasCalledForHelp = true;
                CallForHelp();
            }
            return;
        }

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
                PerformWildHaymaker();
            }
        }
    }

    private void FleeFromPlayer()
    {
        if (_player == null) return;
        Vector3 away = (_body.GlobalPosition - _player.GlobalPosition).Normalized();
        _navAgent.SetNewTarget(_body.GlobalPosition + away * FleeDistance);
        _navAgent.MaxSpeed = ChaseSpeed;   
    }

    private void PickWanderTarget()
    {
        if (_health.IsDead) return;   
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
        if (!active && !_health.IsDead)   
            PickWanderTarget();
    }

    public void StartDialogue()
    {
        if (_health.IsDead || _inDialogue) return;
        _inDialogue = true;
        if (_navAgent != null) _navAgent.MaxSpeed = 0f;
        _navAgent?.SetNewTarget(_body.GlobalPosition);
    }

    public void EndDialogue()
    {
        if (!_inDialogue) return;
        _inDialogue = false;
        if (_navAgent != null) _navAgent.MaxSpeed = ChaseSpeed;
        if (!_possessionActive && !_health.IsDead) PickWanderTarget();
    }

        private MeleeHitbox _npcMeleeHitbox; // Now set dynamically by NpcController

    public void SetHitbox(MeleeHitbox hitbox)
    {
        _npcMeleeHitbox = hitbox;
    }

    private void PerformWildHaymaker()
    {
        GD.Print("NPC swings wildly!");
        Vector3 dir = (_player.GlobalPosition - _body.GlobalPosition).Normalized();
        _body.Rotation = new Vector3(0, Mathf.Atan2(-dir.X, -dir.Z), 0);

        // If the NPC has a real hitbox assigned, use it!
        if (_npcMeleeHitbox != null)
        {
            _npcMeleeHitbox.StartSwing(ItemRegistry.GetWeapon(ImpactType.Fist));
        }
        else
        {
            // Fallback to distance check if hitbox failed to generate
            float dist = _body.GlobalPosition.DistanceTo(_player.GlobalPosition);
            if (dist <= AttackRange + 0.5f)
            {
                var playerHealth = _player.GetNodeOrNull<Health>("Health");
                if (playerHealth != null && !playerHealth.IsDead)
                {
                    playerHealth.TakeDamage(10f, "Torso");
                    var player = _player as CharacterBody3D;
                    if (player != null)
                    {
                        Vector3 knockback = (_player.GlobalPosition - _body.GlobalPosition).Normalized();
                        knockback.Y = 0;
                        player.Velocity = new Vector3(player.Velocity.X + knockback.X * 4.0f, player.Velocity.Y, player.Velocity.Z + knockback.Z * 4.0f);
                    }
                }
            }
        }
    }

    private void CallForHelp()
    {
        GD.Print("NPC shouts for help!");
        foreach (Node node in GetTree().GetNodesInGroup("NPC"))
        {
            if (node is CharacterBody3D npc && npc != _body)
            {
                if (_body.GlobalPosition.DistanceTo(npc.GlobalPosition) < 15f)
                {
                    var combat = npc.GetNodeOrNull<NpcNavCombat>("NPCNavCombat");
                    if (combat != null && combat.Persona == AiPersona.Neutral)
                    {
                        combat.Persona = AiPersona.Strong; 
                    }
                }
            }
        }
    }
}