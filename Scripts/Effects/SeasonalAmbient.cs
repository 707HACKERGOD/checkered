using Godot;

public partial class SeasonalAmbient : AudioStreamPlayer
{
    [Export] private AudioStream _springAmbient;
    [Export] private AudioStream _summerAmbient;
    [Export] private AudioStream _autumnAmbient;
    [Export] private AudioStream _winterAmbient;
    [Export] private float _crossfadeDuration = 3.0f;

    private TimeManager _time;
    private Season _currentSeason;
    private Tween _volumeTween;

    public override void _Ready()
    {
        _time = TimeManager.Instance;
        if (_time != null)
        {
            _time.DayChanged += OnDayChanged;
            UpdateSeasonalAmbient(_time.CurrentSeason);
        }
    }

    private void OnDayChanged(int d, int m, int day, int seasonVal)
    {
        UpdateSeasonalAmbient((Season)seasonVal);
    }

    private void UpdateSeasonalAmbient(Season season)
    {
        AudioStream newStream = season switch
        {
            Season.SPRING => _springAmbient,
            Season.SUMMER => _summerAmbient,
            Season.AUTUMN => _autumnAmbient,
            Season.WINTER => _winterAmbient,
            _ => null
        };

        if (newStream == null || newStream == Stream) return;

        // Crossfade: fade out current, then switch and fade in
        if (_volumeTween != null && _volumeTween.IsValid())
            _volumeTween.Kill();

        _volumeTween = CreateTween();
        _volumeTween.TweenProperty(this, "volume_db", -80f, _crossfadeDuration * 0.5f);
        _volumeTween.TweenCallback(Callable.From(() =>
        {
            Stream = newStream;
            Play();
        }));
        _volumeTween.TweenProperty(this, "volume_db", 0f, _crossfadeDuration * 0.5f);
    }
}