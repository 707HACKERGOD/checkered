using Godot;
using System.Collections.Generic;

public static class VoiceStudio
{
    // ------------------------------------------------------------------
    // NAMED NPCs — tune these sliders freely. Fastest way to iterate:
    // change a value, run, talk to the NPC.
    // ------------------------------------------------------------------
    public static readonly Dictionary<string, VoiceProfile> Named = new()
    {
        ["kendall"] = new VoiceProfile
        {
            Id = "kendall",
            Waveform = VoiceWaveform.Square, Duty = 0.35f,
            Pitch = 7f, Jitter = 1.4f, JitterChance = 0.5f, Drift = 0.8f,
            VibratoDepth = 0.25f, VibratoSpeed = 6f,
            Speed = 18f, BlipEveryN = 2, BlipLength = 0.05f,
            Roughness = 0f, SkipBlipChance = 0.05f, VolumeDb = -7f
        },
        ["strong"] = new VoiceProfile
        {
            Id = "strong",
            Waveform = VoiceWaveform.Saw, Duty = 0.6f,
            Pitch = -6f, Jitter = 0.6f, JitterChance = 0.4f, Drift = -0.5f,
            VibratoDepth = 0f,
            Speed = 22f, BlipEveryN = 1, BlipLength = 0.06f,
            Roughness = 0.3f, SkipBlipChance = 0f, VolumeDb = -5f
        },
        ["stranger"] = new VoiceProfile
        {
            Id = "stranger",
            Waveform = VoiceWaveform.Triangle, Duty = 0.5f,
            Pitch = 1f, Jitter = 0.4f, JitterChance = 0.3f, Drift = 0f,
            VibratoDepth = 0.5f, VibratoSpeed = 4f,
            Speed = 13f, BlipEveryN = 2, BlipLength = 0.07f,
            Roughness = 0.1f, SkipBlipChance = 0.15f, VolumeDb = -10f
        },
        ["slow_ghost_girl"] = new VoiceProfile
        {
            Id = "slow_ghost_girl",
            Waveform = VoiceWaveform.Triangle, Duty = 0.5f,
            Pitch = 1f, Jitter = 0.4f, JitterChance = 0.3f, Drift = 0f,
            VibratoDepth = 0.5f, VibratoSpeed = 4f,
            Speed = 13f, BlipEveryN = 2, BlipLength = 0.07f,
            Roughness = 0.1f, SkipBlipChance = 0.15f, VolumeDb = -10f
        },
        ["fart"] = new VoiceProfile
        {
            Id = "fart",
            Waveform = VoiceWaveform.Square, Duty = 0.30612776f, Roughness = 0.3938021f,
            Pitch = -9f, Jitter = 2.025985f, JitterChance = 0.43777275f, Drift = 0f,
            VibratoDepth = 0.2f, VibratoSpeed = 5.551882f,
            Speed = 25f, BlipEveryN = 1, BlipLength = 0.059317533f, SkipBlipChance = 0.14681779f,
            VolumeDb = -7f
        },
        ["mett"] = new VoiceProfile
        {
            Id = "mett",
            Waveform = VoiceWaveform.Saw, Duty = 0.30579644f, Roughness = 0.47405213f,
            Pitch = 2f, Jitter = 3.2488868f, JitterChance = 0.3559982f, Drift = -1f,
            VibratoDepth = 0f, VibratoSpeed = 3.3167424f,
            Speed = 27f, BlipEveryN = 2, BlipLength = 0.08384079f, SkipBlipChance = 0.14216036f,
            VolumeDb = -3f
        },
        ["reed"] = new VoiceProfile
        {
            Id = "reed",
            Waveform = VoiceWaveform.Square, Duty = 0.2721187f, Roughness = 0.1f,
            Pitch = -7f, Jitter = 0.8119271f, JitterChance = 0.5954826f, Drift = 1f,
            VibratoDepth = 0.2f, VibratoSpeed = 5.1175423f,
            Speed = 16f, BlipEveryN = 1, BlipLength = 0.06617202f, SkipBlipChance = 0.08000958f,
            VolumeDb = -10f
        },
        ["loud_investigator"] = new VoiceProfile
        {
            Id = "loud_investigator",
            Waveform = VoiceWaveform.Square, Duty = 0.42379367f, Roughness = 0.3868288f,
            Pitch = -4f, Jitter = 2.9100308f, JitterChance = 0.5463959f, Drift = 0f,
            VibratoDepth = 0f, VibratoSpeed = 4.547557f,
            Speed = 21f, BlipEveryN = 2, BlipLength = 0.048081376f, SkipBlipChance = 0.052241758f,
            VolumeDb = -3f
        },
    };

    private static readonly Dictionary<string, VoiceProfile> _generated = new();

