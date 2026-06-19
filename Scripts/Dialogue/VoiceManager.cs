using Godot;

public partial class VoiceManager : Node
{
    public static VoiceManager Instance { get; private set; }

    private AudioStreamPlayer _voicePlayer;
    private AudioStreamPlaybackPolyphonic _playback;
    private long _currentVoiceId = -1;

    public override void _Ready()
    {
        Instance = this;
        
        _voicePlayer = new AudioStreamPlayer();
        _voicePlayer.Bus = "Voice";
        _voicePlayer.MaxPolyphony = 4;
        _voicePlayer.Stream = new AudioStreamPolyphonic();
        AddChild(_voicePlayer);
        
        _voicePlayer.Play();
        _playback = (AudioStreamPlaybackPolyphonic)_voicePlayer.GetStreamPlayback();
    }

    /// <summary>
    /// Plays a voice line from a resolved file path.
    /// Immediately cuts off any previous voice.
    /// </summary>
    public void PlayVoice(string audioPath)
    {
        if (string.IsNullOrEmpty(audioPath)) return;
        
        // IMMEDIATE CUTOFF
        if (_currentVoiceId != -1)
        {
            _playback.StopStream(_currentVoiceId);
            _currentVoiceId = -1;
        }

        var stream = VoiceLibrary.LoadStream(audioPath);
        if (stream == null)
        {
            GD.PushWarning($"Voice file not found: {audioPath}");
            return;
        }

        _currentVoiceId = _playback.PlayStream(stream, 0f, 0f, 1f, 
            playbackType: AudioServer.PlaybackType.Default, bus: "Voice");
    }

    public void StopVoice()
    {
        if (_currentVoiceId != -1)
        {
            _playback.StopStream(_currentVoiceId);
            _currentVoiceId = -1;
        }
    }

    public bool IsPlaying => _currentVoiceId != -1 && _playback.IsStreamPlaying(_currentVoiceId);
}