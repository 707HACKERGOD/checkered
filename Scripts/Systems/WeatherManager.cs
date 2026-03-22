using Godot;
using System;

public partial class WeatherManager : Node
{
    public enum WeatherState { Clear, Rain, SummerRain, Snow, SunnySnow, Ice, Storm, Mixed }
    public WeatherState CurrentState { get; private set; } = WeatherState.Clear;
    public float GetRainAmount() => _currentRainVal;

    [Export] private AudioStreamPlayer _thunderPlayer;      // Assign in inspector
    [Export] private AudioStream[] _thunderSounds;          // Array of thunder sounds
    [Export] private float _thunderDelayMin = 0.2f;         // Min delay after lightning
    [Export] private float _thunderDelayMax = 1.5f;         // Max delay (simulates distance)

    // --- REFERENCES ---
    [Export] private GpuParticles3D _rainParticles;
    [Export] private GpuParticles3D _rainSplashParticles;
    [Export] private GpuParticles3D _snowParticles;
    [Export] private MultiMeshInstance3D _canopyLeaves;
    [Export] public ShaderMaterial SkyMaterial;
    [Export] public ShaderMaterial LeafMaterial;
    [Export] public ShaderMaterial GustMaterial;
    [Export] private WorldEnvironment _worldEnv;
    [Export] private Node3D _player;

    // --- LIGHTNING ---
    [Export] private PackedScene _lightningBoltScene;
    [Export] private float _lightningIntervalMin = 5.0f;
    [Export] private float _lightningIntervalMax = 15.0f;
    private float _lightningTimer = 0.0f;
    private float _skyFlashIntensity = 0.0f;

    // --- SEASONAL PALETTES ---
    [ExportCategory("Seasonal Skies")]
    [Export] private SeasonPalette _springSky;
    [Export] private SeasonPalette _summerSky;
    [Export] private SeasonPalette _autumnSky;
    [Export] private SeasonPalette _winterSky;

    // --- SEASONAL LEAF TEXTURES ---
    [ExportCategory("Seasonal Leaves")]
    [Export] private Texture2D _springLeafTexture;
    [Export] private Texture2D _summerLeafTexture;
    [Export] private Texture2D _autumnLeafTexture;

    private Tween _activeTween;

    // Weather blend values
    private float _currentRainVal = 0.0f;
    private float _currentSnowVal = 0.0f;
    private float _currentIceVal  = 0.0f;
    private float _currentGreySkyTint = 0.0f;
    private Color _currentFogBaseColor = new Color(0.8f, 0.8f, 0.8f);
    private float _currentCloudCoverage = 0.3f;
    private float _currentCloudSoftness = 0.5f;
    private float _currentCloudScale    = 0.4f;

    // Wind smoothing
    private Vector3 _targetWindVelocity = Vector3.Zero;
    private Vector3 _currentWindVelocity = Vector3.Zero;
    private Vector2 _currentCloudOffset = Vector2.Zero;
    private float _windSmoothSpeed = 4.0f;

    // --- NEW: Wind direction smoothing and dynamic angle ---
    private float _targetWindAngle;        // desired angle in radians
    private float _currentWindAngle;        // smoothed angle
    private float _windAngleSmoothSpeed = 2.0f; // radians per second
    private float _nextAngleChangeTimer = 0f;
    private const float ANGLE_CHANGE_INTERVAL_MIN = 60f;  // seconds
    private const float ANGLE_CHANGE_INTERVAL_MAX = 300f; // seconds
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // --- NEW: Wind history texture for per‑leaf wind memory ---
    private Image _windHistoryImage;
    private ImageTexture _windHistoryTexture;
    private int _windHistorySize = 256;
    private int _windHistoryIndex = 0;
    private float _windHistoryInterval = 0.1f;
    private float _windHistoryTimer = 0f;
    private float _windHistoryDuration => _windHistorySize * _windHistoryInterval;

    // Wind schedule state
    private enum WindPhase { Calm, Gust, StormBase, StormGust, AutumnLightBreeze }
    private WindPhase _windPhase = WindPhase.Calm;
    private float _windPhaseTimer = 0f;
    private int _autumnCycleCount = 0;         // counts calm/gust cycles in autumn
    private bool _inAutumnLongBreeze = false;  // true when we're in the 2‑minute light breeze

    private bool _manualWindOverride = false;

    // Internal time
    private float _internalTime = 0.0f;

    // Gust mode for ground leaves (now a global uniform)
    private float _currentGustMode = 0.0f;

    // Seasonal / Area state
    private bool _isAutumn = false;
    private Season _currentSeason = Season.SPRING;
    private bool _isInLeafyArea = false;

    // Player tracking
    private Vector3 _lastPlayerPos = Vector3.Zero;

    // Canopy leaves visibility state
    private bool _canopyLeavesVisible = false;
    private Tween _canopyTween;

    // For leaf relaxation after jump
    private bool _lastPlayerOnFloor = true;
    private Vector3 _lastFloorPos;

    // Subscription flag
    private bool _subscribed = false;

    // Transition flag to prevent overlapping animations
    private bool _isTransitioning = false;
    private Tween _currentTransitionTween;

    // Wind freezing during transitions
    private bool _isWindFrozenForTransition = false;
    private Vector3 _frozenWindDir;

    public override void _EnterTree()
    {
        if (!_subscribed && TimeManager.Instance != null)
        {
            SubscribeToTimeManager();
        }
    }

