using Godot;
using System;

[Tool]
public partial class NpcEyeTracker : Node
{
    [ExportGroup("Core References")]
    [Export] public Node3D Target;
    [Export] public Vector3 TargetOffset = new Vector3(0, 1.6f, 0);
    [Export] public Skeleton3D CharacterSkeleton;
    [Export] public MeshInstance3D FaceMesh;
    [Export] public Shader EyeShader;

    [ExportGroup("Toggles")]
    [Export] public bool EnableHeadTracking = true;
    [Export] public bool EnableEyeTracking = true;
    [Export] public bool EnableBlinking = true;

    [ExportGroup("Head Settings")]
    [Export] public string HeadBoneName = "spine.006";
    [Export] public float HeadTrackingSpeed = 6.0f;
    [Export] public float MaxLookAngle = 80.0f;
    [Export] public Vector3 HeadRotationOffset = new Vector3(0, 0, 0);

    [ExportGroup("Neck Override")]
    [Export] public string NeckBoneName = "spine.005";
    [Export] public float NeckBlendSpeed = 8.0f;

    [ExportGroup("Eye UV Settings")]
    [Export] public int EyeMaterialSurfaceIndex = 0;
    [Export] public float EyeTrackingSpeed = 12.0f;
    [Export] public Vector2 UvSensitivity = new Vector2(0.05f, 0.05f);
    [Export] public Vector2 MaxUvOffset = new Vector2(0.1f, 0.1f);
    [Export] public Vector2 EyeUvBase = Vector2.Zero;
    [Export] public bool SidewaysUvFix = false;
    [Export] public bool InvertUvY = false;

    [ExportGroup("Blink Settings")]
    [Export] public string BlinkShapeName = "Blink";
    [Export] public float MinBlinkInterval = 2.0f;
    [Export] public float MaxBlinkInterval = 6.0f;
    [Export] public float BlinkCloseDuration = 0.05f;
    [Export] public float BlinkOpenDuration = 0.15f;
    [Export] public float MaxBlinkWeight = 1.0f;

    private int _headIdx = -1;
    private int _headParentIdx = -1;
    private int _neckIdx = -1;
    private int _neckParentIdx = -1;
    private float _neckBlendWeight = 0f;
    private Quaternion _animNeckRotation = Quaternion.Identity;
    private Material _eyeMaterial;
    private Vector2 _currentUvOffset = Vector2.Zero;
    private bool _initialized = false;

    private int _blinkShapeIdx = -1;
    private double _blinkTimer = 0;
    private float _currentBlinkWeight = 0f;
    private enum BlinkState { Idle, Closing, Opening }
    private BlinkState _blinkState = BlinkState.Idle;

    public override void _Ready()
    {
        ProcessPriority = 50;
        _initialized = false;
        InitializeTracker();
        ResetBlinkTimer();
    }

