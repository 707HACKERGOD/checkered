using Godot;
using System;
using System.Linq;

public partial class CarController : VehicleBody3D
{
    // --- Inspector-side settings for the realistic ~2000 kg setup ---
    //   Mass = 2000, Center Of Mass = Custom (y ≈ -0.3),
    //   each wheel: Suspension Max Force = 20000, Wheel Radius = 0.37,
    //   everything else on the wheels stays as you copied from the demo.

    [ExportGroup("Driving")]
    [Export] public float MaxEngineForce = 5000.0f;  // halve to ~2500 if you enable traction on all 4 wheels (AWD)
    [Export] public float MaxBrake = 3000.0f;
    [Export] public float MaxSteer = 0.55f;          // radians
    [Export] public float SteerSpeed = 10.0f;
    [Export] public float SteerReductionAtSpeed = 0.6f;
    [Export] public float IdleDragBrake = 100.0f;    // engine braking while coasting
    [Export] public bool ParkingBrakeWhenEmpty = true;

    [ExportGroup("Player Integration")]
    [Export] public Node3D DriverSeat;               // Marker3D where the player sits
    [Export] public Node3D CameraFollowTarget;       // camera follow point while driving

    private float _steerCurrent = 0.0f;
    private Player _driver;

    public bool IsOccupied => _driver != null;

    public override void _Ready()
    {
        var wheels = GetChildren().OfType<VehicleWheel3D>().ToList();
        int driven = wheels.Count(w => w.UseAsTraction);
        int steering = wheels.Count(w => w.UseAsSteering);
        GD.Print($"CarController: {wheels.Count} wheels ({driven} driven, {steering} steering).");
        if (DriverSeat == null)
            GD.PushWarning("CarController: DriverSeat not assigned — player won't be moved into the car.");
    }

    public void Enter(Player player)
    {
        if (_driver != null) return;
        _driver = player;
        Freeze = false;

        if (DriverSeat != null)
        {
            player.Reparent(this);
            player.GlobalPosition = DriverSeat.GlobalPosition;
            player.GlobalRotation = DriverSeat.GlobalRotation;
        }

        player.SetDriving(true, this);
        player.SetCameraTarget(CameraFollowTarget ?? this);
    }

    public void Exit()
    {
        if (_driver == null) return;
        Player player = _driver;
        _driver = null;

        player.Reparent(GetTree().Root);
        // Nudge out beside the car so we don't spawn inside the collider (tweak/remove to taste)
        player.GlobalPosition += GlobalTransform.Basis.X * 1.2f;

        player.SetDriving(false, null);
        player.SetCameraTarget(null);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Freeze) return;
        if (_driver == null)
        {
            if (ParkingBrakeWhenEmpty) { EngineForce = 0.0f; Brake = MaxBrake; }
            return;
        }

        float dt = (float)delta;

        // --- Steering ---
        float steerInput = Input.GetAxis("steer_right", "steer_left");
        float speed = LinearVelocity.Length();
        float speedFrac = Mathf.Clamp(speed / 40.0f, 0.0f, 1.0f);
        float steerTarget = steerInput * MaxSteer * (1.0f - SteerReductionAtSpeed * speedFrac);
        _steerCurrent = Mathf.MoveToward(_steerCurrent, steerTarget, SteerSpeed * dt);
        Steering = _steerCurrent;

        // --- Throttle / Brake / Reverse ---
        float throttle = Input.GetAxis("brake", "accelerate");
        float forwardSpeed = LinearVelocity.Dot(-GlobalTransform.Basis.Z);

        if (throttle > 0.0f)
        {
            EngineForce = throttle * MaxEngineForce;
            Brake = 0.0f;
        }
        else if (throttle < 0.0f)
        {
            if (forwardSpeed > 1.0f)
            {
                EngineForce = 0.0f;
                Brake = -throttle * MaxBrake;   // braking while moving forward
            }
            else
            {
                EngineForce = throttle * MaxEngineForce * 0.5f;  // weaker reverse
                Brake = 0.0f;
            }
        }
        else
        {
            EngineForce = 0.0f;
            Brake = IdleDragBrake;              // gentle coasting drag
        }
    }
}