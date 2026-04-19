using Godot;
using System;

public partial class Player : CharacterBody3D
{
    // --- MOVEMENT ---
    [ExportGroup("Movement")]
    [Export] public float WalkSpeed = 5.0f;
    [Export] public float RunSpeed = 10.0f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float TurnSpeed = 12.0f;
    public float Gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    // --- CAMERA SETTINGS ---
    [ExportGroup("Camera & Target")]
    [Export] public Node3D LockOnTarget;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MinPitch = -Mathf.Pi / 3;
    [Export] public float MaxPitch = Mathf.Pi / 4;

    // Nodes
    private Node3D _cameraGimbal;
    private Node3D _innerGimbal;
    private SpringArm3D _springArm;
    [Export] public Camera3D PlayerCamera;

    [Export] private HUD _hud;
    [Export] private float _interactDistance = 3.0f;
    private InteractableItem _currentInteractable;

    // Tracking nodes
    private NpcEyeTracker _eyeTracker;
    private Area3D _interestArea;
    private Node3D _casualTarget;

    // Camera states
    private bool _isFirstPerson = false;
    private bool _isLockedOn = false;
    private float _targetZoom = 3.0f;
    private float _minZoom = 1.5f;
    private float _maxZoom = 6.0f;

    // --- ANIMATION ---
    private AnimationTree _animTree;
    private AnimationPlayer _animPlayer;
    private AnimationNodeStateMachinePlayback _stateMachine;
    private Timer _turnResetTimer;

    private readonly StringName _speedParam = "speed";
    private readonly StringName _turnParam = "turn_trigger";

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _cameraGimbal = GetNode<Node3D>("CameraGimbal");
        _innerGimbal = GetNode<Node3D>("CameraGimbal/InnerGimbal");
        _springArm = GetNode<SpringArm3D>("CameraGimbal/InnerGimbal/SpringArm");

        _springArm.SpringLength = _targetZoom;
        _cameraGimbal.TopLevel = true;

        // Eye tracker
        _eyeTracker = GetNodeOrNull<NpcEyeTracker>("EyeTrackerComponent");
        _interestArea = GetNodeOrNull<Area3D>("InterestArea");
        if (_interestArea != null)
        {
            _interestArea.BodyEntered += OnInterestEntered;
            _interestArea.BodyExited += OnInterestExited;
        }