    public override void _Ready()
    {
        ResolveReferences();
        _rng.Randomize();

        if (GustMaterial != null) GustMaterial = (ShaderMaterial)GustMaterial.Duplicate();

        if (_rainParticles != null && _rainParticles.ProcessMaterial is ParticleProcessMaterial rainMat)
            rainMat.CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Disabled;
        if (_snowParticles != null && _snowParticles.ProcessMaterial is ParticleProcessMaterial snowMat)
            snowMat.CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Disabled;

        RenderingServer.GlobalShaderParameterSet("rain_amount", 0.0f);
        RenderingServer.GlobalShaderParameterSet("snow_amount", 0.0f);
        RenderingServer.GlobalShaderParameterSet("ice_amount", 0.0f);
        RenderingServer.GlobalShaderParameterSet("wind_direction", new Vector3(0, 0, 0));
        RenderingServer.GlobalShaderParameterSet("wind_strength", 0.0f);
        RenderingServer.GlobalShaderParameterSet("player_on_floor", 1.0f);
        RenderingServer.GlobalShaderParameterSet("last_floor_position", Vector3.Zero);
        RenderingServer.GlobalShaderParameterSet("last_floor_time", 0.0f);
        RenderingServer.GlobalShaderParameterSet("gust_mode", 0.0f);

        if (TimeManager.Instance != null)
        {
            _currentSeason = TimeManager.Instance.CurrentSeason;
            _isAutumn = _currentSeason == Season.AUTUMN;
            float targetSpawn = _isAutumn ? 1.0f : 0.0f;
            RenderingServer.GlobalShaderParameterSet("spawn_progress", targetSpawn);
        }
        else
        {
            RenderingServer.GlobalShaderParameterSet("spawn_progress", 0.0f);
        }
        RenderingServer.GlobalShaderParameterSet("despawn_progress", 0.0f);

        SetManualWind(Vector3.Zero);

        SetParticlesActive(_rainParticles, false);
        SetParticlesActive(_rainSplashParticles, false);
        SetParticlesActive(_snowParticles, false);

        if (_canopyLeaves != null)
        {
            _canopyLeaves.Visible = false;
            SetCanopyVisibilityProgress(0.0f);
        }

        if (!_subscribed && TimeManager.Instance != null)
        {
            SubscribeToTimeManager();
        }
        else
        {
            if (TimeManager.Instance != null)
            {
                _currentSeason = TimeManager.Instance.CurrentSeason;
                _isAutumn = _currentSeason == Season.AUTUMN;
                UpdateSeasonalLeafTexture();
            }
        }

        if (_player != null && _player is CharacterBody3D cb)
        {
            _lastPlayerOnFloor = cb.IsOnFloor();
        }

        ChangeWeather(WeatherState.Clear, true);
        UpdateCanopyLeavesState();

        // Initialize wind direction with a random angle and set target accordingly
        _targetWindAngle = (float)GD.RandRange(0, Mathf.Tau);
        _currentWindAngle = _targetWindAngle;
        _targetWindVelocity = new Vector3(Mathf.Cos(_targetWindAngle), 0, Mathf.Sin(_targetWindAngle)) * 0.0f;
        _currentWindVelocity = _targetWindVelocity;

        // Schedule first angle change
        ScheduleNextAngleChange();

        // --- NEW: Initialize wind history texture ---
        _windHistoryImage = Image.CreateEmpty(_windHistorySize, 1, false, Image.Format.Rgbaf);
        _windHistoryTexture = ImageTexture.CreateFromImage(_windHistoryImage);

        // Fill with initial calm values
        for (int i = 0; i < _windHistorySize; i++)
        {
            _windHistoryImage.SetPixel(i, 0, new Color(0, 0, 0, 0));
        }
        _windHistoryTexture.Update(_windHistoryImage);

        RenderingServer.GlobalShaderParameterSet("wind_history", _windHistoryTexture);
        RenderingServer.GlobalShaderParameterSet("wind_history_duration", _windHistoryDuration);
    }

    private void ScheduleNextAngleChange()
    {
        _nextAngleChangeTimer = _rng.RandfRange(ANGLE_CHANGE_INTERVAL_MIN, ANGLE_CHANGE_INTERVAL_MAX);
    }

    private void SubscribeToTimeManager()
    {
        TimeManager.Instance.DayChanged += OnDayChanged;
        _currentSeason = TimeManager.Instance.CurrentSeason;
        _isAutumn = _currentSeason == Season.AUTUMN;

        float targetSpawn = _isAutumn ? 1.0f : 0.0f;
        RenderingServer.GlobalShaderParameterSet("spawn_progress", targetSpawn);

        UpdateSeasonalLeafTexture();
        _subscribed = true;
        GD.Print("WeatherManager subscribed to TimeManager");
    }