    private void InitializeTracker()
    {
        if (_initialized) return;
        
        if (IsInstanceValid(CharacterSkeleton))
        {
            _headIdx = CharacterSkeleton.FindBone(HeadBoneName);
            if (_headIdx != -1) _headParentIdx = CharacterSkeleton.GetBoneParent(_headIdx);

            _neckIdx = CharacterSkeleton.FindBone(NeckBoneName);
            if (_neckIdx != -1) _neckParentIdx = CharacterSkeleton.GetBoneParent(_neckIdx);
        }

        if (IsInstanceValid(FaceMesh))
        {
            if (EyeMaterialSurfaceIndex >= (FaceMesh.Mesh?.GetSurfaceCount() ?? 0))
            {
                GD.PrintErr($"EyeTracker: EyeMaterialSurfaceIndex {EyeMaterialSurfaceIndex} is out of range.");
                return;
            }

            Material activeMat = FaceMesh.GetActiveMaterial(EyeMaterialSurfaceIndex);
            Material original = FaceMesh.Mesh.SurfaceGetMaterial(EyeMaterialSurfaceIndex);
            Material sourceMat = original ?? activeMat;

            if (sourceMat == null)
            {
                GD.PrintErr($"EyeTracker: No material found on surface {EyeMaterialSurfaceIndex}");
                return;
            }
            
            if (sourceMat is ShaderMaterial shdOriginal)
            {
                if (shdOriginal.Shader == null)
                {
                    GD.PrintErr($"EyeTracker: ShaderMaterial has null shader reference on surface {EyeMaterialSurfaceIndex}");
                    return;
                }
                //_eyeMaterial = (ShaderMaterial)shdOriginal.Duplicate();
                _eyeMaterial = shdOriginal;
                if (_eyeMaterial == null)
                {
                    GD.PrintErr("EyeTracker: Failed to duplicate material");
                    return;
                }
                FaceMesh.SetSurfaceOverrideMaterial(EyeMaterialSurfaceIndex, _eyeMaterial);
            }
            else if (sourceMat is StandardMaterial3D stdOriginal)
            {
                _eyeMaterial = (StandardMaterial3D)stdOriginal.Duplicate();
                FaceMesh.SetSurfaceOverrideMaterial(EyeMaterialSurfaceIndex, _eyeMaterial);
                _currentUvOffset = new Vector2(stdOriginal.Uv1Offset.X, stdOriginal.Uv1Offset.Y);
            }
            else
            {
                GD.PrintErr($"EyeTracker: Material type {sourceMat.GetType().Name} not supported.");
                _eyeMaterial = null;
                return;
            }

            ApplyEyeUvOffset(EyeUvBase);
            _currentUvOffset = EyeUvBase;

            if (FaceMesh.Mesh != null)
            {
                _blinkShapeIdx = FaceMesh.FindBlendShapeByName(BlinkShapeName);
            }
        }
        else
        {
            GD.PrintErr("EyeTracker: FaceMesh is not assigned or invalid!");
        }
        
        _initialized = true;
    }

