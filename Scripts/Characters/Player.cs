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
    [Export] public bool RootMotionFlipZ = false;   // tick ONLY if walking drives backward after this fix
    [Export] public float WalkSpeed = 2.0f;    // keep these equal to the blend ring radii
    [Export] public float RunSpeed = 4.9f;
    [Export] public float SprintSpeed = 5.8f;
    [Export] public float CrouchSpeed = 1.2f;
    [Export] public float JumpVelocity = 3.0f;
    [Export] public float TurnSpeed = 12.0f;
    [Export] public float GroundAccel = 40.0f;  // code-driven fallback only
    [Export] public float GroundDecel = 25.0f;
    [Export] public float AirAccel = 8.0f;
    [Export] public float PushForce = 4.0f;
    [Export] public Skeleton3D Skeleton;
    [Export] public string RootMotionBoneName = "Root";
    private bool _rootMotionAvailable;   // set by SetupRootMotion; gates ApplyMovement

    [ExportSubgroup("Sprint Dash")]
    [Export] public float DashSpeed = 3.5f;     // extra m/s on sprint start
    [Export] public float DashDuration = 0.25f;
    private float _stillTime;   // seconds with horizSpeed < 0.8
    private float _moveTime;    // seconds with horizSpeed >= 0.8
    private float _pivotTimer;
    private float _pivotTargetYaw;
    private float _pivotTotal;
    private float _pivotStartYaw;
    private float _stopSpeed;

    [Export] public float ClimbSpeed = 1.5f;
    [Export] public float WallhugSpeed = 0.8f;
    private bool _isClimbing;
    private Vector3 _climbNormal;

    [ExportSubgroup("Step Up")]
    [Export] public float StepHeight = 0.4f;
    [Export] public float StepCheckDistance = 0.3f;
    [Export] public int StepCollisionMask = 2;
    [Export] public CollisionShape3D ColCapsuleFull;
    [Export] public CollisionShape3D ColCapsuleCrouch;

    [ExportSubgroup("Root Motion")]
    [Export] public bool UseRootMotion = true;
    [Export] public float RootMotionScale = 1.0f;    // tune if anim speeds != export speeds
    [Export] public bool MatchDesiredSpeed = false;  // true = exact speeds (may foot-slide)
    [Export] public float BlendSmoothing = 8.0f;     // blend-position smoothing rate
    [Export] public float StandClearance = 1.9f;   // ceiling height you need standing
    [Export] public float CrouchClearance = 1.25f; // height you need fully crouched
    [Export] public CollisionShape3D BodyShape;    // your capsule
    [Export] public float StandShapeHeight = 1.8f;
    [Export] public float CrouchShapeHeight = 1.2f;
    private float _forcedCrouch;   // 0..1
    private bool _isWallhugging; private Vector3 _wallNormal; private float _wallPressTime;
    private float _airTime; private float _landImpact; private bool _wasOnFloor = true;

    [ExportSubgroup("Foot&Leg IK")]

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

    // Foot Rotation
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

    // ==================================================================
    //  ANIMATION TREE PARAMETER PATHS (must match your tree!)
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

    // --- locomotion intent (recomputed every physics frame) ---
    private bool _isWalking;                 // Ctrl toggle: false = run (default)
    private bool _isCrouching;               // C toggle
    private float _targetSpeed;
    private Vector3 _moveDirWorld = Vector3.Zero;
    private Vector2 _blendPos = Vector2.Zero;
    private float _dashTimer;
    private Vector3 _dashDir = Vector3.Zero;

    // --- root motion diagnostics ---
    private bool _rootMotionMissing;
    private bool _rootMotionWarned;
    private float _rmSilentTime;

    // --- nodes ---
    private Node3D _cameraGimbal;
    private Node3D _innerGimbal;
    private SpringArm3D _springArm;

    // --- tracking ---
    private NpcEyeTracker _eyeTracker;
    private Area3D _interestArea;
    private Node3D _casualTarget;
    private NpcInteraction _currentNpc;
    private InteractableItem _currentInteractable;

    // --- camera state ---
    private bool _isFirstPerson;
    private bool _isLockedOn;
    private float _targetZoom = 3.0f;

    // --- possession ---
    private PlayerPossession _possession;
    public bool IsPossessed => _possession != null && _possession.IsPossessed;

    // --- combat ---
    private ItemData _fistWeapon;
    private ItemData _pipeWeapon;
    private ItemData _chairWeapon;
    private ItemData _currentWeapon;
    private AudioStreamPlayer _attackAudioPlayer;
    private float _attackCooldownTimer;

    // --- pinch-to-zoom ---
    private int _pinch0 = -1;
    private int _pinch1 = -1;
    private float _pinchBaseDist;
    private float _pinchBaseZoom;
    private readonly Dictionary<int, Vector2> _touchStartPositions = new();

    // ==================================================================

    public override void _Ready()
    {

        ColCapsuleFull.Disabled = false;
        ColCapsuleCrouch.Disabled = true;
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
            if (_animPlayer != null && _animPlayer.HasAnimation("climb_up_stand"))
                _animPlayer.GetAnimation("climb_up_stand").LoopMode = Animation.LoopModeEnum.None;
            _stateMachine = (AnimationNodeStateMachinePlayback)_animTree.Get("parameters/playback");
            _animTree.Active = true;
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
    }

    private void DebugRootMotion()
    {
        if (_animPlayer == null || _animTree == null) return;
        GD.Print($"=== Root motion diagnostics ===");
        GD.Print($"Tree RootMotionTrack = '{_animTree.RootMotionTrack}'");
        GD.Print($"Tree process mode   = {_animTree.CallbackModeProcess}");
        foreach (StringName animName in _animPlayer.GetAnimationList())
        {
            Animation anim = _animPlayer.GetAnimation(animName);
            for (int i = 0; i < anim.GetTrackCount(); i++)
            {
                if (anim.TrackGetType(i) != Animation.TrackType.Position3D) continue;
                string path = anim.TrackGetPath(i).ToString();
                if (!path.Contains("Root") && !path.Contains("Hips") && !path.Contains("Pelvis")) continue;

                double len = anim.Length;
                Vector3 travel = anim.PositionTrackInterpolate(i, len) - anim.PositionTrackInterpolate(i, 0.0);
                GD.Print($"{animName} | {path} | travel/loop: {travel:F3} m (~{travel.Length() / (float)Math.Max(len, 0.001):F2} m/s)");
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        TouchTracker.Update(@event);
        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed) _touchStartPositions[touch.Index] = touch.Position;
            else _touchStartPositions.Remove(touch.Index);
        }

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
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= dt;

        bool anyMenuOpen = (HUD.Instance != null && HUD.Instance.IsInventoryOpen) ||
                           (HUD.Instance != null && HUD.Instance.IsHealthPanelOpen);

        // Camera & support systems always run (menus, possession, cutscenes...)
        UpdateCamera(dt);
        UpdateLockOn(dt);
        UpdateEyeTracker();

        if (IsPossessed) { MoveAndSlide(); return; }

        UpdateLocomotionIntent(dt, anyMenuOpen);   // input, toggles, dash trigger
        UpdateFacing(dt);                          // rotate-to-move / lock-on strafe

        Vector3 velocity = Velocity;
        float hs = new Vector2(Velocity.X, Velocity.Z).Length();
        if (hs < 0.8f) { _stillTime += dt; _moveTime = 0f; }
        else           { _moveTime += dt;  _stillTime = 0f; }
        FloorSnapLength = hs > 2.0f ? 0.03f : 0.1f;   // at speed: falls off; at idle: stable on slopes
        // gravity: skip when climbing
        if (!_isClimbing && !IsOnFloor())
            velocity.Y -= Gravity * dt;
        if (!anyMenuOpen && Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpVelocity;

        ApplyMovement(dt, ref velocity);           // root motion or code-driven

        Velocity = velocity;
        MoveAndSlide();
        DetectWallhugAndClimb(dt);
        if (ik_is_enabled)
        {handle_leg_ik(dt);
        handle_foot_rotation(dt);}
        RootMotionTrace(dt);

        float fallSpeed = -velocity.Y;

        if (IsOnFloor())
        {
            if (!_wasOnFloor)          // just landed this frame
            {
                _landImpact = fallSpeed;   // store the impact speed
                TryPlayLand(_landImpact);
            }
            _airTime = 0f;
        }
        else
        {
            _airTime += dt;
        }
        _wasOnFloor = IsOnFloor();

        UpdateForcedCrouch(dt);
        TraceAnimState();
        RootMotionDebug(dt);

        PushRigidBodies();
        UpdateAnimationParams(dt, anyMenuOpen);    // feed the tree AFTER moving
        UpdateInteraction(anyMenuOpen);
    }

    // ==================================================================
    //  LOCOMOTION
    // ==================================================================
    private void UpdateLocomotionIntent(float dt, bool anyMenuOpen)
    {
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

        // Camera-relative world direction
        _moveDirWorld = _cameraGimbal.GlobalTransform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y);
        _moveDirWorld.Y = 0f;
        _moveDirWorld = _moveDirWorld.LengthSquared() > 1e-6f ? _moveDirWorld.Normalized() : Vector3.Zero;

        // Remember intent for auto-run while menus are open
        if (!anyMenuOpen && GameState.Instance != null)
        {
            GameState.Instance.AutoRunDirection = inputDir;
            GameState.Instance.AutoRunSprinting = sprintHeld;
        }

        // Sprint-start dash (impulse blended out over DashDuration)
        if (!anyMenuOpen && Input.IsActionJustPressed("sprint") &&
            IsOnFloor() && _moveDirWorld != Vector3.Zero && !_isCrouching && !_isWalking)
        {
            _dashTimer = DashDuration;
            _dashDir = _moveDirWorld;
        }
    }

    private void UpdateFacing(float dt)
    {
        if (_pivotTimer > 0f)   // pivot clip owns the body
        {
            _pivotTimer -= dt;
            float k = Mathf.Clamp(1f - _pivotTimer / Mathf.Max(_pivotTotal, 0.01f), 0f, 1f);
            k = k * k * (3f - 2f * k);   // smoothstep
            Rotation = new Vector3(0f, _pivotStartYaw + Mathf.AngleDifference(_pivotTargetYaw, _pivotStartYaw) * k, 0f);
            return;
        }

        Vector3 faceDir;
        if (_isLockedOn && LockOnTarget != null)
        {
            faceDir = LockOnTarget.GlobalPosition - GlobalPosition; // strafe mode
            faceDir.Y = 0f;
            if (faceDir.LengthSquared() < 0.001f) return;
        }
        else if (_moveDirWorld != Vector3.Zero)
        {
            faceDir = _moveDirWorld;
        }
        else return;

        float targetYaw = Mathf.Atan2(-faceDir.X, -faceDir.Z);
        Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, TurnSpeed * dt), 0f);
        float yawDiff = Mathf.RadToDeg(Mathf.AngleDifference(targetYaw, Rotation.Y)); // [-180,180]
        float speed = new Vector2(Velocity.X, Velocity.Z).Length();
        if (Mathf.Abs(yawDiff) > 110f && speed > 1.5f && IsOnFloor() && _stateMachine != null)
        {
            _stateMachine.Travel(Mathf.Abs(yawDiff) > 155f ? "Pivot180" : "Pivot90L");
            _pivotTimer = _animPlayer != null && _animPlayer.HasAnimation("Run_180")
                ? (float)_animPlayer.GetAnimation("Run_180").Length : 0.6f;
            _pivotTotal = _pivotTimer;
            _pivotStartYaw = Rotation.Y;
            _pivotTargetYaw = Rotation.Y + Mathf.DegToRad(yawDiff);
        }
    }

    private static readonly HashSet<string> RootMotionStates = new()
    { "run_start", "run_stop", "Pivot180", "Pivot90L", "ClimbUpStand" };
    private void ApplyMovement(float dt, ref Vector3 velocity)
    {
        string st = _stateMachine?.GetCurrentNode() ?? "";
        bool rmAllowed = UseRootMotion && _rootMotionAvailable && _animTree != null
                        && RootMotionStates.Contains(st);
        bool usedRootMotion = false;

        if (_isClimbing) { ClimbMove(dt, ref velocity); return; }
        if (_isWallhugging) { WallhugMove(dt, ref velocity); return; }

        if (IsOnFloor())
        {
            if (rmAllowed)
            {
                Vector3 localDelta = _animTree.GetRootMotionPosition();
                UpdateRootMotionWatchdog(dt, localDelta);

                if (!_rootMotionMissing)
                {
                    usedRootMotion = true;
                    Basis basis = _skeleton != null ? _skeleton.GlobalTransform.Basis : GlobalTransform.Basis;
                    Vector3 worldDelta = basis * localDelta;
                    Vector3 rmVel = new Vector3(worldDelta.X, 0f, worldDelta.Z)
                                    * (RootMotionScale / Mathf.Max(dt, 1e-5f));
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

            if (!usedRootMotion)   // ALWAYS runs in Jump/Fall/Land/Attack — friction + control live here
            {
                Vector3 target = _moveDirWorld * _targetSpeed;
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
            _rootMotionMissing = false;   // auto-recovers, even if you fix the track at runtime
            return;
        }

        if (_blendPos.Length() < 0.5f)    // near-idle: zero delta is expected, don't count it
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
                GD.PushError("Root Motion Track returns zero while locomotion is blending. The track only REPORTS " +
                            "displacement already keyframed on that bone in the clips — it doesn't create it. " +
                            "Run DebugRootMotion() to see which bone actually travels, and point the track at that.");
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
            // Only treat it as a step if the surface is nearly flat (normal.y > 0.95)
            if (floorNormal.Y < 0.95f)
                return;

            float floorY = downResult["position"].AsVector3().Y;
            float stepUp = floorY - GlobalPosition.Y;
            if (stepUp > 0.05f && stepUp <= StepHeight)
                GlobalPosition = new Vector3(GlobalPosition.X, floorY, GlobalPosition.Z);
        }
    }

    private void ToggleCrouch()
    {
        if (_isCrouching && _forcedCrouch > 0.15f) return;  // no headroom — refuse to stand
        _isCrouching = !_isCrouching;
        ApplyCrouchShape();
    }

    private void ApplyCrouchShape()
    {
        bool crouched = _isCrouching || _forcedCrouch > 0.5f;   // forced crouch auto-swaps too
        if (ColCapsuleFull != null) ColCapsuleFull.Disabled = crouched;
        if (ColCapsuleCrouch != null) ColCapsuleCrouch.Disabled = !crouched;
    }

    // FOOT&LEG IK
    private bool can_player_move = true;
    public void handle_leg_ik(double delta)
    {
        float d = (float)delta;
        float horizSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        // speedGate: 1.0 at idle, 0.0 when speed >= 2.5 m/s (tune the threshold)
        float speedGate = Mathf.Clamp(1f - horizSpeed / 2.5f, 0f, 1f);
        if (_isClimbing || _isWallhugging)
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

            // Only sink the visual root when nearly stationary (avoids clamping during motion)
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
            // airborne branch (keep your existing code)
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

        // The speedGate reduces IK influence when moving faster
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
                // Calculate left foot rotation based on the average of two raycasts
                _process_foot_alignment(floatDelta, ray_foot_left_front, ray_foot_left_back, copy_rotate_left, left_foot_rotate_offset);
                // Calculate right foot rotation based on the average of two raycasts
                _process_foot_alignment(floatDelta, ray_foot_right_front, ray_foot_right_back, copy_rotate_right, right_foot_rotate_offset);

                // Toggle CopyTransformModifier3D influence: 0.0 when running, 1.0 when idle
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

    // Calculating the rotation of Foot (Target node)
    private void _process_foot_alignment(float delta, RayCast3D ray_front, RayCast3D ray_back, Node3D target_box, Vector3 offset)
    {
        bool is_f_colliding = ray_front.IsColliding();
        bool is_b_colliding = ray_back.IsColliding();

        if (!(is_f_colliding || is_b_colliding))
        {
            return; // If RayCast3D touch nothing, return no value
        }

        // --- 1. Calculate the average value ---
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

        // --- 2. Apply rotation on foot (Target node) ---
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

        // --- 3. Rotation offset for foot due to different rig has different offset ---
        Quaternion target_quat = target_basis.GetRotationQuaternion();
        Quaternion current_quat = target_box.GlobalTransform.Basis.Orthonormalized().GetRotationQuaternion();
        Quaternion smoothed_quat = current_quat.Slerp(target_quat, 15.0f * delta);
        Quaternion offset_quat = Quaternion.FromEuler(new Vector3(Mathf.DegToRad(offset.X), Mathf.DegToRad(offset.Y), Mathf.DegToRad(offset.Z)));

        Transform3D boxTransform = target_box.GlobalTransform;
        boxTransform.Basis = new Basis(smoothed_quat * offset_quat);
        target_box.GlobalTransform = boxTransform;
    }

    // ==================================================================
    //  ANIMATION
    // ==================================================================
    private void UpdateAnimationParams(float dt, bool anyMenuOpen)
    {
        if (_animTree == null) return;

        bool justLanded = IsOnFloor() && _landImpact > 0f;

        Vector2 blendTarget = Vector2.Zero;
        if (_moveDirWorld != Vector3.Zero)
        {
            Vector3 local = GlobalTransform.Basis.Inverse() * _moveDirWorld;
            blendTarget = new Vector2(local.X, -local.Z) * _targetSpeed;
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

        float horizSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        bool wantsMove = _targetSpeed > 0.1f;

        // REMOVED: is_moving and is_stopping conditions They are now driven by Travel() calls.

        bool crouched = _isCrouching || _forcedCrouch > 0.5f;
        _animTree.Set(PIsCrouching, crouched);
        _animTree.Set(PIsStanding, !crouched);
        _animTree.Set(PIsOnFloor, IsOnFloor());
        _animTree.Set(PIsJumping, !IsOnFloor() && Velocity.Y > 0.1f);
        _animTree.Set(PIsFalling, !IsOnFloor() && (_airTime > 0.25f || Velocity.Y < -3.0f));

        // Travel to run_start / run_stop from Locomotion
        string st = _stateMachine?.GetCurrentNode() ?? "";
        if (st == "Locomotion" || st == "run_start")
        {
            // ---- START ----
            if (wantsMove && _stillTime > 0.35f && _blendPos.Length() < 0.8f)
            {
                // Travel to the Start blend space
                _stateMachine.Travel("run_start");
            }
            // ---- STOP ----
            else if (!wantsMove && _moveTime > 0.25f && horizSpeed > 1.0f)
            {
                _stopSpeed = horizSpeed;
                _stateMachine.Travel("run_stop");
            }
        }

        // parameters/Start/blend_position
        _animTree.Set("parameters/Start/blend_position", Mathf.Clamp(_targetSpeed, 0.5f, 5.8f));
        _animTree.Set("parameters/run_stop/blend_position", _stopSpeed);

        if (justLanded) _landImpact = 0f;
    }

    private void TryPlayLand(float impact)
    {
        if (_stateMachine == null || !IsOnFloor() || impact < 1.5f) return;
        string target = impact >= 12f ? "LandStumble"      // <- use YOUR exact state names
                    : impact >= 7f  ? "LandHeavy"
                    :                 "LandSoft";
        _stateMachine.Travel(target);
        GD.Print($"LAND impact={impact:F1} -> {target}");   // keep until verified
    }

    private string _lastState = "";
    private void TraceAnimState()
    {
        if (_stateMachine == null) return;
        string st = _stateMachine.GetCurrentNode();
        if (st == _lastState) return;
        GD.Print($"ANIM '{_lastState}' -> '{st}'" +
            (!RootMotionStates.Contains(st) ? "   [NOT in RootMotionStates -> CODE-DRIVEN, this is your slide]" : ""));
        _lastState = st;
    }

    private float _rmDbgTimer;
    private void RootMotionDebug(float dt)
    {
        // Stop logging when the export flag is off
        if (!DebugRootMotionTrace || _animTree == null) return;

        _rmDbgTimer -= dt;
        if (_rmDbgTimer > 0) return;
        _rmDbgTimer = 0.5f;
        GD.Print($"RMDBG state='{_lastState}' blend={_blendPos:F2} " +
                $"rmDelta={_animTree.GetRootMotionPosition():F3} " +
                $"vel={new Vector2(Velocity.X, Velocity.Z).Length():F2} missing={_rootMotionMissing}");
    }

    // ==================================================================
    //  CAMERA
    // ==================================================================
    private void UpdateCamera(float dt)
    {
        Vector3 targetPos = GlobalPosition + CameraOffset;
        float t = 1f - Mathf.Exp(-CameraSmoothing * dt);

        if (_isFirstPerson)
            _cameraGimbal.GlobalPosition = targetPos;           // rigid: no lag in 1st person
        else
            _cameraGimbal.GlobalPosition = _cameraGimbal.GlobalPosition.Lerp(targetPos, t);

        // Smooth 1st <-> 3rd person swap by animating the spring arm length
        float desiredLength = _isFirstPerson ? 0f : _targetZoom;
        _springArm.SpringLength = Mathf.Lerp(_springArm.SpringLength, desiredLength, t);

        if (PlayerCamera != null)
        {
            // Layer 4 = player body. Hide it once the arm is short enough to sit
            // inside the head. (If your pickups also live on layer 4, move them,
            // or they'll vanish in first person.)
            PlayerCamera.SetCullMaskValue(4, _springArm.SpringLength > 0.6f);
        }
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

    private bool IsAnyMenuOpen()
    {
        return HUD.Instance != null && HUD.Instance.IsGamePaused;
    }

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
        _attackCooldownTimer = _currentWeapon?.AttackCooldown ?? 0.4f;

        Node3D nearestEnemy = FindNearestEnemy();
        if (nearestEnemy != null)
        {
            Vector3 dirToEnemy = (nearestEnemy.GlobalPosition - GlobalPosition).Normalized();
            dirToEnemy.Y = 0;
            float targetAngle = Mathf.Atan2(-dirToEnemy.X, -dirToEnemy.Z);
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, 0.5f), 0);
        }

        _stateMachine?.Travel("Attack");

        if (_attackAudioPlayer != null && _attackSound != null)
        {
            _attackAudioPlayer.Stream = _attackSound;
            _attackAudioPlayer.Play();
        }

        EnableHitbox();
    }

    private async void EnableHitbox()
    {
        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
        if (_rightHandHitbox != null && IsInstanceValid(_rightHandHitbox))
            _rightHandHitbox.StartSwing(_currentWeapon);
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
    private void UpdateInteraction(bool anyMenuOpen)
    {
        if (!anyMenuOpen && PlayerCamera != null)
        {
            var spaceState = GetWorld3D().DirectSpaceState;

            Vector3 origin = PlayerCamera.GlobalPosition;
            Vector3 end = origin - PlayerCamera.GlobalTransform.Basis.Z * 10.0f;

            var query = PhysicsRayQueryParameters3D.Create(origin, end);
            query.CollisionMask = 4;
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            var result = spaceState.IntersectRay(query);
            InteractableItem itemTarget = null;
            NpcInteraction npcTarget = null;

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
                }
            }

            if (itemTarget != _currentInteractable || npcTarget != _currentNpc)
            {
                _currentInteractable = itemTarget;
                _currentNpc = npcTarget;

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
            }
        }
        else if (_hud != null && (_currentInteractable != null || _currentNpc != null))
        {
            _hud.HideTooltip();
            _currentInteractable = null;
            _currentNpc = null;
        }
    }
    private void SetupRootMotion()
    {
        _rootMotionAvailable = false;
        if (_animTree == null) { GD.PushError("RM: AnimationTree node missing."); return; }
        if (_animPlayer == null) { GD.PushError("RM: AnimationPlayer node missing."); return; }

        Node rootNode = _animPlayer.GetNodeOrNull(_animPlayer.RootNode);
        if (rootNode == null) { GD.PushError("RM: AnimationPlayer.RootNode doesn't resolve."); return; }

        // ---- 1. Find the skeleton by resolving the clips' own track paths ----
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
        if (skeleton == null) // fallback: search the whole subtree
        {
            foreach (Node n in FindChildren("Skeleton3D", "Skeleton3D", true, false))
                if (n is Skeleton3D sk)
                { skeleton = sk; skeletonPath = rootNode.GetPathTo(sk).ToString(); break; }
        }
        if (skeleton == null) { GD.PushError("RM: no Skeleton3D found under the player."); return; }

        _skeleton = skeleton;   // also fixes the wrong-basis bug in ApplyMovement
        GD.Print($"RM: skeleton = {skeletonPath} (player root: {rootNode.Name}, tree RootNode: {_animTree.RootNode})");
        ReportRootBaselines();

        // ---- 2. Scan every Position3D track once ----
        string boneTrack = $"{skeletonPath}:{RootMotionBoneName}";
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

        // ---- 3. Report ----
        PrintAllTracks(_animPlayer.HasAnimation("Walk_fwd") ? "Walk_fwd" : "walk_fwd");
        travelers.Sort((a, b) => b.mps.CompareTo(a.mps));
        GD.Print("--- top moving Position3D tracks (whole library) ---");
        foreach (var t in travelers.Take(12))
            GD.Print($"  {t.anim} | {t.path} | {t.mps:F2} m/s");

        // ---- 4. Configure (mixed libraries are normal mid-migration: always convert) ----
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

    // Keys on a root-motion bone track live in the bone's PARENT space. Root's parent
    // is the skeleton itself, so skeleton-space deltas + rest position are exactly right.
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
                if (p.Contains(':')) continue;                 // bone track
                if (!rmNodePaths.Contains(p)) continue;         // not a root-motion node

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
                else // Rotation3D — keys are stored as QUATERNIONS, not Euler vectors
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
        return Quaternion.FromEuler(v.AsVector3());   // handles euler-stored keys, no ambiguous ctor
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
                if (a.TrackGetPath(i).ToString().Contains(':')) continue;   // bone tracks only excluded
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

            // Air clips (big vertical travel = real motion, not a bob): route ALL axes
            // to Root. Ground clips: horizontal to Root, vertical bob stays on hips.
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

        // drives: tree crouch state, capsule, camera
        bool crouched = Mathf.Max(_forcedCrouch, _isCrouching ? 1f : 0f) > 0.5f;
        _animTree?.Set(PIsCrouching, crouched);
        _animTree?.Set(PIsStanding, !crouched);

        if (BodyShape?.Shape is CapsuleShape3D cap)
            cap.Height = Mathf.Lerp(StandShapeHeight, CrouchShapeHeight, _forcedCrouch); // shared resource — duplicate per player if you ever instance
    }

    private void UpdateWallhug(float dt, ref Vector3 velocity)
    {
        if (_isWallhugging) { WallhugMove(dt, ref velocity); return; }
        if (!IsOnFloor() || _forcedCrouch > 0.3f) { _wallPressTime = 0; return; }

        bool pressing = false;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            Vector3 n = GetSlideCollision(i).GetNormal();
            if (Mathf.Abs(n.Y) > 0.3f) continue;                      // not a vertical wall
            if (_moveDirWorld.Dot(-n) > 0.6f)                          // input pushes INTO it
            { _wallNormal = n; pressing = true; break; }
        }

        _wallPressTime = pressing ? _wallPressTime + dt : 0f;
        if (_wallPressTime > 0.15f && _animTree != null)
        {
            _isWallhugging = true;
            _stateMachine.Travel("WallhugStart");
        }
    }

    private void WallhugMove(float dt, ref Vector3 velocity)
    {
        Vector3 tangent = _wallNormal.Cross(Vector3.Up).Normalized();
        if (tangent.Dot(GlobalTransform.Basis.Z) < 0) tangent = -tangent;

        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(-tangent.X, -tangent.Z), 8f * dt), 0);

        bool w = Input.IsActionPressed("move_forward");
        bool a = Input.IsActionPressed("move_left");
        bool d = Input.IsActionPressed("move_right");

        if (!w || (!a && !d))   // must hold W AND (A or D) — release either → end
        {
            _isWallhugging = false; _wallPressTime = 0;
            _stateMachine?.Travel("WallhugEnd");
            return;
        }

        float dir = (d ? 1f : 0f) - (a ? 1f : 0f);
        velocity.X = tangent.X * dir * WallhugSpeed;
        velocity.Z = tangent.Z * dir * WallhugSpeed;
    }
    private bool ProbeLedge(Vector3 wallNormal, out Vector3 anchor, out float height)
    {
        anchor = Vector3.Zero; height = 0f;
        Vector3 dir = -wallNormal;
        float lastHit = -1f;
        for (float h = 0.5f; h <= 2.6f; h += 0.2f)
        {
            var q = PhysicsRayQueryParameters3D.Create(
                GlobalPosition + Vector3.Up * h + dir * 0.75f,
                GlobalPosition + Vector3.Up * h + dir * 0.05f);
            q.CollisionMask = (uint)StepCollisionMask;
            if (GetWorld3D().DirectSpaceState.IntersectRay(q).Count > 0) lastHit = h;
            else if (lastHit > 0f)
            {
                var dq = PhysicsRayQueryParameters3D.Create(
                    GlobalPosition + Vector3.Up * h + dir * 0.75f,
                    GlobalPosition + Vector3.Up * lastHit + dir * 0.75f);
                dq.CollisionMask = (uint)StepCollisionMask;
                var d = GetWorld3D().DirectSpaceState.IntersectRay(dq);
                if (d.Count > 0)
                {
                    height = d["position"].AsVector3().Y - GlobalPosition.Y;
                    anchor = d["position"].AsVector3() + wallNormal * 0.3f;
                    return true;
                }
                lastHit = -1f;
            }
        }
        return false;
    }

    private void DetectWallhugAndClimb(float dt)
    {
        if (_isClimbing)
        {
            string st = _stateMachine?.GetCurrentNode() ?? "";
            if (st == "Locomotion" || st == "Fall") _isClimbing = false;  // exited climb
            return;
        }

        // --- Climb: airborne + into wall ---
        if (!IsOnFloor() && _airTime > 0.1f)
        {
            for (int i = 0; i < GetSlideCollisionCount(); i++)
            {
                Vector3 n = GetSlideCollision(i).GetNormal();
                if (Mathf.Abs(n.Y) > 0.3f) continue;
                if (Velocity.Dot(-n) > 0.5f || _moveDirWorld.Dot(-n) > 0.5f)
                {
                    _climbNormal = n;
                    _isClimbing = true;
                    _isWallhugging = false;
                    Rotation = new Vector3(0, Mathf.Atan2(-n.X, -n.Z), 0);  // face wall
                    _stateMachine?.Travel("ClimbIdle");
                    return;
                }
            }
        }

        // --- Jump during wallhug → climb ---
        if (_isWallhugging && Input.IsActionJustPressed("jump"))
        {
            _climbNormal = _wallNormal;
            _isClimbing = true;
            _isWallhugging = false;
            _stateMachine?.Travel("ClimbIdle");
            return;
        }

        // --- Wallhug entry: grounded + pressing into wall ---
        if (_isWallhugging) return;
        if (!IsOnFloor() || _forcedCrouch > 0.3f) { _wallPressTime = 0; return; }

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            Vector3 n = GetSlideCollision(i).GetNormal();
            if (Mathf.Abs(n.Y) > 0.3f) continue;
            if (_moveDirWorld.Dot(-n) > 0.6f)
            {
                _wallNormal = n;
                _wallPressTime += dt;
                if (_wallPressTime > 0.15f) { _isWallhugging = true; _stateMachine?.Travel("WallhugStart"); }
                return;
            }
        }
        _wallPressTime = 0;
    }

    private void ClimbMove(float dt, ref Vector3 velocity)
    {
        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");

        // Shift, S, or jump = drop off the wall
        if (Input.IsActionJustPressed("sprint") || input.Y < -0.1f || Input.IsActionJustPressed("jump"))
        {
            _isClimbing = false;
            velocity = -_climbNormal * 2.0f;   // push away from wall so gravity catches you
            _stateMachine?.Travel("Fall");
            return;
        }

        velocity = Vector3.Zero;
        velocity += -_climbNormal * 0.5f;      // lean in to maintain contact

        string st = _stateMachine?.GetCurrentNode() ?? "";

        if (st == "ClimbIdle")
        {
            if (input.Y > 0.1f) _stateMachine?.Travel("ClimbUp");
        }
        else if (st == "ClimbUp")
        {
            if (input.Y > 0.1f)
            {
                velocity.Y = ClimbSpeed;
                Vector3 probe = GlobalPosition + (-_climbNormal * 0.3f) + Vector3.Up * 1.6f;
                var q = PhysicsRayQueryParameters3D.Create(probe, probe + _climbNormal * 0.4f);
                q.CollisionMask = (uint)StepCollisionMask;
                q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
                if (GetWorld3D().DirectSpaceState.IntersectRay(q).Count == 0)
                    _stateMachine?.Travel("ClimbUpStand");
            }
            else _stateMachine?.Travel("ClimbIdle");
        }
        else if (st == "ClimbUpStand")
        {
            if (_animTree != null && UseRootMotion)
            {
                Vector3 localDelta = _animTree.GetRootMotionPosition();
                Basis basis = _skeleton != null ? _skeleton.GlobalTransform.Basis : GlobalTransform.Basis;
                velocity = (basis * localDelta) / Mathf.Max(dt, 1e-5f) * RootMotionScale;
            }
        }
    }
}