    public override void _Process(double delta)
    {
        if (!_subscribed && TimeManager.Instance != null)
        {
            SubscribeToTimeManager();
        }

        if (SkyMaterial == null) { ResolveReferences(); if (SkyMaterial == null) return; }

        _internalTime += (float)delta;
        RenderingServer.GlobalShaderParameterSet("global_time", _internalTime);

        float sunProgress = 0.5f;
        if (TimeManager.Instance != null)
            sunProgress = TimeManager.Instance.GetSeasonalSunProgress();

        float dayFactor = 0.0f;
        if (sunProgress > 0.2f && sunProgress < 0.8f)
        {
            float t = (sunProgress - 0.2f) / 0.6f;
            dayFactor = Mathf.Sin(t * Mathf.Pi);
        }

        // --- WIND SCHEDULE (only if not manually overridden and not frozen) ---
        if (!_manualWindOverride && !_isWindFrozenForTransition)
            UpdateWindSchedule((float)delta);

        // --- SMOOTH WIND ANGLE ---
        if (!_isWindFrozenForTransition)
        {
            _currentWindAngle = Mathf.LerpAngle(_currentWindAngle, _targetWindAngle, (float)delta * _windAngleSmoothSpeed);
            // Reconstruct wind vector using the smoothed angle and target strength (which is already smoothed in velocity)
            float targetStrength = _targetWindVelocity.Length();
            Vector3 windVec = new Vector3(Mathf.Cos(_currentWindAngle), 0, Mathf.Sin(_currentWindAngle)) * targetStrength;
            _targetWindVelocity = windVec; // Keep magnitude, but ensure direction matches smoothed angle
        }

        // --- SMOOTH WIND VELOCITY ---
        _currentWindVelocity = _currentWindVelocity.Lerp(_targetWindVelocity, (float)delta * _windSmoothSpeed);
        RenderingServer.GlobalShaderParameterSet("wind_direction", _currentWindVelocity);
        RenderingServer.GlobalShaderParameterSet("wind_strength", _currentWindVelocity.Length());

        // --- WIND FREEZE OVERRIDE ---
        if (_isWindFrozenForTransition)
        {
            _currentWindVelocity = _frozenWindDir;
            _targetWindVelocity = _frozenWindDir;
            RenderingServer.GlobalShaderParameterSet("wind_direction", _currentWindVelocity);
            RenderingServer.GlobalShaderParameterSet("wind_strength", _currentWindVelocity.Length());
        }

        // --- PERIODIC WIND ANGLE CHANGE ---
        if (!_manualWindOverride && !_isWindFrozenForTransition)
        {
            _nextAngleChangeTimer -= (float)delta;
            while (_nextAngleChangeTimer <= 0)
            {
                UpdateTargetWindAngle();
                ScheduleNextAngleChange();
            }
        }

        // --- NEW: Update wind history texture ---
        _windHistoryTimer += (float)delta;
        while (_windHistoryTimer >= _windHistoryInterval)
        {
            _windHistoryTimer -= _windHistoryInterval;
            WriteWindSample();
        }

        // --- UPDATE CANOPY LEAVES BASED ON CURRENT WIND ---
        UpdateCanopyLeavesState();

        // Update gust mode globally
        float targetGust = (CurrentState == WeatherState.Storm || _currentWindVelocity.Length() > 10.0f) ? 1.0f : 0.0f;
        _currentGustMode = Mathf.Lerp(_currentGustMode, targetGust, (float)delta * 2.0f);
        RenderingServer.GlobalShaderParameterSet("gust_mode", _currentGustMode);

        // --- LIGHTNING ---
        if (CurrentState == WeatherState.Storm)
        {
            _lightningTimer -= (float)delta;
            if (_lightningTimer <= 0.0f)
            {
                TriggerLightning();
                _lightningTimer = (float)GD.RandRange(_lightningIntervalMin, _lightningIntervalMax);
            }
        }

        if (_skyFlashIntensity > 0.0f)
        {
            _skyFlashIntensity -= (float)delta * 5.0f;
            if (_skyFlashIntensity < 0.0f) _skyFlashIntensity = 0.0f;
            if (IsInstanceValid(SkyMaterial))
                SkyMaterial.SetShaderParameter("lightning_strength", _skyFlashIntensity);
        }

        // --- PARTICLE TINT ---
        float brightness = Mathf.Lerp(0.1f, 1.0f, dayFactor);

        if (IsInstanceValid(_player))
        {
            float groundY = _player.GlobalPosition.Y;
            RenderingServer.GlobalShaderParameterSet("player_position", _player.GlobalPosition);

            float speed = 0.0f;
            bool onFloor = false;
            CharacterBody3D cb = null;

            if (_player is CharacterBody3D characterBody)
            {
                cb = characterBody;
                speed = cb.Velocity.Length();
                onFloor = cb.IsOnFloor();
            }
            else if (_player is RigidBody3D rb)
            {
                speed = rb.LinearVelocity.Length();
                onFloor = true;
            }
            else if (delta > 0.0001)
            {
                speed = (_player.GlobalPosition - _lastPlayerPos).Length() / (float)delta;
                _lastPlayerPos = _player.GlobalPosition;
                onFloor = true;
            }

            RenderingServer.GlobalShaderParameterSet("player_speed", speed);
            RenderingServer.GlobalShaderParameterSet("player_on_floor", onFloor ? 1.0f : 0.0f);

            SetShaderParam(_rainParticles, "ground_height", groundY);
            SetShaderParam(_rainSplashParticles, "ground_height", groundY);
            SetShaderParam(_snowParticles, "ground_height", groundY);

            if (IsInstanceValid(_rainParticles))
            {
                float rainBright = Mathf.Max(brightness, 0.2f);
                SetMaterialAlbedo(_rainParticles, new Color(rainBright, rainBright, rainBright, 1.0f));
            }
            if (IsInstanceValid(_rainSplashParticles))
                SetMaterialAlbedo(_rainSplashParticles, new Color(brightness, brightness, brightness, 1.0f));
            if (IsInstanceValid(_snowParticles))
                SetMaterialAlbedo(_snowParticles, new Color(brightness, brightness, brightness, 1.0f));

            if (cb != null)
            {
                bool currentOnFloor = cb.IsOnFloor();
                if (!currentOnFloor && _lastPlayerOnFloor)
                {
                    _lastFloorPos = cb.GlobalPosition;
                    RenderingServer.GlobalShaderParameterSet("last_floor_position", _lastFloorPos);
                    RenderingServer.GlobalShaderParameterSet("last_floor_time", _internalTime);
                }
                _lastPlayerOnFloor = currentOnFloor;
            }
        }

        // --- SKY COLORS ---
        if (IsInstanceValid(SkyMaterial) && TimeManager.Instance != null)
        {
            SeasonPalette activePalette = _springSky;
            switch (_currentSeason)
            {
                case Season.SPRING: activePalette = _springSky ?? _summerSky; break;
                case Season.SUMMER: activePalette = _summerSky ?? _springSky; break;
                case Season.AUTUMN: activePalette = _autumnSky ?? _springSky; break;
                case Season.WINTER: activePalette = _winterSky ?? _springSky; break;
            }

            if (activePalette != null && activePalette.TopColor != null)
            {
                Color topCol = activePalette.TopColor.Sample(sunProgress);
                Color horCol = activePalette.HorizonColor.Sample(sunProgress);
                Color sunCol = activePalette.SunColor != null
                    ? activePalette.SunColor.Sample(sunProgress)
                    : new Color(1, 0.9f, 0.7f);

                Color stormGrey = new Color(0.3f, 0.35f, 0.4f);
                topCol = topCol.Lerp(stormGrey, _currentGreySkyTint);
                horCol = horCol.Lerp(stormGrey, _currentGreySkyTint);
                sunCol = sunCol.Lerp(new Color(0.1f, 0.1f, 0.1f), _currentGreySkyTint * 0.9f);

                SkyMaterial.SetShaderParameter("sky_top_color", topCol);
                SkyMaterial.SetShaderParameter("sky_horizon_color", horCol);
                SkyMaterial.SetShaderParameter("sun_color", sunCol);
                SkyMaterial.SetShaderParameter("time_of_day", sunProgress * 24.0f);
            }
        }

        // --- FOG LIGHTING ---
        if (IsInstanceValid(_worldEnv) && _worldEnv.Environment != null)
        {
            float minNightBrightness = 0.02f;
            if (CurrentState == WeatherState.Snow || CurrentState == WeatherState.SunnySnow)
                minNightBrightness = 0.15f;
            float finalBrightness = Mathf.Lerp(minNightBrightness, 1.0f, dayFactor);
            _worldEnv.Environment.VolumetricFogAlbedo = _currentFogBaseColor * finalBrightness;
        }

        // --- CLOUD MOVEMENT (uses smoothed wind) ---
        if (IsInstanceValid(SkyMaterial))
        {
            Vector3 windForClouds = _currentWindVelocity;
            Vector2 baseWind = new Vector2(windForClouds.X, windForClouds.Z) * 0.5f;
            Vector2 driftWind = new Vector2(2.0f, 0.0f);
            Vector2 finalCloudVelocity = baseWind + driftWind;
            Vector2 windDelta = finalCloudVelocity * (float)delta * 0.0125f;

            _currentCloudOffset += windDelta;
            if (_currentCloudOffset.X > 100.0f) _currentCloudOffset.X -= 100.0f;
            if (_currentCloudOffset.Y > 100.0f) _currentCloudOffset.Y -= 100.0f;
            if (_currentCloudOffset.X < -100.0f) _currentCloudOffset.X += 100.0f;
            if (_currentCloudOffset.Y < -100.0f) _currentCloudOffset.Y += 100.0f;

            SkyMaterial.SetShaderParameter("wind_offset", _currentCloudOffset);
        }
    }

