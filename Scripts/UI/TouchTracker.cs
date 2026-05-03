using Godot;
using System.Collections.Generic;
using System.Linq;

// ---------- Touch Position Tracker ----------
public static class TouchTracker
{
    private static readonly Dictionary<int, Vector2> _positions = new();

    public static void Update(InputEvent e)
    {
        if (e is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
                _positions[touch.Index] = touch.Position;
            else
                _positions.Remove(touch.Index);     // clean up on release
        }
        else if (e is InputEventScreenDrag drag)
        {
            _positions[drag.Index] = drag.Position;
        }
    }

    public static bool TryGet(int index, out Vector2 pos) =>
        _positions.TryGetValue(index, out pos);

    public static int ActiveCount => _positions.Count;

    /// <summary>
    /// Number of touches that are NOT owned by UI (joystick, buttons, etc.)
    /// </summary>
    public static int UnownedCount =>
        _positions.Count(kvp => !TouchOwnership.IsOwned(kvp.Key));
}

// ---------- Touch Ownership (UI claims touches) ----------
public static class TouchOwnership
{
    private static readonly HashSet<int> _owned = new();

    public static void Claim(int index)   => _owned.Add(index);
    public static void Release(int index) => _owned.Remove(index);
    public static bool IsOwned(int index) => _owned.Contains(index);
}