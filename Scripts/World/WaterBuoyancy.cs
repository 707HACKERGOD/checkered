using Godot;

public partial class WaterBuoyancy : RigidBody3D
{
    [Export] public NodePath WaterNodePath { get; set; }
    [Export] public float BuoyancyForce { get; set; } = 10.0f;
    [Export] public float WaterDrag { get; set; } = 0.5f;
    [Export] public float WaterAngularDrag { get; set; } = 0.5f;
    [Export] public int SamplePoints { get; set; } = 4;

    private Terrain3DWater _water;

    public override void _Ready()
    {
        if (WaterNodePath != null && !WaterNodePath.IsEmpty)
        {
            _water = GetNode<Terrain3DWater>(WaterNodePath);
        }
        else
        {
            var waters = GetTree().GetNodesInGroup("Water");
            if (waters.Count > 0) _water = waters[0] as Terrain3DWater;
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (_water == null) return;

        for (int i = 0; i < SamplePoints; i++)
        {
            Vector3 samplePos = GlobalPosition;
            float angle = (float)i / SamplePoints * Mathf.Tau;
            samplePos.X += Mathf.Cos(angle) * 0.5f;
            samplePos.Z += Mathf.Sin(angle) * 0.5f;

            float waterSurface = _water.GetWaterSurfaceY(new Vector2(samplePos.X, samplePos.Z));
            float depth = waterSurface - samplePos.Y;

            if (depth > 0)
            {
                float force = depth * BuoyancyForce / SamplePoints;
                state.ApplyForce(Vector3.Up * force, samplePos - GlobalPosition);
                state.LinearVelocity *= (1.0f - WaterDrag * (float)state.Step);
                state.AngularVelocity *= (1.0f - WaterAngularDrag * (float)state.Step);
            }
        }
    }
}