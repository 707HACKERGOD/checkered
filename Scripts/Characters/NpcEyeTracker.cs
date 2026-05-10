using Godot;
using System;

    public partial class NpcEyeTracker : Node
{
    [ExportGroup("Core References")]
    [Export] public Node3D Target;
    [Export] public Vector3 TargetOffset = new Vector3(0, 1.6f, 0); // Height offset to look at face
    [Export] public Skeleton3D CharacterSkeleton;
    [Export] public MeshInstance3D FaceMesh;

    [ExportGroup("Toggles")]
    [Export] public bool EnableHeadTracking = true;
    [Export] public bool EnableEyeTracking = true;
    [Export] public bool EnableBlinking = true;

    [ExportGroup("Head Settings")]
    [Export] public string HeadBoneName = "spine.006";
    [Export] public float HeadTrackingSpeed = 6.0f;
    [Export] public float MaxLookAngle = 80.0f; // Prevents 180-degree Exorcist head spins!
    [Export] public Vector3 HeadRotationOffset = new Vector3(0, 0, 0);

    [ExportGroup("Eye UV Settings")]
    [Export] public int EyeMaterialSurfaceIndex = 0;
    [Export] public float EyeTrackingSpeed = 12.0f;
    [Export] public Vector2 UvSensitivity = new Vector2(0.05f, 0.05f);
    [Export] public Vector2 MaxUvOffset = new Vector2(0.1f, 0.1f);

    // NEW: Base offset that brings the iris to centre before tracking
    [Export] public Vector2 EyeUvBase = Vector2.Zero;
    [Export] public bool SidewaysUvFix = true;

    [ExportGroup("Blink Settings")]
    [Export] public string BlinkShapeName = "Blink"; 
    [Export] public float MinBlinkInterval = 2.0f; 
    [Export] public float MaxBlinkInterval = 6.0f; 
    [Export] public float BlinkCloseDuration = 0.05f; 
    [Export] public float BlinkOpenDuration = 0.15f; 
    [Export] public float MaxBlinkWeight = 1.0f;

    private int _headIdx = -1;
    private int _headParentIdx = -1;
    private Material _eyeMaterial;          // can be either ShaderMaterial or StandardMaterial3D
    private Vector2 _currentUvOffset = Vector2.Zero;
    
    private int _blinkShapeIdx = -1;
    private double _blinkTimer = 0;
    private float _currentBlinkWeight = 0f;
    private enum BlinkState { Idle, Closing, Opening }
    private BlinkState _blinkState = BlinkState.Idle;

    public override void _Ready()
    {
        ProcessPriority = 1;
        InitializeTracker();
        ResetBlinkTimer(); 
    }

    private void InitializeTracker()
    {
        if (IsInstanceValid(CharacterSkeleton))
        {
            _headIdx = CharacterSkeleton.FindBone(HeadBoneName);
            if (_headIdx != -1) _headParentIdx = CharacterSkeleton.GetBoneParent(_headIdx);
        }

        if (IsInstanceValid(FaceMesh))
        {
            Material original = FaceMesh.GetActiveMaterial(EyeMaterialSurfaceIndex);
            if (original is ShaderMaterial shdOriginal)
            {
                _eyeMaterial = (ShaderMaterial)shdOriginal.Duplicate();
                FaceMesh.SetSurfaceOverrideMaterial(EyeMaterialSurfaceIndex, _eyeMaterial);
            }
            else if (original is StandardMaterial3D stdOriginal)
            {
                _eyeMaterial = (StandardMaterial3D)stdOriginal.Duplicate();
                FaceMesh.SetSurfaceOverrideMaterial(EyeMaterialSurfaceIndex, _eyeMaterial);
                // Get initial UV offset if needed (optional)
                _currentUvOffset = new Vector2(((StandardMaterial3D)_eyeMaterial).Uv1Offset.X, ((StandardMaterial3D)_eyeMaterial).Uv1Offset.Y);
            }
            else
            {
                GD.PrintErr($"Eye material at surface {EyeMaterialSurfaceIndex} is not supported.");
                _eyeMaterial = null;
            }

            if (FaceMesh.Mesh != null)
            {
                _blinkShapeIdx = FaceMesh.FindBlendShapeByName(BlinkShapeName);
            }
        }
    }