        // --- ANIMATION SETUP ---
        _animTree = GetNode<AnimationTree>("AnimationTree");
        if (_animTree != null)
        {
            _animTree.Active = true;
            _stateMachine = (AnimationNodeStateMachinePlayback)_animTree.Get("parameters/playback");

            _animPlayer = GetNode<AnimationPlayer>("syl_base_5/AnimationPlayer");
            if (_animPlayer != null)
            {
                if (_animPlayer.HasAnimation("Idle"))
                    _animPlayer.GetAnimation("Idle").LoopMode = Animation.LoopModeEnum.Linear;
                if (_animPlayer.HasAnimation("Run"))
                    _animPlayer.GetAnimation("Run").LoopMode = Animation.LoopModeEnum.Linear;
                if (_animPlayer.HasAnimation("Turn180Right"))
                    _animPlayer.GetAnimation("Turn180Right").LoopMode = Animation.LoopModeEnum.None;
            }

            _turnResetTimer = new Timer();
            _turnResetTimer.OneShot = true;
            AddChild(_turnResetTimer);
            _turnResetTimer.Timeout += () => _animTree.Set($"parameters/{_turnParam}", false);
        }
    }

    public override void _Input(InputEvent @event)
    {
        // 1. FREE LOOK (always allowed)
        if (@event is InputEventMouseMotion mouseMotion && !_isLockedOn)
        {
            _cameraGimbal.RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            _innerGimbal.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);

            Vector3 rot = _innerGimbal.Rotation;
            rot.X = Mathf.Clamp(rot.X, MinPitch, MaxPitch);
            _innerGimbal.Rotation = rot;
        }

        // 2. Block keyboard actions if inventory or health panel is open
        bool anyMenuOpen = (HUD.Instance != null && HUD.Instance.IsInventoryOpen) ||
                          (HUD.Instance != null && HUD.Instance.IsHealthPanelOpen);
        if (anyMenuOpen)
            return;

        // 3. Normal gameplay actions
        if (@event.IsActionPressed("toggle_camera"))
        {
            _isFirstPerson = !_isFirstPerson;
            if (_eyeTracker != null) _eyeTracker.EnableHeadTracking = !_isFirstPerson;
        }

        if (@event.IsActionPressed("zoom_in"))
            _targetZoom = Mathf.Max(_targetZoom - 0.5f, _minZoom);

        if (@event.IsActionPressed("zoom_out"))
            _targetZoom = Mathf.Min(_targetZoom + 0.5f, _maxZoom);

        if (@event.IsActionPressed("lock_on") && LockOnTarget != null)
            _isLockedOn = !_isLockedOn;

        if (@event.IsActionPressed("ui_accept")) // Debug damage
        {
            Health health = GetNode<Health>("Health");
            if (health != null)
            {
                var limbNames = new System.Collections.Generic.List<string>();
                foreach (Node child in health.GetChildren())
                {
                    if (child is LimbHealth limb && !limb.IsDestroyed)
                        limbNames.Add(limb.LimbName);
                }

                if (limbNames.Count > 0)
                {
                    int randomIndex = new Random().Next(limbNames.Count);
                    string limbName = limbNames[randomIndex];
                    LimbHealth limb = health.GetNode<LimbHealth>(limbName);
                    float damage = limb.MaxHealth * 0.1f;
                    health.TakeDamage(damage, limbName);
                }
                else
                {
                    health.TakeDamage(health.MaxHealth * 0.1f);
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta; // Engine.TimeScale is applied automatically by Godot

        _cameraGimbal.GlobalPosition = GlobalPosition + new Vector3(0, 1.5f, 0);
        Vector3 velocity = Velocity;

        bool anyMenuOpen = (HUD.Instance != null && HUD.Instance.IsInventoryOpen) ||
                           (HUD.Instance != null && HUD.Instance.IsHealthPanelOpen);

        // --- GRAVITY ---
        if (!IsOnFloor())
            velocity.Y -= Gravity * dt;

        // --- JUMP ---
        if (!anyMenuOpen && Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;

        // --- HORIZONTAL MOVEMENT ---
        Vector2 inputDir;
        float speed;
        bool canTurn180 = false;

        if (anyMenuOpen)
        {
            inputDir = GameState.Instance.AutoRunDirection;
            speed = GameState.Instance.AutoRunSprinting ? RunSpeed : WalkSpeed;
        }
        else
        {
            inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            bool sprinting = Input.IsActionPressed("sprint");
            speed = sprinting ? RunSpeed : WalkSpeed;
            canTurn180 = Input.IsActionJustPressed("turn_180");
            GameState.Instance.AutoRunSprinting = sprinting;
        }

        Vector3 direction = (_cameraGimbal.Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        float currentSpeed = velocity.Length();

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;

            bool isTurning = _stateMachine != null && _stateMachine.GetCurrentNode() == "Turn";
            if (!isTurning)
            {
                float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);
                Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, TurnSpeed * dt), 0);
            }
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, speed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, speed);
        }

        Velocity = velocity;
        MoveAndSlide();

        // --- ANIMATION UPDATE ---
        if (_animTree != null)
        {
            _animTree.Set($"parameters/{_speedParam}", currentSpeed);

            bool isStandingStill = currentSpeed < 0.1f && IsOnFloor();
            if (isStandingStill && canTurn180)
            {
                _animTree.Set($"parameters/{_turnParam}", true);

                if (_animPlayer != null && _animPlayer.HasAnimation("Turn180Right"))
                    _turnResetTimer.Start(_animPlayer.GetAnimation("Turn180Right").Length);
                else
                    _turnResetTimer.Start(0.5f);
            }
        }

        // Camera transitions (smooth even in slow-mo)
        UpdateCameraTransitions(dt);
        UpdateLockOn(dt);
        UpdateEyeTracker();

        // --- INTERACTION ---
        if (!anyMenuOpen && PlayerCamera != null)
        {
            var spaceState = GetWorld3D().DirectSpaceState;
            Vector3 origin = PlayerCamera.GlobalPosition;
            Vector3 end = origin - PlayerCamera.GlobalTransform.Basis.Z * _interactDistance;
            var query = PhysicsRayQueryParameters3D.Create(origin, end);
            query.CollisionMask = 2;
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;

            var result = spaceState.IntersectRay(query);
            InteractableItem newTarget = null;
            if (result.Count > 0 && result["collider"].AsGodotObject() is InteractableItem item)
                newTarget = item;

            if (newTarget != _currentInteractable)
            {
                _currentInteractable = newTarget;
                if (_hud != null)
                {
                    if (_currentInteractable != null)
                        _hud.ShowTooltipAtWorldPosition($"Pick up {_currentInteractable.Data.Name}", _currentInteractable.GlobalPosition, "E");
                    else
                        _hud.HideTooltip();
                }
            }

            if (Input.IsActionJustPressed("interact") && _currentInteractable != null)
            {
                _currentInteractable.Pickup();
                _currentInteractable = null;
                _hud?.HideTooltip();
            }
        }
        else if (_hud != null && _currentInteractable != null)
        {
            _hud.HideTooltip();
            _currentInteractable = null;
        }
    }

    private void UpdateCameraTransitions(float dt)
    {
        float desiredLength = _isFirstPerson ? 0.0f : _targetZoom;
        _springArm.SpringLength = Mathf.Lerp(_springArm.SpringLength, desiredLength, dt * 8.0f);

        if (PlayerCamera != null)
        {
            if (_isFirstPerson) PlayerCamera.SetCullMaskValue(4, false);
            else PlayerCamera.SetCullMaskValue(4, true);
        }
    }

    private void UpdateLockOn(float dt)
    {
        if (_isLockedOn && LockOnTarget != null)
        {
            Vector3 targetPos = LockOnTarget.GlobalPosition + new Vector3(0, 1.0f, 0);
            Vector3 lookDirection = _cameraGimbal.GlobalPosition.DirectionTo(targetPos);
            float targetRotationY = Mathf.Atan2(-lookDirection.X, -lookDirection.Z);

            Vector3 currentRot = _cameraGimbal.Rotation;
            currentRot.Y = Mathf.LerpAngle(currentRot.Y, targetRotationY, dt * 8.0f);
            _cameraGimbal.Rotation = currentRot;

            _innerGimbal.Rotation = new Vector3(Mathf.LerpAngle(_innerGimbal.Rotation.X, 0, dt * 3.0f), 0, 0);
        }
    }

    private void UpdateEyeTracker()
    {
        if (_eyeTracker == null) return;
        if (_isLockedOn && LockOnTarget != null)
            _eyeTracker.Target = LockOnTarget;
        else if (_casualTarget != null)
            _eyeTracker.Target = _casualTarget;
        else
            _eyeTracker.Target = null;
    }

    private void OnInterestEntered(Node3D body)
    {
        if (body != this && body.IsInGroup("NPC"))
            _casualTarget = body;
    }

    private void OnInterestExited(Node3D body)
    {
        if (body == _casualTarget)
            _casualTarget = null;
    }
}