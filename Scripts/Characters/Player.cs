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

    [Export] private HUD _hud;                 // Drag your HUD node here
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
        // In Player._Ready()
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

            // Get the AnimationPlayer directly by its path in the scene.
            // Adjust "syl_base_5" if your imported model node has a different name.
            _animPlayer = GetNode<AnimationPlayer>("syl_base_5/AnimationPlayer");
            if (_animPlayer == null)
            {
                GD.PrintErr("Player.cs: AnimationPlayer not found at syl_base_5/AnimationPlayer. Check node name.");
            }
            else
            {
                // Set looping for idle and run animations.
                if (_animPlayer.HasAnimation("Idle"))
                    _animPlayer.GetAnimation("Idle").LoopMode = Animation.LoopModeEnum.Linear;
                if (_animPlayer.HasAnimation("Run"))
                    _animPlayer.GetAnimation("Run").LoopMode = Animation.LoopModeEnum.Linear;
                if (_animPlayer.HasAnimation("Turn180Right"))
                    _animPlayer.GetAnimation("Turn180Right").LoopMode = Animation.LoopModeEnum.None;
            }

            // Timer to reset the turn trigger after the animation finishes.
            _turnResetTimer = new Timer();
            _turnResetTimer.OneShot = true;
            AddChild(_turnResetTimer);
            _turnResetTimer.Timeout += () => _animTree.Set($"parameters/{_turnParam}", false);
        }
    }

    public override void _Input(InputEvent @event)
    {
        // 1. FREE LOOK 
        if (@event is InputEventMouseMotion mouseMotion && !_isLockedOn)
        {
            _cameraGimbal.RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            _innerGimbal.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
            
            Vector3 rot = _innerGimbal.Rotation;
            rot.X = Mathf.Clamp(rot.X, MinPitch, MaxPitch);
            _innerGimbal.Rotation = rot;
        }

        // 2. TOGGLE 1ST/3RD PERSON
        if (@event.IsActionPressed("toggle_camera"))
        {
            _isFirstPerson = !_isFirstPerson;
            if (_eyeTracker != null) _eyeTracker.EnableHeadTracking = !_isFirstPerson;
        }

        // 3. ZOOMING 
        if (@event.IsActionPressed("zoom_in"))
            _targetZoom = Mathf.Max(_targetZoom - 0.5f, _minZoom);
            
        if (@event.IsActionPressed("zoom_out"))
            _targetZoom = Mathf.Min(_targetZoom + 0.5f, _maxZoom);

        // 4. TOGGLE LOCK-ON
        if (@event.IsActionPressed("lock_on") && LockOnTarget != null)
        {
            _isLockedOn = !_isLockedOn;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _cameraGimbal.GlobalPosition = this.GlobalPosition + new Vector3(0, 1.5f, 0);

        Vector3 velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= Gravity * (float)delta;

        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;

        // --- CAMERA-RELATIVE MOVEMENT ---
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = (_cameraGimbal.Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        float speed = Input.IsActionPressed("sprint") ? RunSpeed : WalkSpeed;

        // Determine current speed for animation
        float currentSpeed = velocity.Length();

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;

            // Only apply manual rotation if we are NOT in the turn animation (which uses root motion)
            bool isTurning = _stateMachine != null && _stateMachine.GetCurrentNode() == "Turn";
            if (!isTurning)
            {
                float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);
                Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, TurnSpeed * (float)delta), 0);
            }
        }
        else
        {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, speed);
        }

        Velocity = velocity;
        MoveAndSlide();

        // --- ANIMATION UPDATE ---
        if (_animTree != null)
        {
            // Update speed parameter for locomotion blending
            _animTree.Set($"parameters/{_speedParam}", currentSpeed);

            // Check for 180° turn while standing still
            bool isStandingStill = currentSpeed < 0.1f && IsOnFloor();
            if (isStandingStill && Input.IsActionJustPressed("turn_180"))
            {
                // Trigger the turn animation (set boolean true)
                _animTree.Set($"parameters/{_turnParam}", true);

                // Get the length of the turn animation to reset the trigger later
                if (_animPlayer != null && _animPlayer.HasAnimation("Turn180Right"))
                {
                    float turnLength = _animPlayer.GetAnimation("Turn180Right").Length;
                    _turnResetTimer.Start(turnLength);
                }
                else
                {
                    _turnResetTimer.Start(0.5f); // fallback
                }
            }
        }

        // Camera transitions
        UpdateCameraTransitions(delta);
        UpdateLockOn(delta);
        
        // Eye tracking
        UpdateEyeTracker();


        // Interaction raycast
        if (PlayerCamera != null)
        {
            var spaceState = GetWorld3D().DirectSpaceState;
            Vector3 origin = PlayerCamera.GlobalPosition;
            Vector3 end = origin - PlayerCamera.GlobalTransform.Basis.Z * _interactDistance;
            var query = PhysicsRayQueryParameters3D.Create(origin, end);
            query.CollisionMask = 2; // Items layer
            query.CollideWithAreas = true;  // Allows hitting the InteractableItem (Area3D)
            query.CollideWithBodies = true; // Allows hitting walls/floors (blocks line of sight)

            var result = spaceState.IntersectRay(query);

            InteractableItem newTarget = null;
            if (result.Count > 0)
            {
                var collider = result["collider"].AsGodotObject();
                if (collider is InteractableItem item)
                {
                    newTarget = item;
                    GD.Print($"Raycast hit: {item.Data.Name}");
                }
            }

            if (newTarget != _currentInteractable)
            {
                _currentInteractable = newTarget;
                if (_hud != null)
                {
                    if (_currentInteractable != null)
                    {
                        // Use the world position tooltip
                        _hud.ShowTooltipAtWorldPosition($"Pick up {_currentInteractable.Data.Name}", _currentInteractable.GlobalPosition, "E");
                        GD.Print($"Showing tooltip for {_currentInteractable.Data.Name}");
                    }
                    else
                    {
                        _hud.HideTooltip();
                        GD.Print("Hiding tooltip");
                    }
                }
            }

            if (Input.IsActionJustPressed("interact") && _currentInteractable != null)
            {
                _currentInteractable.Pickup();
                _currentInteractable = null;
                if (_hud != null) _hud.HideTooltip();
            }
        }
    }

    private void UpdateCameraTransitions(double delta)
    {
        float desiredLength = _isFirstPerson ? 0.0f : _targetZoom;
        _springArm.SpringLength = Mathf.Lerp(_springArm.SpringLength, desiredLength, (float)delta * 8.0f);

        if (PlayerCamera != null)
        {
            if (_isFirstPerson) PlayerCamera.SetCullMaskValue(4, false); 
            else PlayerCamera.SetCullMaskValue(4, true); 
        }
    }

    private void UpdateLockOn(double delta)
    {
        if (_isLockedOn && LockOnTarget != null)
        {
            Vector3 targetPos = LockOnTarget.GlobalPosition + new Vector3(0, 1.0f, 0);
            Vector3 lookDirection = _cameraGimbal.GlobalPosition.DirectionTo(targetPos);
            float targetRotationY = Mathf.Atan2(-lookDirection.X, -lookDirection.Z);
            
            Vector3 currentRot = _cameraGimbal.Rotation;
            currentRot.Y = Mathf.LerpAngle(currentRot.Y, targetRotationY, (float)delta * 8.0f);
            _cameraGimbal.Rotation = currentRot;
            
            _innerGimbal.Rotation = new Vector3(Mathf.LerpAngle(_innerGimbal.Rotation.X, 0, (float)delta * 3.0f), 0, 0);
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