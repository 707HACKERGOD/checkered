using Godot;
using System.Linq;

/// <summary>
/// Sky renderer + seasonal palette driver + fog quad. [Tool]: runs in editor
/// AND game. In game, WeatherManager drives it via FeedWeather(...) once per
/// frame (plus SetWind / SetPaletteTime / SetLightning). Standalone scenes /
/// editor fall back to the old rain/snow-derived values and its own clock.
/// </summary>
[Tool]
[GlobalClass]
public partial class SkyWeaver : WorldEnvironment
{
    public static SkyWeaver Instance { get; private set; }

    private const string ShaderPath   = "res://Shaders/new_sky.gdshader";
    private const string FogShaderPath = "res://Shaders/AtmFog.gdshader";
    private const string StarMapPath  = "res://Shaders/Milkyway.jpg";
    private const string MoonPath     = "res://Shaders/moon.png";
    private const string CirrusPath   = "res://Shaders/SNoise.tres";
    private const string CumulusPath  = "res://Shaders/noiseClouds.png";

    // ------------------------------------------------------------------
    // WEATHER FEED
    //
    // WeatherManager calls FeedWeather(...) once per frame with the active
    // preset's values. Every override follows the SENTINEL pattern:
    //   * float fields default to -1f  → "inherit, don't write this param"
    //   * Color fields default to alpha < 0 → "inherit"
    //   * bool fields are paired with a *Active flag because booleans have
    //     no spare value to use as a sentinel
    // A fresh preset therefore leaves the SkyWeaver inspector values alone,
    // which is what makes "editor look == preset look" work.
    //
    // When nothing has fed us (standalone scene, editor with no manager),
    // _weatherFed is false and the old rain/snow-derived path runs instead.
    // ------------------------------------------------------------------
    private bool _weatherFed;

    // Sky — cloud color
    private float _coverageOverride = -1f;
    private float _stormDarknessOverride = -1f;
    private Color _cloudTintOverride = new Color(1, 1, 1, -1);
    private Color _sunColorOverride  = new Color(1, 1, 1, -1);
    private Color _moonColorOverride = new Color(1, 1, 1, -1);
    private float _moonBoost;
    private bool  _moonBoostActive;

    // Sky — sun / horizon
    private float _sunEnergyOverride = -1f;
    private float _sunDiscSizeOverride = -1f;
    private float _sunDiscIntensityOverride = -1f;
    private float _sunHaloStrengthOverride = -1f;
    private float _horizonPowerOverride = -1f;
    private float _sunsetBandStrengthOverride = -1f;
    private Color _groundColorOverride = new Color(1, 1, 1, -1);

    // Sky — cirrus
    private float _cirrusIntensityOverride = -1f;
    private float _cirrusThicknessOverride = -1f;
    private float _cirrusAbsorptionOverride = -1f;
    private float _cirrusCoverageScaleOverride = -1f;
    private float _cirrusSkyTintFadeOverride = -1f;
    private bool  _cirrusVisibleOverride;
    private bool  _cirrusVisibilityActive;

    // Sky — cumulus
    private float _cumulusIntensityOverride = -1f;
    private float _cumulusThicknessOverride = -1f;
    private float _cumulusAbsorptionOverride = -1f;
    private float _cumulusSizeOverride = -1f;
    private float _cumulusMieIntensityOverride = -1f;
    private float _cumulusSkyTintFadeOverride = -1f;
    private bool  _cumulusVisibleOverride;
    private bool  _cumulusVisibilityActive;

    // Sky — stars / moon
    private float _starBrightnessOverride = -1f;
    private float _twinkleSpeedOverride = -1f;
    private Color _starmapTintOverride = new Color(1, 1, 1, -1);
    private float _starmapBrightnessOverride = -1f;
    private float _moonEnergyOverride = -1f;
    private float _moonSizeOverride = -1f;
    private float _moonBrightnessOverride = -1f;

    // Fog
    private float _fogDensityOverride = -1f;
    private float _fogFalloffOverride = -1f;
    private float _fogStartOverride = -1f;
    private float _fogEndOverride = -1f;
    private float _fogExposureOverride = -1f;
    private float _fogTonemapOverride = -1f;
    private Color _fogTint = new Color(1, 1, 1, 0);   // alpha = tint strength, 0 = no tint

    // Lightning
    private Color _lightningColorOverride = new Color(1, 1, 1, -1);

    private float _fogScale = 1f;

