public enum VoiceWaveform { Square, Triangle, Saw, Sine, Noise }
public enum VoiceGender { Male, Female }
public enum Predisposition { Aggressive, Neutral, Peaceful }
public enum MbtiType { INTJ, INTP, ENTJ, ENTP, INFJ, INFP, ENFJ, ENFP,
                       ISTJ, ISFJ, ESTJ, ESFJ, ISTP, ISFP, ESTP, ESFP }

/// <summary>All the sliders. This is the whole timbre of a character.</summary>
public class VoiceProfile
{
    public string Id = "generic";

    // --- waveform ---
    public VoiceWaveform Waveform = VoiceWaveform.Square;
    public float Duty = 0.5f;          // 0.05..0.95 — pulse width (Square only). Thin = tinny, fat = round
    public float Roughness = 0f;       // 0..1 — noise mixed into the tone (gravelly/whispery)

    // --- pitch ---
    public float Pitch = 0f;           // -24..24 semitones
    public float Jitter = 0f;          // 0..6 — random detune per blip (nervous/unstable)
    public float JitterChance = 0.3f;  // 0..1 — how often jitter fires
    public float Drift = 0f;           // -6..6 — voice rises/falls a bit with every line (sing-song, doom)
    public float VibratoDepth = 0f;    // 0..3 — wobble across consecutive blips
    public float VibratoSpeed = 5f;    // 1..12

    // --- timing ---
    public float Speed = 16f;          // 6..40 — chars/sec, drives the typewriter AND the blip rate
    public int BlipEveryN = 2;         // blip every N characters (1 = very chattery)
    public float BlipLength = 0.055f;  // 0.02..0.15 s
    public float SkipBlipChance = 0f;  // 0..0.5 — randomly skipped blips = breathy, shy, tired

    // --- output ---
    public float VolumeDb = -8f;
    public string SamplePath = null;   // optional: imported blip sample replaces the synth blip entirely;
                                       // pitch/jitter/vibrato still apply on top of it (Tomodachi style)
}