using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody3D
{
    // ==================================================================
    //  MOVEMENT
    // ==================================================================
    [ExportGroup("Movement")]
    [Export] public bool DebugManualBlend = false;
    [Export] public bool DebugRootMotionTrace = false;
    private float _rmTraceTimer;
    [Export] public string HipsBoneName = "Hips";
    [Export] public bool RootMotionFlipZ = false;
    [Export] public float WalkSpeed = 2.0f;
    [Export] public float RunSpeed = 4.9f;
    [Export] public float SprintSpeed = 5.8f;
    [Export] public float CrouchSpeed = 1.2f;
    [Export] public float JumpVelocity = 3.0f;
    [Export] public float TurnSpeed = 12.0f;
    [Export] public float GroundAccel = 40.0f;
    [Export] public float GroundDecel = 25.0f;
    [Export] public float AirAccel = 8.0f;
    [Export] public float PushForce = 4.0f;
    [Export] public Skeleton3D Skeleton;
    [Export] public string RootMotionBoneName = "Root";
    private bool _rootMotionAvailable;
    private string _rootBoneTrack;

    [ExportSubgroup("Step Up")]
    [Export] public float StepHeight = 0.4f;
    [Export] public float StepCheckDistance = 0.3f;
    [Export] public int StepCollisionMask = 2;
    [Export] public CollisionShape3D ColCapsuleFull;
    [Export] public CollisionShape3D ColCapsuleCrouch;

    [ExportSubgroup("Root Motion")]
    [Export] public bool UseRootMotion = true;
    [Export] public float RootMotionScale = 1.0f;
    [Export] public bool MatchDesiredSpeed = false;
    [Export] public float BlendSmoothing = 8.0f;
    [Export] public float StandClearance = 1.9f;
    [Export] public float CrouchClearance = 1.25f;
    [Export] public CollisionShape3D BodyShape;
    [Export] public float StandShapeHeight = 1.8f;
    [Export] public float CrouchShapeHeight = 1.2f;
    private float _forcedCrouch;
    private float _airTime; private float _landImpact; private bool _wasOnFloor = true;

    [ExportSubgroup("Door Ram")]
    [Export] public float DoorRamForce = 12.0f;
    [Export] public float DoorPassThroughDuration = 0.6f;

    [ExportSubgroup("Sprint Dash")]
    [Export] public float DashSpeed = 3.5f;
    [Export] public float DashDuration = 0.25f;
    private float _stillTime;
    private float _moveTime;
    private float _pivotTimer;
    private float _pivotTargetYaw;
    private float _pivotTotal;
    private float _pivotStartYaw;
    private float _stopSpeed;
    private float _pivotCooldown;

    // ==================================================================
    //  TRAVERSAL
    // ==================================================================
    public enum TraverseMode { None, Climb, Wallhug, WallLean, Vault }

    [ExportGroup("Model Orientation")]
    [Export(PropertyHint.None, "Tick if your mesh/animations face +Z (Blender default).")]
    public bool ModelFacesPlusZ = false;
    [Export] public bool BlendFlipForward = false;
    [Export] public bool BlendFlipSide = false;
    [Export] public bool EnablePivotTurns = false;

    [ExportGroup("Traversal Settings")]
    [Export] public float ProbeLowHeight  = 0.55f;
    [Export] public float ProbeMidHeight  = 1.0f;
    [Export] public float ProbeHighHeight = 1.5f;
    [Export] public float WallGrabDistance = 0.85f;
    [Export] public float WallStickDistance = 0.55f;
    [Export] public float ClimbGrabMaxFallSpeed = 9.0f;
    [Export] public float ClimbExitCooldown = 0.4f;
    [Export] public float LedgeProbeHeight = 1.9f;
    [Export] public float BodyHalfHeight = 0.9f;
    [Export] public float ClimbSpeed = 1.5f;
    [Export] public float WallhugSpeed = 0.8f;
    [Export] public float ClimbLateralSpeed = 0.8f;
    [Export] public float MantleClipRise = 1.6f;
    [Export] public bool WallhugEnabled = true;
    [Export] public float WallhugExitHop = 2.0f;
    [Export] public float LeanIdleDelay = 2.0f;
    [Export] public float LeanDetectDistance = 0.7f;
    [Export] public float VaultMaxHeight = 0.9f;
    [Export] public float VaultMinSpeed = 1.0f;
    [Export] public uint WallProbeMask = 0xFFFFFFFF;

    [ExportGroup("Door & Hand IK")]
    [Export] public float DoorReachDistance = 1.4f;
    [Export] public float HandIKHoldTime = 0.45f;

    private TraverseMode _traverse = TraverseMode.None;
    private float _traverseTime;
    private float _climbCooldown;
    private float _leanTimer;
    private float _vaultTimer, _vaultLen;
    private Vector3 _vaultFrom, _vaultTo;
    private string _curClimbAnim = "";
    private readonly HashSet<string> _missingStates = new();
    private Vector3 _climbNormal;

    // --- wall contact from physics engine ---
    private bool _touchingWall;
    private Vector3 _wallNormalCached = Vector3.Zero;
    private Vector3 _lastWallNormal = Vector3.Zero;
    private string _wallColliderName = "";
    private bool _rayDiagDone;

    // --- traversal grace / debounce ---
    private float _noContactTime;
    private string _pendingTravAnim = "";
    private float _travAnimHold;
    private AnimationNodeStateMachine _sm;

    [Export] public bool DebugTraversal = false;
    private float _trvDbg;

    private bool IsClimbing  => _traverse == TraverseMode.Climb;
    private bool IsVaulting  => _traverse == TraverseMode.Vault;
    private bool IsLeaning   => _traverse == TraverseMode.WallLean;

    private float _feetOffset = 0.9f;
    private Vector3 FeetPos()  => GlobalPosition + Vector3.Down * _feetOffset;
    private Vector3 ChestPos() => FeetPos() + Vector3.Up * ProbeMidHeight;

    private float YawFromDir(Vector3 dir) => ModelFacesPlusZ
        ? Mathf.Atan2(dir.X, dir.Z)
        : Mathf.Atan2(-dir.X, -dir.Z);

    private Vector3 FacingDir()
    {
        Vector3 f = ModelFacesPlusZ ? GlobalTransform.Basis.Z : -GlobalTransform.Basis.Z;
        f.Y = 0f;
        return f.LengthSquared() > 1e-6f ? f.Normalized() : Vector3.Forward;
    }

    // ==================================================================
    //  FOOT & LEG IK
    // ==================================================================
    [ExportGroup("Foot&Leg IK")]
    [Export] public Node3D visual_for_IK;
    [Export] public TwoBoneIK3D ik_leg_left;
    [Export] public TwoBoneIK3D ik_leg_right;
    [Export] public RayCast3D ray_leg_left_front;
    [Export] public RayCast3D ray_leg_right_front;
    [Export] public RayCast3D ray_leg_left_back;
    [Export] public RayCast3D ray_leg_right_back;
    [Export] public Marker3D target_leg_left;
    [Export] public Marker3D target_leg_right;
    [Export] public bool ik_is_enabled = true;
    [Export(PropertyHint.Range, "0.0,1.0,0.05")] public float front_ray_weight { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float pos_y_height_up { get; set; } = 0.11f;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float pos_y_height_flat { get; set; } = 0.11f;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float pos_y_height_down { get; set; } = 0.1f;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float slope_threshold { get; set; } = -0.02f;
    [Export(PropertyHint.Range, "0,100,1.0")] public float ik_lerp_speed { get; set; } = 10.0f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float active_ik_influence { get; set; } = 1.0f;

    public float inactive_ik_influence = 0.0f;
    public float last_offset_l = 0.0f;
    public float last_offset_r = 0.0f;

    [ExportGroup("Foot rotation")]
    [Export] public bool rotate_foot_active { get; set; } = true;
    [Export] public SkeletonModifier3D copy_left_foot { get; set; }
    [Export] public SkeletonModifier3D copy_right_foot { get; set; }
    [Export] public Marker3D copy_rotate_left { get; set; }
    [Export] public Marker3D copy_rotate_right { get; set; }
    [Export] public RayCast3D ray_foot_left_front { get; set; }
    [Export] public RayCast3D ray_foot_left_back { get; set; }
    [Export] public RayCast3D ray_foot_right_front { get; set; }
    [Export] public RayCast3D ray_foot_right_back { get; set; }
    [Export] public float rotation_speed { get; set; } = 10.0f;
    [Export] public float rotation_influence { get; set; } = 1.0f;
    [Export] public Vector3 left_foot_rotate_offset { get; set; } = new Vector3(1, 0, 0);
    [Export] public Vector3 right_foot_rotate_offset { get; set; } = new Vector3(-5, 5, 0);

    public float Gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    [ExportGroup("Hand IK")]
    [Export] public TwoBoneIK3D ik_hand_right;
    [Export] public Marker3D target_hand_right;
    [Export] public float HandReachSpeed = 15.0f;
    [Export] public float HandRestSpeed = 10.0f;
    [Export] public Vector3 HandRestOffset = new(0.25f, 1.2f, 0.3f);

    // ==================================================================
    //  CAMERA
    // ==================================================================
    [ExportGroup("Camera")]
    [Export] public Node3D LockOnTarget;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MinPitch = -Mathf.Pi / 3;
    [Export] public float MaxPitch = Mathf.Pi / 4;
    [Export] public float MinZoom = 1.5f;
    [Export] public float MaxZoom = 6.0f;
    [Export] public float CameraSmoothing = 10.0f;
    [Export] public Vector3 CameraOffset = new(0, 1.5f, 0);
    [Export] public Camera3D PlayerCamera;
    [Export] public Node3D visual_for_camera;

    [ExportGroup("Combat & Interaction")]
    [Export] private MeleeHitbox _rightHandHitbox;
    [Export] private HUD _hud;
    [Export] private float _interactDistance = 3.0f;
    [Export] private AudioStream _attackSound;

    [ExportGroup("Vehicles")]

    private CarController _currentVehicle;
    private bool _isDriving;
    private Node3D _cameraTarget; // for following car

    // ==================================================================
    //  ANIMATION TREE PARAMETERS
    // ==================================================================
    private static readonly StringName PLocomotionBlend = "parameters/Locomotion/blend_position";
    private static readonly StringName PCrouchBlend     = "parameters/Crouch/blend_position";
    private static readonly StringName PIsCrouching     = "parameters/conditions/is_crouching";
    private static readonly StringName PIsStanding      = "parameters/conditions/is_standing";
    private static readonly StringName PIsOnFloor       = "parameters/conditions/is_on_floor";
    private static readonly StringName PIsJumping       = "parameters/conditions/is_jumping";
    private static readonly StringName PIsFalling       = "parameters/conditions/is_falling";

    private AnimationTree _animTree;
    private AnimationPlayer _animPlayer;
    private AnimationNodeStateMachinePlayback _stateMachine;
    private Skeleton3D _skeleton;

    private bool _isWalking;
    private bool _isCrouching;
    private float _targetSpeed;
    private Vector3 _moveDirWorld = Vector3.Zero;
    private Vector2 _blendPos = Vector2.Zero;
    private float _dashTimer;
    private Vector3 _dashDir = Vector3.Zero;

    private bool _rootMotionMissing;
    private bool _rootMotionWarned;
    private float _rmSilentTime;

    private Node3D _cameraGimbal;
    private Node3D _innerGimbal;
    private SpringArm3D _springArm;

    private NpcEyeTracker _eyeTracker;
    private Area3D _interestArea;
    private Node3D _casualTarget;
    private NpcInteraction _currentNpc;
    private InteractableItem _currentInteractable;

    private bool _isFirstPerson;
    private bool _isLockedOn;
    private float _targetZoom = 3.0f;

    private PlayerPossession _possession;
    public bool IsPossessed => _possession != null && _possession.IsPossessed;

    private ItemData _fistWeapon;
    private ItemData _pipeWeapon;
    private ItemData _chairWeapon;
    private ItemData _currentWeapon;
    private AudioStreamPlayer _attackAudioPlayer;
    private float _attackCooldownTimer;
    private float _attackTime = -1f;
    private float _attackLen = 0.6f;
    [Export] public string AttackClipName = "attack";

    private int _pinch0 = -1;
    private int _pinch1 = -1;
    private float _pinchBaseDist;
    private float _pinchBaseZoom;
    private readonly Dictionary<int, Vector2> _touchStartPositions = new();

    private Node3D _handOpenable;
    private Vector3 _handHandlePos;
    private float _handIKTimer;
    private readonly HashSet<string> _warnedOpenables = new();

    private int _ikWarned;
    private float _wallHoldTimer;
    private float _hugClimbTimer;
    private bool _vaultCrouchExit;

    // ==================================================================
    //  READY
    // ==================================================================
    public override void _Ready()
    {
        ColCapsuleFull.Disabled = false;
        ColCapsuleCrouch.Disabled = true;

        DetectFeetOffset();

        _skeleton = Skeleton
         ?? GetNodeOrNull<Skeleton3D>("Syl/char_grp/rig/Skeleton3D")
         ?? GetNodeOrNull<Skeleton3D>("Syl/char_grp/rig_mc/Skeleton3D");
        if (_skeleton == null)
            GD.PushWarning("Player: Skeleton3D not found — root motion will be transformed by the body basis (wrong if the model is scaled/rotated).");
        if (_skeleton != null)
            for (int i = 0; i < _skeleton.GetBoneCount(); i++)
                _skeleton.ResetBonePose(i);

        _possession = GetNodeOrNull<PlayerPossession>("PlayerPossession");
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _cameraGimbal = GetNode<Node3D>("CameraGimbal");
        _innerGimbal = GetNode<Node3D>("CameraGimbal/InnerGimbal");
        _springArm = GetNode<SpringArm3D>("CameraGimbal/InnerGimbal/SpringArm");
        _springArm.SpringLength = _targetZoom;
        _cameraGimbal.TopLevel = true;

        _eyeTracker = GetNodeOrNull<NpcEyeTracker>("EyeTrackerComponent");
        _interestArea = GetNodeOrNull<Area3D>("InterestArea");
        if (_interestArea != null)
        {
            _interestArea.BodyEntered += OnInterestEntered;
            _interestArea.BodyExited += OnInterestExited;
        }

        _animTree = GetNodeOrNull<AnimationTree>("AnimationTree");
        _animPlayer = GetNodeOrNull<AnimationPlayer>("Syl/AnimationPlayer");
        if (_animTree != null && _animPlayer != null)
        {
            StripBlendShapeTracks(_animPlayer);
            SetupRootMotion();
            ForceLoopModes();
            foreach (string clip in new[] { "climb_start", "climb_up_stand", "climb_end_crouch",
                                "climb_ladder_top", "vault_through_window",
                                "vault_down_from_crouch", "roll" })
                if (_animPlayer.HasAnimation(clip))
                    _animPlayer.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.None;
            _stateMachine = (AnimationNodeStateMachinePlayback)_animTree.Get("parameters/playback");
            _sm = _animTree.TreeRoot as AnimationNodeStateMachine;
            ReportMissingStates();
            AuditTransitions();
            _animTree.Active = true;
            _animTree.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Physics;
        }

        if (_attackSound != null)
        {
            _attackAudioPlayer = new AudioStreamPlayer();
            AddChild(_attackAudioPlayer);
        }

        _fistWeapon = ItemRegistry.GetWeapon(ImpactType.Fist);
        _pipeWeapon = ItemRegistry.GetWeapon(ImpactType.Pipe);
        _chairWeapon = ItemRegistry.GetWeapon(ImpactType.Chair);
        _currentWeapon = ItemRegistry.GetWeapon(ImpactType.Fist);

        if (TimeManager.Instance != null)
            TimeManager.Instance.CameraShakeTarget = _innerGimbal;

        if (!ik_is_enabled)
            DisableAllIkModifiers();

        // Add exceptions to all IK rays to avoid self-hits
        foreach (RayCast3D r in new[] { ray_leg_left_front, ray_leg_left_back, ray_leg_right_front,
                ray_leg_right_back, ray_foot_left_front, ray_foot_left_back,
                ray_foot_right_front, ray_foot_right_back })
            r?.AddExceptionRid(GetRid());

        GD.Print("Player v2 traversal ACTIVE");
    }

    private void DetectFeetOffset()
    {
        if (ColCapsuleFull is { Shape: CapsuleShape3D cap })
        {
            _feetOffset = cap.Height * 0.5f - ColCapsuleFull.Position.Y;
        }
        GD.Print($"Player: feet offset = {_feetOffset:F2}");
    }

    private void DisableAllIkModifiers()
    {
        if (ik_leg_left  != null) { ik_leg_left.Active = false;  ik_leg_left.Influence = 0f; }
        if (ik_leg_right != null) { ik_leg_right.Active = false; ik_leg_right.Influence = 0f; }
        if (copy_left_foot  != null) { copy_left_foot.Active = false;  copy_left_foot.Influence = 0f; }
        if (copy_right_foot != null) { copy_right_foot.Active = false; copy_right_foot.Influence = 0f; }
    }

    // ==================================================================
    //  INPUT
    // ==================================================================
    public override void _Input(InputEvent @event)
    {
        TouchTracker.Update(@event);
        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed) _touchStartPositions[touch.Index] = touch.Position;
            else _touchStartPositions.Remove(touch.Index);
        }

        if (PlayerInputOverride.Active) return;

        if (HandlePinchZoom(@event)) { GetViewport().SetInputAsHandled(); return; }
        if (HandleCameraLook(@event)) { GetViewport().SetInputAsHandled(); return; }

        bool anyMenuOpen = (HUD.Instance != null && HUD.Instance.IsInventoryOpen) ||
                           (HUD.Instance != null && HUD.Instance.IsHealthPanelOpen);
        if (HUD.Instance != null && HUD.Instance.IsGamePaused)
            return;

        if (@event.IsActionPressed("toggle_camera"))
            ToggleCamera();

        if (@event.IsActionPressed("zoom_in"))
            _targetZoom = Mathf.Max(_targetZoom - 0.5f, MinZoom);
        if (@event.IsActionPressed("zoom_out"))
            _targetZoom = Mathf.Min(_targetZoom + 0.5f, MaxZoom);

        if (MindEyeMode.ActiveNow) return;

        if (@event.IsActionPressed("lock_on") && LockOnTarget != null)
            _isLockedOn = !_isLockedOn;

        if (@event.IsActionPressed("attack") && !anyMenuOpen)
        {
            if (IsPossessed) { GetViewport().SetInputAsHandled(); return; }
            PerformAttack();
            GetViewport().SetInputAsHandled();
        }
    }

    // ==================================================================
    //  PHYSICS TICK
    // ==================================================================
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Vector3 velocity = Velocity;
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= dt;
        TickAttack(dt);

        bool anyMenuOpen = (HUD.Instance != null && HUD.Instance.IsInventoryOpen) ||
                           (HUD.Instance != null && HUD.Instance.IsHealthPanelOpen);
        bool inputLocked = anyMenuOpen || PlayerInputOverride.Active || PlayerInputOverride.MovementLock;

        UpdateCamera(dt);
        UpdateLockOn(dt);
        UpdateEyeTracker();
        if (_isDriving)
        {
            // The car moves the player – we only need interaction to exit.
            UpdateInteraction(anyMenuOpen);
            return;
        }
        if (IsPossessed) { Velocity = velocity; MoveAndSlide(); return; }

        UpdateLocomotionIntent(dt, anyMenuOpen);
        UpdateTraversal(dt, ref velocity, inputLocked);
        UpdateFacing(dt);
        ApplyMovement(dt, ref velocity);

        if (_traverse != TraverseMode.Climb && _traverse != TraverseMode.Vault && !IsOnFloor())
            velocity.Y -= Gravity * dt;
        if (_traverse == TraverseMode.None && !inputLocked &&
            Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;

        Velocity = velocity;
        MoveAndSlide();

        CacheWallContact();
        HandleDoorRam();
        HandleHandReach(dt);

        float fallSpeed = -velocity.Y;
        if (IsOnFloor())
        {
            if (!_wasOnFloor)
            {
                _landImpact = fallSpeed;
                TryPlayLand(_landImpact);
                _landImpact = 0f;
            }
            _airTime = 0f;
        }
        else
            _airTime += dt;
        _wasOnFloor = IsOnFloor();

        float hs = new Vector2(Velocity.X, Velocity.Z).Length();
        if (hs < 0.8f) { _stillTime += dt; _moveTime = 0f; }
        else           { _moveTime += dt;  _stillTime = 0f; }
        FloorSnapLength = hs > 2.0f ? 0.03f : 0.1f;

        UpdateAnimationParams(dt, anyMenuOpen);   // AFTER the move
        TraceAnimState();
        UpdateForcedCrouch(dt);
        RootMotionDebug(dt);
        PushRigidBodies();

        if (ik_is_enabled)
        {
            handle_leg_ik(dt);
            handle_foot_rotation(dt);
        }
        UpdateInteraction(inputLocked);
    }

    // ==================================================================
    //  LOCOMOTION INTENT
    // ==================================================================
    private void UpdateLocomotionIntent(float dt, bool anyMenuOpen)
    {
        if (PlayerInputOverride.Active)
        {
            Vector3 steer = PlayerInputOverride.WorldDirection;
            _moveDirWorld = steer.LengthSquared() > 1e-6f ? steer.Normalized() : Vector3.Zero;
            _targetSpeed = _moveDirWorld == Vector3.Zero ? 0f : PlayerInputOverride.SpeedMps;
            return;
        }

        if (PlayerInputOverride.MovementLock)
        {
            _moveDirWorld = Vector3.Zero;
            _targetSpeed = 0f;
            return;
        }

        Vector2 inputDir = Vector2.Zero;
        float analog = 0f;

        if (anyMenuOpen)
        {
            inputDir = GameState.Instance.AutoRunDirection;
            analog = inputDir.Length();
        }
        else if (DisplayServer.IsTouchscreenAvailable())
        {
            inputDir = MobileInput.MovementDirection;
            analog = Mathf.Clamp(inputDir.Length(), 0f, 1f);
        }
        else
        {
            inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            analog = Mathf.Clamp(inputDir.Length(), 0f, 1f);
        }

        if (analog > 0.001f) inputDir /= analog;
        else { inputDir = Vector2.Zero; analog = 0f; }

        if (!anyMenuOpen)
        {
            if (Input.IsActionJustPressed("walk_toggle")) _isWalking = !_isWalking;
            if (Input.IsActionJustPressed("crouch")) ToggleCrouch();
        }

        bool sprintHeld;
        if (anyMenuOpen) sprintHeld = GameState.Instance.AutoRunSprinting;
        else if (DisplayServer.IsTouchscreenAvailable()) sprintHeld = analog > 0.85f;
        else sprintHeld = Input.IsActionPressed("sprint");

        float baseSpeed;
        if (_isCrouching)                              baseSpeed = CrouchSpeed;
        else if (sprintHeld && analog > 0.1f)          baseSpeed = SprintSpeed;
        else if (_isWalking)                           baseSpeed = WalkSpeed;
        else                                           baseSpeed = RunSpeed;

        _targetSpeed = baseSpeed * analog;

        _moveDirWorld = _cameraGimbal.GlobalTransform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y);
        _moveDirWorld.Y = 0f;
        _moveDirWorld = _moveDirWorld.LengthSquared() > 1e-6f ? _moveDirWorld.Normalized() : Vector3.Zero;

        if (!anyMenuOpen && GameState.Instance != null)
        {
            GameState.Instance.AutoRunDirection = inputDir;
            GameState.Instance.AutoRunSprinting = sprintHeld;
        }

        if (!anyMenuOpen && _traverse == TraverseMode.None && Input.IsActionJustPressed("sprint") &&
            IsOnFloor() && _moveDirWorld != Vector3.Zero && !_isCrouching && !_isWalking)
        {
            _dashTimer = DashDuration;
            _dashDir = _moveDirWorld;
        }
    }

    // ==================================================================
    //  FACING
    // ==================================================================
    private void UpdateFacing(float dt)
    {
        _pivotCooldown -= dt;

        if (_traverse == TraverseMode.Climb || _traverse == TraverseMode.Wallhug)
        {
            float offset = 0f;
            if (_stateMachine != null)
            {
                string currentState = _stateMachine.GetCurrentNode();
                if (_stateOffsets.TryGetValue(currentState, out float deg))
                    offset = Mathf.DegToRad(deg);
            }
            float wallYaw = YawFromDir(-_climbNormal) + offset;
            Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, wallYaw, 14f * dt), 0f);
            return;
        }
        if (_traverse == TraverseMode.Vault || _traverse == TraverseMode.WallLean) return;

        if (_pivotTimer > 0f)
        {
            _pivotTimer -= dt;
            float k = Mathf.Clamp(1f - _pivotTimer / Mathf.Max(_pivotTotal, 0.01f), 0f, 1f);
            k = k * k * (3f - 2f * k);
            Rotation = new Vector3(0f, _pivotStartYaw + Mathf.AngleDifference(_pivotTargetYaw, _pivotStartYaw) * k, 0f);
            return;
        }

        Vector3 faceDir;
        if (_isLockedOn && LockOnTarget != null)
        {
            faceDir = LockOnTarget.GlobalPosition - GlobalPosition;
            faceDir.Y = 0f;
            if (faceDir.LengthSquared() < 0.001f) return;
        }
        else if (_moveDirWorld != Vector3.Zero) faceDir = _moveDirWorld;
        else return;

        float currentYaw = Rotation.Y;
        float targetYaw = YawFromDir(faceDir);
        Rotation = new Vector3(0f, Mathf.LerpAngle(currentYaw, targetYaw, TurnSpeed * dt), 0f);

        float diff = Mathf.AngleDifference(currentYaw, targetYaw);
        float speed = new Vector2(Velocity.X, Velocity.Z).Length();
        if (EnablePivotTurns && _pivotCooldown <= 0f && Mathf.Abs(Mathf.RadToDeg(diff)) > 110f && speed > 1.5f && IsOnFloor() && _stateMachine != null)
        {
            _stateMachine.Travel(Mathf.Abs(diff) > Mathf.DegToRad(155f) ? "Pivot180" : "Pivot90L");
            _pivotTimer = _animPlayer != null && _animPlayer.HasAnimation("Run_180")
                ? (float)_animPlayer.GetAnimation("Run_180").Length : 0.6f;
            _pivotTotal = _pivotTimer;
            _pivotStartYaw = currentYaw;
            _pivotTargetYaw = currentYaw + diff;
            _pivotCooldown = _pivotTimer + 0.5f;
        }
    }

    // ==================================================================
    //  APPLY MOVEMENT
    // ==================================================================
    private static readonly HashSet<string> RootMotionStates = new()
    { "run_start", "run_stop", "Pivot180", "Pivot90L",
      "climb_start", "climb_up_stand", "climb_end_crouch", "climb_ladder_top",
      "vault_through_window", "vault_down_from_crouch" };

    private void ApplyMovement(float dt, ref Vector3 velocity)
    {
        if (_traverse == TraverseMode.Vault) { velocity = Vector3.Zero; return; }

        // Climb: use root motion if available
        if (_traverse == TraverseMode.Climb)
        {
            if (UseRootMotion && _rootMotionAvailable && _animTree != null)
            {
                Vector3 rm = _animTree.GetRootMotionPosition();
                if (RootMotionFlipZ) rm = new Vector3(-rm.X, rm.Y, -rm.Z);
                velocity = GlobalTransform.Basis.Orthonormalized() * rm * (RootMotionScale / Mathf.Max(dt, 1e-4f))
                           + -_climbNormal * 0.3f;
            }
            else
            {
                // fallback: code-driven climb movement (should not happen if root motion available)
                velocity = Vector3.Zero;
            }
            return;
        }

        // Other traversal states (Wallhug, WallLean) are handled in their own ticks; they set velocity directly.
        if (_traverse != TraverseMode.None) return;

        // Remove the pivot early-return – let root motion handle pivots if needed
        // The pivot timer still controls rotation but not movement; we'll let velocity be set normally.

        string st = _stateMachine?.GetCurrentNode() ?? "";
        bool rmAllowed = UseRootMotion && _rootMotionAvailable && _animTree != null
                        && RootMotionStates.Contains(st);
        bool usedRootMotion = false;

        if (IsOnFloor())
        {
            if (rmAllowed)
            {
                Vector3 localDelta = _animTree.GetRootMotionPosition();
                if (RootMotionFlipZ)
                    localDelta = new Vector3(-localDelta.X, localDelta.Y, -localDelta.Z);
                UpdateRootMotionWatchdog(dt, localDelta);

                if (!_rootMotionMissing)
                {
                    usedRootMotion = true;
                    Basis basis = GlobalTransform.Basis.Orthonormalized();
                    Vector3 worldDelta = basis * localDelta;
                    Vector3 rmVel = new Vector3(worldDelta.X, 0f, worldDelta.Z)
                                    * (RootMotionScale / Mathf.Max(dt, 1e-5f));
                    float maxRm = Mathf.Max(SprintSpeed * 2.5f, 10f);
                    if (rmVel.LengthSquared() > maxRm * maxRm)
                        rmVel = rmVel.Normalized() * maxRm;
                    if (MatchDesiredSpeed && _targetSpeed > 0.05f && rmVel.LengthSquared() > 1e-8f)
                        rmVel = rmVel.Normalized() * _targetSpeed;
                    velocity.X = rmVel.X;
                    velocity.Z = rmVel.Z;

                    if (_dashTimer > 0f)
                    {
                        _dashTimer -= dt;
                        float k = Mathf.Max(_dashTimer, 0f) / DashDuration;
                        velocity.X += _dashDir.X * DashSpeed * k * k;
                        velocity.Z += _dashDir.Z * DashSpeed * k * k;
                    }
                }
            }

            if (!usedRootMotion)
            {
                // Clamp speed while attacking
                float speedMul = _attackTime >= 0f ? 0.35f : 1f;
                Vector3 target = _moveDirWorld * (_targetSpeed * speedMul);
                float accel = _moveDirWorld != Vector3.Zero ? GroundAccel : GroundDecel;
                velocity.X = Mathf.MoveToward(velocity.X, target.X, accel * dt);
                velocity.Z = Mathf.MoveToward(velocity.Z, target.Z, accel * dt);
            }

            if (_moveDirWorld != Vector3.Zero)
                TryStepUp();
        }
        else
        {
            _dashTimer = 0f;
            Vector3 target = _moveDirWorld * _targetSpeed;
            velocity.X = Mathf.MoveToward(velocity.X, target.X, AirAccel * dt);
            velocity.Z = Mathf.MoveToward(velocity.Z, target.Z, AirAccel * dt);
        }
    }

    private void UpdateRootMotionWatchdog(float dt, Vector3 localDelta)
    {
        if (localDelta.LengthSquared() > 1e-10f)
        {
            _rmSilentTime = 0f;
            _rootMotionMissing = false;
            return;
        }
        if (_blendPos.Length() < 0.5f)
        {
            _rmSilentTime = 0f;
            return;
        }
        _rmSilentTime += dt;
        if (_rmSilentTime > 0.5f)
        {
            _rootMotionMissing = true;
            if (!_rootMotionWarned)
            {
                _rootMotionWarned = true;
                GD.PushError("Root Motion Track returns zero while locomotion is blending. ...");
            }
        }
    }

    private void TryStepUp()
    {
        Vector3 stepOrigin = GlobalPosition + new Vector3(0, StepHeight, 0);
        Vector3 stepEnd = stepOrigin + _moveDirWorld * StepCheckDistance;
        var stepQuery = PhysicsRayQueryParameters3D.Create(stepOrigin, stepEnd);
        stepQuery.CollisionMask = (uint)StepCollisionMask;
        if (GetWorld3D().DirectSpaceState.IntersectRay(stepQuery).Count > 0)
            return;

        Vector3 downEnd = stepEnd + Vector3.Down * (StepHeight + 0.1f);
        var downQuery = PhysicsRayQueryParameters3D.Create(stepEnd, downEnd);
        downQuery.CollisionMask = (uint)StepCollisionMask;
        var downResult = GetWorld3D().DirectSpaceState.IntersectRay(downQuery);

        if (downResult.Count > 0)
        {
            Vector3 floorNormal = downResult["normal"].AsVector3();
            if (floorNormal.Y < 0.95f) return;
            float floorY = downResult["position"].AsVector3().Y;
            float stepUp = floorY - GlobalPosition.Y;
            if (stepUp > 0.05f && stepUp <= StepHeight)
                GlobalPosition = new Vector3(GlobalPosition.X, floorY, GlobalPosition.Z);
        }
    }

    private void ToggleCrouch()
    {
        if (_isCrouching && _forcedCrouch > 0.15f) return;
        _isCrouching = !_isCrouching;
        ApplyCrouchShape();
    }

    private void ApplyCrouchShape()
    {
        bool crouched = _isCrouching || _forcedCrouch > 0.5f;
        if (ColCapsuleFull != null) ColCapsuleFull.Disabled = crouched;
        if (ColCapsuleCrouch != null) ColCapsuleCrouch.Disabled = !crouched;
    }

    // ==================================================================
    //  ANIMATION
    // ==================================================================
    private void UpdateAnimationParams(float dt, bool anyMenuOpen)
    {
        if (_animTree == null) return;

        if (_traverse != TraverseMode.None)
        {
            _blendPos = _blendPos.Lerp(Vector2.Zero, 1f - Mathf.Exp(-BlendSmoothing * dt));
            _animTree.Set(PLocomotionBlend, _blendPos);
            _animTree.Set(PCrouchBlend, _blendPos);
            _animTree.Set(PIsOnFloor, true);
            _animTree.Set(PIsJumping, false);
            _animTree.Set(PIsFalling, false);
            return;
        }

        float horizSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        Vector2 blendTarget = Vector2.Zero;
        if (_moveDirWorld != Vector3.Zero)
        {
            Vector3 local = GlobalTransform.Basis.Inverse() * _moveDirWorld;
            Vector2 dir2 = ModelFacesPlusZ ? new Vector2(-local.X, local.Z) : new Vector2(local.X, -local.Z);
            if (BlendFlipForward) dir2.Y = -dir2.Y;
            if (BlendFlipSide)    dir2.X = -dir2.X;
            blendTarget = dir2.Normalized() * horizSpeed;   // measured, not intended
        }
        _blendPos = _blendPos.Lerp(blendTarget, 1f - Mathf.Exp(-BlendSmoothing * dt));

        if (DebugManualBlend)
        {
            _blendPos = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down") * 5.0f;
            _animTree.Set(PLocomotionBlend, _blendPos);
            return;
        }

        _animTree.Set(PLocomotionBlend, _blendPos);
        _animTree.Set(PCrouchBlend, _blendPos);

        bool crouched = _isCrouching || _forcedCrouch > 0.5f;
        _animTree.Set(PIsCrouching, crouched);
        _animTree.Set(PIsStanding, !crouched);
        _animTree.Set(PIsOnFloor, IsOnFloor());
        _animTree.Set(PIsJumping, !IsOnFloor() && Velocity.Y > 0.1f);
        _animTree.Set(PIsFalling, !IsOnFloor() && (_airTime > 0.25f || Velocity.Y < -3.0f));

        string st = _stateMachine?.GetCurrentNode() ?? "";
        bool wantsMove = _targetSpeed > 0.1f;

        if (st == "Locomotion")   // one request per one-shot, only from Locomotion
        {
            if (wantsMove && _stillTime > 0.30f)
            {
                _animTree.Set("parameters/Start/blend_position", Mathf.Clamp(_targetSpeed, 0.5f, 5.8f));
                _stateMachine.Travel("run_start");
            }
            else if (!wantsMove && horizSpeed > 1.2f && _moveTime > 0.05f)
            {
                _stopSpeed = horizSpeed;
                _animTree.Set("parameters/run_stop/blend_position", _stopSpeed);
                _stateMachine.Travel("run_stop");
            }
        }
        else if (st == "run_stop" && wantsMove && horizSpeed > 1.5f)
            _stateMachine.Travel("run_start");   // cancel the stop early
    }

    private void TryPlayLand(float impact)
    {
        if (_stateMachine == null || !IsOnFloor() || impact < 1.5f) return;
        if (_traverse != TraverseMode.None) return;
        string target = impact >= 12f ? "LandStumble"
                    : impact >= 7f  ? "LandHeavy"
                    :                 "LandSoft";
        _stateMachine.Travel(target);
    }

    private string _lastState = "";
    private void TraceAnimState()
    {
        if (_stateMachine == null) return;
        string st = _stateMachine.GetCurrentNode();
        if (st == _lastState) return;
        GD.Print($"ANIM '{_lastState}' -> '{st}'" +
            (!RootMotionStates.Contains(st) ? "   [NOT in RootMotionStates -> CODE-DRIVEN]" : ""));
        _lastState = st;
    }

    private float _rmDbgTimer;
    private void RootMotionDebug(float dt)
    {
        if (!DebugRootMotionTrace || _animTree == null) return;
        _rmDbgTimer -= dt;
        if (_rmDbgTimer > 0) return;
        _rmDbgTimer = 0.5f;
        GD.Print($"RMDBG state='{_lastState}' blend={_blendPos:F2} " +
                $"rmDelta={_animTree.GetRootMotionPosition():F3} " +
                $"vel={new Vector2(Velocity.X, Velocity.Z).Length():F2} missing={_rootMotionMissing}");
    }

    // ==================================================================
    //  TRAVERSAL (contact-based)
    // ==================================================================
    private bool WallCast(Vector3 origin, Vector3 dir, float dist, out Vector3 hitNormal, out Vector3 hitPoint)
    {
        hitNormal = Vector3.Zero; hitPoint = Vector3.Zero;
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + dir.Normalized() * dist);
        query.CollisionMask = CollisionMask | WallProbeMask;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;
        hitNormal = hit["normal"].AsVector3();
        hitPoint = hit["position"].AsVector3();
        return true;
    }

    private void CacheWallContact()
    {
        _touchingWall = false;
        Vector3 bestN = Vector3.Zero; float bestDot = float.NegativeInfinity;
        bool preferAlign = _traverse == TraverseMode.Climb || _traverse == TraverseMode.Wallhug;

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            Vector3 n = GetSlideCollision(i).GetNormal();
            if (n.Y > 0.4f || n.LengthSquared() < 0.001f) continue;
            Vector3 flat = new Vector3(n.X, 0f, n.Z).Normalized();
            float dot = preferAlign ? flat.Dot(_climbNormal) : 1f;
            if (!_touchingWall || dot > bestDot)
            {
                _touchingWall = true; bestDot = dot; bestN = flat;
                _wallColliderName = (GetSlideCollision(i).GetCollider() as Node)?.Name ?? "?";
            }
        }

        // FIX: gliding parallel to a wall produces no slide hits — probe with a ray
        if (!_touchingWall && (_traverse == TraverseMode.Wallhug || _traverse == TraverseMode.Climb))
        {
            if (WallCast(ChestPos(), -_climbNormal, WallGrabDistance + 0.35f, out Vector3 rn, out _) && rn.Y < 0.45f)
            {
                _touchingWall = true;
                bestN = new Vector3(rn.X, 0f, rn.Z).Normalized();
            }
        }

        if (!_touchingWall) return;
        _wallNormalCached = bestN;
        _lastWallNormal = bestN;

        if (!_rayDiagDone)
        {
            var q = PhysicsRayQueryParameters3D.Create(
                ChestPos(), ChestPos() - _wallNormalCached * (WallGrabDistance + 0.4f));
            q.CollisionMask = CollisionMask | WallProbeMask;
            q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            if (GetWorld3D().DirectSpaceState.IntersectRay(q).Count == 0)
                RayDiagnostic();
        }
    }

    private void RayDiagnostic()
    {
        if (_rayDiagDone) return; _rayDiagDone = true;
        GD.Print($"=== RAYDIAG pos={GlobalPosition:F2} touched='{_wallColliderName}' " +
                 $"colMask={CollisionMask} wallMask={WallProbeMask} n={_wallNormalCached:F2} ===");
        Vector3 dir = -_wallNormalCached;
        foreach (float h in new[] { 0.3f, 0.55f, 1.0f, 1.5f })
        {
            Vector3 o = FeetPos() + Vector3.Up * h;
            for (int m = 0; m < 3; m++)
            {
                uint mask = m == 0 ? CollisionMask : m == 1 ? WallProbeMask : 0xFFFFFFFF;
                var q = PhysicsRayQueryParameters3D.Create(o, o + dir * 1.5f);
                q.CollisionMask = mask;
                q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
                var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
                string res = hit.Count == 0 ? "MISS"
                    : $"HIT {(hit["collider"].AsGodotObject() as Node)?.Name ?? "unknown"} n={hit["normal"].AsVector3():F2}";
                GD.Print($"  h={h:F2} mask={(mask == 0xFFFFFFFF ? "ALL" : mask.ToString())} -> {res}");
            }
        }
    }

    private static readonly string[] RequiredStates =
    {
        "climb_start", "climb_idle", "climb_up", "climb_L", "climb_R",
        "wallhug_start", "wallhug_idle", "wallhug_LF", "wallhug_right_fw",
        "lean_wall", "climb_up_stand", "climb_end_crouch", "vault_through_window",
    };

    private void ReportMissingStates()
    {
        if (_sm == null) { GD.Print("TRAVEL: TreeRoot is not a state machine"); return; }
        var missing = new List<string>();
        foreach (var s in RequiredStates)
            if (_sm.GetNode(s) == null) missing.Add(s);
        GD.Print(missing.Count == 0
            ? "TRAVEL: all required SM states present"
            : $"TRAVEL: MISSING STATES — add these to the AnimationTree state machine: {string.Join(", ", missing)}");
    }

    private void AuditTransitions()
    {
        if (_sm == null) return;
        var edges = new List<string>();
        int tc = _sm.GetTransitionCount();
        for (int i = 0; i < tc; i++)
            edges.Add($"{_sm.GetTransitionFrom(i)} -> {_sm.GetTransitionTo(i)}");

        GD.Print("TRAVEL: SM edges:\n  " + string.Join("\n  ", edges));

        var problems = new List<string>();
        foreach (string s in RequiredStates)
        {
            if (_sm.GetNode(s) == null) { problems.Add($"missing state '{s}'"); continue; }
            if (!edges.Contains($"Locomotion -> {s}")) problems.Add($"missing edge Locomotion -> {s}");
            if (!edges.Contains($"{s} -> Locomotion")) problems.Add($"missing edge {s} -> Locomotion");
        }
        if (!edges.Contains("wallhug_start -> wallhug_idle"))
            problems.Add("missing edge wallhug_start -> wallhug_idle");

        GD.Print(problems.Count == 0
            ? "TRAVEL: wiring OK. Now set Advance Mode=Enabled for code-driven edges, Auto for auto-return ones (At End for one-shots)."
            : "TRAVEL: WIRING ISSUES:\n  " + string.Join("\n  ", problems));
    }

    private void SafeTravel(string state)
    {
        if (_stateMachine == null) return;
        if (_sm == null || _sm.GetNode(state) == null)
        {
            if (_missingStates.Add(state))
                GD.PushWarning($"Player: SM state '{state}' doesn't exist — travel skipped (this was the T-pose).");
            return;
        }
        _stateMachine.Travel(state);
    }

    private string PickClip(params string[] names)
    {
        if (_animPlayer == null) return names.Length > 0 ? names[0] : "";
        foreach (string s in names) if (_animPlayer.HasAnimation(s)) return s;
        return names[0];
    }

    private void SwitchTravAnim(string want, float dt)
    {
        if (want != _pendingTravAnim) { _pendingTravAnim = want; _travAnimHold = 0.05f; }
        if (_travAnimHold > 0f) { _travAnimHold -= dt; return; }
        if (want == _curClimbAnim) return;
        if (_stateMachine != null && _stateMachine.GetCurrentNode() == want)
        { _curClimbAnim = want; return; }
        _curClimbAnim = want;
        SafeTravel(want);
    }

    // ==================================================================
    //  TRAVERSAL – main update
    // ==================================================================
    private void UpdateTraversal(float dt, ref Vector3 velocity, bool inputLocked)
    {
        if (_climbCooldown > 0f) _climbCooldown -= dt;
        _traverseTime += dt;
        _noContactTime = _touchingWall ? 0f : _noContactTime + dt;
        TrvDebug(dt);

        switch (_traverse)
        {
            case TraverseMode.Vault:    VaultTick(dt, ref velocity);                return;
            case TraverseMode.Climb:    ClimbTick(dt, ref velocity, inputLocked);   return;
            case TraverseMode.Wallhug:  WallhugTick(dt, ref velocity, inputLocked); return;
            case TraverseMode.WallLean: WallLeanTick(dt, ref velocity);             return;
        }

        if (inputLocked || PlayerInputOverride.Active) { _leanTimer = 0f; _wallHoldTimer = 0f; return; }
        if (_climbCooldown > 0f)                       { _leanTimer = 0f; return; }

        if (_touchingWall)
        {
            Vector3 n = _wallNormalCached;
            Vector3 horizVel = new Vector3(velocity.X, 0f, velocity.Z);
            bool inputInto = _moveDirWorld != Vector3.Zero && (-n).Dot(_moveDirWorld) > 0.6f;

            if (!IsOnFloor())
            {
                _leanTimer = 0f;
                bool momentumInto = horizVel.LengthSquared() > 0.25f && (-n).Dot(horizVel.Normalized()) > 0.55f;
                if ((inputInto || momentumInto) && velocity.Y > -ClimbGrabMaxFallSpeed)
                    EnterClimb(n, ref velocity);
                return;
            }

            if (inputInto)
            {
                if (horizVel.Length() >= VaultMinSpeed &&
                    TryStartVault(n, VaultMaxHeight, preferFarSide: true, minRise: Mathf.Max(0.2f, StepHeight + 0.05f)))
                    return;
                _wallHoldTimer += dt;                     // debounce: don't hug on drive-bys
                if (_wallHoldTimer > 0.15f && WallhugEnabled) { EnterWallhug(n, ref velocity); return; }
            }
            else _wallHoldTimer = 0f;
            return;
        }
        _wallHoldTimer = 0f;

        if (_moveDirWorld == Vector3.Zero &&
            new Vector2(Velocity.X, Velocity.Z).Length() < 0.3f &&
            _stillTime > 0.5f && _lastWallNormal != Vector3.Zero)
        {
            if (WallCast(ChestPos(), -_lastWallNormal, 1.0f, out Vector3 ln, out _) && ln.Y < 0.4f)
            {
                _leanTimer += dt;
                if (_leanTimer >= LeanIdleDelay) { _leanTimer = 0f; EnterWallLean(_lastWallNormal, ref velocity); }
            }
            else _leanTimer = 0f;
        }
        else _leanTimer = 0f;
    }

    private void TrvDebug(float dt)
    {
        if (!DebugTraversal) return;
        _trvDbg -= dt; if (_trvDbg > 0f) return; _trvDbg = 0.3f;
        GD.Print($"TRV floor={IsOnFloor()} touch={_touchingWall} " +
                 $"n={(_touchingWall ? _wallNormalCached.ToString("F1") : "-")} " +
                 $"trav={_traverse} cd={_climbCooldown:F2} velY={Velocity.Y:F1}");
    }

    // ==================================================================
    //  ENTER / EXIT
    // ==================================================================
    private void EnterClimb(Vector3 normal, ref Vector3 velocity)
    {
        _traverse = TraverseMode.Climb; _traverseTime = 0f; _noContactTime = 0f;
        _climbNormal = normal;
        float offset = _stateOffsets.TryGetValue("climb_start", out float deg) ? Mathf.DegToRad(deg) : 0f;
        Rotation = new Vector3(0f, YawFromDir(-_climbNormal) + offset, 0f);
        velocity = -_climbNormal * 0.8f;
        _curClimbAnim = _pendingTravAnim = "climb_start"; _travAnimHold = 0f;
        SafeTravel("climb_start");
    }

    private void EnterWallhug(Vector3 normal, ref Vector3 velocity)
    {
        _traverse = TraverseMode.Wallhug; _traverseTime = 0f; _noContactTime = 0f;
        _climbNormal = normal;
        float offset = _stateOffsets.TryGetValue("wallhug_start", out float deg) ? Mathf.DegToRad(deg) : 0f;
        Rotation = new Vector3(0f, YawFromDir(-_climbNormal) + offset, 0f);
        velocity = -_climbNormal * 0.5f;   // gentle push into wall
        _curClimbAnim = _pendingTravAnim = "wallhug_start"; _travAnimHold = 0f;
        SafeTravel("wallhug_start");
    }

    private void ExitTraversal(ref Vector3 velocity, float cooldown = -1f)
    {
        if (IsVaulting) return;
        _traverse = TraverseMode.None; _traverseTime = 0f;
        _climbCooldown = cooldown >= 0f ? cooldown : ClimbExitCooldown;
        _wallHoldTimer = 0f; _hugClimbTimer = 0f; _noContactTime = 0f;
        velocity *= new Vector3(0.3f, 1f, 0.3f);
        if (_stateMachine != null && _stateMachine.GetCurrentNode() != "Locomotion")
            _stateMachine.Travel("Locomotion");
    }

    // ==================================================================
    //  CLIMB
    // ==================================================================
    private void ClimbTick(float dt, ref Vector3 velocity, bool inputLocked)
    {
        Vector3 wish = inputLocked ? Vector3.Zero : _moveDirWorld;
        bool jumpPressed = !inputLocked && Input.IsActionJustPressed("jump");

        if (WallCast(ChestPos(), -_climbNormal, WallGrabDistance + 0.35f, out Vector3 n, out _))
        {
            _climbNormal = new Vector3(n.X, 0f, n.Z).Normalized();
            _noContactTime = 0f;
        }
        else _noContactTime += dt;

        Vector3 into  = -_climbNormal;
        Vector3 right = Vector3.Up.Cross(_climbNormal).Normalized();
        float upInput = wish == Vector3.Zero ? 0f : Mathf.Clamp(into.Dot(wish), -1f, 1f);
        float side    = wish == Vector3.Zero ? 0f : right.Dot(wish);

        if (upInput > 0.1f && !WallCast(FeetPos() + Vector3.Up * LedgeProbeHeight, into, 0.7f, out _, out _))
            if (TryStartVault(_climbNormal, MantleClipRise, preferFarSide: false, minRise: 0.05f)) return;

        if (upInput < -0.1f && (IsOnFloor() ||
            WallCast(FeetPos() + Vector3.Up * 0.15f, Vector3.Down, 0.6f, out _, out _)))
        { ExitTraversal(ref velocity); return; }

        if (_noContactTime > 0.3f) { ExitTraversal(ref velocity); return; }

        if (jumpPressed && _traverseTime > 0.25f)
        {
            ExitTraversal(ref velocity, 0.25f);
            velocity = _climbNormal * 2.2f + Vector3.Up * 3.2f;
            return;
        }

        if (UseRootMotion && _rootMotionAvailable && _animTree != null)
            velocity = into * 0.4f;                       // clip's root motion moves the body
        else
            velocity = right * (side * ClimbLateralSpeed) + Vector3.Up * (upInput * ClimbSpeed) + into * 0.8f;

        string want = upInput > 0.1f  ? "climb_up"
                    : upInput < -0.1f ? PickClip("climb_down", "climb_idle")
                    : side    > 0.15f ? "climb_R"
                    : side    < -0.15f ? "climb_L"
                    :                      "climb_idle";
        SwitchTravAnim(want, dt);
    }

    // ==================================================================
    //  WALLHUG
    // ==================================================================
    private void WallhugTick(float dt, ref Vector3 velocity, bool inputLocked)
    {
        if (WallCast(ChestPos(), -_climbNormal, WallGrabDistance + 0.3f, out Vector3 n, out _))
        {
            _climbNormal = new Vector3(n.X, 0f, n.Z).Normalized();
            _noContactTime = 0f;
        }
        else if (_noContactTime > 0.35f) { ExitTraversal(ref velocity); return; }

        Vector3 wish = inputLocked ? Vector3.Zero : _moveDirWorld;
        Vector3 into = -_climbNormal;
        Vector3 right = Vector3.Up.Cross(_climbNormal).Normalized();
        float push = wish == Vector3.Zero ? 0f : into.Dot(wish);
        float side = wish == Vector3.Zero ? 0f : right.Dot(wish);

        if (push < -0.45f) { ExitTraversal(ref velocity, 0.25f); return; }

        if (!inputLocked && Input.IsActionJustPressed("jump") && _traverseTime > 0.2f)
        {
            ExitTraversal(ref velocity, 0.25f);
            velocity = _climbNormal * 2.2f + Vector3.Up * JumpVelocity;
            return;
        }

        if (push > 0.5f && TryStartVault(_climbNormal, VaultMaxHeight, preferFarSide: true, minRise: 0.2f))
            return;

        if (push > 0.75f && Mathf.Abs(side) < 0.3f &&
            WallCast(FeetPos() + Vector3.Up * LedgeProbeHeight, into, 0.7f, out _, out _))
        {
            _hugClimbTimer += dt;
            if (_hugClimbTimer > 0.4f)
            {
                _hugClimbTimer = 0f;
                _traverse = TraverseMode.Climb; _traverseTime = 0f;
                _curClimbAnim = _pendingTravAnim = "climb_start"; _travAnimHold = 0f;
                SafeTravel("climb_start");
                return;
            }
        }
        else _hugClimbTimer = 0f;

        velocity = into * 0.6f + right * (side * WallhugSpeed);

        string want = side > 0.25f ? "wallhug_LF"
                    : side < -0.25f ? "wallhug_right_fw"
                    : "wallhug_idle";
        SwitchTravAnim(want, dt);
    }

    // ==================================================================
    //  WALL LEAN
    // ==================================================================
    private void WallLeanTick(float dt, ref Vector3 velocity)
    {
        velocity = Vector3.Zero;
        bool disturbed = _moveDirWorld != Vector3.Zero
            || (!PlayerInputOverride.Active && Input.IsActionJustPressed("jump"))
            || !WallCast(ChestPos(), -_climbNormal, 1.1f, out _, out _);
        if (disturbed) ExitTraversal(ref velocity, 0.3f);
    }

    private void EnterWallLean(Vector3 normal, ref Vector3 velocity)
    {
        _traverse = TraverseMode.WallLean; _traverseTime = 0f;
        _climbNormal = normal;
        float offset = _stateOffsets.TryGetValue("lean_wall", out float deg) ? Mathf.DegToRad(deg) : 0f;
        Rotation = new Vector3(0f, YawFromDir(_climbNormal) + offset, 0f); // face AWAY from wall
        velocity = Vector3.Zero;
        _curClimbAnim = _pendingTravAnim = "lean_wall"; _travAnimHold = 0f;
        SafeTravel("lean_wall");
    }

    // ==================================================================
    //  VAULT
    // ==================================================================
    private bool TryStartVault(Vector3 wallNormal, float maxHeight, bool preferFarSide, float minRise)
    {
        Vector3 into = -wallNormal;
        if (!WallCast(FeetPos() + Vector3.Up * ProbeMidHeight, into, WallGrabDistance + 0.45f, out _, out Vector3 face))
            return false;

        Vector3 topOrigin = face + into * 0.12f + Vector3.Up * (maxHeight + 0.7f);
        if (!WallCast(topOrigin, Vector3.Down, maxHeight + 1.4f, out Vector3 topN, out Vector3 top) || topN.Y < 0.65f)
            return false;

        float rise = top.Y - FeetPos().Y;
        if (rise < minRise || rise > maxHeight) return false;

        Vector3 landPos = top + into * (preferFarSide ? 1.5f : 0.8f);
        if (WallCast(landPos + Vector3.Up * 0.05f, Vector3.Down, maxHeight + 1.4f, out Vector3 landN, out Vector3 land)
            && landN.Y > 0.65f)
            landPos = land;

        bool crouchExit = !IsSpotClear(landPos + Vector3.Up * (_feetOffset + CrouchClearance * 0.55f), 0.32f);
        if (!IsSpotClear(landPos + Vector3.Up * (_feetOffset + (crouchExit ? CrouchClearance : StandClearance) * 0.55f), 0.32f))
            return false;

        string clip = crouchExit || _isCrouching
            ? PickClip("vault_down_from_crouch", "climb_end_crouch", "vault_through_window")
            : PickClip(rise > 1.1f ? "vault_through_window" : "climb_up_stand", "vault_through_window");
        if (_animPlayer == null || !_animPlayer.HasAnimation(clip)) return false;

        _traverse = TraverseMode.Vault; _traverseTime = 0f;
        _vaultFrom = GlobalPosition;
        _vaultTo = landPos + Vector3.Up * _feetOffset;
        _vaultLen = Mathf.Max((float)_animPlayer.GetAnimation(clip).Length, 0.35f);
        _vaultCrouchExit = crouchExit || _isCrouching;
        float off = _stateOffsets.TryGetValue(clip, out float d) ? Mathf.DegToRad(d) : 0f;
        Rotation = new Vector3(0f, YawFromDir(into) + off, 0f);
        SetBodyCollision(false);
        _curClimbAnim = _pendingTravAnim = clip; _travAnimHold = 0f;
        SafeTravel(clip);
        return true;
    }

    private void VaultTick(float dt, ref Vector3 velocity)
    {
        velocity = Vector3.Zero;
        _traverseTime += dt;

        if (_animTree != null && UseRootMotion && _rootMotionAvailable)
        {
            Vector3 rm = _animTree.GetRootMotionPosition();
            if (RootMotionFlipZ) rm = new Vector3(-rm.X, rm.Y, -rm.Z);
            GlobalPosition += GlobalTransform.Basis.Orthonormalized() * rm * RootMotionScale;
        }
        if (_traverseTime > _vaultLen * 0.6f)                       // steer to the measured exit
            GlobalPosition = GlobalPosition.Lerp(_vaultTo, 4.0f * dt);

        if (_traverseTime < _vaultLen) return;

        _traverse = TraverseMode.None; _traverseTime = 0f; _climbCooldown = 0.25f;
        GlobalPosition = _vaultTo;
        if (_vaultCrouchExit) _isCrouching = true;
        ApplyCrouchShape();
        _curClimbAnim = "";
        SafeTravel(_vaultCrouchExit ? "Crouch" : "Locomotion");
    }

    private bool IsSpotClear(Vector3 at, float radius)
    {
        var q = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = radius },
            Transform = new Transform3D(Basis.Identity, at),
            CollisionMask = CollisionMask | WallProbeMask,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(q).Count == 0;
    }

    private void SetBodyCollision(bool on)
    {
        if (on) ApplyCrouchShape();
        else
        {
            if (ColCapsuleFull != null) ColCapsuleFull.Disabled = true;
            if (ColCapsuleCrouch != null) ColCapsuleCrouch.Disabled = true;
        }
    }

    // ==================================================================
    //  DOOR RAM
    // ==================================================================
    private RigidBody3D _ridingDoor; private Vector3 _rideOffset;
    private void HandleDoorRam()
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var col = GetSlideCollision(i);
            if (col.GetCollider() is not RigidBody3D rb || !rb.IsInGroup("OpenableDoor")) continue;

            Vector3 v = new Vector3(Velocity.X, 0, Velocity.Z);
            if (v.Length() < 1.0f) continue;

            var marker = rb.GetNodeOrNull<Node3D>("OpensToward");
            Vector3 openDir = marker != null
                ? (marker.GlobalPosition - rb.GlobalPosition).Normalized()
                : -col.GetNormal();

            rb.ApplyImpulse(openDir * DoorRamForce, col.GetPosition() - rb.GlobalPosition);

            _ridingDoor = rb;
            _rideOffset = rb.GlobalTransform.AffineInverse() * (col.GetPosition() + col.GetNormal() * 0.06f);

            var shape = rb.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (shape != null && !shape.Disabled)
            {
                shape.SetDeferred("disabled", true);
                GetTree().CreateTimer(DoorPassThroughDuration).Timeout +=
                    () => { if (IsInstanceValid(shape)) shape.SetDeferred("disabled", false); };
            }
        }
    }

    private bool PredictDoor(out Vector3 hitPoint, out RigidBody3D door, out Vector3 normal)
    {
        hitPoint = Vector3.Zero; door = null; normal = Vector3.Up;
        Vector3 v = new Vector3(Velocity.X, 0, Velocity.Z);
        if (v.Length() < 1.0f) return false;

        float lookahead = Mathf.Clamp(v.Length() * 0.22f, 0.3f, 1.2f);
        var q = PhysicsRayQueryParameters3D.Create(
            GlobalPosition + Vector3.Up * 1.3f,
            GlobalPosition + Vector3.Up * 1.3f + v.Normalized() * lookahead);
        q.CollisionMask = (uint)StepCollisionMask;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count == 0) return false;
        if (hit["collider"].AsGodotObject() is not RigidBody3D rb || !rb.IsInGroup("OpenableDoor"))
            return false;

        hitPoint = hit["position"].AsVector3();
        normal = hit["normal"].AsVector3();
        door = rb;
        return true;
    }

    // ==================================================================
    //  HAND IK (fixed to use group/method checks instead of Openable class)
    // ==================================================================
    private void HandleHandReach(float dt)
    {
        if (ik_hand_right == null || target_hand_right == null) return;

        if (_handIKTimer <= 0f && _traverse == TraverseMode.None)
        {
            Vector3 fwd = FacingDir();
            var q = PhysicsRayQueryParameters3D.Create(ChestPos(), ChestPos() + fwd * DoorReachDistance);
            q.CollisionMask = CollisionMask | WallProbeMask;
            q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
            if (hit.Count > 0)
            {
                Node3D node = hit["collider"].As<Node3D>();
                while (node != null)
                {
                    // Use your global groups and method checks
                    bool isOpenable = node.IsInGroup("Openable") || node.IsInGroup("OpenableDoor") ||
                                      node.HasMethod("Open") || node.HasMethod("Toggle") || node.HasMethod("Interact");
                    if (isOpenable)
                    {
                        bool approaching = _moveDirWorld != Vector3.Zero && _moveDirWorld.Dot(fwd) > 0.5f;
                        if (approaching || Input.IsActionJustPressed("interact"))
                        {
                            _handHandlePos = hit["position"].AsVector3();
                            _handIKTimer = HandIKHoldTime;
                            // Try common interaction methods
                            if (node.HasMethod("Open")) node.Call("Open");
                            else if (node.HasMethod("Toggle")) node.Call("Toggle");
                            else if (node.HasMethod("Interact")) node.Call("Interact");
                            else if (node is RigidBody3D rb)
                            {
                                Vector3 toPlayer = GlobalPosition - rb.GlobalPosition; toPlayer.Y = 0f;
                                rb.ApplyImpulse(toPlayer.Normalized() * 2.5f, _handHandlePos - rb.GlobalPosition);
                            }
                        }
                        break;
                    }
                    node = node.GetParentOrNull<Node3D>();
                }
            }
        }

        if (_handIKTimer > 0f)
        {
            _handIKTimer -= dt;
            target_hand_right.GlobalPosition = target_hand_right.GlobalPosition.Lerp(
                _handHandlePos, 1f - Mathf.Exp(-HandReachSpeed * dt));
            ik_hand_right.Influence = Mathf.MoveToward(ik_hand_right.Influence, 1f, dt * HandReachSpeed);
        }
        else
        {
            target_hand_right.GlobalPosition = target_hand_right.GlobalPosition.Lerp(
                GlobalTransform * HandRestOffset, 1f - Mathf.Exp(-HandRestSpeed * dt));
            ik_hand_right.Influence = Mathf.MoveToward(ik_hand_right.Influence, 0f, dt * HandRestSpeed);
        }
    }

    // ==================================================================
    //  CAMERA
    // ==================================================================
    private void UpdateCamera(float dt)
    {
        Vector3 targetPos;
        if (_isDriving && _cameraTarget != null)
        {
            targetPos = _cameraTarget.GlobalPosition + CameraOffset;
        }
        else
        {
            targetPos = GlobalPosition + CameraOffset;
        }

        float t = 1f - Mathf.Exp(-CameraSmoothing * dt);
        if (_isFirstPerson)
            _cameraGimbal.GlobalPosition = targetPos;
        else
            _cameraGimbal.GlobalPosition = _cameraGimbal.GlobalPosition.Lerp(targetPos, t);

        float desiredLength = _isFirstPerson ? 0f : _targetZoom;
        _springArm.SpringLength = Mathf.Lerp(_springArm.SpringLength, desiredLength, t);

        if (PlayerCamera != null)
            PlayerCamera.SetCullMaskValue(4, _springArm.SpringLength > 0.6f);
    }

    public void ToggleCamera()
    {
        _isFirstPerson = !_isFirstPerson;
        if (_eyeTracker != null) _eyeTracker.EnableHeadTracking = !_isFirstPerson;
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
        if (_isLockedOn && LockOnTarget != null) _eyeTracker.Target = LockOnTarget;
        else if (_casualTarget != null) _eyeTracker.Target = _casualTarget;
        else _eyeTracker.Target = null;
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

    private bool IsAnyMenuOpen() => HUD.Instance != null && HUD.Instance.IsGamePaused;

    // ==================================================================
    //  TOUCH / PINCH / LOOK
    // ==================================================================
    private bool HandlePinchZoom(InputEvent @event)
    {
        if (@event is InputEventScreenTouch t)
        {
            if (IsTouchInMenu(_pinch0) || IsTouchInMenu(t.Index))
                return false;
            if (t.Pressed)
            {
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
                    return true;
                }
            }
            else
            {
                if (t.Index == _pinch0) _pinch0 = -1;
                if (t.Index == _pinch1) _pinch1 = -1;
            }
            return false;
        }

        if (_pinch0 != -1 && _pinch1 != -1 &&
            TouchTracker.TryGet(_pinch0, out Vector2 cur0) &&
            TouchTracker.TryGet(_pinch1, out Vector2 cur1))
        {
            float curDist = cur0.DistanceTo(cur1);
            if (curDist > 0.01f && _pinchBaseDist > 0.01f)
            {
                float scale = curDist / _pinchBaseDist;
                _targetZoom = Mathf.Clamp(_pinchBaseZoom / scale, MinZoom, MaxZoom);
            }
            return true;
        }
        return false;
    }

    private bool HandleCameraLook(InputEvent @event)
    {
        if (_isLockedOn || IsAnyMenuOpen()) return false;
        if (_pinch0 != -1 && _pinch1 != -1) return false;

        if (@event is InputEventMouseMotion mouse && !DisplayServer.IsTouchscreenAvailable())
        {
            RotateCamera(mouse.Relative);
            return true;
        }

        if (@event is InputEventScreenDrag drag &&
            DisplayServer.IsTouchscreenAvailable() &&
            IsInFreeArea(drag.Index) &&
            drag.Index != VirtualJoystick.ActiveTouchIndex)
        {
            if (DisplayServer.IsTouchscreenAvailable() && IsTouchInMenu(drag.Index))
                return false;
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
        if (MobileUIController.Joystick != null && MobileUIController.Joystick.GetGlobalRect().HasPoint(startPos))
            return false;
        foreach (var container in MobileUIController.ButtonContainers)
            if (container != null && container.GetGlobalRect().HasPoint(startPos))
                return false;
        return true;
    }

    private bool IsTouchInMenu(int touchIndex)
    {
        if (!_touchStartPositions.TryGetValue(touchIndex, out Vector2 startPos))
            return false;
        return HUD.Instance != null && HUD.Instance.IsPointInsideAnyMenu(startPos);
    }

    // ==================================================================
    //  COMBAT
    // ==================================================================
    public void PerformAttack()
    {
        if (_attackCooldownTimer > 0f) return;
        if (_traverse == TraverseMode.Climb || _traverse == TraverseMode.Vault) return;

        float len = 0.55f;
        if (_animPlayer != null && _animPlayer.HasAnimation(AttackClipName))
        {
            var a = _animPlayer.GetAnimation(AttackClipName);
            a.LoopMode = Animation.LoopModeEnum.None;
            len = Mathf.Max((float)a.Length, 0.25f);
        }
        _attackLen = len;
        _attackTime = 0f;
        _attackCooldownTimer = len * 0.85f;
        SafeTravel("Attack");
        _attackAudioPlayer?.Play();
    }

    private void TickAttack(float dt)
    {
        if (_attackTime < 0f) return;
        _attackTime += dt;

        bool active = _attackTime > _attackLen * 0.25f && _attackTime < _attackLen * 0.65f;
        if (_rightHandHitbox != null && _rightHandHitbox.Monitoring != active)
        {
            _rightHandHitbox.SetDeferred(Area3D.PropertyName.Monitoring, active);
            if (active) _rightHandHitbox.CallDeferred("BeginSwing");
        }
        if (_attackTime >= _attackLen)
        {
            _attackTime = -1f;
            if (_rightHandHitbox != null)
                _rightHandHitbox.SetDeferred(Area3D.PropertyName.Monitoring, false);
        }
    }

    private Node3D FindNearestEnemy()
    {
        Node3D best = null;
        float bestDist = 5.0f;
        foreach (Node node in GetTree().GetNodesInGroup("NPC"))
        {
            if (node is CharacterBody3D npc && !npc.GetNode<Health>("Health").IsDead)
            {
                float dist = GlobalPosition.DistanceTo(npc.GlobalPosition);
                if (dist < bestDist)
                {
                    Vector3 dirToNpc = (npc.GlobalPosition - GlobalPosition).Normalized();
                    if (GlobalTransform.Basis.Z.Dot(dirToNpc) < -0.3f)
                    {
                        bestDist = dist;
                        best = npc;
                    }
                }
            }
        }
        return best;
    }

    private void CheckMeleeHits(Area3D arc, ItemData weapon, List<Rid> hitBodies)
    {
        foreach (var body in arc.GetOverlappingAreas())
        {
            if (body is Area3D hitArea)
            {
                var npc = FindNpcFromLimbArea(hitArea);
                if (npc == null || hitBodies.Contains(npc.GetRid())) continue;
                hitBodies.Add(npc.GetRid());

                string limbName = hitArea.Name;
                var health = npc.GetNodeOrNull<Health>("Health");
                if (health == null) continue;

                health.TakeDamage(weapon.Damage.Value, limbName);

                Vector3 knockbackDir = (npc.GlobalPosition - GlobalPosition).Normalized();
                knockbackDir.Y = 0.5f;
                float knockbackForce = weapon.KnockbackForce ?? 5f;
                ApplyKnockbackToNpc(npc, knockbackDir * knockbackForce);

                TimeManager.Instance?.TriggerHitstop(weapon.HitstopDuration ?? 0.05f, weapon.CameraShake ?? 0.1f);
                FlashLimb(npc, limbName);
            }
        }
        arc.QueueFree();
    }

    private CharacterBody3D FindNpcFromLimbArea(Area3D limbArea)
    {
        Node current = limbArea.GetParent();
        current = current?.GetParent();
        while (current != null && !(current is CharacterBody3D))
            current = current.GetParent();
        return current as CharacterBody3D;
    }

    private void ApplyKnockbackToNpc(CharacterBody3D npc, Vector3 force)
    {
        var navAgent = npc.GetNodeOrNull<NavAgentNPC>("NavAgentNPC");
        if (navAgent != null)
            navAgent.KnockbackVelocity = force;
    }

    private async void FlashLimb(CharacterBody3D npc, string limbName)
    {
        var modelRoot = npc.GetNodeOrNull<Node3D>("ModelRoot");
        if (modelRoot == null) return;

        var allMeshes = new List<MeshInstance3D>();
        FindAllMeshes(modelRoot, allMeshes);
        if (allMeshes.Count == 0) return;

        MeshInstance3D limbMesh = null;
        if (NpcController.LimbMeshNames.TryGetValue(limbName, out string meshName))
            limbMesh = allMeshes.FirstOrDefault(m =>
                m.Name.ToString().Equals(meshName, StringComparison.OrdinalIgnoreCase));

        List<MeshInstance3D> meshesToFlash;
        if (limbMesh != null)
            meshesToFlash = new List<MeshInstance3D> { limbMesh };
        else
            meshesToFlash = allMeshes;

        var originalMaterials = new Dictionary<MeshInstance3D, Material[]>();
        foreach (var mesh in meshesToFlash)
        {
            int surfaceCount = mesh.Mesh.GetSurfaceCount();
            var mats = new Material[surfaceCount];
            for (int i = 0; i < surfaceCount; i++)
            {
                mats[i] = mesh.GetActiveMaterial(i);
                var redMat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1, 0, 0),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                };
                mesh.SetSurfaceOverrideMaterial(i, redMat);
            }
            originalMaterials[mesh] = mats;
        }

        await ToSignal(GetTree().CreateTimer(0.15f), "timeout");

        foreach (var (mesh, mats) in originalMaterials)
        {
            for (int i = 0; i < mats.Length; i++)
                mesh.SetSurfaceOverrideMaterial(i, mats[i]);
        }
    }

    private static MeshInstance3D FindMeshByPartialName(Node start, string partialName)
    {
        if (start is MeshInstance3D mi)
        {
            string nodeName = mi.Name.ToString();
            if (nodeName.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
                return mi;
        }
        foreach (Node child in start.GetChildren())
        {
            var result = FindMeshByPartialName(child, partialName);
            if (result != null) return result;
        }
        return null;
    }

    private void FindAllMeshes(Node node, List<MeshInstance3D> list)
    {
        if (node is MeshInstance3D mi)
            list.Add(mi);
        foreach (Node child in node.GetChildren())
            FindAllMeshes(child, list);
    }

    private static T FindNodeRecursive<T>(Node start, string name) where T : class
    {
        if (start is T t && start.Name == name) return t;
        foreach (Node child in start.GetChildren())
        {
            var found = FindNodeRecursive<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    public void EquipWeapon(ImpactType type)
    {
        _currentWeapon = ItemRegistry.GetWeapon(type);
    }

    private void StripBlendShapeTracks(AnimationPlayer animPlayer) => AnimationFixer.StripBlendShapeTracks(animPlayer);

    // ==================================================================
    //  WORLD
    // ==================================================================
    private void PushRigidBodies()
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is RigidBody3D rb)
            {
                Vector3 pushDir = -collision.GetNormal();
                Vector3 arm = collision.GetPosition() - rb.GlobalPosition;
                rb.ApplyImpulse(pushDir * PushForce, arm);
            }
        }
    }

    // ==================================================================
    //  INTERACTION
    // ==================================================================
    private CarController _currentVehicleInteract;
    private void UpdateInteraction(bool anyMenuOpen)
    {
        if (!anyMenuOpen && PlayerCamera != null)
        {
            var spaceState = GetWorld3D().DirectSpaceState;

            Vector3 origin = PlayerCamera.GlobalPosition;
            Vector3 end = origin - PlayerCamera.GlobalTransform.Basis.Z * 10.0f;

            var query = PhysicsRayQueryParameters3D.Create(origin, end);
            query.CollisionMask = (1 << 4) | (1 << 5) | (1 << 6);
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            var result = spaceState.IntersectRay(query);
            InteractableItem itemTarget = null;
            NpcInteraction npcTarget = null;
            CarController vehicleTarget = null;

            if (result.Count > 0)
            {
                Vector3 playerCenter = GlobalPosition + new Vector3(0, 1.5f, 0);
                Vector3 hitPoint = (Vector3)result["position"];
                float distToPlayer = playerCenter.DistanceTo(hitPoint);

                if (distToPlayer <= _interactDistance)
                {
                    var collider = result["collider"].AsGodotObject();
                    if (collider is InteractableItem item)
                        itemTarget = item;
                    else if (collider is CharacterBody3D body)
                    {
                        var npcInteract = body.GetNodeOrNull<NpcInteraction>("Interaction");
                        if (npcInteract != null && !npcInteract.IsInDialogue)
                            npcTarget = npcInteract;
                    }
                    else if (collider is CarController car)
                    {
                        // Only show if not occupied or if player is already inside
                        if (!car.IsOccupied || car == _currentVehicle)
                            vehicleTarget = car;
                    }
                }
            }

            if (itemTarget != _currentInteractable || npcTarget != _currentNpc || vehicleTarget != _currentVehicleInteract)
            {
                _currentInteractable = itemTarget;
                _currentNpc = npcTarget;
                _currentVehicleInteract = vehicleTarget;

                if (_currentInteractable != null)
                {
                    _hud.ShowTooltipAtWorldPosition($"Pick up {_currentInteractable.Data.Name}",
                                                    _currentInteractable.GlobalPosition, "E");
                }
                else if (_currentNpc != null)
                {
                    string prefix = _currentNpc.IsDead ? "Dead " : "";
                    _hud.ShowTooltipAtWorldPosition($"Talk to {prefix}{_currentNpc.NpcName}",
                        _currentNpc.GetParent<CharacterBody3D>().GlobalPosition, "E");
                }
                else if (_currentVehicleInteract != null)
                {
                    string action = (_currentVehicleInteract == _currentVehicle) ? "Exit" : "Drive";
                    _hud.ShowTooltipAtWorldPosition($"{action} {_currentVehicleInteract.Name}", 
                        _currentVehicleInteract.GlobalPosition, "E");
                }
                else
                {
                    _hud.HideTooltip();
                }
            }

            if (Input.IsActionJustPressed("interact"))
            {
                if (_currentInteractable != null)
                {
                    _currentInteractable.Pickup();
                    _currentInteractable = null;
                    _currentNpc = null;
                    _hud?.HideTooltip();
                }
                else if (_currentNpc != null)
                {
                    _currentNpc.Interact();
                }
                else if (_currentVehicleInteract != null)
                {
                    if (_currentVehicleInteract == _currentVehicle)
                    {
                        // Exit the current vehicle
                        _currentVehicle.Exit();
                        _currentVehicle = null;
                        _isDriving = false;
                        SetCameraTarget(null);
                    }
                    else
                    {
                        // Enter the vehicle
                        _currentVehicleInteract.Enter(this);
                        _currentVehicle = _currentVehicleInteract;
                        _isDriving = true;
                        SetCameraTarget(_currentVehicleInteract.CameraFollowTarget ?? _currentVehicleInteract);
                    }
                    // Clear the tooltip after action
                    _hud.HideTooltip();
                    _currentVehicleInteract = null;
                }
            }
        }
        else if (_hud != null && (_currentInteractable != null || _currentNpc != null))
        {
            _hud.HideTooltip();
            _currentInteractable = null;
            _currentNpc = null;
        }
    }

    // ==================================================================
    //  ROOT MOTION SETUP
    // ==================================================================
    private void SetupRootMotion()
    {
        _rootMotionAvailable = false;
        if (_animTree == null) { GD.PushError("RM: AnimationTree node missing."); return; }
        if (_animPlayer == null) { GD.PushError("RM: AnimationPlayer node missing."); return; }

        Node rootNode = _animPlayer.GetNodeOrNull(_animPlayer.RootNode);
        if (rootNode == null) { GD.PushError("RM: AnimationPlayer.RootNode doesn't resolve."); return; }

        Skeleton3D skeleton = null;
        string skeletonPath = null;
        foreach (StringName animName in _animPlayer.GetAnimationList())
        {
            Animation a = _animPlayer.GetAnimation(animName);
            for (int i = 0; i < a.GetTrackCount(); i++)
            {
                string p = a.TrackGetPath(i).ToString();
                int colon = p.IndexOf(':');
                if (colon <= 0) continue;
                string nodePart = p.Substring(0, colon);
                var sk = rootNode.GetNodeOrNull<Skeleton3D>(nodePart);
                if (sk != null) { skeleton = sk; skeletonPath = nodePart; break; }
            }
            if (skeleton != null) break;
        }
        if (skeleton == null)
        {
            foreach (Node n in FindChildren("Skeleton3D", "Skeleton3D", true, false))
                if (n is Skeleton3D sk)
                { skeleton = sk; skeletonPath = rootNode.GetPathTo(sk).ToString(); break; }
        }
        if (skeleton == null) { GD.PushError("RM: no Skeleton3D found under the player."); return; }

        _skeleton = skeleton;
        GD.Print($"RM: skeleton = {skeletonPath} (player root: {rootNode.Name}, tree RootNode: {_animTree.RootNode})");
        ReportRootBaselines();

        string boneTrack = $"{skeletonPath}:{RootMotionBoneName}";
        _rootBoneTrack = boneTrack;
        float bestBoneMps = 0f;
        int nodeTracksWithMotion = 0;
        var travelers = new List<(string anim, string path, float mps)>();

        foreach (StringName animName in _animPlayer.GetAnimationList())
        {
            Animation anim = _animPlayer.GetAnimation(animName);
            for (int i = 0; i < anim.GetTrackCount(); i++)
            {
                if (anim.TrackGetType(i) != Animation.TrackType.Position3D) continue;
                if (anim.TrackGetKeyCount(i) < 2) continue;

                string p = anim.TrackGetPath(i).ToString();
                Vector3 travel = anim.PositionTrackInterpolate(i, anim.Length)
                            - anim.PositionTrackInterpolate(i, 0.0);
                float mps = travel.Length() / Mathf.Max((float)anim.Length, 0.001f);

                if (p == boneTrack) bestBoneMps = Mathf.Max(bestBoneMps, mps);
                if (!p.Contains(':') && mps > 0.3f) nodeTracksWithMotion++;
                if (mps > 0.3f) travelers.Add((animName.ToString(), p, mps));
            }
        }

        PrintAllTracks(_animPlayer.HasAnimation("Walk_fwd") ? "Walk_fwd" : "walk_fwd");
        travelers.Sort((a, b) => b.mps.CompareTo(a.mps));
        GD.Print("--- top moving Position3D tracks (whole library) ---");
        foreach (var t in travelers.Take(12))
            GD.Print($"  {t.anim} | {t.path} | {t.mps:F2} m/s");

        if (nodeTracksWithMotion > 0)
        {
            GD.Print($"RM: {nodeTracksWithMotion} node tracks carry motion — converting.");
            ConvertNodeRootMotionToBone(skeleton, skeletonPath, rootNode, boneTrack);
        }
        int split = SplitHipsRootMotion(skeleton, skeletonPath, boneTrack);

        if (bestBoneMps > 0.2f || nodeTracksWithMotion > 0 || split > 0)
        {
            _animTree.RootMotionTrack = new NodePath(boneTrack);
            _rootMotionAvailable = true;
        }
        else GD.PushWarning("RM: no root motion found anywhere. Staying code-driven.");
    }

    private void PrintAllTracks(string animName)
    {
        if (!_animPlayer.HasAnimation(animName)) return;
        Animation anim = _animPlayer.GetAnimation(animName);
        GD.Print($"--- '{animName}': {anim.GetTrackCount()} tracks, {anim.Length:F2}s, loop={anim.LoopMode} ---");
        for (int i = 0; i < anim.GetTrackCount(); i++)
        {
            string p = anim.TrackGetPath(i).ToString();
            int keys = anim.TrackGetKeyCount(i);
            string extra = "";
            if (anim.TrackGetType(i) == Animation.TrackType.Position3D && keys > 1)
            {
                Vector3 travel = anim.PositionTrackInterpolate(i, anim.Length)
                            - anim.PositionTrackInterpolate(i, 0.0);
                extra = $" | travel {travel.Length():F3}m ({travel.Length() / Mathf.Max((float)anim.Length, 0.001f):F2} m/s)";
            }
            GD.Print($"  [{i}] {anim.TrackGetType(i)} | {p} | {keys} keys{extra}");
        }
    }

    private void ConvertNodeRootMotionToBone(Skeleton3D skeleton, string skeletonPath, Node rootNode, string boneTrack)
    {
        int rootIdx = skeleton.FindBone(RootMotionBoneName);
        if (rootIdx == -1) { GD.PushError($"RM: bone '{RootMotionBoneName}' not found."); return; }
        Transform3D rootRest = skeleton.GetBoneRest(rootIdx);
        Quaternion rootRestRot = rootRest.Basis.GetRotationQuaternion();
        Quaternion skelGlobalRot = skeleton.GlobalBasis.GetRotationQuaternion();

        var rmNodePaths = new HashSet<string>();
        for (Node n = skeleton.GetParent(); n != null && n != rootNode; n = n.GetParent())
            rmNodePaths.Add(rootNode.GetPathTo(n).ToString());

        int convertedPos = 0, convertedRot = 0, removedDupes = 0;

        foreach (StringName animName in _animPlayer.GetAnimationList())
        {
            Animation anim = _animPlayer.GetAnimation(animName);

            bool hasBonePositionTrack = false;
            for (int i = 0; i < anim.GetTrackCount(); i++)
                if (anim.TrackGetType(i) == Animation.TrackType.Position3D &&
                    anim.TrackGetPath(i).ToString() == boneTrack &&
                    anim.TrackGetKeyCount(i) > 0)
                { hasBonePositionTrack = true; break; }

            for (int i = anim.GetTrackCount() - 1; i >= 0; i--)
            {
                Animation.TrackType type = anim.TrackGetType(i);
                if (type != Animation.TrackType.Position3D &&
                    type != Animation.TrackType.Rotation3D) continue;

                string p = anim.TrackGetPath(i).ToString();
                if (p.Contains(':')) continue;
                if (!rmNodePaths.Contains(p)) continue;

                Node3D animatedNode = rootNode.GetNodeOrNull<Node3D>(p);
                if (animatedNode == null) continue;
                int keys = anim.TrackGetKeyCount(i);
                if (keys == 0) continue;

                if (hasBonePositionTrack) { anim.RemoveTrack(i); removedDupes++; continue; }

                int newTrack = anim.AddTrack(type);
                anim.TrackSetPath(newTrack, boneTrack);
                anim.TrackSetInterpolationType(newTrack, anim.TrackGetInterpolationType(i));

                if (type == Animation.TrackType.Position3D)
                {
                    Node3D parentNode = animatedNode.GetParent() as Node3D;
                    Basis parentToWorld = parentNode != null ? parentNode.GlobalBasis : Basis.Identity;
                    Basis worldToSkeleton = skeleton.GlobalBasis.Inverse();
                    Vector3 baseLocal = anim.TrackGetKeyValue(i, 0).AsVector3();

                    for (int k = 0; k < keys; k++)
                    {
                        double time = anim.TrackGetKeyTime(i, k);
                        Vector3 localPos = anim.TrackGetKeyValue(i, k).AsVector3();
                        Vector3 worldDelta = parentToWorld * (localPos - baseLocal);
                        anim.TrackInsertKey(newTrack, time, rootRest.Origin + worldToSkeleton * worldDelta);
                    }
                    convertedPos++;
                }
                else
                {
                    Quaternion baseRot = ReadRotKey(anim, i, 0);
                    for (int k = 0; k < keys; k++)
                    {
                        double time = anim.TrackGetKeyTime(i, k);
                        Quaternion delta = baseRot.Inverse() * ReadRotKey(anim, i, k);
                        Quaternion boneLocal = skelGlobalRot.Inverse() * (delta * skelGlobalRot) * rootRestRot;
                        anim.TrackInsertKey(newTrack, time, boneLocal);
                    }
                    convertedRot++;
                }
                anim.RemoveTrack(i);
            }
        }

        _animTree.RootMotionTrack = new NodePath(boneTrack);
        _rootMotionAvailable = true;
        GD.Print($"RM: converted {convertedPos} pos + {convertedRot} rot node tracks, " +
                $"removed {removedDupes} dupes -> '{boneTrack}'.");
        ReportRootMotionSpeeds(boneTrack);
    }

    private static Quaternion ReadRotKey(Animation anim, int trackIdx, int keyIdx)
    {
        Variant v = anim.TrackGetKeyValue(trackIdx, keyIdx);
        if (v.VariantType == Variant.Type.Quaternion) return v.AsQuaternion();
        return Quaternion.FromEuler(v.AsVector3());
    }

    private void ReportRootMotionSpeeds(string boneTrack)
    {
        GD.Print("--- clip root-motion speeds (use these for exports & ring) ---");
        var rows = new List<(string name, float mps)>();
        foreach (StringName animName in _animPlayer.GetAnimationList())
        {
            Animation anim = _animPlayer.GetAnimation(animName);
            for (int i = 0; i < anim.GetTrackCount(); i++)
            {
                if (anim.TrackGetType(i) != Animation.TrackType.Position3D) continue;
                if (anim.TrackGetPath(i).ToString() != boneTrack) continue;
                if (anim.TrackGetKeyCount(i) < 2) continue;
                Vector3 travel = anim.PositionTrackInterpolate(i, anim.Length)
                            - anim.PositionTrackInterpolate(i, 0.0);
                rows.Add((animName.ToString(),
                        travel.Length() / Mathf.Max((float)anim.Length, 0.001f)));
            }
        }
        foreach (var r in rows.OrderByDescending(r => r.mps))
            GD.Print($"  {r.name} : {r.mps:F2} m/s");
    }

    private void ReportRootBaselines()
    {
        GD.Print("--- node-rotation baselines (only clips with |start yaw|>1 or net>1 deg) ---");
        foreach (StringName animName in _animPlayer.GetAnimationList())
        {
            Animation a = _animPlayer.GetAnimation(animName);
            for (int i = 0; i < a.GetTrackCount(); i++)
            {
                if (a.TrackGetType(i) != Animation.TrackType.Rotation3D) continue;
                if (a.TrackGetPath(i).ToString().Contains(':')) continue;
                int keys = a.TrackGetKeyCount(i);
                if (keys == 0) continue;
                float yaw0 = Mathf.RadToDeg(ReadRotKey(a, i, 0).GetEuler().Y);
                float yawE = Mathf.RadToDeg(ReadRotKey(a, i, keys - 1).GetEuler().Y);
                if (Mathf.Abs(yaw0) > 1f || Mathf.Abs(yawE - yaw0) > 1f)
                    GD.Print($"  {animName} | start {yaw0:F0}° | net {yawE - yaw0:F0}°");
            }
        }
    }

    private static readonly string[] LoopWords =
    { "walk", "run", "sprint", "idle", "stand", "crouch", "climb", "swim", "fall", "sleep", "strafe", "lean", "wallhug" };

    private void ForceLoopModes()
    {
        if (_animPlayer == null) return;
        foreach (StringName animName in _animPlayer.GetAnimationList())
        {
            string n = animName.ToString().ToLowerInvariant();
            bool loops = LoopWords.Any(n.Contains) && !OneShotWords.Any(n.Contains);
            _animPlayer.GetAnimation(animName).LoopMode = loops
                ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        }
    }

    private static readonly string[] OneShotWords =
    { "start", "stop", "to ", "180", "turn", "jump", "land", "attack",
      "mantle", "end", "react", "leap", "drop", "loot", "take" };

    private void ForceOneShotsToNone()
    {
        if (_animPlayer == null) return;
        foreach (StringName name in _animPlayer.GetAnimationList())
        {
            string n = name.ToString().ToLowerInvariant();
            if (OneShotWords.Any(n.Contains))
                _animPlayer.GetAnimation(name).LoopMode = Animation.LoopModeEnum.None;
        }
    }

    private int SplitHipsRootMotion(Skeleton3D skeleton, string skeletonPath, string boneTrack)
    {
        int hipsIdx = skeleton.FindBone(HipsBoneName);
        int rootIdx = skeleton.FindBone(RootMotionBoneName);
        if (hipsIdx == -1 || rootIdx == -1)
        { GD.PushError($"RM split: '{HipsBoneName}' or '{RootMotionBoneName}' missing."); return 0; }

        Basis hipsParentB = skeleton.GetBoneGlobalRest(skeleton.GetBoneParent(hipsIdx)).Basis;
        int rootParent = skeleton.GetBoneParent(rootIdx);
        Basis rootParentInv = rootParent == -1 ? Basis.Identity
                            : skeleton.GetBoneGlobalRest(rootParent).Basis.Inverse();
        Vector3 rootRest = skeleton.GetBoneRest(rootIdx).Origin;
        string hipsPath = $"{skeletonPath}:{HipsBoneName}";

        int split = 0, airClips = 0;
        foreach (StringName animName in _animPlayer.GetAnimationList())
        {
            Animation anim = _animPlayer.GetAnimation(animName);
            int hipsTrack = -1, rootTrack = -1;
            for (int i = 0; i < anim.GetTrackCount(); i++)
            {
                if (anim.TrackGetType(i) != Animation.TrackType.Position3D) continue;
                string p = anim.TrackGetPath(i).ToString();
                if (p == hipsPath) hipsTrack = i;
                else if (p == boneTrack) rootTrack = i;
            }
            if (hipsTrack == -1 || anim.TrackGetKeyCount(hipsTrack) < 2) continue;

            if (rootTrack != -1 && anim.TrackGetKeyCount(rootTrack) >= 2)
            {
                Vector3 t = anim.PositionTrackInterpolate(rootTrack, anim.Length)
                        - anim.PositionTrackInterpolate(rootTrack, 0.0);
                if (t.Length() / Mathf.Max((float)anim.Length, 0.001f) > 0.2f) continue;
            }
            if (rootTrack == -1)
            {
                rootTrack = anim.AddTrack(Animation.TrackType.Position3D);
                anim.TrackSetPath(rootTrack, boneTrack);
            }

            Vector3 endKey   = anim.PositionTrackInterpolate(hipsTrack, anim.Length);
            Vector3 startKey = anim.PositionTrackInterpolate(hipsTrack, 0.0);
            bool full3D = Mathf.Abs((hipsParentB * (endKey - startKey)).Y) > 2.5f;
            if (full3D) airClips++;

            Vector3 k0 = anim.TrackGetKeyValue(hipsTrack, 0).AsVector3();
            for (int k = 0; k < anim.TrackGetKeyCount(hipsTrack); k++)
            {
                Vector3 key = anim.TrackGetKeyValue(hipsTrack, k).AsVector3();
                Vector3 dSkel = hipsParentB * (key - k0);
                if (RootMotionFlipZ) dSkel.Z = -dSkel.Z;

                Vector3 horiz = full3D ? dSkel : new Vector3(dSkel.X, 0f, dSkel.Z);
                Vector3 vert  = full3D ? Vector3.Zero : new Vector3(0f, dSkel.Y, 0f);

                anim.TrackSetKeyValue(hipsTrack, k, k0 + hipsParentB.Inverse() * vert);
                anim.TrackInsertKey(rootTrack, anim.TrackGetKeyTime(hipsTrack, k),
                                    rootRest + rootParentInv * horiz);
            }
            split++;
        }
        GD.Print($"RM: split hips->Root on {split} clips ({airClips} air clips routed full-3D).");
        return split;
    }

    private void RootMotionTrace(float dt)
    {
        if (!DebugRootMotionTrace || _animTree == null) return;
        _rmTraceTimer -= dt;
        if (_rmTraceTimer > 0f) return;
        _rmTraceTimer = 0.25f;
        GD.Print($"RMDBG | state={_stateMachine?.GetCurrentNode()} | blend={_blendPos:F2} | " +
                $"delta={_animTree.GetRootMotionPosition():F4} | " +
                 $"oldGetType={_animTree.Get("root_motion_position").VariantType} | " +
                $"track='{_animTree.RootMotionTrack}' | cb={_animTree.CallbackModeProcess}");
    }

    // ==================================================================
    //  FORCED CROUCH
    // ==================================================================
    private void UpdateForcedCrouch(float dt)
    {
        var q = PhysicsRayQueryParameters3D.Create(
            GlobalPosition + Vector3.Up * 0.3f, GlobalPosition + Vector3.Up * (StandClearance + 0.3f));
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        q.CollisionMask = (uint)StepCollisionMask;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);

        float target = 0f;
        if (hit.Count > 0)
        {
            float clearance = GlobalPosition.Y + StandClearance - hit["position"].AsVector3().Y - 0.3f;
            target = 1f - Mathf.Clamp((clearance - CrouchClearance) / (StandClearance - CrouchClearance), 0f, 1f);
        }
        _forcedCrouch = Mathf.Lerp(_forcedCrouch, target, 1f - Mathf.Exp(-10f * dt));

        bool crouched = Mathf.Max(_forcedCrouch, _isCrouching ? 1f : 0f) > 0.5f;
        _animTree?.Set(PIsCrouching, crouched);
        _animTree?.Set(PIsStanding, !crouched);

        if (BodyShape?.Shape is CapsuleShape3D cap)
            cap.Height = Mathf.Lerp(StandShapeHeight, CrouchShapeHeight, _forcedCrouch);
    }

    // ==================================================================
    //  LEG IK
    // ==================================================================
    private bool can_player_move = true;
    public void handle_leg_ik(double delta)
    {
        if (visual_for_IK == null || ik_leg_left == null || ik_leg_right == null ||
            ray_leg_left_front == null || ray_leg_right_front == null ||
            target_leg_left == null || target_leg_right == null)
        {
            if (_ikWarned++ == 0)
                GD.PushWarning("Player: leg-IK exports unassigned — IK disabled.");
            return;
        }

        float d = (float)delta;
        float horizSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        float speedGate = Mathf.Clamp(1f - horizSpeed / 2.5f, 0f, 1f);

        if (IsClimbing || IsVaulting)
        {
            ik_leg_left.Active = false;  ik_leg_right.Active = false;
            ik_leg_left.Influence = 0f;  ik_leg_right.Influence = 0f;
            Vector3 p = visual_for_IK.Position;
            p.Y = Mathf.Lerp(p.Y, 0f, 15f * d);
            visual_for_IK.Position = p;
            return;
        }

        bool grounded = IsOnFloor() && ik_is_enabled;
        ik_leg_left.Active = grounded;
        ik_leg_right.Active = grounded;

        if (grounded)
        {
            last_offset_l = _process_leg_ik(ray_leg_left_front, ray_leg_left_back, target_leg_left, ik_leg_left, d, speedGate);
            last_offset_r = _process_leg_ik(ray_leg_right_front, ray_leg_right_back, target_leg_right, ik_leg_right, d, speedGate);
            if (speedGate > 0.2f)
                choose_lowest_gap(d);
            else
            {
                Vector3 p = visual_for_IK.Position;
                p.Y = Mathf.Lerp(p.Y, 0f, 10f * d);
                visual_for_IK.Position = p;
            }
        }
        else
        {
            Vector3 visualPos = visual_for_IK.Position;
            visualPos.Y = Mathf.Lerp(visualPos.Y, 0.0f, 15.0f * d);
            visual_for_IK.Position = visualPos;
            ik_leg_left.Influence = 0.0f;
            ik_leg_right.Influence = 0.0f;
        }
    }

    public void choose_lowest_gap(float delta)
    {
        float lowest_gap = Mathf.Min(last_offset_l, last_offset_r);
        Vector3 visualPos = visual_for_IK.Position;
        if (lowest_gap < 0.0f)
        {
            visualPos.Y = Mathf.Lerp(visualPos.Y, lowest_gap, 10.0f * delta);
        }
        else
        {
            visualPos.Y = Mathf.Lerp(visualPos.Y, 0.0f, 10.0f * delta);
        }
        visual_for_IK.Position = visualPos;
    }

    private float _process_leg_ik(RayCast3D ray_f, RayCast3D ray_b, Marker3D target_marker, TwoBoneIK3D ik, float delta, float speedGate)
    {
        // Force raycast update to get current frame data
        ray_f.ForceRaycastUpdate();
        ray_b.ForceRaycastUpdate();

        bool is_f_colliding = ray_f.IsColliding();
        bool is_b_colliding = ray_b.IsColliding();

        if (!(is_f_colliding || is_b_colliding))
        {
            ik.Influence = Mathf.Lerp(ik.Influence, inactive_ik_influence, ik_lerp_speed * delta);
            return 0.0f;
        }

        float avg_hit_y;

        if (ray_f.IsColliding() && ray_b.IsColliding())
        {
            float w_f = front_ray_weight;
            float w_b = 1.0f - front_ray_weight;
            avg_hit_y = (ray_f.GetCollisionPoint().Y * w_f) + (ray_b.GetCollisionPoint().Y * w_b);
        }
        else if (is_f_colliding)
        {
            avg_hit_y = ray_f.GetCollisionPoint().Y;
        }
        else
        {
            avg_hit_y = ray_b.GetCollisionPoint().Y;
        }

        float height_diff = avg_hit_y - GlobalPosition.Y;
        float current_pos_y = 0.0f;

        if (height_diff > slope_threshold)
        {
            current_pos_y = pos_y_height_up;
        }
        else if (height_diff < -slope_threshold)
        {
            current_pos_y = pos_y_height_down;
        }
        else
        {
            current_pos_y = pos_y_height_flat;
        }

        Vector3 targetPos = target_marker.GlobalPosition;
        targetPos.Y = avg_hit_y + current_pos_y;
        target_marker.GlobalPosition = targetPos;

        ik.Influence = Mathf.Lerp(ik.Influence, active_ik_influence * speedGate, ik_lerp_speed * delta);

        return height_diff;
    }

    public void handle_foot_rotation(double delta)
    {
        float floatDelta = (float)delta;
        if (rotate_foot_active)
        {
            copy_left_foot.Active = true;
            copy_right_foot.Active = true;

            bool is_idle_or_ik_forced = IsOnFloor() && (ik_is_enabled || !can_player_move);

            if (is_idle_or_ik_forced)
            {
                _process_foot_alignment(floatDelta, ray_foot_left_front, ray_foot_left_back, copy_rotate_left, left_foot_rotate_offset);
                _process_foot_alignment(floatDelta, ray_foot_right_front, ray_foot_right_back, copy_rotate_right, right_foot_rotate_offset);
                _update_influence(floatDelta, rotation_influence);
            }
            else
            {
                _update_influence(floatDelta, 0.0f);
            }
        }
        else
        {
            copy_left_foot.Active = false;
            copy_right_foot.Active = false;
        }
    }

    private void _update_influence(float delta, float target)
    {
        copy_left_foot.Influence = Mathf.Lerp(copy_left_foot.Influence, target, 15.0f * delta);
        copy_right_foot.Influence = Mathf.Lerp(copy_right_foot.Influence, target, 15.0f * delta);
    }

    private void _process_foot_alignment(float delta, RayCast3D ray_front, RayCast3D ray_back, Node3D target_box, Vector3 offset)
    {
        // Force raycast update
        ray_front.ForceRaycastUpdate();
        ray_back.ForceRaycastUpdate();

        bool is_f_colliding = ray_front.IsColliding();
        bool is_b_colliding = ray_back.IsColliding();

        if (!(is_f_colliding || is_b_colliding))
        {
            return;
        }

        Vector3 final_normal;
        float final_hit_y;

        if (is_f_colliding && is_b_colliding)
        {
            final_normal = (ray_front.GetCollisionNormal() + ray_back.GetCollisionNormal()).Normalized();
            final_hit_y = (ray_front.GetCollisionPoint().Y + ray_back.GetCollisionPoint().Y) / 2.0f;
        }
        else if (is_f_colliding)
        {
            final_normal = ray_front.GetCollisionNormal().Normalized();
            final_hit_y = ray_front.GetCollisionPoint().Y;
        }
        else
        {
            final_normal = ray_back.GetCollisionNormal().Normalized();
            final_hit_y = ray_back.GetCollisionPoint().Y;
        }

        Vector3 boxPos = target_box.GlobalPosition;
        boxPos.Y = Mathf.Lerp(boxPos.Y, final_hit_y, delta * 20.0f);
        target_box.GlobalPosition = boxPos;

        Vector3 real_foot_forward = -visual_for_camera.GlobalTransform.Basis.Z.Normalized();
        Vector3 lateral_right_axis = real_foot_forward.Cross(final_normal).Normalized();
        if (Mathf.Abs(real_foot_forward.Dot(final_normal)) > 0.99f)
        {
            lateral_right_axis = visual_for_camera.GlobalTransform.Basis.X.Normalized();
        }

        Vector3 final_forward_z = final_normal.Cross(lateral_right_axis).Normalized();
        Basis target_basis = new Basis(lateral_right_axis, final_normal, final_forward_z).Orthonormalized();

        Quaternion target_quat = target_basis.GetRotationQuaternion();
        Quaternion current_quat = target_box.GlobalTransform.Basis.Orthonormalized().GetRotationQuaternion();
        Quaternion smoothed_quat = current_quat.Slerp(target_quat, 15.0f * delta);
        Quaternion offset_quat = Quaternion.FromEuler(new Vector3(Mathf.DegToRad(offset.X), Mathf.DegToRad(offset.Y), Mathf.DegToRad(offset.Z)));

        Transform3D boxTransform = target_box.GlobalTransform;
        boxTransform.Basis = new Basis(smoothed_quat * offset_quat);
        target_box.GlobalTransform = boxTransform;
    }

    [Export] public bool DebugModelOrientation = true;

    private void DumpModelOrientation()
    {
        if (_skeleton == null) return;
        GD.Print("=== MODEL ORIENTATION AUDIT ===");

        Node3D cur = _skeleton;
        while (cur != null && cur != this)
        {
            Vector3 r = cur.RotationDegrees;
            string flag = (Mathf.Abs(r.X) > 0.5f || Mathf.Abs(r.Y) > 0.5f || Mathf.Abs(r.Z) > 0.5f)
                ? "   <-- STALE ROTATION — fix or compensate here" : "";
            GD.Print($"  node '{cur.Name}'  rot=({r.X:F1}, {r.Y:F1}, {r.Z:F1}){flag}");
            cur = cur.GetParentOrNull<Node3D>();
        }

        foreach (string bone in new[] { "Root", HipsBoneName })
        {
            int i = _skeleton.FindBone(bone);
            if (i < 0) { GD.Print($"  bone '{bone}': not found"); continue; }
            Vector3 deg = _skeleton.GetBoneRest(i).Basis.GetEuler() * (180f / Mathf.Pi);
            GD.Print($"  bone '{bone}' rest rot=({deg.X:F1}, {deg.Y:F1}, {deg.Z:F1}) deg");
        }

        int hips = _skeleton.FindBone(HipsBoneName);
        if (hips >= 0)
        {
            Transform3D inBody = GlobalTransform.AffineInverse() *
                    _skeleton.GetBoneGlobalPose(hips);
            Vector3 pz = inBody.Basis.Z.Normalized();
            GD.Print($"  Hips +Z (body space) = ({pz.X:F2}, {pz.Y:F2}, {pz.Z:F2})   |   " +
                    $"Hips -Z = ({-pz.X:F2}, {-pz.Y:F2}, {-pz.Z:F2})");
            GD.Print("  Whichever vector points where his FACE points is his forward:");
            GD.Print("    (0,0,-1) -> model is FINE, the 90 deg is in the CAMERA, not the model");
            GD.Print("    (1,0,0)  -> faces 90 right  -> rotate visual +90 deg");
            GD.Print("    (-1,0,0) -> faces 90 left   -> rotate visual -90 deg");
            GD.Print("    (0,0,1)  -> faces backward  -> rotate visual 180 deg");
        }
    }

    [Export] public float RotationOffsetDeg = 90f;

    private readonly Dictionary<string, float> _stateOffsets = new()
    {
        { "wallhug_start", 90f },
        { "climb_down", 90f },
        // add more if needed
    };

    public void SetDriving(bool driving, CarController vehicle)
    {
        _isDriving = driving;
        _currentVehicle = vehicle;

        if (driving)
        {
            SetCollisionLayerValue(1, false);
            SetCollisionMaskValue(1, false); // also disable mask if needed
            // You might also want to hide the player mesh.
        }
        else
        {
            SetCollisionLayerValue(1, true);
            SetCollisionMaskValue(1, true);
        }
    }

    public void SetCameraTarget(Node3D target)
    {
        _cameraTarget = target;
    }
}