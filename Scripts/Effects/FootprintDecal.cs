using Godot;

public partial class FootprintDecal : Decal
{
    public void StartFade(float lifetime)
    {
        Tween tween = CreateTween();
        // Animate the modulate alpha – this affects the whole decal, including our shader's alpha.
        tween.TweenProperty(this, "modulate:a", 0.0, lifetime);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}