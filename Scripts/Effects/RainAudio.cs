using Godot;

public partial class RainAudio : AudioStreamPlayer
{
    [Export] private float _fadeSpeed = 4.0f; // dB per second

    private WeatherManager _weather;
    private float _targetVolume = -80f;

    public override void _Ready()
    {
        _weather = GetNode<WeatherManager>("/root/WeatherManager");
        VolumeDb = -80f; // start silent
    }

    public override void _Process(double delta)
    {
        if (_weather == null) return;

        // Get current rain amount (0–1)
        float rainAmount = _weather.GetRainAmount(); // you need to add this method

        // Determine target volume
        // Map rainAmount 0→-80dB (silent), 1→0dB (full volume)
        _targetVolume = rainAmount > 0.01f ? Mathf.Lerp(-30f, 0f, rainAmount) : -80f;

        // Smoothly adjust volume
        VolumeDb = Mathf.MoveToward(VolumeDb, _targetVolume, _fadeSpeed * (float)delta);

        // Auto‑start/stop playback
        if (rainAmount > 0.01f && !Playing)
        {
            Play();
        }
        else if (rainAmount <= 0.01f && Playing && VolumeDb <= -60f)
        {
            Stop();
        }
    }
}