    public override void _Process(double delta)
    {
        if ((_headIdx == -1 && IsInstanceValid(CharacterSkeleton)) || 
            (_blinkShapeIdx == -1 && IsInstanceValid(FaceMesh) && FaceMesh.Mesh != null)) 
        {
            InitializeTracker();
        }

        try 
        {
            if (EnableBlinking && IsInstanceValid(FaceMesh)) ProcessBlinking(delta);

            if (IsInstanceValid(CharacterSkeleton))
            {
                bool hasTarget = IsInstanceValid(Target);
                Vector3 targetGlobalPos = Vector3.Zero;

                if (hasTarget)
                {
                    // Apply height offset so we look at their face, not their feet
                    targetGlobalPos = Target.GlobalPosition + (Target.GlobalTransform.Basis * TargetOffset);
                }

                // We MUST pass these functions even if hasTarget is false, so they can return to Idle!
                ProcessHeadTracking(hasTarget, targetGlobalPos, delta);
                ProcessEyeTracking(hasTarget, targetGlobalPos, delta);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Tracker Error: {e.Message}");
        }
    }

    private void ProcessHeadTracking(bool hasTarget, Vector3 targetGlobalPos, double delta)
    {
        if (_headIdx == -1) return;

        Quaternion targetRotation = Quaternion.Identity;
        bool shouldTrack = false;

        if (hasTarget && EnableHeadTracking)
        {
            Vector3 targetInSkeletonSpace = CharacterSkeleton.ToLocal(targetGlobalPos);
            Transform3D boneGlobalPose = CharacterSkeleton.GetBoneGlobalPose(_headIdx);
            Vector3 boneInSkeletonSpace = boneGlobalPose.Origin;
            
            // Check angle: Is the player in front of us, or behind us?
            Vector3 lookDir = (targetInSkeletonSpace - boneInSkeletonSpace).Normalized();
            Vector3 skeletonForward = Vector3.Back; // Assuming the skeleton faces -Z locally
            float angleToTarget = Mathf.RadToDeg(skeletonForward.AngleTo(lookDir));

            // Only track if the target is within our human neck limits!
            if (angleToTarget <= MaxLookAngle)
            {
                shouldTrack = true;
                Vector3 upVector = Mathf.Abs(lookDir.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
                Basis lookAtBasis = Basis.LookingAt(-lookDir, upVector); 

                Basis offsetBasis = Basis.FromEuler(new Vector3(
                    Mathf.DegToRad(HeadRotationOffset.X),
                    Mathf.DegToRad(HeadRotationOffset.Y),
                    Mathf.DegToRad(HeadRotationOffset.Z)
                ));
                lookAtBasis = lookAtBasis * offsetBasis;

                if (_headParentIdx != -1)
                {
                    Transform3D parentGlobalPose = CharacterSkeleton.GetBoneGlobalPose(_headParentIdx);
                    lookAtBasis = parentGlobalPose.Basis.Inverse() * lookAtBasis;
                }

                targetRotation = lookAtBasis.GetRotationQuaternion();
            }
        }

        // If target is null, OR if target walked behind our back
        if (!shouldTrack)
            return;   // leave the bone alone; animation takes over

        Quaternion currentRot = CharacterSkeleton.GetBonePoseRotation(_headIdx);
        Quaternion newRot = currentRot.Normalized().Slerp(targetRotation.Normalized(), (float)delta * HeadTrackingSpeed);
        CharacterSkeleton.SetBonePoseRotation(_headIdx, newRot);
    }

    private void ProcessEyeTracking(bool hasTarget, Vector3 targetGlobalPos, double delta)
    {
        if (_eyeMaterial == null || _headIdx == -1) return;

        Vector2 targetUvOffset = EyeUvBase;

        // Only do math if we have a target (otherwise it stays Zero)
        if (hasTarget && EnableEyeTracking)
        {
            Transform3D headGlobalTransform = CharacterSkeleton.GlobalTransform * CharacterSkeleton.GetBoneGlobalPose(_headIdx);
            Vector3 targetLocalToHead = headGlobalTransform.Inverse() * targetGlobalPos;

            // +Z is forward in our AAA World-Aligned rig. 
            // We only shift the eyes if the target is physically in front of the face plane.
            if (targetLocalToHead.Z > 0) 
            {
                float rawU = targetLocalToHead.X * UvSensitivity.X;
                float rawV = targetLocalToHead.Y * UvSensitivity.Y;

                Vector2 trackingDelta;
                if (SidewaysUvFix)
                    trackingDelta = new Vector2(rawV, rawU);
                else
                    trackingDelta = new Vector2(rawU, -rawV);

                // Clamp the tracking delta so the total offset (base + delta) stays within MaxUvOffset
                Vector2 totalOffset = EyeUvBase + trackingDelta;
                totalOffset.X = Mathf.Clamp(totalOffset.X, -MaxUvOffset.X, MaxUvOffset.X);
                totalOffset.Y = Mathf.Clamp(totalOffset.Y, -MaxUvOffset.Y, MaxUvOffset.Y);

                targetUvOffset = totalOffset;
            }
        }

        _currentUvOffset = _currentUvOffset.Lerp(targetUvOffset, (float)delta * EyeTrackingSpeed);
        if (_eyeMaterial is ShaderMaterial shd)
        {
            shd.SetShaderParameter("eye_uv_offset", _currentUvOffset);
        }
        else if (_eyeMaterial is StandardMaterial3D std)
        {
            std.Uv1Offset = new Vector3(_currentUvOffset.X, _currentUvOffset.Y, 0);
        }
    }

    private void ProcessBlinking(double delta)
    {
        if (_blinkShapeIdx == -1) return;

        switch (_blinkState)
        {
            case BlinkState.Idle:
                _blinkTimer -= delta;
                if (_blinkTimer <= 0) _blinkState = BlinkState.Closing;
                break;
            case BlinkState.Closing:
                _currentBlinkWeight += (float)(delta / BlinkCloseDuration) * MaxBlinkWeight;
                if (_currentBlinkWeight >= MaxBlinkWeight)
                {
                    _currentBlinkWeight = MaxBlinkWeight;
                    _blinkState = BlinkState.Opening;
                }
                FaceMesh.SetBlendShapeValue(_blinkShapeIdx, _currentBlinkWeight);
                break;
            case BlinkState.Opening:
                _currentBlinkWeight -= (float)(delta / BlinkOpenDuration) * MaxBlinkWeight;
                if (_currentBlinkWeight <= 0)
                {
                    _currentBlinkWeight = 0;
                    _blinkState = BlinkState.Idle;
                    ResetBlinkTimer(); 
                }
                FaceMesh.SetBlendShapeValue(_blinkShapeIdx, _currentBlinkWeight);
                break;
        }
    }

    private void ResetBlinkTimer()
    {
        _blinkTimer = GD.RandRange(MinBlinkInterval, MaxBlinkInterval);
    }
}