    /// <summary>
    /// Called every frame by WeatherManager with the active WeatherPreset's
    /// values. Each value is stored as-is; the sentinel check happens at write
    /// time in TickPalette. -1 / alpha<0 means "inherit from the SkyWeaver
    /// inspector, don't touch the shader param".
    /// </summary>
    public void FeedWeather(
        float cloudCoverage, float stormDarkness, Color cloudTint,
        Color sunColorOverride, Color moonColorOverride, float moonBoost,
        float sunEnergy, float sunDiscSize, float sunDiscIntensity, float sunHaloStrength,
        float horizonPower, float sunsetBandStrength, Color groundColor,
        float cirrusIntensity, float cirrusThickness, float cirrusAbsorption,
        float cirrusCoverageScale, float cirrusSkyTintFade, bool cirrusVisible,
        float cumulusIntensity, float cumulusThickness, float cumulusAbsorption,
        float cumulusSize, float cumulusMieIntensity, float cumulusSkyTintFade, bool cumulusVisible,
        float starBrightness, float twinkleSpeed, Color starmapTint, float starmapBrightness,
        float moonEnergy, float moonSize, float moonBrightness,
        float fogDensity, float fogFalloff, float fogStart, float fogEnd,
        float fogExposure, float fogTonemap, Color fogTint,
        Color lightningColor)
    {
        // Sky — cloud color
        _coverageOverride       = cloudCoverage;
        _stormDarknessOverride  = stormDarkness;
        _cloudTintOverride      = cloudTint;
        _sunColorOverride       = sunColorOverride;
        _moonColorOverride      = moonColorOverride;
        _moonBoost              = moonBoost < 0f ? 0f : moonBoost;
        _moonBoostActive        = moonBoost >= 0f;

        // Sky — sun / horizon
        _sunEnergyOverride          = sunEnergy;
        _sunDiscSizeOverride        = sunDiscSize;
        _sunDiscIntensityOverride   = sunDiscIntensity;
        _sunHaloStrengthOverride    = sunHaloStrength;
        _horizonPowerOverride       = horizonPower;
        _sunsetBandStrengthOverride = sunsetBandStrength;
        _groundColorOverride        = groundColor;

        // Sky — clouds
        _cirrusIntensityOverride      = cirrusIntensity;
        _cirrusThicknessOverride      = cirrusThickness;
        _cirrusAbsorptionOverride     = cirrusAbsorption;
        _cirrusCoverageScaleOverride  = cirrusCoverageScale;
        _cirrusSkyTintFadeOverride    = cirrusSkyTintFade;
        _cirrusVisibleOverride        = cirrusVisible;
        _cirrusVisibilityActive       = true;   // bools can't be sentinel — preset always decides
        _cumulusIntensityOverride     = cumulusIntensity;
        _cumulusThicknessOverride     = cumulusThickness;
        _cumulusAbsorptionOverride    = cumulusAbsorption;
        _cumulusSizeOverride          = cumulusSize;
        _cumulusMieIntensityOverride  = cumulusMieIntensity;
        _cumulusSkyTintFadeOverride   = cumulusSkyTintFade;
        _cumulusVisibleOverride       = cumulusVisible;
        _cumulusVisibilityActive      = true;

        // Sky — stars / moon
        _starBrightnessOverride    = starBrightness;
        _twinkleSpeedOverride      = twinkleSpeed;
        _starmapTintOverride       = starmapTint;
        _starmapBrightnessOverride = starmapBrightness;
        _moonEnergyOverride        = moonEnergy;
        _moonSizeOverride          = moonSize;
        _moonBrightnessOverride    = moonBrightness;

        // Fog
        _fogDensityOverride  = fogDensity;
        _fogFalloffOverride  = fogFalloff;
        _fogStartOverride    = fogStart;
        _fogEndOverride      = fogEnd;
        _fogExposureOverride = fogExposure;
        _fogTonemapOverride  = fogTonemap;
        _fogTint             = fogTint;

        // Lightning
        _lightningColorOverride = lightningColor;

        _weatherFed = true;
    }

    /// <summary>
    /// Convenience overload: pulls every value straight from a preset. Used by
    /// the editor PreviewPreset property and callable from anywhere with a
    /// preset reference in hand.
    /// </summary>
    public void FeedWeather(WeatherPreset p)
    {
        if (p == null) { ClearWeatherFeed(); return; }
        FeedWeather(
            p.CloudCoverage, p.StormDarkness, p.CloudTint,
            p.SunColorOverride, p.MoonColorOverride, p.MoonBoost,
            p.SunEnergy, p.SunDiscSize, p.SunDiscIntensity, p.SunHaloStrength,
            p.HorizonPower, p.SunsetBandStrength, p.GroundColor,
            p.CirrusIntensity, p.CirrusThickness, p.CirrusAbsorption,
            p.CirrusCoverageScale, p.CirrusSkyTintFade, p.CirrusVisible,
            p.CumulusIntensity, p.CumulusThickness, p.CumulusAbsorption,
            p.CumulusSize, p.CumulusMieIntensity, p.CumulusSkyTintFade, p.CumulusVisible,
            p.StarBrightness, p.TwinkleSpeed, p.StarmapTint, p.StarmapBrightness,
            p.MoonEnergy, p.MoonSize, p.MoonBrightness,
            p.FogDensity, p.FogFalloff, p.FogStart, p.FogEnd,
            p.FogExposure, p.FogTonemap, p.FogTint,
            p.LightningColor);
    }

    /// <summary>Called by WeatherManager when no preset is active (e.g. before world load).</summary>
    public void ClearWeatherFeed() => _weatherFed = false;

    // ---- Legacy inputs: still used by the derived fallback path ----
    // In game these are fed by WeatherManager for the shader globals only;
    // the sky itself no longer derives anything from them when _weatherFed.
    public float Rain { get => _rain; set { _rain = Mathf.Clamp(value, 0f, 1f); } }
    public float Snow { get => _snow; set { _snow = Mathf.Clamp(value, 0f, 1f); } }
    public float StormGreyTint { get => _stormGrey; set => _stormGrey = Mathf.Clamp(value, 0f, 1f); }

