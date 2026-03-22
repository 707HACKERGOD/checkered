using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class FoliageGenerator : Node3D
{
    [ExportCategory("Tree Configuration")]
    [Export] private MultiMeshInstance3D _treeSource;
    [Export] private Shape3D _collisionShape; 

    [ExportCategory("Ground Piles (Wide)")]
    [Export] private MultiMeshInstance3D _groundLeafTarget;
    [Export] private Mesh _groundLeafMesh;
    [Export] private int _groundCountPerTree = 25;
    [Export] private float _groundRadiusMin = 0.5f;
    [Export] private float _groundRadiusMax = 4.0f;
    [Export] private float _groundScale = 0.1f;

    [ExportCategory("Flying Gust Leaves (Sparse)")]
    [Export] private MultiMeshInstance3D _gustLeafTarget;
    [Export] private Mesh _gustLeafMesh;
    [Export] private int _gustCountPerTree = 8;
    [Export] private float _gustRadiusMin = 1.0f;
    [Export] private float _gustRadiusMax = 6.0f;
    [Export] private float _gustScale = 0.15f;

    [ExportCategory("Falling Canopy (Tight)")]
    [Export] private MultiMeshInstance3D _canopyLeafTarget;
    [Export] private Mesh _singleLeafMesh;
    [Export] private int _canopyCountPerTree = 10;
    [Export] private float _canopyRadiusMin = 0.5f;
    [Export] private float _canopyRadiusMax = 2.0f;
    [Export] private float _canopyScale = 0.15f;
    
    [ExportGroup("Canopy Settings")]
    [Export] private bool _useManualHeight = false; 
    [Export] private float _manualCanopyHeight = 6.0f; 
    [Export] private Vector3 _canopyOffsetVector = Vector3.Zero; 
    
    [Export] private float _maxFallHeight = 50.0f;

    [ExportCategory("Editor Actions")]
    [Export] private bool _generateVisuals = false;
    [Export] private bool _clearVisuals = false;

    private Rid _staticBodyRid;
    private List<float> _successfulGroundHeights = new List<float>();

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        GenerateColliders();
    }

    private void GenerateColliders()
    {
        if (_treeSource == null || _treeSource.Multimesh == null || _collisionShape == null) return;
        MultiMesh mm = _treeSource.Multimesh;
        int count = mm.InstanceCount;
        
        _staticBodyRid = PhysicsServer3D.BodyCreate();
        PhysicsServer3D.BodySetMode(_staticBodyRid, PhysicsServer3D.BodyMode.Static);
        PhysicsServer3D.BodySetSpace(_staticBodyRid, GetWorld3D().Space);
        
        PhysicsServer3D.BodySetCollisionLayer(_staticBodyRid, 2); 
        PhysicsServer3D.BodySetCollisionMask(_staticBodyRid, 0);
        PhysicsServer3D.BodySetState(_staticBodyRid, PhysicsServer3D.BodyState.Transform, GlobalTransform);

        Rid shapeRid = _collisionShape.GetRid();
        float heightOffset = GetShapeHeight(_collisionShape) * 0.5f; 

        for (int i = 0; i < count; i++)
        {
            Transform3D t = mm.GetInstanceTransform(i);
            t.Basis = t.Basis.Orthonormalized(); 
            t.Origin += t.Basis.Y * heightOffset; 
            PhysicsServer3D.BodyAddShape(_staticBodyRid, shapeRid, t);
        }
    }

    public override void _ExitTree()
    {
        if (_staticBodyRid.IsValid) PhysicsServer3D.FreeRid(_staticBodyRid);
    }

    public override void _Process(double delta)
    {
        if (_generateVisuals)
        {
            _generateVisuals = false;
            GenerateFoliage();
        }
        if (_clearVisuals)
        {
            _clearVisuals = false;
            ClearFoliage();
        }
    }

    private void ClearFoliage()
    {
        GD.Print("--- FoliageGenerator: ClearFoliage() called ---");
        if (_groundLeafTarget != null)
        {
            _groundLeafTarget.Multimesh = null;
            GD.Print("  Cleared Ground layer");
        }
        if (_gustLeafTarget != null)
        {
            _gustLeafTarget.Multimesh = null;
            GD.Print("  Cleared Gust layer");
        }
        if (_canopyLeafTarget != null)
        {
            _canopyLeafTarget.Multimesh = null;
            GD.Print("  Cleared Canopy layer");
        }
        GD.Print("FoliageGenerator: Foliage Cleared.");
    }

    private void GenerateFoliage()
    {
        GD.Print("--- FoliageGenerator: GenerateFoliage() called ---");
        float baseHeight = _useManualHeight ? _manualCanopyHeight : GetShapeHeight(_collisionShape);
        GD.Print($"  BaseHeight for canopy: {baseHeight} (useManual={_useManualHeight})");

        GD.Print("Spawning Ground Layer...");
        SpawnLayer(_groundLeafTarget, _groundLeafMesh, _groundCountPerTree, 
                   _groundRadiusMin, _groundRadiusMax, 0.0f, _groundScale, false, Vector3.Zero, true);
        
        GD.Print("Spawning Gust Layer...");
        SpawnLayer(_gustLeafTarget, _gustLeafMesh, _gustCountPerTree, 
                   _gustRadiusMin, _gustRadiusMax, 0.0f, _gustScale, true, Vector3.Zero, true);

        GD.Print("Spawning Canopy Layer...");
        SpawnLayer(_canopyLeafTarget, _singleLeafMesh, _canopyCountPerTree, 
                   _canopyRadiusMin, _canopyRadiusMax, baseHeight, _canopyScale, true, _canopyOffsetVector, false);
                   
        GD.Print("FoliageGenerator: Foliage Generated.");
    }

    private float GetShapeHeight(Shape3D shape)
    {
        if (shape == null) return 5.0f;
        if (shape is CylinderShape3D cyl) return cyl.Height;
        if (shape is CapsuleShape3D cap) return cap.Height;
        if (shape is BoxShape3D box) return box.Size.Y;
        return 5.0f;
    }

    private void SpawnLayer(MultiMeshInstance3D target, Mesh mesh, int countPerTree, float rMin, float rMax, float spawnHeight, float scaleBase, bool fullRotation, Vector3 manualOffset, bool snapToGround)
    {
        GD.Print($"  >> SpawnLayer: target={target?.Name}, mesh={mesh?.ResourceName}, countPerTree={countPerTree}, snapToGround={snapToGround}");

        if (_treeSource == null) { GD.PrintErr("  ERROR: _treeSource is null"); return; }
        if (target == null) { GD.PrintErr("  ERROR: target is null"); return; }
        if (mesh == null) { GD.PrintErr("  ERROR: mesh is null"); return; }
        
        MultiMesh treeMM = _treeSource.Multimesh;
        if (treeMM == null || treeMM.InstanceCount == 0)
        {
            GD.PrintErr("  ERROR: treeMM is null or has no instances");
            return;
        }

        // --- SAFE MATERIAL HANDLING: duplicate and set as override ---
        ShaderMaterial materialToUse = null;
        Material meshMaterial = mesh.SurfaceGetMaterial(0);
        if (meshMaterial is ShaderMaterial sm)
        {
            materialToUse = (ShaderMaterial)sm.Duplicate();
            GD.Print("    Created copy of mesh material");
        }
        else
        {
            GD.PrintErr("    ERROR: Mesh surface material is not a ShaderMaterial");
            return;
        }

        target.MaterialOverride = materialToUse;

        // --- Build the MultiMesh ---
        MultiMesh newMM = new MultiMesh();
        newMM.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        newMM.UseColors = true; 
        newMM.Mesh = mesh;
        newMM.InstanceCount = treeMM.InstanceCount * countPerTree;
        newMM.VisibleInstanceCount = -1;

        int idx = 0;
        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Randomize();
        var spaceState = GetWorld3D().DirectSpaceState;

        int hitCount = 0, missCount = 0, upHitCount = 0;
        _successfulGroundHeights.Clear();

        // First pass: collect all successful ground heights to compute average
        for (int i = 0; i < treeMM.InstanceCount; i++)
        {
            Transform3D treeTrans = treeMM.GetInstanceTransform(i);
            Vector3 treeWorldPos = _treeSource.GlobalTransform * treeTrans.Origin;
            float treeScaleY = treeTrans.Basis.Scale.Y;

            for (int j = 0; j < countPerTree; j++)
            {
                float angle = rng.RandfRange(0, Mathf.Tau);
                float dist = rng.RandfRange(rMin, rMax);
                
                float finalH = spawnHeight * treeScaleY;
                if (spawnHeight > 1.0f) finalH += rng.RandfRange(-0.5f, 0.5f);
                Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, finalH, Mathf.Sin(angle) * dist);
                offset += manualOffset;
                Vector3 worldPos = treeWorldPos + offset; 

                // Raycast
                Vector3 rayFrom = new Vector3(worldPos.X, worldPos.Y + 50.0f, worldPos.Z); 
                Vector3 rayTo = new Vector3(worldPos.X, worldPos.Y - 100.0f, worldPos.Z);
                var query = PhysicsRayQueryParameters3D.Create(rayFrom, rayTo);
                query.CollisionMask = 2; // ground layer

                float groundY;
                var result = spaceState.IntersectRay(query);

                if (result.Count > 0)
                {
                    groundY = result["position"].AsVector3().Y;
                    hitCount++;
                    _successfulGroundHeights.Add(groundY);
                }
                else
                {
                    var queryUp = PhysicsRayQueryParameters3D.Create(worldPos, worldPos + Vector3.Up * 100.0f);
                    queryUp.CollisionMask = 2;
                    var resultUp = spaceState.IntersectRay(queryUp);
                    if (resultUp.Count > 0)
                    {
                        groundY = resultUp["position"].AsVector3().Y;
                        upHitCount++;
                        _successfulGroundHeights.Add(groundY);
                    }
                    else
                    {
                        missCount++;
                        // Do not add to successful list; will handle later
                    }
                }
            }
        }

        // Compute average ground height from successful hits
        float avgGroundY = 0f;
        if (_successfulGroundHeights.Count > 0)
        {
            float sum = 0f;
            foreach (float h in _successfulGroundHeights) sum += h;
            avgGroundY = sum / _successfulGroundHeights.Count;
            GD.Print($"  Average ground height from {_successfulGroundHeights.Count} hits: {avgGroundY:F2}");
        }
        else
        {
            avgGroundY = 0f; // fallback
            GD.Print("  No successful raycasts – using fallback height 0");
        }

        // Second pass: actually place instances, using average for misses
        hitCount = 0; missCount = 0; upHitCount = 0;
        int sampleIdx = 0;
        for (int i = 0; i < treeMM.InstanceCount; i++)
        {
            Transform3D treeTrans = treeMM.GetInstanceTransform(i);
            Vector3 treeWorldPos = _treeSource.GlobalTransform * treeTrans.Origin;
            float treeScaleY = treeTrans.Basis.Scale.Y;

            for (int j = 0; j < countPerTree; j++)
            {
                float angle = rng.RandfRange(0, Mathf.Tau);
                float dist = rng.RandfRange(rMin, rMax);
                
                float finalH = spawnHeight * treeScaleY;
                if (spawnHeight > 1.0f) finalH += rng.RandfRange(-0.5f, 0.5f);
                Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, finalH, Mathf.Sin(angle) * dist);
                offset += manualOffset;
                Vector3 worldPos = treeWorldPos + offset; 

                // Raycast
                Vector3 rayFrom = new Vector3(worldPos.X, worldPos.Y + 50.0f, worldPos.Z); 
                Vector3 rayTo = new Vector3(worldPos.X, worldPos.Y - 100.0f, worldPos.Z);
                var query = PhysicsRayQueryParameters3D.Create(rayFrom, rayTo);
                query.CollisionMask = 2; // ground layer

                float groundY;
                var result = spaceState.IntersectRay(query);

                if (result.Count > 0)
                {
                    groundY = result["position"].AsVector3().Y;
                    hitCount++;
                }
                else
                {
                    var queryUp = PhysicsRayQueryParameters3D.Create(worldPos, worldPos + Vector3.Up * 100.0f);
                    queryUp.CollisionMask = 2;
                    var resultUp = spaceState.IntersectRay(queryUp);
                    if (resultUp.Count > 0)
                    {
                        groundY = resultUp["position"].AsVector3().Y;
                        upHitCount++;
                    }
                    else
                    {
                        missCount++;
                        // Use average ground height as fallback
                        groundY = avgGroundY;
                    }
                }

                float fallDistance = worldPos.Y - groundY;
                if (snapToGround) worldPos.Y = groundY;
                if (fallDistance < 0) fallDistance = 0;

                // Pack fall distance into color
                float normalizedHeight = Mathf.Clamp(fallDistance / _maxFallHeight, 0.0f, 1.0f);
                float r = Mathf.Floor(normalizedHeight * 255.0f) / 255.0f;
                float g = (normalizedHeight - r) * 255.0f;
                newMM.SetInstanceColor(idx, new Color(r, g, 0, 1));

                Transform3D t = new Transform3D(Basis.Identity, Vector3.Zero);
                t = t.Rotated(Vector3.Up, rng.RandfRange(0, Mathf.Tau));
                if (!fullRotation) t = t.Rotated(Vector3.Right, rng.RandfRange(-0.1f, 0.1f));
                
                float s = rng.RandfRange(scaleBase * 0.8f, scaleBase * 1.2f);
                t = t.Scaled(Vector3.One * s);
                Vector3 localPos = target.ToLocal(worldPos);
                t.Origin = localPos;
                
                newMM.SetInstanceTransform(idx, t);

                if (sampleIdx < 3 && i < 2 && j < 2)
                {
                    GD.Print($"    Sample leaf {idx}: worldPos={worldPos:F2}, groundY={groundY:F2}, snap={snapToGround}, localPos={localPos:F2}, scale={s:F2}");
                    sampleIdx++;
                }

                idx++;
            }
        }

        target.Multimesh = newMM;
        
        GD.Print($"  << SpawnLayer finished: total={idx}, hits={hitCount}, upHits={upHitCount}, misses={missCount}");
    }
}