    public override void _Process(double delta)
    {
        // Re-init if bone indices are invalid (skeleton not ready yet)
        if ((_headIdx == -1 && IsInstanceValid(CharacterSkeleton)) ||
            (_blinkShapeIdx == -1 && IsInstanceValid(FaceMesh) && FaceMesh.Mesh != null))
        {
            _initialized = false;
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
                    targetGlobalPos = Target.GlobalPosition + (Target.GlobalTransform.Basis * TargetOffset);
                }

                ProcessNeckTracking(hasTarget, targetGlobalPos, delta);
                ProcessHeadTracking(hasTarget, targetGlobalPos, delta);
                ProcessEyeTracking(hasTarget, targetGlobalPos, delta);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Tracker Error: {e.Message}\n{e.StackTrace}");
        }
    }

    private void ProcessNeckTracking(bool hasTarget, Vector3 targetGlobalPos, double delta)
    {
        if (_neckIdx == -1) return;

        bool shouldTrack = hasTarget && EnableHeadTracking;
        float targetWeight = shouldTrack ? 1.0f : 0.0f;
        _neckBlendWeight = Mathf.Lerp(_neckBlendWeight, targetWeight, (float)delta * NeckBlendSpeed);

        if (_neckBlendWeight < 0.01f)
        {
            _animNeckRotation = CharacterSkeleton.GetBonePoseRotation(_neckIdx);
            return;
        }

        Quaternion targetRotation = Quaternion.Identity;

        if (shouldTrack)
        {
            Vector3 targetInSkeletonSpace = CharacterSkeleton.ToLocal(targetGlobalPos);
            Transform3D neckGlobalPose = CharacterSkeleton.GetBoneGlobalPose(_neckIdx);
            Vector3 neckInSkeletonSpace = neckGlobalPose.Origin;

            Vector3 lookDir = (targetInSkeletonSpace - neckInSkeletonSpace).Normalized();
            Vector3 skeletonForward = Vector3.Back;
            float angleToTarget = Mathf.RadToDeg(skeletonForward.AngleTo(lookDir));

            if (angleToTarget <= MaxLookAngle)
            {
                Vector3 upVector = Mathf.Abs(lookDir.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
                Basis lookAtBasis = Basis.LookingAt(-lookDir, upVector);

                Basis offsetBasis = Basis.FromEuler(new Vector3(
                    Mathf.DegToRad(HeadRotationOffset.X),
                    Mathf.DegToRad(HeadRotationOffset.Y),
                    Mathf.DegToRad(HeadRotationOffset.Z)
                ));
                lookAtBasis = lookAtBasis * offsetBasis;

                if (_neckParentIdx != -1)
                {
                    Transform3D parentGlobalPose = CharacterSkeleton.GetBoneGlobalPose(_neckParentIdx);
                    lookAtBasis = parentGlobalPose.Basis.Inverse() * lookAtBasis;
                }

                targetRotation = lookAtBasis.GetRotationQuaternion();
            }
        }

        Quaternion blendedRotation = _animNeckRotation.Slerp(targetRotation, _neckBlendWeight);
        CharacterSkeleton.SetBonePoseRotation(_neckIdx, blendedRotation);
        _animNeckRotation = CharacterSkeleton.GetBonePoseRotation(_neckIdx);
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

            Vector3 lookDir = (targetInSkeletonSpace - boneInSkeletonSpace).Normalized();
            Vector3 skeletonForward = Vector3.Back;
            float angleToTarget = Mathf.RadToDeg(skeletonForward.AngleTo(lookDir));

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

        if (!shouldTrack)
            return;

        Quaternion currentRot = CharacterSkeleton.GetBonePoseRotation(_headIdx);
        Quaternion newRot = currentRot.Normalized().Slerp(targetRotation.Normalized(), (float)delta * HeadTrackingSpeed);
        CharacterSkeleton.SetBonePoseRotation(_headIdx, newRot);
    }

    private void ProcessEyeTracking(bool hasTarget, Vector3 targetGlobalPos, double delta)
    {
        if (_eyeMaterial == null || _headIdx == -1) return;

        Vector2 targetUvOffset = EyeUvBase;

        if (hasTarget && EnableEyeTracking)
        {
            // Same skeleton-space setup as head tracking
            Vector3 targetInSkeletonSpace = CharacterSkeleton.ToLocal(targetGlobalPos);
            Transform3D headGlobalPose = CharacterSkeleton.GetBoneGlobalPose(_headIdx);
            Vector3 headInSkeletonSpace = headGlobalPose.Origin;

            Vector3 lookDir = (targetInSkeletonSpace - headInSkeletonSpace).Normalized();
            
            // Same angle check as head tracking
            float angleToTarget = Mathf.RadToDeg(Vector3.Back.AngleTo(lookDir));
            
            if (angleToTarget <= MaxLookAngle)
            {
                // Use head's REST pose basis
                //Transform3D headRest = CharacterSkeleton.GetBoneRest(_headIdx);
                //Vector3 localDir = headRest.Basis.Inverse() * lookDir;
                Vector3 localDir = headGlobalPose.Basis.Inverse() * lookDir;
                
                // +Z is forward
                if (localDir.Z > 0.01f)
                {
                    float rawU = localDir.X * UvSensitivity.X;
                    // negative Y fix for up/down inversion
                    float rawV = -localDir.Y * UvSensitivity.Y;

                    Vector2 trackingDelta = new Vector2(rawU, rawV);

                    if (SidewaysUvFix)
                        trackingDelta = new Vector2(trackingDelta.Y, trackingDelta.X);

                    if (InvertUvY)
                        trackingDelta.Y = -trackingDelta.Y;

                    Vector2 totalOffset = EyeUvBase + trackingDelta;
                    totalOffset.X = Mathf.Clamp(totalOffset.X, -MaxUvOffset.X, MaxUvOffset.X);
                    totalOffset.Y = Mathf.Clamp(totalOffset.Y, -MaxUvOffset.Y, MaxUvOffset.Y);

                    targetUvOffset = totalOffset;
                }
            }
        }

        _currentUvOffset = _currentUvOffset.Lerp(targetUvOffset, (float)delta * EyeTrackingSpeed);

        if (!hasTarget && _currentUvOffset.DistanceTo(EyeUvBase) < 0.001f)
        {
            _currentUvOffset = EyeUvBase;
        }

        ApplyEyeUvOffset(_currentUvOffset);
    }

    private void ApplyEyeUvOffset(Vector2 offset)
    {
        if (_eyeMaterial == null) return;

        if (_eyeMaterial is ShaderMaterial shd)
        {
            shd.SetShaderParameter("eye_uv_offset", offset);
        }
        else if (_eyeMaterial is StandardMaterial3D std)
        {
            std.Uv1Offset = new Vector3(offset.X, offset.Y, 0);
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