    /// <summary>Named preset wins; otherwise deterministic per npcId so voices never change between runs.</summary>
    public static VoiceProfile Get(string npcId, MbtiType mbti, Predisposition predis, VoiceGender gender)
    {
        if (Named.TryGetValue(npcId, out var named)) return named;
        string key = $"{npcId}|{mbti}|{predis}|{gender}";
        if (_generated.TryGetValue(key, out var cached)) return cached;
        var p = Generate(npcId, mbti, predis, gender);
        _generated[key] = p;
        return p;
    }

    // 16 MBTI x 3 predispositions x 2 genders — every archetype sounds distinct,
    // and each individual npcId gets its own deterministic spin on the archetype.
    private static VoiceProfile Generate(string npcId, MbtiType mbti, Predisposition predis, VoiceGender gender)
    {
        uint seed = Fnv1a(npcId) ^ Fnv1a(mbti.ToString()) ^ Fnv1a(predis.ToString()) ^ ((uint)gender * 7919u);
        var rng = new System.Random((int)seed);
        var p = new VoiceProfile { Id = npcId };

        p.Pitch = gender == VoiceGender.Female ? rng.Next(4, 11) : rng.Next(-7, 2);

        switch (predis)
        {
            case Predisposition.Aggressive:
                p.Speed = rng.Next(20, 30);
                p.Waveform = rng.Next(2) == 0 ? VoiceWaveform.Saw : VoiceWaveform.Square;
                p.Roughness = 0.15f + (float)rng.NextDouble() * 0.35f;
                p.Jitter = 0.8f + (float)rng.NextDouble() * 2.5f;
                p.Pitch -= 2;
                p.VolumeDb = -5f;
                break;
            case Predisposition.Peaceful:
                p.Speed = rng.Next(9, 15);
                p.Waveform = rng.Next(2) == 0 ? VoiceWaveform.Sine : VoiceWaveform.Triangle;
                p.Roughness = 0f;
                p.Jitter = 0.3f + (float)rng.NextDouble() * 0.8f;
                p.VibratoDepth = 0.3f + (float)rng.NextDouble() * 0.8f;
                break;
            default:
                p.Speed = rng.Next(14, 22);
                p.Waveform = rng.Next(2) == 0 ? VoiceWaveform.Square : VoiceWaveform.Triangle;
                p.Jitter = 0.5f + (float)rng.NextDouble() * 1.2f;
                break;
        }

        // MBTI flavor
        string m = mbti.ToString();
        bool extrovert = m.StartsWith('E');
        bool intuitive = m.Contains('N');
        bool thinker   = m.Contains('T');
        bool perceiver = m.EndsWith('P');
        if (extrovert) p.VolumeDb += 2f; else p.VolumeDb -= 2f;
        if (thinker)   p.Roughness = Mathf.Min(0.6f, p.Roughness + 0.1f);
        if (perceiver) p.Jitter += 1.0f; else p.Drift = rng.Next(-1, 2); // J = steady ramp, P = bouncy
        if (intuitive) p.VibratoDepth += 0.2f;

        p.Duty = 0.2f + (float)rng.NextDouble() * 0.6f;
        p.BlipEveryN = rng.Next(1, 3);
        p.BlipLength = 0.035f + (float)rng.NextDouble() * 0.05f;
        p.SkipBlipChance = (float)rng.NextDouble() * 0.15f;
        p.JitterChance = 0.25f + (float)rng.NextDouble() * 0.4f;
        p.VibratoSpeed = 3f + (float)rng.NextDouble() * 7f;
        return p;
    }

    public static uint Fnv1a(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) { h ^= c; h *= 16777619; }
        return h;
    }

        public static readonly Dictionary<string, VoiceProfile> RuntimeOverride = new();

    // in Get(): after the Named check, before the generated-cache check:
    //     if (RuntimeOverride.TryGetValue(npcId, out var ov)) return ov;

    public static VoiceProfile Reroll(string npcId)
    {
        var p = Generate(npcId + "#" + GD.Randi(),
            (MbtiType)GD.RandRange(0, 15), (Predisposition)GD.RandRange(0, 2), (VoiceGender)GD.RandRange(0, 1));
        p.Id = npcId;
        RuntimeOverride[npcId] = p;
        return p;
    }

    public static string Dump(VoiceProfile p)
    {
        if (p == null) return "// no profile";
        return $"[\"{p.Id}\"] = new VoiceProfile\n{{\n" +
            $"    Id = \"{p.Id}\",\n" +
            $"    Waveform = VoiceWaveform.{p.Waveform}, Duty = {p.Duty}f, Roughness = {p.Roughness}f,\n" +
            $"    Pitch = {p.Pitch}f, Jitter = {p.Jitter}f, JitterChance = {p.JitterChance}f, Drift = {p.Drift}f,\n" +
            $"    VibratoDepth = {p.VibratoDepth}f, VibratoSpeed = {p.VibratoSpeed}f,\n" +
            $"    Speed = {p.Speed}f, BlipEveryN = {p.BlipEveryN}, BlipLength = {p.BlipLength}f, SkipBlipChance = {p.SkipBlipChance}f,\n" +
            $"    VolumeDb = {p.VolumeDb}f\n}},";
    }
}