    // -------------------------------------------------------------------------
    // NEW: Determine target wind angle based on season, time of day, randomness
    // -------------------------------------------------------------------------
    private void UpdateTargetWindAngle()
    {
        float baseAngle;
        float hour = 12f; // default
        if (TimeManager.Instance != null)
            hour = TimeManager.Instance.Hour + TimeManager.Instance.Minute / 60f;

        // Seasonal base direction (in radians)
        switch (_currentSeason)
        {
            case Season.SPRING:
                baseAngle = Mathf.DegToRad(225f); // SW
                break;
            case Season.SUMMER:
                baseAngle = Mathf.DegToRad(270f); // W
                break;
            case Season.AUTUMN:
                baseAngle = Mathf.DegToRad(225f); // SW
                break;
            case Season.WINTER:
                baseAngle = Mathf.DegToRad(270f); // W (could also be NW)
                break;
            default:
                baseAngle = 0f;
                break;
        }

        // Diurnal bias: sea breeze effect (stronger in summer)
        float diurnalAmplitude = (_currentSeason == Season.SUMMER) ? 0.3f : 0.15f;
        float dayPhase = Mathf.Sin((hour - 6f) / 24f * Mathf.Tau); // peaks around midday
        float diurnalBias = diurnalAmplitude * dayPhase; // positive => shift toward west (sea) during day

        // Random walk: small persistent change
        float randomWalk = _rng.RandfRange(-0.2f, 0.2f);

        float newAngle = baseAngle + diurnalBias + randomWalk;
        // Keep within 0..2π
        newAngle = Mathf.PosMod(newAngle, Mathf.Tau);

        _targetWindAngle = newAngle;
    }

    // -------------------------------------------------------------------------
    // WIND SCHEDULE (now includes autumn pattern)
    // -------------------------------------------------------------------------
    private void UpdateWindSchedule(float delta)
    {
        _windPhaseTimer -= delta;

        while (_windPhaseTimer <= 0f)
        {
            bool isStorm = CurrentState == WeatherState.Storm;
            bool isAutumn = _currentSeason == Season.AUTUMN && !isStorm;

            // Default durations and strengths (for non‑autumn, non‑storm)
            float baseStrength = 2f;
            float gustStrength = 4f;
            float calmDuration = 8f;
            float gustDuration = 3f;

            if (isStorm)
            {
                baseStrength = 5f;
                gustStrength = 9f;
                calmDuration = 10f;
                gustDuration = 3f;
                _windPhase = WindPhase.StormBase; // ensure we start in base
            }
            else if (isAutumn)
            {
                // Autumn special handling
                if (_inAutumnLongBreeze)
                {
                    // We are in the 2‑minute light breeze after three cycles
                    baseStrength = 1f;
                    calmDuration = 120f; // 2 minutes
                    gustDuration = 0f;    // not used in this phase
                    _windPhase = WindPhase.AutumnLightBreeze;
                }
                else
                {
                    // Normal autumn pattern: 3 cycles of calm/gust
                    switch (_windPhase)
                    {
                        case WindPhase.Calm:
                            calmDuration = _rng.RandfRange(10f, 15f);
                            gustDuration = _rng.RandfRange(5f, 8f);
                            baseStrength = 0f;   // calm
                            gustStrength = 3f;   // moderate gust
                            break;
                        case WindPhase.Gust:
                            // After a gust, if we've done 3 cycles, move to long breeze
                            _autumnCycleCount++;
                            if (_autumnCycleCount >= 3)
                            {
                                _inAutumnLongBreeze = true;
                                _autumnCycleCount = 0;
                                // Immediately start long breeze
                                _windPhase = WindPhase.AutumnLightBreeze;
                                _windPhaseTimer = 120f;
                                SetTargetWindStrength(1f);
                                continue; // skip the rest of this iteration
                            }
                            // Otherwise, go back to calm
                            calmDuration = _rng.RandfRange(10f, 15f);
                            baseStrength = 0f;
                            break;
                        default:
                            // Fallback to calm
                            calmDuration = 10f;
                            baseStrength = 0f;
                            break;
                    }
                }
            }

            // Phase transitions
            if (!isAutumn || _inAutumnLongBreeze)
            {
                // Standard phase progression (non‑autumn, or during long breeze)
                switch (_windPhase)
                {
                    case WindPhase.Calm:
                    case WindPhase.StormBase:
                        _windPhase = isStorm ? WindPhase.StormGust : WindPhase.Gust;
                        _windPhaseTimer = gustDuration;
                        SetTargetWindStrength(gustStrength);
                        break;
                    case WindPhase.Gust:
                    case WindPhase.StormGust:
                        _windPhase = isStorm ? WindPhase.StormBase : WindPhase.Calm;
                        _windPhaseTimer = calmDuration;
                        SetTargetWindStrength(baseStrength);
                        break;
                    case WindPhase.AutumnLightBreeze:
                        // After the long breeze, reset to normal autumn pattern
                        _inAutumnLongBreeze = false;
                        _windPhase = WindPhase.Calm;
                        _windPhaseTimer = _rng.RandfRange(10f, 15f);
                        SetTargetWindStrength(0f);
                        break;
                }
            }
            else
            {
                // Autumn pattern (within the 3 cycles)
                switch (_windPhase)
                {
                    case WindPhase.Calm:
                        _windPhase = WindPhase.Gust;
                        _windPhaseTimer = gustDuration;
                        SetTargetWindStrength(gustStrength);
                        break;
                    case WindPhase.Gust:
                        _windPhase = WindPhase.Calm;
                        _windPhaseTimer = calmDuration;
                        SetTargetWindStrength(baseStrength);
                        break;
                    default:
                        _windPhase = WindPhase.Calm;
                        _windPhaseTimer = calmDuration;
                        SetTargetWindStrength(baseStrength);
                        break;
                }
            }
        }
    }

