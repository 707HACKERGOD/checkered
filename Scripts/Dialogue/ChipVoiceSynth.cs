using Godot;
using System.Collections.Generic;

/// <summary>Builds short chiptune blips as AudioStreamWav at runtime. No assets needed.</summary>
public static class ChipVoiceSynth
{
    private const int SampleRate = 22050;
    private static readonly Dictionary<string, AudioStreamWav> _cache = new();

    public static AudioStream GetStream(VoiceProfile p)
    {
        if (!string.IsNullOrEmpty(p.SamplePath))
        {
            if (ResourceLoader.Exists(p.SamplePath))
                return ResourceLoader.Load<AudioStream>(p.SamplePath);
            GD.PushWarning($"ChipVoice: sample '{p.SamplePath}' missing, using synth");
        }

        string key = $"{p.Waveform}|{p.Duty:0.00}|{p.Roughness:0.00}|{p.BlipLength:0.000}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        float len = Mathf.Clamp(p.BlipLength, 0.02f, 0.15f);
        int count = (int)(SampleRate * len);
        var pcm = new byte[count * 2];
        var rng = new System.Random(1337); // fixed seed → consistent noise timbre

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float phase = (t * 261.63f) % 1f; // C4 base — PitchScale shifts it live
            float s = p.Waveform switch
            {
                VoiceWaveform.Sine     => Mathf.Sin(phase * Mathf.Tau),
                VoiceWaveform.Triangle => 4f * Mathf.Abs(phase - 0.5f) - 1f,
                VoiceWaveform.Saw      => 2f * phase - 1f,
                VoiceWaveform.Noise    => (float)(rng.NextDouble() * 2.0 - 1.0),
                _                      => (phase < p.Duty) ? 1f : -1f, // Square
            };
            if (p.Roughness > 0f && p.Waveform != VoiceWaveform.Noise)
                s = Mathf.Lerp(s, (float)(rng.NextDouble() * 2.0 - 1.0), p.Roughness);

            float attack  = Mathf.Min(t / 0.004f, 1f);
            float release = Mathf.Min((len - t) / Mathf.Max(len * 0.35f, 0.006f), 1f);
            short v = (short)(Mathf.Clamp(s * attack * release * 0.8f, -1f, 1f) * 32000f);
            pcm[i * 2]     = (byte)(v & 0xFF);
            pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }

        var wav = new AudioStreamWav
        {
            Data = pcm,
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = SampleRate,
            Stereo = false
        };
        _cache[key] = wav;
        return wav;
    }
}