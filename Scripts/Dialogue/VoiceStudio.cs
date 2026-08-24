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
}