    private void SetTargetWindStrength(float strength)
    {
        // Use the current smoothed angle (or target angle) to set direction.
        // We'll use _targetWindAngle for consistency; the smoothing will handle it.
        Vector3 windVec = new Vector3(Mathf.Cos(_targetWindAngle), 0, Mathf.Sin(_targetWindAngle)) * strength;
        _targetWindVelocity = windVec;
    }

    // --- NEW: Write a sample to the wind history texture ---
    private void WriteWindSample()
    {
        // Use the *target* wind values (not heavily smoothed) so the history reflects actual changes.
        float strength = _targetWindVelocity.Length();
        Vector3 dir = strength > 0.01f ? _targetWindVelocity.Normalized() : Vector3.Zero;

        Color sample = new Color(strength, dir.X, dir.Z, 0f);
        _windHistoryImage.SetPixel(_windHistoryIndex, 0, sample);
        _windHistoryIndex = (_windHistoryIndex + 1) % _windHistorySize;

        _windHistoryTexture.Update(_windHistoryImage);
    }

    // -------------------------------------------------------------------------
    // CANOPY LEAF STATE
    // -------------------------------------------------------------------------
    public void SetInLeafyArea(bool inArea)
    {
        _isInLeafyArea = inArea;
        UpdateCanopyLeavesState();
    }

    private void UpdateCanopyLeavesState()
    {
        if (_canopyLeaves == null) return;

        if (_currentSeason == Season.WINTER)
        {
            if (_canopyLeavesVisible) SetCanopyVisible(false);
            return;
        }

        float windStrength = _currentWindVelocity.Length();
        bool shouldShow = CurrentState == WeatherState.Storm || _isInLeafyArea || windStrength > 0.5f;

        if (shouldShow != _canopyLeavesVisible)
        {
            SetCanopyVisible(shouldShow);
        }
    }

