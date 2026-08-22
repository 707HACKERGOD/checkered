using Godot;

public partial class Door_close_automatic : RigidBody3D
{
    // How hard the door pulls itself shut. 
    // Try 1.5 for a slow close, or 3.0 for a snappy heavy door.
    [Export] public float AutoCloseForce = 2.0f; 

    public override void _PhysicsProcess(double delta)
    {
        // Check the door's current rotation on the Y axis.
        // (If your door swings on a different axis, change .Y to .X or .Z)
        float currentAngle = Rotation.Y;

        // If the door is open more than 0.05 radians (about 3 degrees)...
        if (Mathf.Abs(currentAngle) > 0.05f)
        {
            // Calculate which direction to push to get back to 0
            float direction = -Mathf.Sign(currentAngle);
            
            // Apply a gentle, continuous torque to close it
            ApplyTorque(new Vector3(0, direction * AutoCloseForce, 0));
        }
        else
        {
            // If it's basically closed, stop applying force so it doesn't jitter
            ApplyTorque(new Vector3(0, 0, 0));
        }
    }
}