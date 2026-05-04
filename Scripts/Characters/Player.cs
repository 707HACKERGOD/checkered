using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
    // Pinch‑to‑zoom
    private int _pinch0 = -1;
    private int _pinch1 = -1;
    private float _pinchBaseDist;
    private float _pinchBaseZoom;
    private Dictionary<int, Vector2> _touchStartPositions = new();

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
        // Track all touch positions for pinch detection
        TouchTracker.Update(@event);
        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
                _touchStartPositions[touch.Index] = touch.Position;
            else
                _touchStartPositions.Remove(touch.Index);
        }

        // 1. Pinch zoom (two fingers) – highest priority
        if (HandlePinchZoom(@event))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        // 2. Camera look (only if single finger and not joystick finger)
        if (HandleCameraLook(@event))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        // ----- Menu / action handling -----
        bool anyMenuOpen = (HUD.Instance != null && HUD.Instance.IsInventoryOpen) ||
                        (HUD.Instance != null && HUD.Instance.IsHealthPanelOpen);
        if (HUD.Instance != null && HUD.Instance.IsGamePaused)
            return;

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

        if (@event.IsActionPressed("ui_accept"))
        {
            // debug damage
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
    bool sprinting = false;

    if (anyMenuOpen)
    {
        // When menu is open, use auto‑run direction (stored from when menu opened)
        inputDir = GameState.Instance.AutoRunDirection;
        speed = GameState.Instance.AutoRunSprinting ? RunSpeed : WalkSpeed;
    }
    else
    {
        // Normal gameplay: get input from mobile joystick or keyboard
        if (DisplayServer.IsTouchscreenAvailable())
        {
            inputDir = MobileInput.MovementDirection;
            float mag = inputDir.Length();
            sprinting = mag > 0.85f;
            // map magnitude to speed: up to 0.85 = walk, 0.85+ = run/sprint
            speed = Mathf.Lerp(WalkSpeed, sprinting ? RunSpeed : WalkSpeed, Mathf.InverseLerp(0.2f, sprinting ? 1f : 0.85f, mag));
            inputDir = mag > 0.001f ? inputDir.Normalized() : Vector2.Zero;
        }
        else
        {
            inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            sprinting = Input.IsActionPressed("sprint");
        }
        speed = sprinting ? RunSpeed : WalkSpeed;
        canTurn180 = Input.IsActionJustPressed("turn_180");
        
        // Store for auto‑run if a menu is opened later
        GameState.Instance.AutoRunDirection = inputDir;
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

    private bool IsAnyMenuOpen()
    {
        return HUD.Instance != null && HUD.Instance.IsGamePaused;
    }

    private bool HandlePinchZoom(InputEvent @event)
    {
        if (@event is InputEventScreenTouch t)
        {
            if (t.Pressed)
            {
                // Only fingers that started in free area can be part of pinch
                if (!IsInFreeArea(t.Index))
                    return false;

                if (_pinch0 == -1)
                    _pinch0 = t.Index;
                else if (_pinch1 == -1 && t.Index != _pinch0)
                {
                    _pinch1 = t.Index;
                    if (TouchTracker.TryGet(_pinch0, out Vector2 p0) &&
                        TouchTracker.TryGet(_pinch1, out Vector2 p1))
                    {
                        _pinchBaseDist = p0.DistanceTo(p1);
                        _pinchBaseZoom = _targetZoom;
                    }
                    return true;   // immediately claim the two fingers
                }
            }
            else
            {
                if (t.Index == _pinch0) _pinch0 = -1;
                if (t.Index == _pinch1) _pinch1 = -1;
            }
            return false;
        }

        // Process ongoing pinch drag
        if (_pinch0 != -1 && _pinch1 != -1 &&
            TouchTracker.TryGet(_pinch0, out Vector2 cur0) &&
            TouchTracker.TryGet(_pinch1, out Vector2 cur1))
        {
            float curDist = cur0.DistanceTo(cur1);
            if (curDist > 0.01f && _pinchBaseDist > 0.01f)
            {
                float scale = curDist / _pinchBaseDist;
                _targetZoom = Mathf.Clamp(_pinchBaseZoom / scale, _minZoom, _maxZoom);
            }
            return true;
        }
        return false;
    }

    private bool HandleCameraLook(InputEvent @event)
    {
        if (_isLockedOn || IsAnyMenuOpen()) return false;

        // Don't allow camera look while pinch is active
        if (_pinch0 != -1 && _pinch1 != -1) return false;

        if (@event is InputEventMouseMotion mouse && !DisplayServer.IsTouchscreenAvailable())
        {
            RotateCamera(mouse.Relative);
            return true;
        }

        if (@event is InputEventScreenDrag drag &&
            DisplayServer.IsTouchscreenAvailable() &&
            IsInFreeArea(drag.Index) &&                       // <-- uses start position
            drag.Index != VirtualJoystick.ActiveTouchIndex)
        {
            RotateCamera(drag.Relative);
            return true;
        }
        return false;
    }

    private void RotateCamera(Vector2 relative)
    {
        _cameraGimbal.RotateY(-relative.X * MouseSensitivity);
        _innerGimbal.RotateX(-relative.Y * MouseSensitivity);
        Vector3 rot = _innerGimbal.Rotation;
        rot.X = Mathf.Clamp(rot.X, MinPitch, MaxPitch);
        _innerGimbal.Rotation = rot;
    }

    private bool IsInFreeArea(int touchIndex)
    {
        if (!_touchStartPositions.TryGetValue(touchIndex, out Vector2 startPos))
            return false;

        // Check joystick (using the static reference)
        if (MobileUIController.Joystick != null && MobileUIController.Joystick.GetGlobalRect().HasPoint(startPos))
            return false;

        // Check all mobile buttons
        foreach (var container in MobileUIController.ButtonContainers)
        {
            if (container != null && container.GetGlobalRect().HasPoint(startPos))
                return false;
        }
        return true;
    }

    public void ToggleCamera()
    {
        _isFirstPerson = !_isFirstPerson;
        if (_eyeTracker != null) _eyeTracker.EnableHeadTracking = !_isFirstPerson;
    }
}