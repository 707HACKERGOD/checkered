using Godot;

public partial class PostProcessController : ColorRect
{
    private SanityCycle _sanityCycle;
    private PlayerPossession _possession;

    public override void _Ready()
    {
        _sanityCycle = GetNode<SanityCycle>("/root/SanityCycle");
        var player = GetTree().Root.FindChild("Player", true, false) as Player;
        _possession = player?.GetNodeOrNull<PlayerPossession>("PlayerPossession");
    }
    public override void _Process(double delta)
    {
        if (Material is ShaderMaterial shaderMaterial)
        {
            Vector2 viewportSize = GetViewportRect().Size;
            shaderMaterial.SetShaderParameter("resolution", viewportSize);

            // Update sanity and distortion
            float sanity = 1.0f;
            float distortion = 0.0f;

            if (_sanityCycle != null)
                sanity = _sanityCycle.Sanity / 100.0f;

            // Intensify distortion during possession countdown or possession itself
            if (_possession != null && (_possession.IsCountdownActive || _possession.IsPossessed))
                distortion = 0.6f;

            shaderMaterial.SetShaderParameter("sanity", sanity);
            shaderMaterial.SetShaderParameter("distortion", distortion);
        }
    }
}