    /// Call every frame with your wind velocity (where wind blows TOWARD, m/s).
    /// While fed, SkyWeaver does NOT write the wind globals — WeatherManager owns them.
    public void SetWind(Vector3 velocity) { _externalWind = velocity; _externalWindActive = true; }

    /// Call every frame with the value your gradients are authored against
    /// (e.g. TimeManager.GetSeasonalSunProgress()). Negative = use TimeOfDay/24.
    public void SetPaletteTime(float normalized) => _paletteTime = normalized;

    // ------------------------------------------------------------------
    // INSPECTOR
    // ------------------------------------------------------------------

    [ExportGroup("Time")]
    [Export] public bool EditorTimeEnabled { get; set; } = true;
    [Export] public float MinutesPerDay { get; set; } = 15f; // used only when nobody feeds TimeOfDay
    [Export(PropertyHint.Range, "0,23.999,0.01")] public float TimeOfDay { get; set; } = 9f;
    [Export] public bool TimePaused { get; set; } // game only; editor uses EditorTimeEnabled

    [ExportGroup("Celestials")]
    /// false = your TimeManager rotates SunLight/MoonLight itself;
    /// SkyWeaver then reads sun/moon direction FROM those lights.
    [Export] public bool OwnSunMoonLights { get; set; } = true;
    [Export(PropertyHint.Range, "-180,180,radians_as_degrees")] public float SunAzimuth { get; set; }
    [Export(PropertyHint.Range, "0,16,0.01")] public float SunEnergy { get; set; } = 1.3f;
    [Export(PropertyHint.Range, "0,16,0.01")] public float MoonEnergy { get; set; } = 0.25f;

    [ExportGroup("Seasons")]
    [Export] public SeasonPalette[] Seasons { get; set; } = new SeasonPalette[0];
    [Export(PropertyHint.Enum, "Spring,Summer,Autumn,Winter")] public int SeasonIndex { get; set; }
    [Export(PropertyHint.Range, "0,600,0.1")] public float SeasonTransitionSeconds { get; set; }
    [Export] public Color StormGreyColor { get; set; } = new Color(0.3f, 0.35f, 0.4f);

    [ExportGroup("Wind")]
    [Export(PropertyHint.Range, "0,120,0.1,suffix:m/s")] public float WindSpeed { get; set; } = 3f; // fallback wind
    [Export(PropertyHint.Range, "-180,180,radians_as_degrees")] public float WindDirection { get; set; }
    [Export(PropertyHint.Range, "-0.02,0.02,0.0002")] public float CloudDriftScale { get; set; } = 0.004f;

    [ExportGroup("Weather (fallback)")]
    // These only matter when nothing calls FeedWeather(...) — e.g. opening a
    // scene in the editor with no WeatherManager. In game, presets override them.
    [Export(PropertyHint.Range, "0,1")] public float OvercastCoverage { get; set; } = 0.92f;

    [ExportGroup("Fog")]
    [Export] public bool UseEngineFog { get; set; }
    [Export] public bool FogVisible { get; set; } = true;
    [Export] public uint FogRenderLayers { get; set; } = 1; // layer 1 = every camera (Sky3D used layer 20)
    // Fallback fog values for the derived path only.
    [Export(PropertyHint.Range, "0,0.01,0.00001,or_greater")] public float ClearFogDensity { get; set; } = 0.0004f;
    [Export(PropertyHint.Range, "0,0.01,0.00001,or_greater")] public float RainFogDensity { get; set; } = 0.0022f;
    [Export(PropertyHint.Range, "0,0.01,0.00001,or_greater")] public float SnowFogDensity { get; set; } = 0.0018f;
    [Export(PropertyHint.Range, "0,10,0.01")] public float FogFalloff { get; set; } = 3f;
    [Export(PropertyHint.Range, "0,10000,0.1")] public float FogStart { get; set; }
    [Export(PropertyHint.Range, "0,10000,0.1")] public float FogEnd { get; set; } = 1000f;
    [Export(PropertyHint.Range, "-2048,2048,0.01")] public float SeaLevel { get; set; }
    [Export] public float FogExposure { get; set; } = 1f;
    [Export(PropertyHint.Range, "0,1,0.001")] public float FogTonemap { get; set; }
    [Export(PropertyHint.Range, "0,1")] public float AerialPerspective { get; set; } = 0.5f; // engine fog only

    [ExportGroup("Moon")]
    [Export(PropertyHint.Range, "7,60,0.01")] public float MoonCycleDays { get; set; } = 29.53f; // 28 = tidy calendar
    [Export(PropertyHint.Range, "0.02,0.5,0.01")] public float MoonFullWindow { get; set; } = 0.1f; // ±1.5 days count as "full"

    [ExportGroup("Weather Presets")]
    [Export] public WeatherPreset[] Presets { get; set; } = new WeatherPreset[0];

    // ------------------------------------------------------------------
    // STATE
    // ------------------------------------------------------------------

