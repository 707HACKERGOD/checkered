using Godot;

public partial class ChipVoicePlayer : Node
{
    private AudioStreamPlayer _player;
    private VoiceProfile _profile;
    private float _drift;
    private int _blipIndex;

    public float CharsPerSecond => _profile?.Speed ?? 24f;
    public int BlipEveryN => _profile?.BlipEveryN ?? 2;

    public override void _Ready()
    {
        _player = new AudioStreamPlayer { Name = "BlipPlayer" };
        AddChild(_player);
    }

    public void SetProfile(VoiceProfile profile)
    {
        _profile = profile;
        if (profile == null) { _player.Stream = null; return; }
        _player.Stream = ChipVoiceSynth.GetStream(profile);
        _player.VolumeDb = profile.VolumeDb;
        _drift = 0f;
        _blipIndex = 0;
    }

    public void ResetLine() { _drift = 0f; _blipIndex = 0; }   // call on dialogue start
    public void BeginLine()                                     // call per paragraph
    {
        _drift = Mathf.Clamp(_drift + (_profile?.Drift ?? 0f), -10f, 10f);
        _blipIndex = 0;
    }

    public void Blip()                                          // call per revealed character chunk
    {
        if (_profile == null || _player.Stream == null) return;
        if (GD.Randf() < _profile.SkipBlipChance) return;

        float semis = _profile.Pitch + _drift;
        if (GD.Randf() < _profile.JitterChance)
            semis += (float)GD.RandRange(-_profile.Jitter, _profile.Jitter);
        if (_profile.VibratoDepth > 0f)
            semis += Mathf.Sin(_blipIndex * _profile.VibratoSpeed * 0.26f) * _profile.VibratoDepth;
        _blipIndex++;

        _player.PitchScale = Mathf.Clamp(Mathf.Pow(2f, semis / 12f), 0.05f, 20f);
        _player.Play();
    }
}