    private void SetCanopyVisible(bool visible)
    {
        _canopyLeavesVisible = visible;

        if (_canopyTween != null && _canopyTween.IsValid())
            _canopyTween.Kill();

        if (visible)
        {
            _canopyLeaves.Visible = true;
            _canopyTween = CreateTween();
            _canopyTween.TweenMethod(
                Callable.From<float>(v => SetCanopyVisibilityProgress(v)),
                GetCanopyVisibilityProgress(),
                1.0f,
                6.0f
            );
        }
        else
        {
            _canopyTween = CreateTween();
            _canopyTween.TweenMethod(
                Callable.From<float>(v => SetCanopyVisibilityProgress(v)),
                GetCanopyVisibilityProgress(),
                0.0f,
                3.0f
            );
            _canopyTween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(_canopyLeaves))
                    _canopyLeaves.Visible = false;
            }));
        }
    }

    private void SetCanopyVisibilityProgress(float v)
    {
        if (!IsInstanceValid(_canopyLeaves)) return;
        SetMultiMeshShaderParam(_canopyLeaves, "visibility_progress", v);
    }

    private float GetCanopyVisibilityProgress()
    {
        if (!IsInstanceValid(_canopyLeaves)) return 0.0f;
        if (_canopyLeaves.MaterialOverride is ShaderMaterial m)
        {
            var val = m.GetShaderParameter("visibility_progress");
            if (val.VariantType != Variant.Type.Nil)
                return val.AsSingle();
        }
        else if (_canopyLeaves.Multimesh?.Mesh?.SurfaceGetMaterial(0) is ShaderMaterial sm)
        {
            var val = sm.GetShaderParameter("visibility_progress");
            if (val.VariantType != Variant.Type.Nil)
                return val.AsSingle();
        }
        return 0.0f;
    }

    // -------------------------------------------------------------------------
    // MANUAL OVERRIDE CONTROL
    // -------------------------------------------------------------------------
    public void SetAutoWindEnabled(bool enabled)
    {
        _manualWindOverride = !enabled;
    }

    public bool IsAutoWindEnabled()
    {
        return !_manualWindOverride;
    }

    // -------------------------------------------------------------------------
    // SEASONAL TRANSITIONS (using global uniforms, with wind freeze)
    // -------------------------------------------------------------------------
    private void OnDayChanged(int d, int m, int day, int seasonVal)
    {
        Season newSeason = (Season)seasonVal;

        // If season hasn't changed, ignore
        if (newSeason == _currentSeason)
            return;

        // If a transition is already in progress, ignore this call completely
        if (_isTransitioning)
        {
            GD.Print("WeatherManager: Ignoring season change because transition in progress");
            return;
        }

        GD.Print($"WeatherManager.OnDayChanged: newSeason={seasonVal}, currentSeason={_currentSeason}");

        if (newSeason == Season.AUTUMN && _currentSeason == Season.SUMMER)
        {
            GD.Print("  --> Summer -> Autumn: AnimateLeavesIn()");
            _isAutumn = true;
            // Reset autumn cycle counters so the pattern repeats every year
            _autumnCycleCount = 0;
            _inAutumnLongBreeze = false;
            _windPhase = WindPhase.Calm;
            AnimateLeavesIn();
        }
        else if (newSeason == Season.WINTER && _currentSeason == Season.AUTUMN)
        {
            GD.Print("  --> Autumn -> Winter: AnimateLeavesOut()");
            _isAutumn = false;
            AnimateLeavesOut();
        }

        _currentSeason = newSeason;
        _isAutumn = _currentSeason == Season.AUTUMN;

        UpdateSeasonalLeafTexture();
        UpdateCanopyLeavesState();

        // Daily weather roll (wind will be frozen if a transition is ongoing)
        float roll = GD.Randf();
        WeatherState next = WeatherState.Clear;
        if (seasonVal == (int)Season.WINTER)
        {
            if (roll > 0.7f) next = WeatherState.Snow;
            else if (roll > 0.4f) next = WeatherState.Ice;
        }
        else if (seasonVal == (int)Season.AUTUMN)
        {
            if (roll > 0.5f) next = WeatherState.Rain;
        }
        else if (seasonVal == (int)Season.SPRING)
        {
            if (roll > 0.7f) next = WeatherState.Rain;
        }
        ChangeWeather(next);
    }

    private void UpdateSeasonalLeafTexture()
    {
        Texture2D targetTex = null;
        switch (_currentSeason)
        {
            case Season.SPRING: targetTex = _springLeafTexture; break;
            case Season.SUMMER: targetTex = _summerLeafTexture; break;
            case Season.AUTUMN: targetTex = _autumnLeafTexture; break;
            case Season.WINTER: return;
        }

        if (targetTex == null) return;

        if (_canopyLeaves != null)
        {
            if (_canopyLeaves.MaterialOverride is ShaderMaterial overrideMat)
                overrideMat.SetShaderParameter("leaf_texture", targetTex);
            else if (_canopyLeaves.Multimesh?.Mesh?.SurfaceGetMaterial(0) is ShaderMaterial surfaceMat)
                surfaceMat.SetShaderParameter("leaf_texture", targetTex);
        }

        if (LeafMaterial != null)
            LeafMaterial.SetShaderParameter("leaf_texture", targetTex);
        if (GustMaterial != null)
            GustMaterial.SetShaderParameter("leaf_texture", targetTex);
    }

    private void AnimateLeavesIn()
    {
        // Prevent overlapping transitions
        if (_isTransitioning)
            return;

        bool wasAuto = IsAutoWindEnabled();
        if (wasAuto)
            SetAutoWindEnabled(false);

        // Capture current wind direction and freeze it
        _frozenWindDir = _currentWindVelocity;
        _isWindFrozenForTransition = true;

        _isTransitioning = true;
        _currentTransitionTween = CreateTween();
        _currentTransitionTween.TweenMethod(
            Callable.From<float>(v => RenderingServer.GlobalShaderParameterSet("spawn_progress", v)),
            0.0f, 1.0f, 5.0f
        );
        _currentTransitionTween.TweenCallback(Callable.From(() =>
        {
            _isTransitioning = false;
            _isWindFrozenForTransition = false;
            if (wasAuto)
            {
                GetTree().CreateTimer(0.1f).Timeout += () => SetAutoWindEnabled(true);
            }
        }));
        if (_canopyLeaves != null) _canopyLeaves.Visible = true;
    }

    private void AnimateLeavesOut()
    {
        // Prevent overlapping transitions
        if (_isTransitioning)
            return;

        bool wasAuto = IsAutoWindEnabled();
        if (wasAuto)
            SetAutoWindEnabled(false);

        // Capture current wind direction and freeze it
        _frozenWindDir = _currentWindVelocity;
        _isWindFrozenForTransition = true;

        _isTransitioning = true;
        _currentTransitionTween = CreateTween();
        _currentTransitionTween.TweenMethod(
            Callable.From<float>(v => RenderingServer.GlobalShaderParameterSet("despawn_progress", v)),
            0.0f, 1.0f, 5.0f
        );
        _currentTransitionTween.TweenCallback(Callable.From(() =>
        {
            RenderingServer.GlobalShaderParameterSet("spawn_progress", 0.0f);
            RenderingServer.GlobalShaderParameterSet("despawn_progress", 0.0f);
            _isTransitioning = false;
            _isWindFrozenForTransition = false;
            if (wasAuto)
            {
                GetTree().CreateTimer(0.1f).Timeout += () => SetAutoWindEnabled(true);
            }
        }));
    }

    // -------------------------------------------------------------------------
    // WEATHER CHANGE
    // -------------------------------------------------------------------------
    public void ChangeWeather(WeatherState newState, bool immediate = false)
    {
        CurrentState = newState;
        float targetRain = 0f, targetSnow = 0f, targetIce = 0f, windSpeed = 0f;
        float targetFogDensity = 0f;
        Color targetFogColor = new Color(0.8f, 0.8f, 0.8f);
        float targetCloudCoverage = 0.3f, targetCloudSoftness = 0.5f, targetCloudScale = 0.4f;
        Color targetCloudColor = new Color(1.0f, 1.0f, 1.0f);
        float targetGreyTint = 0.0f;

        switch (newState)
        {
            case WeatherState.Clear:
                windSpeed = 0.0f; targetFogDensity = 0.0f;
                targetFogColor = new Color(0.8f, 0.8f, 0.8f);
                targetCloudCoverage = 0.3f; targetCloudSoftness = 0.5f;
                targetCloudColor = new Color(1.0f, 1.0f, 1.0f);
                break;
            case WeatherState.Rain:
                targetRain = 1.0f; targetGreyTint = 0.6f;
                windSpeed = 2.0f; targetFogDensity = 0.02f;
                targetFogColor = new Color(0.6f, 0.65f, 0.7f);
                targetCloudCoverage = 0.7f; targetCloudSoftness = 0.7f;
                targetCloudColor = new Color(0.9f, 0.92f, 0.95f);
                break;
            case WeatherState.SummerRain:
                targetRain = 1.0f; targetGreyTint = 0.0f;
                windSpeed = 1.0f; targetFogDensity = 0.01f;
                targetFogColor = new Color(0.8f, 0.85f, 0.9f);
                targetCloudCoverage = 0.5f; targetCloudSoftness = 0.5f;
                targetCloudColor = new Color(0.9f, 0.92f, 0.95f);
                break;
            case WeatherState.Snow:
                targetSnow = 1.0f; targetGreyTint = 0.4f;
                windSpeed = 1.5f; targetFogDensity = 0.04f;
                targetFogColor = new Color(0.75f, 0.8f, 0.85f);
                targetCloudCoverage = 0.85f; targetCloudSoftness = 0.6f;
                targetCloudColor = new Color(1.0f, 1.0f, 1.0f);
                break;
            case WeatherState.SunnySnow:
                targetSnow = 1.0f; targetGreyTint = 0.0f;
                windSpeed = 0.5f; targetFogDensity = 0.02f;
                targetFogColor = new Color(0.85f, 0.9f, 0.95f);
                targetCloudCoverage = 0.4f; targetCloudSoftness = 0.5f;
                targetCloudColor = new Color(1.0f, 1.0f, 1.0f);
                break;
            case WeatherState.Storm:
                targetRain = 1.0f; targetGreyTint = 1.0f;
                windSpeed = 20.0f; targetFogDensity = 0.15f;
                targetFogColor = new Color(0.5f, 0.5f, 0.55f);
                targetCloudCoverage = 0.9f; targetCloudSoftness = 0.9f;
                targetCloudColor = new Color(0.5f, 0.52f, 0.55f);
                break;
            case WeatherState.Ice:
                targetIce = 1.0f; windSpeed = 0.0f; targetFogDensity = 0.01f;
                targetFogColor = new Color(0.8f, 0.85f, 0.9f);
                targetCloudCoverage = 0.45f; targetCloudSoftness = 0.05f;
                targetCloudColor = new Color(0.9f, 0.95f, 1.0f);
                break;
            case WeatherState.Mixed:
                targetRain = 0.5f; targetSnow = 0.5f; targetIce = 0.3f; targetGreyTint = 0.5f;
                windSpeed = 5.0f; targetFogDensity = 0.03f;
                targetFogColor = new Color(0.6f, 0.65f, 0.7f);
                targetCloudCoverage = 0.6f; targetCloudSoftness = 0.3f;
                targetCloudColor = new Color(0.8f, 0.8f, 0.8f);
                break;
        }

        // If wind is frozen for a transition, do not change the target wind velocity
        if (!_isWindFrozenForTransition)
        {
            // Preserve current target angle, only adjust strength
            // We'll use _targetWindAngle to compute new vector.
            Vector3 windVec = new Vector3(Mathf.Cos(_targetWindAngle), 0, Mathf.Sin(_targetWindAngle)) * windSpeed;
            _targetWindVelocity = windVec;
        }

        UpdateCanopyLeavesState();

        bool isRaining = targetRain > 0.1f;
        bool isSnowing = targetSnow > 0.1f;

        if (IsInstanceValid(_rainParticles))
        {
            SetParticlesActive(_rainParticles, isRaining);
            SetShaderParam(_rainParticles, "do_recycle", isRaining ? 1.0f : 0.0f);
        }
        if (IsInstanceValid(_rainSplashParticles))
            SetParticlesActive(_rainSplashParticles, isRaining);
        if (IsInstanceValid(_snowParticles))
        {
            SetParticlesActive(_snowParticles, isSnowing);
            SetShaderParam(_snowParticles, "is_snow", 1.0f);
            SetShaderParam(_snowParticles, "do_recycle", isSnowing ? 1.0f : 0.0f);
        }

        if (_activeTween != null && _activeTween.IsValid()) _activeTween.Kill();

        if (immediate)
        {
            _currentRainVal = targetRain;
            RenderingServer.GlobalShaderParameterSet("rain_amount", targetRain);
            _currentSnowVal = targetSnow;
            RenderingServer.GlobalShaderParameterSet("snow_amount", targetSnow);
            _currentIceVal = targetIce;
            RenderingServer.GlobalShaderParameterSet("ice_amount", targetIce);
            _currentGreySkyTint = targetGreyTint;
            _currentFogBaseColor = targetFogColor;

            if (IsInstanceValid(_worldEnv) && _worldEnv.Environment != null)
                _worldEnv.Environment.VolumetricFogDensity = targetFogDensity;

            if (IsInstanceValid(SkyMaterial))
            {
                _currentCloudCoverage = targetCloudCoverage;
                _currentCloudSoftness = targetCloudSoftness;
                _currentCloudScale = targetCloudScale;
                SkyMaterial.SetShaderParameter("cloud_coverage", targetCloudCoverage);
                SkyMaterial.SetShaderParameter("cloud_softness", targetCloudSoftness);
                SkyMaterial.SetShaderParameter("cloud_scale", targetCloudScale);
                SkyMaterial.SetShaderParameter("cloud_tint", targetCloudColor);
            }
        }
        else
        {
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);
            _activeTween.TweenMethod(
                Callable.From<float>(v => { _currentRainVal = v; RenderingServer.GlobalShaderParameterSet("rain_amount", v); }),
                _currentRainVal, targetRain, 8.0f);
            _activeTween.TweenMethod(
                Callable.From<float>(v => { _currentSnowVal = v; RenderingServer.GlobalShaderParameterSet("snow_amount", v); }),
                _currentSnowVal, targetSnow, 8.0f);
            _activeTween.TweenMethod(
                Callable.From<float>(v => { _currentIceVal = v; RenderingServer.GlobalShaderParameterSet("ice_amount", v); }),
                _currentIceVal, targetIce, 8.0f);
            _activeTween.TweenMethod(
                Callable.From<float>(v => _currentGreySkyTint = v),
                _currentGreySkyTint, targetGreyTint, 8.0f);
            _activeTween.TweenMethod(
                Callable.From<Color>(c => _currentFogBaseColor = c),
                _currentFogBaseColor, targetFogColor, 8.0f);

            if (IsInstanceValid(_worldEnv) && _worldEnv.Environment != null)
                _activeTween.TweenProperty(_worldEnv.Environment, "volumetric_fog_density", targetFogDensity, 8.0f);

            if (IsInstanceValid(SkyMaterial))
            {
                _activeTween.TweenMethod(
                    Callable.From<float>(v => { _currentCloudCoverage = v; SkyMaterial.SetShaderParameter("cloud_coverage", v); }),
                    _currentCloudCoverage, targetCloudCoverage, 8.0f);
                _activeTween.TweenMethod(
                    Callable.From<float>(v => { _currentCloudSoftness = v; SkyMaterial.SetShaderParameter("cloud_softness", v); }),
                    _currentCloudSoftness, targetCloudSoftness, 8.0f);
                _activeTween.TweenMethod(
                    Callable.From<float>(v => { _currentCloudScale = v; SkyMaterial.SetShaderParameter("cloud_scale", v); }),
                    _currentCloudScale, targetCloudScale, 8.0f);
                _activeTween.TweenMethod(
                    Callable.From<Color>(c => SkyMaterial.SetShaderParameter("cloud_tint", c)),
                    (Color)SkyMaterial.GetShaderParameter("cloud_tint"), targetCloudColor, 8.0f);
            }
        }
    }

    // -------------------------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------------------------
    public void SetManualWind(Vector3 wind)
    {
        if (!_isWindFrozenForTransition)
            _targetWindVelocity = wind;
    }

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------
    private void SetParticlesActive(GpuParticles3D particles, bool active)
    {
        if (!IsInstanceValid(particles)) return;
        if (active)
        {
            particles.ProcessMode = ProcessModeEnum.Inherit;
            particles.Visible = true;
            particles.Emitting = true;
        }
        else
        {
            if (!particles.Emitting && particles.ProcessMode == ProcessModeEnum.Disabled) return;
            particles.Emitting = false;
            float lifetime = (float)particles.Lifetime;
            GetTree().CreateTimer(lifetime, false).Timeout += () =>
            {
                if (IsInstanceValid(particles) && !particles.Emitting)
                {
                    particles.ProcessMode = ProcessModeEnum.Disabled;
                    particles.Visible = false;
                }
            };
        }
    }

    private void SetShaderParam(Node node, string param, Variant val)
    {
        if (node is GpuParticles3D p && p.ProcessMaterial is ShaderMaterial sm)
            sm.SetShaderParameter(param, val);
    }

    private void SetMultiMeshShaderParam(MultiMeshInstance3D mm, string param, Variant val)
    {
        if (mm.MaterialOverride is ShaderMaterial overrideMat)
            overrideMat.SetShaderParameter(param, val);
        else if (mm.Multimesh?.Mesh?.SurfaceGetMaterial(0) is ShaderMaterial surfaceMat)
            surfaceMat.SetShaderParameter(param, val);
    }

    private void SetMaterialAlbedo(GpuParticles3D particles, Color color)
    {
        if (particles.DrawPass1?.SurfaceGetMaterial(0) is StandardMaterial3D stdMat)
        { stdMat.AlbedoColor = color; return; }
        if (particles.MaterialOverride is StandardMaterial3D overrideMat)
            overrideMat.AlbedoColor = color;
    }

    private void ResolveReferences()
    {
        if (_player == null)
            _player = GetTree().Root.FindChild("Player", true, false) as Node3D;
        if (_rainParticles == null)
            _rainParticles = GetTree().Root.FindChild("RainParticles", true, false) as GpuParticles3D;
        if (_rainSplashParticles == null)
            _rainSplashParticles = GetTree().Root.FindChild("RainSplashParticles", true, false) as GpuParticles3D;
        if (_snowParticles == null)
            _snowParticles = GetTree().Root.FindChild("SnowParticles", true, false) as GpuParticles3D;
        if (_canopyLeaves == null)
            _canopyLeaves = GetTree().Root.FindChild("FallingLeaves_MM", true, false) as MultiMeshInstance3D;
        if (_worldEnv == null)
            _worldEnv = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
        if (SkyMaterial == null && _worldEnv?.Environment?.Sky != null)
            SkyMaterial = _worldEnv.Environment.Sky.SkyMaterial as ShaderMaterial;
        if (_lightningBoltScene == null)
            _lightningBoltScene = GD.Load<PackedScene>("res://Scenes/Effects/LightningBolt.tscn");
    }

    private void TriggerLightning()
    {
        if (_lightningBoltScene == null) { GD.PrintErr("WeatherManager: Lightning Scene is NULL!"); return; }
        if (!IsInstanceValid(_player)) return;

        Node3D bolt = _lightningBoltScene.Instantiate<Node3D>();
        GetTree().Root.AddChild(bolt);

        float angle = (float)GD.RandRange(0, Mathf.Tau);
        float dist = (float)GD.RandRange(50.0f, 150.0f);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * dist;

        bolt.GlobalPosition = new Vector3(
            _player.GlobalPosition.X + offset.X,
            _player.GlobalPosition.Y,
            _player.GlobalPosition.Z + offset.Z
        );
        _skyFlashIntensity = 1.0f;

        // --- Play thunder ---
        if (_thunderPlayer != null && _thunderSounds != null && _thunderSounds.Length > 0)
        {
            // Choose a random thunder sound
            _thunderPlayer.Stream = _thunderSounds[GD.RandRange(0, _thunderSounds.Length - 1)];
            
            // Calculate delay based on distance (speed of sound ~343 m/s)
            float soundDelay = dist / 343f; // seconds
            // Clamp to reasonable range and add randomness
            soundDelay = Mathf.Clamp(soundDelay, _thunderDelayMin, _thunderDelayMax);
            
            // Play after delay
            GetTree().CreateTimer(soundDelay).Timeout += () =>
            {
                if (IsInstanceValid(_thunderPlayer))
                    _thunderPlayer.Play();
            };
        }
    }
}