    private float _rain, _snow, _windTime, _stormGrey;
    private float _paletteTime = -1f;
    private Vector3 _externalWind = Vector3.Zero;
    private bool _externalWindActive;
    private int _seasonFrom, _seasonTarget;
    private float _seasonBlend01 = 1f;
    private ShaderMaterial _mat, _fogMat;
    private MeshInstance3D _fogMesh;
    private DirectionalLight3D _sun, _moon;
    private Vector2 _cloudDrift;
    private Vector3 _sunDir = Vector3.Up, _moonDir = Vector3.Down;
    private readonly SeasonPalette _fallback = new();
    private float _lunarDay, _lastTimeOfDay, _moonFullness;

    public override void _Ready()
    {
        Instance = this;
        SetProcess(true); // ensure [Tool] processing in the editor

        EnsureGlobal("wind_direction", RenderingServer.GlobalShaderParameterType.Vec3, Vector3.Zero);
        EnsureGlobal("wind_strength", RenderingServer.GlobalShaderParameterType.Float, 0f);
        EnsureGlobal("global_time", RenderingServer.GlobalShaderParameterType.Float, 0f);
        EnsureGlobal("lightning_strength", RenderingServer.GlobalShaderParameterType.Float, 0f);

        if (Seasons == null || Seasons.Length == 0 || Seasons.All(s => s == null))
            Seasons = SeasonPalette.CreateDefaults();

        _seasonTarget = SeasonIndex;
        _seasonFrom = SeasonIndex;
        _lastTimeOfDay = TimeOfDay;

        SetupSky();
        SetupFog();
        SetupLights();
        Rain = _rain;
        Snow = _snow;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void SetLightning(float strength) =>
        RenderingServer.GlobalShaderParameterSet("lightning_strength", Mathf.Clamp(strength, 0f, 1f));

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool inEditor = Engine.IsEditorHint();
        bool advance = inEditor ? EditorTimeEnabled : !TimePaused;
        if (advance && !Mathf.IsZeroApprox(MinutesPerDay))
            TimeOfDay = Mathf.PosMod(TimeOfDay + dt * 24f / (MinutesPerDay * 60f), 24f);
        if (_mat == null) return; // shader path broken — see the error in the Output panel
        float d = TimeOfDay - _lastTimeOfDay;
        if (d > 12f) d -= 24f; else if (d < -12f) d += 24f;   // handle midnight wrap & savegame jumps
        _lunarDay += d / 24f;
        _lastTimeOfDay = TimeOfDay;
        UpdateMoonPhase();
        _mat.SetShaderParameter("moon_fullness", _moonFullness);

        // Only write moon_brightness here if the preset hasn't overridden it.
        // Otherwise the preset's value would be clobbered every frame by the
        // natural phase-derived value.
        if (!(_weatherFed && _moonBrightnessOverride >= 0f))
            _mat.SetShaderParameter("moon_brightness", Mathf.Lerp(0.6f, 1.5f, _moonFullness));

        TickCelestials();
        TickWind(dt);
        TickPalette(dt);
    }

    // ------------------------------------------------------------
    // SETUP
    // ------------------------------------------------------------
    private void SetupSky()
    {
        if (Environment == null)
            Environment = new Godot.Environment();
        var sky = Environment.Sky ?? new Sky();

        var shader = ResourceLoader.Load<Shader>(ShaderPath);
        if (shader == null)
        {
            GD.PushError($"SkyWeaver: sky shader not found at '{ShaderPath}'. " +
                "Fix the ShaderPath constant. Keeping whatever material is already on the Sky.");
            _mat = sky.SkyMaterial as ShaderMaterial;
        }
        else if (sky.SkyMaterial is ShaderMaterial existing && existing.Shader == shader)
        {
            _mat = existing; // adopt: your inspector-assigned material and its tweaks survive
        }
        else
        {
            _mat = new ShaderMaterial { Shader = shader };
            sky.SkyMaterial = _mat;
        }

        Environment.Sky = sky;
        Environment.BackgroundMode = Godot.Environment.BGMode.Sky;
        Environment.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
        Environment.AmbientLightEnergy = 1.0f;
        Environment.TonemapMode = Godot.Environment.ToneMapper.Aces;
        Environment.TonemapWhite = 6.0f;
        Environment.FogEnabled = UseEngineFog;

        if (_mat != null)
        {
            // Only set a texture if the material doesn't already have one. This
            // lets you assign textures directly on the .tres material in the
            // inspector — including a custom moon — and have SetupSky() leave
            // them alone instead of clobbering them every time the scene loads.
            TrySetTextureIfUnset(_mat, "starmap_texture", StarMapPath);
            TrySetTextureIfUnset(_mat, "moon_texture",    MoonPath);
            TrySetTextureIfUnset(_mat, "cirrus_texture",  CirrusPath);
            TrySetTextureIfUnset(_mat, "cumulus_texture", CumulusPath);
        }
    }

