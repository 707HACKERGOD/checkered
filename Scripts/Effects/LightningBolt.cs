using Godot;
using System;

public partial class LightningBolt : Node3D
{
    private MeshInstance3D _mesh;
    private ShaderMaterial _mat;
    private float _lifeTime = 0.0f;
    private float _duration = 0.4f; // Short flash

    public override void _Ready()
    {
        _mesh = GetNode<MeshInstance3D>("MeshInstance3D");
        _mat = _mesh.Mesh.SurfaceGetMaterial(0) as ShaderMaterial;
        
        // Ensure we have a unique material so we don't flash all bolts at once
        if (_mat != null)
        {
            _mat = (ShaderMaterial)_mat.Duplicate();
            _mesh.MaterialOverride = _mat;
            
            // Randomize shape
            _mat.SetShaderParameter("seed", GD.Randf() * 100.0f);
        }

        // BILLBOARD FIX: Always face the active camera
        Camera3D cam = GetViewport().GetCamera3D();
        if (cam != null)
        {
            // LookAt makes -Z point to target. 
            // We want the quad (facing Z) to look at camera.
            // Actually, for a vertical bolt, we only want to rotate on Y axis.
            Vector3 target = new Vector3(cam.GlobalPosition.X, GlobalPosition.Y, cam.GlobalPosition.Z);
            LookAt(target, Vector3.Up);
        }
    }

    public override void _Process(double delta)
    {
        _lifeTime += (float)delta;
        
        if (_lifeTime > _duration)
        {
            QueueFree();
            return;
        }

        // Animation Curve:
        // 0.0 - 0.05: Full Brightness (Hold)
        // 0.05 - 0.4: Linear Fade Out
        float vanish = 0.0f;
        if (_lifeTime > 0.05f)
        {
            vanish = (_lifeTime - 0.05f) / (_duration - 0.05f);
        }
        
        if (_mat != null)
        {
            _mat.SetShaderParameter("vanish_value", vanish);
        }
    }
}