using Godot;

public partial class PostProcessController : ColorRect
{
    // FIX: Changed 'float delta' to 'double delta' to match the Godot 4 API.
    public override void _Process(double delta)
    {
        // Get the material and cast it to a ShaderMaterial
        if (Material is ShaderMaterial shaderMaterial)
        {
            // Get the size of the viewport
            Vector2 viewportSize = GetViewportRect().Size;
            
            // Pass the size to the shader's 'viewport_size' uniform
            shaderMaterial.SetShaderParameter("viewport_size", viewportSize);
        }
    }
}