    private static void TrySetTextureIfUnset(ShaderMaterial mat, string param, string path)
    {
        // GetShaderParameter returns Nil (Variant.Type.Nil) when the uniform
        // has never been written. If it's already a Texture2D, the material
        // carries an inspector-assigned texture and we must not clobber it.
        Variant current = mat.GetShaderParameter(param);
        if (current.VariantType == Variant.Type.Object && current.AsGodotObject() is Texture2D)
            return;

        var tex = ResourceLoader.Load<Texture2D>(path);
        if (tex != null)
            mat.SetShaderParameter(param, tex);
        else
            GD.PushWarning($"SkyWeaver: could not load '{path}' for shader parameter '{param}'");
    }

    private void SetupFog()
    {
        if (UseEngineFog)
        {
            Environment.FogAerialPerspective = AerialPerspective;
            Environment.FogSkyAffect = 0.1f;
            Environment.FogHeight = 1.0f;
            return;
        }

        var fogShader = ResourceLoader.Load<Shader>(FogShaderPath);
        if (fogShader == null)
        {
            GD.PushError($"SkyWeaver: fog shader not found at '{FogShaderPath}'. Fog disabled.");
            return;
        }

        var quad = new QuadMesh { Size = new Vector2(2f, 2f) };
        _fogMesh = new MeshInstance3D
        {
            Name = "FogMesh",
            Mesh = quad,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(
                new Vector3(-1e30f, -1e30f, -1e30f),
                new Vector3(2e30f, 2e30f, 2e30f)),
            Layers = FogRenderLayers,
            Visible = FogVisible,
        };
        _fogMat = new ShaderMaterial { Shader = fogShader };
        _fogMat.RenderPriority = 100;
        _fogMesh.MaterialOverride = _fogMat;
        AddChild(_fogMesh); // no owner set: not saved into the .tscn, recreated on load

        _fogMat.SetShaderParameter("color_correction", new Vector2(FogTonemap, FogExposure));
        _fogMat.SetShaderParameter("sea_level", SeaLevel);
        _fogMat.SetShaderParameter("fog_start", FogStart);
        _fogMat.SetShaderParameter("fog_end", FogEnd);
        _fogMat.SetShaderParameter("fog_rayleigh_depth", 0.115f);
        _fogMat.SetShaderParameter("fog_mie_depth", 0.0001f);
        _fogMat.SetShaderParameter("atm_darkness", 0.5f);
        _fogMat.SetShaderParameter("atm_sun_intensity", 18f);
        _fogMat.SetShaderParameter("atm_thickness", 0.7f);
        _fogMat.SetShaderParameter("atm_level_params", new Vector3(1f, 0f, 0f));
        _fogMat.SetShaderParameter("atm_beta_ray", new Vector3(5.8e-6f, 1.35e-5f, 3.1e-5f));
        _fogMat.SetShaderParameter("atm_beta_mie", new Vector3(3.038e-8f, 3.038e-8f, 3.038e-8f));
        _fogMat.SetShaderParameter("atm_sun_partial_mie_phase", new Vector3(0.36f, 1.64f, 1.6f));
        _fogMat.SetShaderParameter("atm_moon_partial_mie_phase", new Vector3(0.36f, 1.64f, 1.6f));
        _fogMat.SetShaderParameter("atm_sun_mie_tint", Colors.White);
        _fogMat.SetShaderParameter("atm_sun_mie_intensity", 1f);
        _fogMat.SetShaderParameter("atm_moon_mie_tint", new Color(0.137f, 0.184f, 0.292f));
        _fogMat.SetShaderParameter("atm_moon_mie_intensity", 0.3f);
    }

    private void SetupLights()
    {
        // Auto-created children have no owner: they work in editor & game but
        // aren't saved into the scene. Add your own SunLight/MoonLight children
        // to the SkyWeaver node if you want to tune shadows in the inspector.
        _sun = GetNodeOrNull<DirectionalLight3D>("SunLight") ?? MakeLight("SunLight");
        _moon = GetNodeOrNull<DirectionalLight3D>("MoonLight") ?? MakeLight("MoonLight");
    }

    private DirectionalLight3D MakeLight(string name)
    {
        var l = new DirectionalLight3D { Name = name, ShadowEnabled = true };
        AddChild(l);
        return l;
    }

    // ------------------------------------------------------------
    // PER-FRAME TICKS
    // ------------------------------------------------------------
    private void TickCelestials()
    {
        if (OwnSunMoonLights)
        {
            float ang = (TimeOfDay - 6f) * Mathf.Pi / 12f; // 6h rise, 12h zenith, 18h set
            _sunDir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            if (!Mathf.IsZeroApprox(SunAzimuth)) _sunDir = _sunDir.Rotated(Vector3.Up, SunAzimuth);
            _moonDir = -_sunDir;
            if (_sun != null) _sun.GlobalTransform = LookingFrom(_sunDir);
            if (_moon != null) _moon.GlobalTransform = LookingFrom(_moonDir);
        }
        else
        {
            // TimeManager owns the lights; sun sits along the light's +Z (light shines along -Z).
            if (_sun != null) _sunDir = _sun.GlobalTransform.Basis.Z;
            if (_moon != null) _moonDir = _moon.GlobalTransform.Basis.Z;
        }

        _mat.SetShaderParameter("sun_dir", _sunDir);
        _mat.SetShaderParameter("moon_dir", _moonDir);
        _mat.SetShaderParameter("star_rotation", TimeOfDay / 24f * Mathf.Tau);

        float dayness = Mathf.Clamp(_sunDir.Y * 4f + 0.08f, 0f, 1f);
        if (_sun != null)
        {
            float sunEnergy = (_weatherFed && _sunEnergyOverride >= 0f) ? _sunEnergyOverride : SunEnergy;
            _sun.LightEnergy = sunEnergy * dayness;
            _sun.ShadowEnabled = dayness > 0.01f;
        }
        float moonness = Mathf.Clamp(-_sunDir.Y * 3f, 0f, 1f);
        if (_moon != null)
        {
            float moonEnergy = (_weatherFed && _moonEnergyOverride >= 0f) ? _moonEnergyOverride : MoonEnergy;
            _moon.LightEnergy = moonEnergy * moonness * Mathf.Lerp(0.35f, 1.2f, _moonFullness);
            _moon.ShadowEnabled = moonness > 0.02f;
        }
    }

