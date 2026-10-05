using Godot;

namespace Crafting
{
    // Attach to any node (e.g. child of a WorldObject). Positive power heats,
    // negative power cools (fridge). "Stove-ness" is a behavior, not a type.
    public partial class HeatEmitter : Node3D
    {
        [Export] public float Radius = 2.5f;
        [Export] public float Power = 20f; // degC per 0.2s tick at point blank

        public override void _EnterTree() { if (CraftingSim.Instance != null) CraftingSim.Instance.Emitters.Add(this); }
        public override void _ExitTree() { if (CraftingSim.Instance != null) CraftingSim.Instance.Emitters.Remove(this); }
    }
}