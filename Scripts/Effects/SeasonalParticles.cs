using Godot;
using System;

public partial class SeasonalParticles : GpuParticles3D
{
    [ExportCategory("Seasonal Textures")]
    [Export] private Texture2D _springTex;
    [Export] private Color _springColor = new Color(1.0f, 0.8f, 0.9f);
    
    [Export] private Texture2D _summerTex;
    [Export] private Color _summerColor = new Color(0.4f, 0.8f, 0.2f);
    
    [Export] private Texture2D _autumnTex;
    [Export] private Color _autumnColor = new Color(0.8f, 0.4f, 0.1f);

    [Export] private Texture2D _winterTex;
    
    private WeatherManager _weather;

    public override void _Ready()
    {
        _weather = GetNodeOrNull<WeatherManager>("/root/WeatherManager");
        if (_weather != null && TimeManager.Instance != null)
        {
            TimeManager.Instance.DayChanged += OnDayChanged;
            UpdateSeason(TimeManager.Instance.CurrentSeason);
        }
    }

    private void OnDayChanged(int d, int m, int day, int seasonVal)
    {
        UpdateSeason((Season)seasonVal);
    }

    private void UpdateSeason(Season season)
    {
        // Handle BOTH material types
        Material rawMat = DrawPass1?.SurfaceGetMaterial(0);
        if (rawMat == null) return;

        Texture2D tex = null;
        Color col = Colors.White;
        int amount = 50;

        switch (season)
        {
            case Season.SPRING: tex = _springTex; col = _springColor; amount = 200; break;
            case Season.SUMMER: tex = _summerTex; col = _summerColor; amount = 50; break;
            case Season.AUTUMN: tex = _autumnTex; col = _autumnColor; amount = 500; break;
            case Season.WINTER: Emitting = false; return;
        }

        // Apply settings
        Amount = amount;
        
        if (rawMat is StandardMaterial3D stdMat)
        {
            stdMat.AlbedoTexture = tex;
            stdMat.AlbedoColor = col;
        }
        else if (rawMat is ShaderMaterial shdMat)
        {
            // Assuming your shader uses 'leaf_texture' and 'leaf_color'
            shdMat.SetShaderParameter("leaf_texture", tex);
            shdMat.SetShaderParameter("leaf_color", col);
        }
    }
}