    private static Transform3D LookingFrom(Vector3 dir)
    {
        Vector3 up = Mathf.Abs(dir.Y) > 0.98f ? Vector3.Forward : Vector3.Up;
        return new Transform3D(Basis.LookingAt(-dir, up), dir * 1000f);
    }

    private void TickWind(float dt)
    {
        _windTime += dt;
        Vector3 wind;
        if (_externalWindActive)
        {
            wind = _externalWind; // WeatherManager owns wind globals in game
        }
        else
        {
            // Fallback (editor / scenes without WeatherManager): we own the globals.
            wind = new Vector3(Mathf.Cos(WindDirection), 0f, Mathf.Sin(WindDirection)) * WindSpeed;
            RenderingServer.GlobalShaderParameterSet("wind_direction", wind.Normalized());
            RenderingServer.GlobalShaderParameterSet("wind_strength", WindSpeed);
            RenderingServer.GlobalShaderParameterSet("global_time", _windTime);
        }
        _cloudDrift += new Vector2(wind.X, wind.Z) * (dt * CloudDriftScale);
        _mat.SetShaderParameter("cloud_offset", _cloudDrift);
    }

    private void TickPalette(float dt)
    {
        // Season crossfade (starts automatically when SeasonIndex changes)
        if (SeasonIndex != _seasonTarget)
        {
            _seasonFrom = _seasonTarget;
            _seasonTarget = SeasonIndex;
            _seasonBlend01 = 0f;
        }
        if (SeasonTransitionSeconds > 0f)
            _seasonBlend01 = Mathf.Min(_seasonBlend01 + dt / SeasonTransitionSeconds, 1f);
        else
            _seasonBlend01 = 1f;
        float k = _seasonBlend01 * _seasonBlend01 * (3f - 2f * _seasonBlend01);

        SeasonPalette a = SeasonAt(_seasonFrom);
        SeasonPalette b = SeasonAt(_seasonTarget);
        float t = _paletteTime >= 0f ? _paletteTime : TimeOfDay / 24f;

        Color sun = BlendGrad(a.SunColor, b.SunColor, t, k, new Color(1f, .92f, .8f));
        Color top = BlendGrad(a.TopColor, b.TopColor, t, k, new Color(.1f, .45f, .85f));
        Color hor = BlendGrad(a.HorizonColor, b.HorizonColor, t, k, new Color(.55f, .7f, .95f));
        Color night = a.NightSkyColor.Lerp(b.NightSkyColor, k);
        Color tint = a.CloudTint.Lerp(b.CloudTint, k);
        Color moonCol = a.MoonColor.Lerp(b.MoonColor, k);

        // Legacy storm-grey (still honoured for standalone scenes / old callers).
        // Does NOT touch cloud_tint or storm_darkness — those are explicit now.
        if (_stormGrey > 0f)
        {
            top = top.Lerp(StormGreyColor, _stormGrey);
            hor = hor.Lerp(StormGreyColor, _stormGrey);
            sun = sun.Lerp(new Color(0.1f, 0.1f, 0.1f), _stormGrey * 0.9f);
        }

        // ---- Sun / moon color override (applies BEFORE the shader write) ----
        if (_weatherFed && _sunColorOverride.A >= 0f) sun = new Color(_sunColorOverride.R, _sunColorOverride.G, _sunColorOverride.B);
        if (_weatherFed && _moonColorOverride.A >= 0f) moonCol = new Color(_moonColorOverride.R, _moonColorOverride.G, _moonColorOverride.B);

        _mat.SetShaderParameter("sun_color", sun);
        _mat.SetShaderParameter("sky_top_color", top);
        _mat.SetShaderParameter("sky_horizon_color", hor);
        _mat.SetShaderParameter("sky_night_color", night);
        _mat.SetShaderParameter("moon_color", moonCol);

        if (_sun != null) _sun.LightColor = sun;
        if (_moon != null) _moon.LightColor = moonCol;

        // ---- cloud coverage / darkness / tint ----
        // Sentinels: preset -1 / alpha<0 = inherit. Falls through to the
        // editor's OvercastCoverage + seasonal palette mix in that case.
        float wet = Mathf.Max(_rain, _snow * 0.9f);

        float coverage = (_weatherFed && _coverageOverride >= 0f)
            ? _coverageOverride
            : Mathf.Lerp(Mathf.Lerp(a.CloudCoverage, b.CloudCoverage, k), OvercastCoverage, wet);

        float darkness = (_weatherFed && _stormDarknessOverride >= 0f)
            ? _stormDarknessOverride
            : wet * (0.6f + 0.4f * _rain);

        Color cloudTintFinal = (_weatherFed && _cloudTintOverride.A >= 0f)
            ? new Color(_cloudTintOverride.R, _cloudTintOverride.G, _cloudTintOverride.B)
            : tint.Lerp(new Color(0.55f, 0.56f, 0.60f), wet * 0.7f);

        _mat.SetShaderParameter("cloud_coverage", coverage);
        _mat.SetShaderParameter("storm_darkness", darkness);
        _mat.SetShaderParameter("cloud_tint", cloudTintFinal);

        // ---- everything else: skip the write entirely when the preset
        //      says "inherit". Sentinel-guarded, no clobbering of inspector.
        if (_weatherFed)
        {
            // Sky — sun / horizon
            if (_sunDiscSizeOverride >= 0f)        _mat.SetShaderParameter("sun_disc_size", _sunDiscSizeOverride);
            if (_sunDiscIntensityOverride >= 0f)   _mat.SetShaderParameter("sun_disc_intensity", _sunDiscIntensityOverride);
            if (_sunHaloStrengthOverride >= 0f)    _mat.SetShaderParameter("sun_halo_strength", _sunHaloStrengthOverride);
            if (_horizonPowerOverride >= 0f)       _mat.SetShaderParameter("horizon_power", _horizonPowerOverride);
            if (_sunsetBandStrengthOverride >= 0f) _mat.SetShaderParameter("sunset_band_strength", _sunsetBandStrengthOverride);
            if (_groundColorOverride.A >= 0f)      _mat.SetShaderParameter("ground_color", new Color(_groundColorOverride.R, _groundColorOverride.G, _groundColorOverride.B));

            // Sky — cirrus clouds
            if (_cirrusVisibilityActive)             _mat.SetShaderParameter("cirrus_visible", _cirrusVisibleOverride);
            if (_cirrusIntensityOverride >= 0f)      _mat.SetShaderParameter("cirrus_intensity", _cirrusIntensityOverride);
            if (_cirrusThicknessOverride >= 0f)      _mat.SetShaderParameter("cirrus_thickness", _cirrusThicknessOverride);
            if (_cirrusAbsorptionOverride >= 0f)     _mat.SetShaderParameter("cirrus_absorption", _cirrusAbsorptionOverride);
            if (_cirrusCoverageScaleOverride >= 0f)  _mat.SetShaderParameter("cirrus_coverage_scale", _cirrusCoverageScaleOverride);
            if (_cirrusSkyTintFadeOverride >= 0f)    _mat.SetShaderParameter("cirrus_sky_tint_fade", _cirrusSkyTintFadeOverride);

            // Sky — cumulus clouds
            if (_cumulusVisibilityActive)            _mat.SetShaderParameter("cumulus_visible", _cumulusVisibleOverride);
            if (_cumulusIntensityOverride >= 0f)     _mat.SetShaderParameter("cumulus_intensity", _cumulusIntensityOverride);
            if (_cumulusThicknessOverride >= 0f)     _mat.SetShaderParameter("cumulus_thickness", _cumulusThicknessOverride);
            if (_cumulusAbsorptionOverride >= 0f)    _mat.SetShaderParameter("cumulus_absorption", _cumulusAbsorptionOverride);
            if (_cumulusSizeOverride >= 0f)          _mat.SetShaderParameter("cumulus_size", _cumulusSizeOverride);
            if (_cumulusMieIntensityOverride >= 0f)  _mat.SetShaderParameter("cumulus_mie_intensity", _cumulusMieIntensityOverride);
            if (_cumulusSkyTintFadeOverride >= 0f)   _mat.SetShaderParameter("cumulus_sky_tint_fade", _cumulusSkyTintFadeOverride);

            // Sky — stars / moon
            if (_starBrightnessOverride >= 0f)    _mat.SetShaderParameter("star_brightness", _starBrightnessOverride);
            if (_twinkleSpeedOverride >= 0f)      _mat.SetShaderParameter("twinkle_speed", _twinkleSpeedOverride);
            if (_starmapTintOverride.A >= 0f)     _mat.SetShaderParameter("starmap_tint", new Color(_starmapTintOverride.R, _starmapTintOverride.G, _starmapTintOverride.B));
            if (_starmapBrightnessOverride >= 0f) _mat.SetShaderParameter("starmap_brightness", _starmapBrightnessOverride);
            if (_moonSizeOverride >= 0f)          _mat.SetShaderParameter("moon_size", _moonSizeOverride);
            // moon_brightness handled in _Process with the same guard

            // Lightning color
            if (_lightningColorOverride.A >= 0f)  _mat.SetShaderParameter("lightning_color", new Color(_lightningColorOverride.R, _lightningColorOverride.G, _lightningColorOverride.B));
        }

        // ---- fog: preset override > derived ----
        // Sentinel: -1 = inherit the derived path (which uses Clear/Rain/Snow
        // FogDensity and the FogFalloff inspector value).
        float density = (_weatherFed && _fogDensityOverride >= 0f)
            ? _fogDensityOverride
            : (ClearFogDensity
                + _rain * Mathf.Max(RainFogDensity - ClearFogDensity, 0f)
                + _snow * Mathf.Max(SnowFogDensity - ClearFogDensity, 0f)) * _fogScale;

        float falloff = (_weatherFed && _fogFalloffOverride > 0f)
            ? _fogFalloffOverride
            : Mathf.Lerp(FogFalloff, FogFalloff * 2.2f, _snow);

        Color fogDay = top.Lerp(hor, 0.5f).Lerp(Colors.White, 0.3f);
        Color fogSun = sun;
        Color fogNight = night * 3f;
        if (_fogTint.A > 0.001f)
        {
            Color fogTintCol = new Color(_fogTint.R, _fogTint.G, _fogTint.B);
            float tintAmount = Mathf.Clamp(_fogTint.A, 0f, 1f);
            fogDay = fogDay.Lerp(fogTintCol, tintAmount);
            fogSun = fogSun.Lerp(fogTintCol, tintAmount);
            fogNight = fogNight.Lerp(fogTintCol, tintAmount);
        }

        if (UseEngineFog)
        {
            Environment.FogDensity = density;
            Environment.FogLightColor = fogSun;
            Environment.FogHeightDensity = 0.12f * wet + 0.06f * _snow;
        }
        else if (_fogMat != null)
        {
            _fogMat.SetShaderParameter("sun_direction", _sunDir);
            _fogMat.SetShaderParameter("moon_direction", _moonDir);
            _fogMat.SetShaderParameter("fog_density", density);
            _fogMat.SetShaderParameter("fog_falloff", falloff);
            _fogMat.SetShaderParameter("atm_day_tint", new Color(fogDay, 1f));
            _fogMat.SetShaderParameter("atm_horizon_light_tint", new Color(fogSun, 1f));
            _fogMat.SetShaderParameter("atm_night_tint", new Color(fogNight, 1f));

            // Sentinel-guarded fog overrides
            if (_weatherFed)
            {
                if (_fogStartOverride >= 0f)    _fogMat.SetShaderParameter("fog_start", _fogStartOverride);
                if (_fogEndOverride >= 0f)      _fogMat.SetShaderParameter("fog_end", _fogEndOverride);
                if (_fogExposureOverride >= 0f || _fogTonemapOverride >= 0f)
                {
                    float e  = _fogExposureOverride >= 0f ? _fogExposureOverride : FogExposure;
                    float tm = _fogTonemapOverride  >= 0f ? _fogTonemapOverride  : FogTonemap;
                    _fogMat.SetShaderParameter("color_correction", new Vector2(tm, e));
                }
            }
        }
    }

