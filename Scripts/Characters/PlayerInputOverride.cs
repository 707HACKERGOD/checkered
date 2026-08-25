using Godot;
using System;

/// Single arbiter for "who is driving the Player". Scripted walks (intro),
/// the calendar, and future cutscenes activate this and feed direction+speed.
/// The Player's own code keeps running — gravity, MoveAndSlide, root motion,
/// blend tree, IK, footsteps — all through the normal code paths.
public static class PlayerInputOverride
{
    public static bool Active { get; private set; }

    /// <summary>Normalized world-space direction to walk (or Zero to stand).</summary>
    public static Vector3 WorldDirection { get; private set; }

    /// <summary>Desired speed in m/s.</summary>
    public static float SpeedMps { get; private set; }

    /// <summary>Approximate input-space copy (y = -forward) if HUD/anim code wants it.</summary>
    public static Vector2 MoveInput { get; private set; }

    public static event Action BecameActive;
    public static event Action BecameInactive;

    public static void Begin()
    {
        if (Active) return;
        Active = true;
        BecameActive?.Invoke();
    }

    public static void End()
    {
        if (!Active) return;
        Active = false;
        WorldDirection = Vector3.Zero;
        SpeedMps = 0f;
        MoveInput = Vector2.Zero;
        BecameInactive?.Invoke();
    }

    /// <param name="worldDir">Direction (any length; normalized internally).</param>
    /// <param name="speedMps">Speed in meters/second. 0 = stand still.</param>
    public static void Steer(Vector3 worldDir, float speedMps)
    {
        if (worldDir.LengthSquared() > 1e-6f && speedMps > 0f)
        {
            WorldDirection = worldDir.Normalized();
            SpeedMps = speedMps;
        }
        else
        {
            WorldDirection = Vector3.Zero;
            SpeedMps = 0f;
        }
        MoveInput = new Vector2(0, -Mathf.Clamp(speedMps / 5f, 0f, 1f));
    }
}