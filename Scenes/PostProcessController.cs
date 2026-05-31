using Godot;

public partial class PostProcessController : ColorRect
{
    private SanityCycle _sanityCycle;
    private float _shaderTime = 0.0f;

    public float PixelationStrength { get; set; } = 0.0f;
    public Vector2 PixelationResolution { get; set; } = new Vector2(320, 240);
    public float Quantization { get; set; } = 0.0f;
    public float ScanlineIntensity { get; set; } = 0.0f;   // was 1.0f
    public float VignetteIntensity { get; set; } = 0.5f;

    public override void _Ready()
    {
        _sanityCycle = GetNode<SanityCycle>("/root/SanityCycle");
    }

    public override void _Process(double delta)
    {
        _shaderTime += (float)delta;

        if (Material is ShaderMaterial mat)
        {
            mat.SetShaderParameter("time", _shaderTime);

            float sanity = 1.0f;
            if (_sanityCycle != null)
                sanity = Mathf.Clamp(_sanityCycle.Sanity / 100.0f, 0.0f, 1.0f);

            float distortion = (100.0f - (sanity * 100.0f)) / 500.0f;

            mat.SetShaderParameter("sanity", sanity);
            mat.SetShaderParameter("distortion", distortion);
            mat.SetShaderParameter("pixelation_strength", PixelationStrength);
            mat.SetShaderParameter("pixelation_resolution", PixelationResolution);
            mat.SetShaderParameter("quantization", Quantization);
            mat.SetShaderParameter("scanline_intensity", ScanlineIntensity);
            mat.SetShaderParameter("vignette_intensity", VignetteIntensity);
        }
    }

    public void AdjustPixelationStrength(float delta)
    {
        PixelationStrength = Mathf.Clamp(PixelationStrength + delta, 0.0f, 1.0f);
    }

    public void SetPixelationStrength(float value)
    {
        PixelationStrength = Mathf.Clamp(value, 0.0f, 1.0f);
    }

    public void SetPixelationResolution(Vector2 res)
    {
        PixelationResolution = res;
    }
}