    // ------------------------------------------------------------
    // HELPERS
    // ------------------------------------------------------------
    private SeasonPalette SeasonAt(int i)
    {
        if (Seasons == null || Seasons.Length == 0) return _fallback;
        SeasonPalette p = Seasons[((i % Seasons.Length) + Seasons.Length) % Seasons.Length];
        return p ?? _fallback;
    }

    private static Color BlendGrad(Gradient ga, Gradient gb, float t, float blend, Color fallback)
    {
        Color ca = ga != null ? SampleGrad(ga, t) : fallback;
        if (blend <= 0f || gb == null) return ca;
        return ca.Lerp(SampleGrad(gb, t), blend);
    }

    private static Color SampleGrad(Gradient g, float t)
    {
        float[] offs = g.Offsets; Color[] cols = g.Colors;
        if (cols.Length == 0) return Colors.White;
        if (t <= offs[0]) return cols[0];
        for (int i = 1; i < offs.Length; i++)
            if (t <= offs[i])
                return cols[i - 1].Lerp(cols[i], (t - offs[i - 1]) / Mathf.Max(offs[i] - offs[i - 1], 1e-5f));
        return cols[^1];
    }

    private static void EnsureGlobal(string name, RenderingServer.GlobalShaderParameterType type, Variant def)
    {
        if (!ProjectSettings.HasSetting($"shader_globals/{name}"))
            RenderingServer.GlobalShaderParameterAdd(name, type, def);
    }

    /// Optional: feed TimeManager's total day count. If never called, days are
    /// counted automatically from TimeOfDay wraps (works with savegame jumps).
    public void SetLunarDay(float totalDays) => _lunarDay = totalDays;

    private void UpdateMoonPhase()
    {
        float phase = Mathf.PosMod(_lunarDay / Mathf.Max(MoonCycleDays, 1f), 1f); // 0.5 = full
        float dist = Mathf.Abs(phase - 0.5f) * 2f;              // 0 at full, 1 at new
        float w = Mathf.Max(MoonFullWindow, 0.02f);
        _moonFullness = 1f - Mathf.Clamp(dist / w, 0f, 1f);
        _moonFullness = _moonFullness * _moonFullness * (3f - 2f * _moonFullness); // smooth
        if (_moonBoostActive)
            _moonFullness = Mathf.Max(_moonFullness, _moonBoost);   // preset can force it
    }
}