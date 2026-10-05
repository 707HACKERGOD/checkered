using Godot;

/// <summary>
/// A weather state as a set of overrides. Every float defaults to -1f
/// ("unset") and every color defaults to alpha < 0 ("unset"). A preset left
/// untouched therefore does NOTHING — the sky keeps whatever SkyWeaver has
/// from its inspector, seasonal palette, and derivation. Only fields you
/// explicitly set take over.
///
/// This means: editor look == fresh preset look. Tune individual fields only
/// when you want that specific preset to differ from the editor setup.
/// </summary>
[GlobalClass]
[Tool]
public partial class WeatherPreset : Resource
{
    // ------------------------------------------------------------------
    // PRECIPITATION
    // Ground shader globals + particle gates. 0 = none is a sensible
    // default; there's no "editor precipitation" to inherit from.
    // ------------------------------------------------------------------
    [ExportGroup("Precipitation")]
    [Export(PropertyHint.Range, "0,1")] public float RainAmount;
    [Export(PropertyHint.Range, "0,1")] public float SnowAmount;
    [Export(PropertyHint.Range, "0,1")] public float IceAmount;

    [Export] public bool RainParticles   = true;
    [Export] public bool SplashParticles = true;
    [Export] public bool SnowParticles   = true;

    // ------------------------------------------------------------------
    // SKY — cloud color. -1 / alpha<0 = inherit from SkyWeaver.
    // ------------------------------------------------------------------
    [ExportGroup("Sky — Cloud Color")]
    [Export(PropertyHint.Range, "-1,1,0.01")] public float CloudCoverage = -1f;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float StormDarkness = -1f;

    /// Multiplied into the cloud color. Alpha < 0 = inherit seasonal palette tint.
    [Export] public Color CloudTint = new Color(1, 1, 1, -1);

    /// Replace the seasonal sun color. Alpha <= 0 = inherit palette.
    [Export] public Color SunColorOverride = new Color(1, 1, 1, -1);

    /// Replace the seasonal moon color. Alpha <= 0 = inherit palette.
    [Export] public Color MoonColorOverride = new Color(1, 1, 1, -1);

    /// -1 = normal phase, otherwise force 0..1.
    [Export(PropertyHint.Range, "-1,1,0.01")] public float MoonBoost = -1f;

    // ------------------------------------------------------------------
    // SKY — sun / horizon. All -1 = inherit SkyWeaver inspector values.
    // ------------------------------------------------------------------
    [ExportGroup("Sky — Sun")]
    [Export(PropertyHint.Range, "-1,16,0.01")] public float SunEnergy = -1f;
    [Export(PropertyHint.Range, "-1,0.05,0.0001")] public float SunDiscSize = -1f;
    [Export(PropertyHint.Range, "-1,20,0.1")] public float SunDiscIntensity = -1f;
    [Export(PropertyHint.Range, "-1,5,0.01")] public float SunHaloStrength = -1f;

    [ExportGroup("Sky — Horizon")]
    [Export(PropertyHint.Range, "-1,4,0.01")] public float HorizonPower = -1f;
    [Export(PropertyHint.Range, "-1,4,0.01")] public float SunsetBandStrength = -1f;
    [Export] public Color GroundColor = new Color(1, 1, 1, -1);

    // ------------------------------------------------------------------
    // SKY — clouds. -1 / -1 alpha = inherit.
    // ------------------------------------------------------------------
    [ExportGroup("Sky — Clouds (Cirrus)")]
    [Export(PropertyHint.Range, "-1,5,0.01")] public float CirrusIntensity = -1f;
    [Export(PropertyHint.Range, "-1,10,0.01")] public float CirrusThickness = -1f;
    [Export(PropertyHint.Range, "-1,10,0.01")] public float CirrusAbsorption = -1f;
    [Export(PropertyHint.Range, "-1,1.5,0.01")] public float CirrusCoverageScale = -1f;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float CirrusSkyTintFade = -1f;
    [Export] public bool CirrusVisible = true;

    [ExportGroup("Sky — Clouds (Cumulus)")]
    [Export(PropertyHint.Range, "-1,5,0.01")] public float CumulusIntensity = -1f;
    [Export(PropertyHint.Range, "-1,0.2,0.0001")] public float CumulusThickness = -1f;
    [Export(PropertyHint.Range, "-1,10,0.01")] public float CumulusAbsorption = -1f;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float CumulusSize = -1f;
    [Export(PropertyHint.Range, "-1,5,0.01")] public float CumulusMieIntensity = -1f;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float CumulusSkyTintFade = -1f;
    [Export] public bool CumulusVisible = true;

    // ------------------------------------------------------------------
    // SKY — stars / moon.
    // ------------------------------------------------------------------
    [ExportGroup("Sky — Stars")]
    [Export(PropertyHint.Range, "-1,10,0.01")] public float StarBrightness = -1f;
    [Export(PropertyHint.Range, "-1,5,0.01")] public float TwinkleSpeed = -1f;
    [Export] public Color StarmapTint = new Color(1, 1, 1, -1);
    [Export(PropertyHint.Range, "-1,5,0.01")] public float StarmapBrightness = -1f;

    [ExportGroup("Sky — Moon")]
    [Export(PropertyHint.Range, "-1,16,0.01")] public float MoonEnergy = -1f;
    [Export(PropertyHint.Range, "-1,0.3,0.001")] public float MoonSize = -1f;
    [Export(PropertyHint.Range, "-1,10,0.01")] public float MoonBrightness = -1f;

    // ------------------------------------------------------------------
    // FOG. -1 = inherit SkyWeaver's ClearFogDensity and derived falloff.
    // Note: ClearFogDensity / FogFalloff are the "editor defaults" here;
    // changing them here overrides the derived path.
    // ------------------------------------------------------------------
    [ExportGroup("Fog")]
    [Export(PropertyHint.Range, "-1,0.02,0.00001,or_greater")] public float FogDensity = -1f;
    [Export(PropertyHint.Range, "-1,10,0.01")] public float FogFalloff = -1f;
    [Export(PropertyHint.Range, "-1,10000,0.1")] public float FogStart = -1f;
    [Export(PropertyHint.Range, "-1,10000,0.1")] public float FogEnd = -1f;
    [Export(PropertyHint.Range, "-1,5,0.01")] public float FogExposure = -1f;
    [Export(PropertyHint.Range, "-1,1,0.001")] public float FogTonemap = -1f;
    [Export] public Color FogTint = new Color(1, 1, 1, -1);

    // ------------------------------------------------------------------
    // WIND. -1 = inherit. Non-negative = pin.
    // ------------------------------------------------------------------
    [ExportGroup("Wind")]
    [Export(PropertyHint.Range, "-1,120,0.1,suffix:m/s")] public float WindSpeed = -1f;
    [Export(PropertyHint.Range, "-1,180,1,radians_as_degrees")] public float WindDirectionDeg = -1f;
    [Export] public bool AutoWind = true;

    // ------------------------------------------------------------------
    // LIGHTNING
    // ------------------------------------------------------------------
    [ExportGroup("Lightning")]
    [Export(PropertyHint.Range, "0,1")] public float LightningRate;
    [Export] public Color LightningColor = new Color(1, 1, 1, -1);
    [Export] public bool Thunder;
}