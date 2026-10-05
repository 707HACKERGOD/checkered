using Godot;

/// <summary>
/// One season's sky identity. Gradient X axis = time of day:
/// 0 = midnight, 0.5 = noon, 1 = midnight. Field names (TopColor /
/// HorizonColor / SunColor) match your WeatherManager's existing API.
/// </summary>
[GlobalClass]
[Tool]
public partial class SeasonPalette : Resource
{
    [Export] public Gradient SunColor;      // sun disc, halo, sunset band, sun light
    [Export] public Gradient TopColor;      // zenith
    [Export] public Gradient HorizonColor;  // horizon band — THE sunset color
    [Export] public Color NightSkyColor = new("040610");
    [Export] public Color CloudTint = Colors.White;
    [Export] public Color MoonColor = new("cfe0ff");
    [Export(PropertyHint.Range, "0,1")] public float CloudCoverage = 0.35f; // clear-sky coverage

    public static SeasonPalette[] CreateDefaults() => new SeasonPalette[]
    {
        new() { // 0 SPRING — greens, rainforest air
            SunColor = Grad(new[]{0f,.24f,.34f,.5f,.78f,.88f,1f},
                "1a2033","aef29a","f2ffe0","ffffff","ffe9b0","6f8455","1a2033"),
            TopColor = Grad(new[]{0f,.3f,.5f,.82f,1f},
                "04060d","14392c","1e6f8c","22304a","04060d"),
            HorizonColor = Grad(new[]{0f,.24f,.42f,.5f,.79f,.9f,1f},
                "05070f","7ed98a","a5e2bd","bfe6cf","c9e6a8","33482c","05070f"),
            NightSkyColor = new("030810"), CloudTint = new("e8f5e0"),
            MoonColor = new("d5f0d8"), CloudCoverage = .38f },

        new() { // 1 SUMMER — 2016 beach / wild-west red sun
            SunColor = Grad(new[]{0f,.23f,.33f,.5f,.78f,.85f,.9f,.95f,1f},
                "0a0d1a","ff8c3a","ffd9a0","fffdf5","ffcf80","ff7030","e63214","5a0a08","0a0d1a"),
            TopColor = Grad(new[]{0f,.28f,.5f,.84f,1f},
                "04071a","1d5aa8","1466d1","3a2f6e","04071a"),
            HorizonColor = Grad(new[]{0f,.23f,.36f,.5f,.78f,.855f,.9f,1f},
                "03050c","ffb26e","8fd2ff","a8e0ff","ff9a70","ff4a68","550811","03050c"),
            NightSkyColor = new("040613"), CloudTint = Colors.White,
            MoonColor = new("cfe0ff"), CloudCoverage = .3f },

        new() { // 2 AUTUMN — grey-purple overcast romance
            SunColor = Grad(new[]{0f,.25f,.4f,.5f,.77f,.87f,1f},
                "14101c","d9a86a","ffe6c4","ffe9c9","f2b98a","a8889e","14101c"),
            TopColor = Grad(new[]{0f,.3f,.5f,.82f,1f},
                "050408","2b2a3a","565870","3a3648","050408"),
            HorizonColor = Grad(new[]{0f,.25f,.42f,.5f,.78f,.88f,1f},
                "060509","8f8399","b0a8ba","aca4b8","95859e","3a3244","060509"),
            NightSkyColor = new("06060c"), CloudTint = new("d9d2e0"),
            MoonColor = new("c9c4dc"), CloudCoverage = .55f },

        new() { // 3 WINTER — pink-purple fairytale
            SunColor = Grad(new[]{0f,.27f,.38f,.5f,.78f,.88f,1f},
                "141021","ffc4d9","fff2f8","fffdff","ffd9e6","9a6fa8","141021"),
            TopColor = Grad(new[]{0f,.3f,.5f,.82f,1f},
                "070512","233158","4a5f96","2c2b58","070512"),
            HorizonColor = Grad(new[]{0f,.27f,.42f,.5f,.8f,.9f,1f},
                "0a0716","f0b8d0","dcc8f0","e3daf2","e0b0d8","463258","0a0716"),
            NightSkyColor = new("05030e"), CloudTint = new("f0e8ff"),
            MoonColor = new("e8d8ff"), CloudCoverage = .45f },
    };

    private static Gradient Grad(float[] offsets, params string[] hex)
    {
        var colors = new Color[hex.Length];
        for (int i = 0; i < hex.Length; i++) colors[i] = new Color(hex[i]);
        return new Gradient { Offsets = offsets, Colors = colors };
    }
}