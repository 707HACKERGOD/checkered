using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody3D
{
    // --- MOVEMENT ---
    [ExportGroup("Movement")]
    [Export] public float WalkSpeed = 3.5f;
    [Export] public float RunSpeed = 6.0f;
    [Export] public float JumpVelocity = 3.0f;
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
    private NpcInteraction _currentNpc;

    // Camera states
    private bool _isFirstPerson = false;
    private bool _isLockedOn = false;
    private float _targetZoom = 3.0f;

    private float _maxZoom = 6.0f;

    // --- POSSESSION ---

    private PlayerPossession _possession;
    public bool IsPossessed => _possession != null && _possession.IsPossessed;

    // --- COMBAT ---
    [Export] private AudioStream _attackSound;
    private AudioStreamPlayer _attackAudioPlayer;
    private float _minZoom = 1.5f;

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
        _possession = GetNodeOrNull<PlayerPossession>("PlayerPossession");
        
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
            // Attack sound
            if (_attackSound != null)
            {
                _attackAudioPlayer = new AudioStreamPlayer();
                AddChild(_attackAudioPlayer);
            }
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

        // Attack
        if (@event.IsActionPressed("attack") && !anyMenuOpen)
        {
            if (IsPossessed)
            {
                GetViewport().SetInputAsHandled();
                return;
            }
            PerformAttack();
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta; // Engine.TimeScale is applied automatically by Godot

        _cameraGimbal.GlobalPosition = GlobalPosition + new Vector3(0, 1.5f, 0);
        Vector3 velocity = Velocity;

        // Always process camera (first/third person, zoom, lock-on)
        UpdateCameraTransitions(dt);
        UpdateLockOn(dt);
        UpdateEyeTracker();

        bool anyMenuOpen = (HUD.Instance != null && HUD.Instance.IsInventoryOpen) ||
                           (HUD.Instance != null && HUD.Instance.IsHealthPanelOpen);

        if (IsPossessed)
        {
            // Movement is handled entirely by PlayerPossession – we still need MoveAndSlide() once.
            MoveAndSlide();
            return;
        }

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

        // --- INTERACTION ---
        if (!anyMenuOpen && PlayerCamera != null)
        {
            var spaceState = GetWorld3D().DirectSpaceState;

            // Ray from camera – always long enough to reach anything regardless of camera mode
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
                // Distance check: measure from player, not from camera
                Vector3 playerCenter = GlobalPosition + new Vector3(0, 1.5f, 0);   // chest height
                Vector3 hitPoint = (Vector3)result["position"];
                float distToPlayer = playerCenter.DistanceTo(hitPoint);

                // Only accept hits within the player's personal interaction radius
                if (distToPlayer <= _interactDistance)
                {
                    var collider = result["collider"].AsGodotObject();
                    if (collider is InteractableItem item)
                        itemTarget = item;
                    else if (collider is CharacterBody3D body)
                        npcTarget = body.GetNodeOrNull<NpcInteraction>("Interaction");
                }
            }

            // Handle tooltip switching
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

            // Interact key
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
                    _currentNpc.Interact();   // start dialogue
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
            if (IsTouchInMenu(_pinch0) || IsTouchInMenu(t.Index))
                return false;
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

    private bool IsTouchInMenu(int touchIndex)
    {
        if (!_touchStartPositions.TryGetValue(touchIndex, out Vector2 startPos))
            return false;   // touch not tracked, allow camera (safety)
        return HUD.Instance != null && HUD.Instance.IsPointInsideAnyMenu(startPos);
    }

    public void PerformAttack()
    {
        // Play swing sound (always, even if none hit)
        if (_attackAudioPlayer != null && _attackSound != null)
        {
            _attackAudioPlayer.Stream = _attackSound;
            _attackAudioPlayer.Play();
        }

        if (PlayerCamera == null) return;

        var spaceState = GetWorld3D().DirectSpaceState;
        Vector3 origin = PlayerCamera.GlobalPosition;
        Vector3 end = origin - PlayerCamera.GlobalTransform.Basis.Z * 100.0f;

        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = 1 << 4; // layer 5 = BodyParts
        query.CollideWithAreas = true;
        query.CollideWithBodies = false;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            Area3D hitArea = result["collider"].As<Area3D>();
            if (hitArea == null) return;

            string limbName = hitArea.Name; // we named the Area3D after the limb in NpcController
            if (string.IsNullOrEmpty(limbName)) return;

            // Find the NPC
            Node current = hitArea.GetParent();   // BoneAttachment3D
            current = current?.GetParent();       // Skeleton3D
            while (current != null && !(current is CharacterBody3D))
                current = current.GetParent();
            var npc = current as CharacterBody3D;
            if (npc == null || npc.GetNodeOrNull<NpcController>(".")?.IsDead == true) return;

            var health = npc.GetNodeOrNull<Health>("Health");
            if (health == null) return;

            // Get the specific limb health
            LimbHealth limbHealth = null;
            foreach (Node child in health.GetChildren())
            {
                if (child is LimbHealth lh && lh.Name == limbName)
                {
                    limbHealth = lh;
                    break;
                }
            }

            float damage = limbHealth != null ? limbHealth.MaxHealth * 0.2f : 20f; // fallback
            health.TakeDamage(damage, limbName);

            float currentHealth = limbHealth?.CurrentHealth ?? 0;
            // Get NPC controller and display name
            var npcController = npc.GetNodeOrNull<NpcController>(".");
            string npcName = npcController != null ? npcController.DisplayName : "Unknown";
            string status = npcController != null && npcController.IsDead ? "DEAD" : "alive";

            GD.Print($"[{npcName}] {status} | Total Health: {health.CurrentHealth:F1}/{health.MaxHealth:F1} | Hit {limbName} for {damage:F1} damage. {limbName} HP: {currentHealth:F1}/{limbHealth?.MaxHealth ?? 0:F1}");
            // Flash the hit limb (or whole body for old NPCs)
            FlashLimb(npc, limbName);
        }
    }

    private async void FlashLimb(CharacterBody3D npc, string limbName)
    {
        var modelRoot = npc.GetNodeOrNull<Node3D>("ModelRoot");
        if (modelRoot == null) return;

        // Collect all meshes under ModelRoot
        var allMeshes = new List<MeshInstance3D>();
        FindAllMeshes(modelRoot, allMeshes);
        if (allMeshes.Count == 0) return;

        MeshInstance3D limbMesh = null;

        // Try to find the exact limb mesh using the corrected dictionary
        if (NpcController.LimbMeshNames.TryGetValue(limbName, out string meshName))
        {
            limbMesh = allMeshes.FirstOrDefault(m =>
                m.Name.ToString().Equals(meshName, StringComparison.OrdinalIgnoreCase));
        }

        // If not found, flash ALL meshes as a fallback (guaranteed visual feedback)
        List<MeshInstance3D> meshesToFlash;
        if (limbMesh != null)
            meshesToFlash = new List<MeshInstance3D> { limbMesh };
        else
            meshesToFlash = allMeshes;   // whole body flash if limb unknown

        // Store original surface materials for each mesh
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

        // Restore original materials
        foreach (var (mesh, mats) in originalMaterials)
        {
            for (int i = 0; i < mats.Length; i++)
                mesh.SetSurfaceOverrideMaterial(i, mats[i]);
        }
    }

    // Helper: finds first MeshInstance3D whose name contains the partial string (case‑insensitive)
    private static MeshInstance3D FindMeshByPartialName(Node start, string partialName)
    {
        if (start is MeshInstance3D mi)
        {
            string nodeName = mi.Name.ToString();   // convert StringName to string
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
    // Helper: recursively gather all MeshInstance3D nodes
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
}