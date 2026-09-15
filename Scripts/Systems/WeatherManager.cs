using Godot;
using System;

public partial class WeatherManager : Node
{
    public enum WeatherState { Clear, Rain, Snow, Rainstorm }
    public WeatherState CurrentState { get; private set; } = WeatherState.Clear;
    public float GetRainAmount() => _currentRainVal;

    // --- StringName cache (prevents GC allocations) ---
    [Export] private WeatherPreset[] _weatherPresets; // ORDER = WeatherState: Clear, Rain, Snow, Rainstorm
    public static WeatherManager Instance { get; private set; }
    public override void _ExitTree() { if (Instance == this) Instance = null; }
    private bool  _worldResolved;
    private bool  _weatherReapplied;
    private float _worldResolveTimer;
    private static readonly StringName ParamGlobalTime          = "global_time";
    private static readonly StringName ParamRainAmount          = "rain_amount";
    private static readonly StringName ParamSnowAmount          = "snow_amount";
    private static readonly StringName ParamIceAmount           = "ice_amount";
    private static readonly StringName ParamWindDirection       = "wind_direction";
    private static readonly StringName ParamWindStrength        = "wind_strength";
    private static readonly StringName ParamPlayerPos           = "player_position";
    private static readonly StringName ParamPlayerSpeed         = "player_speed";
    private static readonly StringName ParamPlayerOnFloor       = "player_on_floor";
    private static readonly StringName ParamLastFloorPos        = "last_floor_position";
    private static readonly StringName ParamLastFloorTime       = "last_floor_time";
    private static readonly StringName ParamGustMode            = "gust_mode";
    private static readonly StringName ParamSpawnProgress       = "spawn_progress";
    private static readonly StringName ParamDespawnProgress     = "despawn_progress";
    private static readonly StringName ParamWindHistory         = "wind_history";
    private static readonly StringName ParamWindHistoryDuration = "wind_history_duration";
    private static readonly StringName ParamGroundHeight        = "ground_height";
    private static readonly StringName ParamDoRecycle           = "do_recycle";
    private static readonly StringName ParamIsSnow              = "is_snow";
    private static readonly StringName ParamVisibilityProgress  = "visibility_progress";
    private static readonly StringName ParamLeafTexture         = "leaf_texture";

    // --- Cached materials (avoid per-frame interop) ---
    private StandardMaterial3D _rainMaterialPass;
    private ShaderMaterial     _rainProcessMaterial;
    private StandardMaterial3D _rainSplashMaterialPass;
    private ShaderMaterial     _rainSplashProcessMaterial;
    private StandardMaterial3D _snowMaterialPass;
    private ShaderMaterial     _snowProcessMaterial;
    private ShaderMaterial     _canopyOverrideMaterial;
    private ShaderMaterial     _canopySurfaceMaterial;

    // --- Retry cooldown for ResolveReferences ---
    private float _resolveRetryTimer = 0f;
    private const float RESOLVE_RETRY_INTERVAL = 2.0f;

    // [SKY3D] Reference to the SkyDome node so we can drive its wind / cloud /
    // fog properties instead of writing raw shader uniforms. SkyDome owns the
    // cloud drift loop and the fog mesh, so routing through it keeps the
    // inspector in sync and avoids fighting the plugin.
    private Node _skyDome;
    private const string SKYDOME_NAME = "SkyDome";

    // [SKY3D] Our weather fog density values range 0..0.15; Sky3D's fog_density
    // property expects values in the ~0.0001..0.01 range. This scales between them.
    private const float FOG_DENSITY_SCALE = 0.01f;

    // --- Other ---
    [Export] private bool _constantWind = true; // 1 speed per weather state; no gust scheduler
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
    private WeatherPreset _activePreset = new WeatherPreset();

    // Weather blend values
    private float _currentRainVal = 0.0f;
    private float _currentSnowVal = 0.0f;
    private float _currentIceVal  = 0.0f;
    private float _currentGreySkyTint = 0.0f;
    // [SKY3D] _currentFogBaseColor is retained as dead state — the fog color is
    // now derived from SkyDome's atm_day_tint / atm_horizon_light_tint, which
    // SkyDome manages. If you later want to tint fog toward a specific color,
    // we can blend it into those SkyDome properties the same way we route
    // fog_density below.
    private Color _currentFogBaseColor = new Color(0.8f, 0.8f, 0.8f);
    private float _currentCloudCoverage = 0.3f;
    // [SKY3D] _currentCloudSoftness / _currentCloudScale retained for compatibility
    // but no longer written anywhere — Sky3D exposes cumulus_noise_freq / cumulus_size
    // via SkyDome inspector properties if you need to tune them.
    private float _currentCloudSoftness = 0.5f;
    private float _currentCloudScale    = 0.4f;

    // Wind smoothing
    private Vector3 _targetWindVelocity = Vector3.Zero;
    private Vector3 _currentWindVelocity = Vector3.Zero;
    // [SKY3D] _currentCloudOffset is dead state — SkyDome's own process_tick()
    // integrates cloud positions from wind_speed / wind_direction. Kept for
    // compatibility with any external scripts that might read it.
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

    // Optional: dictionary to track per-particle disable tweens (to fix timer accumulation)
    //private Dictionary<GpuParticles3D, Tween> _disableTweens = new();

    public override void _EnterTree()
    {
        if (!_subscribed && TimeManager.Instance != null)
        {
            SubscribeToTimeManager();
        }
    }

    public override void _Ready()
    {
        Instance = this;
        ResolveReferences();
        CacheMaterials(); // <-- NEW: Cache materials once

        _rng.Randomize();

        // Use cached StringNames for initial shader sets
        RenderingServer.GlobalShaderParameterSet(ParamRainAmount, 0.0f);
        RenderingServer.GlobalShaderParameterSet(ParamSnowAmount, 0.0f);
        RenderingServer.GlobalShaderParameterSet(ParamIceAmount, 0.0f);
        RenderingServer.GlobalShaderParameterSet(ParamWindDirection, Vector3.Zero);
        RenderingServer.GlobalShaderParameterSet(ParamWindStrength, 0.0f);
        RenderingServer.GlobalShaderParameterSet(ParamPlayerOnFloor, 1.0f);
        RenderingServer.GlobalShaderParameterSet(ParamLastFloorPos, Vector3.Zero);
        RenderingServer.GlobalShaderParameterSet(ParamLastFloorTime, 0.0f);
        RenderingServer.GlobalShaderParameterSet(ParamGustMode, 0.0f);

        if (TimeManager.Instance != null)
        {
            _currentSeason = TimeManager.Instance.CurrentSeason;
            _isAutumn = _currentSeason == Season.AUTUMN;
            float targetSpawn = _isAutumn ? 1.0f : 0.0f;
            RenderingServer.GlobalShaderParameterSet(ParamSpawnProgress, targetSpawn);
        }
        else
        {
            RenderingServer.GlobalShaderParameterSet(ParamSpawnProgress, 0.0f);
        }
        RenderingServer.GlobalShaderParameterSet(ParamDespawnProgress, 0.0f);

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

        _targetWindAngle = (float)GD.RandRange(0, Mathf.Tau);
        _currentWindAngle = _targetWindAngle;
        _targetWindVelocity = new Vector3(Mathf.Cos(_targetWindAngle), 0, Mathf.Sin(_targetWindAngle)) * 0.0f;
        _currentWindVelocity = _targetWindVelocity;

        ScheduleNextAngleChange();

        _windHistoryImage = Image.CreateEmpty(_windHistorySize, 1, false, Image.Format.Rgbaf);
        _windHistoryTexture = ImageTexture.CreateFromImage(_windHistoryImage);

        for (int i = 0; i < _windHistorySize; i++)
        {
            _windHistoryImage.SetPixel(i, 0, new Color(0, 0, 0, 0));
        }
        _windHistoryTexture.Update(_windHistoryImage);

        RenderingServer.GlobalShaderParameterSet(ParamWindHistory, _windHistoryTexture);
        RenderingServer.GlobalShaderParameterSet(ParamWindHistoryDuration, _windHistoryDuration);

        ValidatePresetArray();
    }

    // -------------------------------------------------------------------------
    // NEW: Cache materials to avoid per-frame interop
    // -------------------------------------------------------------------------
    private void CacheMaterials()
    {
        if (_rainParticles != null)
        {
            _rainMaterialPass = _rainParticles.DrawPass1?.SurfaceGetMaterial(0) as StandardMaterial3D;
            _rainProcessMaterial = _rainParticles.ProcessMaterial as ShaderMaterial;
        }
        if (_rainSplashParticles != null)
        {
            _rainSplashMaterialPass = _rainSplashParticles.DrawPass1?.SurfaceGetMaterial(0) as StandardMaterial3D;
            _rainSplashProcessMaterial = _rainSplashParticles.ProcessMaterial as ShaderMaterial;
        }
        if (_snowParticles != null)
        {
            _snowMaterialPass = _snowParticles.DrawPass1?.SurfaceGetMaterial(0) as StandardMaterial3D;
            _snowProcessMaterial = _snowParticles.ProcessMaterial as ShaderMaterial;
        }

        // Cache canopy materials
        if (_canopyLeaves != null)
        {
            _canopyOverrideMaterial = _canopyLeaves.MaterialOverride as ShaderMaterial;
            _canopySurfaceMaterial = _canopyLeaves.Multimesh?.Mesh?.SurfaceGetMaterial(0) as ShaderMaterial;
        }
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
        RenderingServer.GlobalShaderParameterSet(ParamSpawnProgress, targetSpawn);

        UpdateSeasonalLeafTexture();
        _subscribed = true;
    }
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!_subscribed && TimeManager.Instance != null)
        {
            SubscribeToTimeManager();
        }
        TryResolveWorldReferences(dt);

        if (SkyMaterial == null && SkyWeaver.Instance == null)
        {
            _resolveRetryTimer -= dt;
            if (_resolveRetryTimer <= 0f)
            {
                ResolveReferences();
                CacheMaterials();
                _resolveRetryTimer = RESOLVE_RETRY_INTERVAL;
            }
            return;
        }

        if (_skyDome == null)
        {
            _resolveRetryTimer -= dt;
            if (_resolveRetryTimer <= 0f)
            {
                ResolveReferences();
                _resolveRetryTimer = RESOLVE_RETRY_INTERVAL;
            }
        }

        _internalTime += dt;
        RenderingServer.GlobalShaderParameterSet(ParamGlobalTime, _internalTime);

        float sunProgress = 0.5f;
        if (TimeManager.Instance != null)
            sunProgress = TimeManager.Instance.GetSeasonalSunProgress();

        float dayFactor = 0.0f;
        if (sunProgress > 0.2f && sunProgress < 0.8f)
        {
            float t = (sunProgress - 0.2f) / 0.6f;
            dayFactor = Mathf.Sin(t * Mathf.Pi);
        }

        if (!_constantWind && !_manualWindOverride && !_isWindFrozenForTransition)
            UpdateWindSchedule(dt);

        if (!_isWindFrozenForTransition)
        {
            _currentWindAngle = Mathf.LerpAngle(_currentWindAngle, _targetWindAngle, dt * _windAngleSmoothSpeed);
            float targetStrength = _targetWindVelocity.Length();
            Vector3 windVec = new Vector3(Mathf.Cos(_currentWindAngle), 0, Mathf.Sin(_currentWindAngle)) * targetStrength;
            _targetWindVelocity = windVec;
        }

        _currentWindVelocity = _currentWindVelocity.Lerp(_targetWindVelocity, dt * _windSmoothSpeed);
        RenderingServer.GlobalShaderParameterSet(ParamWindDirection, _currentWindVelocity);
        RenderingServer.GlobalShaderParameterSet(ParamWindStrength, _currentWindVelocity.Length());

        if (_isWindFrozenForTransition)
        {
            _currentWindVelocity = _frozenWindDir;
            _targetWindVelocity = _frozenWindDir;
            RenderingServer.GlobalShaderParameterSet(ParamWindDirection, _currentWindVelocity);
            RenderingServer.GlobalShaderParameterSet(ParamWindStrength, _currentWindVelocity.Length());
        }

        if (!_constantWind && !_manualWindOverride && !_isWindFrozenForTransition)
        {
            _nextAngleChangeTimer -= dt;
            while (_nextAngleChangeTimer <= 0)
            {
                UpdateTargetWindAngle();
                ScheduleNextAngleChange();
            }
        }

        _windHistoryTimer += dt;
        while (_windHistoryTimer >= _windHistoryInterval)
        {
            _windHistoryTimer -= _windHistoryInterval;
            WriteWindSample();
        }

        UpdateCanopyLeavesState();

        float targetGust = (CurrentState == WeatherState.Rainstorm || _currentWindVelocity.Length() > 10.0f) ? 1.0f : 0.0f;
        _currentGustMode = Mathf.Lerp(_currentGustMode, targetGust, dt * 2.0f);
        RenderingServer.GlobalShaderParameterSet(ParamGustMode, _currentGustMode);

        if (_activePreset.LightningRate > 0f)
        {
            _lightningTimer -= dt;
            if (_lightningTimer <= 0.0f)
            {
                TriggerLightning();
                _lightningTimer = (float)GD.RandRange(_lightningIntervalMin, _lightningIntervalMax);
            }
        }

        // [SKY3D] Lightning writes to the shader's lightning_strength uniform.
        // Sky3D does NOT touch this uniform, so there is no conflict.
        // (If you're using the stock Sky3D shader, this uniform doesn't exist
        // and the write is a harmless no-op. It only works on the merged shader.)
        if (_skyFlashIntensity > 0.0f)
        {
            _skyFlashIntensity -= dt * 5.0f;
            if (_skyFlashIntensity < 0.0f) _skyFlashIntensity = 0.0f;
            if (SkyWeaver.Instance != null)
                SkyWeaver.Instance.SetLightning(_skyFlashIntensity);
        }

        float brightness = Mathf.Lerp(0.1f, 1.0f, dayFactor);

        if (IsInstanceValid(_player))
        {
            float groundY = _player.GlobalPosition.Y;
            RenderingServer.GlobalShaderParameterSet(ParamPlayerPos, _player.GlobalPosition);

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

            RenderingServer.GlobalShaderParameterSet(ParamPlayerSpeed, speed);
            RenderingServer.GlobalShaderParameterSet(ParamPlayerOnFloor, onFloor ? 1.0f : 0.0f);

            // Optimized helpers using cached materials
            SetShaderParameter(_rainParticles, _rainProcessMaterial, ParamGroundHeight, groundY);
            SetShaderParameter(_rainSplashParticles, _rainSplashProcessMaterial, ParamGroundHeight, groundY);
            SetShaderParameter(_snowParticles, _snowProcessMaterial, ParamGroundHeight, groundY);

            if (IsInstanceValid(_rainParticles))
            {
                float rainBright = Mathf.Max(brightness, 0.2f);
                SetMaterialAlbedo(_rainParticles, _rainMaterialPass, new Color(rainBright, rainBright, rainBright, 1.0f));
            }
            if (IsInstanceValid(_rainSplashParticles))
                SetMaterialAlbedo(_rainSplashParticles, _rainSplashMaterialPass, new Color(brightness, brightness, brightness, 1.0f));
            if (IsInstanceValid(_snowParticles))
                SetMaterialAlbedo(_snowParticles, _snowMaterialPass, new Color(brightness, brightness, brightness, 1.0f));

            if (cb != null)
            {
                bool currentOnFloor = cb.IsOnFloor();
                if (!currentOnFloor && _lastPlayerOnFloor)
                {
                    _lastFloorPos = cb.GlobalPosition;
                    RenderingServer.GlobalShaderParameterSet(ParamLastFloorPos, _lastFloorPos);
                    RenderingServer.GlobalShaderParameterSet(ParamLastFloorTime, _internalTime);
                }
                _lastPlayerOnFloor = currentOnFloor;
            }
        }

        // --- Feed SkyWeaver — single call pulls every value from the active
        // preset. SkyWeaver applies each field per its own sentinel rules:
        // fields the preset left at -1 / alpha<0 inherit the SkyWeaver
        // inspector values, so a fresh preset == the editor look.
        // Rain/snow particle emission and ground shader globals are handled
        // separately (above) — the sky only cares about sky and fog fields.
        if (SkyWeaver.Instance != null && TimeManager.Instance != null)
        {
            SkyWeaver.Instance.TimeOfDay   = TimeManager.Instance.Hour + TimeManager.Instance.Minute / 60f;
            SkyWeaver.Instance.SeasonIndex = (int)_currentSeason;
            SkyWeaver.Instance.SetPaletteTime(sunProgress);
            SkyWeaver.Instance.SetWind(_currentWindVelocity);

            SkyWeaver.Instance.FeedWeather(_activePreset);
        }

        if (IsInstanceValid(_skyDome))
        {
            float speed = _currentWindVelocity.Length();
            // Sky3D's wind_direction is where the wind COMES FROM (0 = north).
            // Our _currentWindVelocity points where the wind BLOWS TOWARD.
            float towardAngle = Mathf.Atan2(_currentWindVelocity.Z, _currentWindVelocity.X);
            float sky3dWindDir = towardAngle - Mathf.Pi * 0.5f;

            _skyDome.Set("wind_speed", speed);
            _skyDome.Set("wind_direction", sky3dWindDir);
        }
    }

    private void SetShaderParameter(GpuParticles3D particles, ShaderMaterial cachedMat, StringName param, Variant val)
    {
        if (cachedMat != null)
            cachedMat.SetShaderParameter(param, val);
    }

    private void SetMaterialAlbedo(GpuParticles3D particles, StandardMaterial3D cachedMat, Color color)
    {
        if (cachedMat != null)
            cachedMat.AlbedoColor = color;
    }

    private void SetSkyDomeProperty(string name, Variant value)
    {
        if (IsInstanceValid(_skyDome))
            _skyDome.Set(name, value);
        else if (IsInstanceValid(SkyMaterial))
            SkyMaterial.SetShaderParameter(name, value);
    }

    // --- Determine target wind angle based on season, time of day, randomness ---
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
            bool isStorm = CurrentState == WeatherState.Rainstorm;
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
        // use _targetWindAngle for consistency; the smoothing will handle it.
        Vector3 windVec = new Vector3(Mathf.Cos(_targetWindAngle), 0, Mathf.Sin(_targetWindAngle)) * strength;
        _targetWindVelocity = windVec;
    }

    // Write a sample to the wind history texture ---
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
        bool shouldShow = CurrentState == WeatherState.Rainstorm || _isInLeafyArea || windStrength > 0.5f;

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
        SetMultiMeshShaderParam(ParamVisibilityProgress, v);
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
            //GD.Print("WeatherManager: Ignoring season change because transition in progress");
            return;
        }

        //GD.Print($"WeatherManager.OnDayChanged: newSeason={seasonVal}, currentSeason={_currentSeason}");

        if (newSeason == Season.AUTUMN && _currentSeason == Season.SUMMER)
        {
            //GD.Print("  --> Summer -> Autumn: AnimateLeavesIn()");
            _isAutumn = true;
            // Reset autumn cycle counters so the pattern repeats every year
            _autumnCycleCount = 0;
            _inAutumnLongBreeze = false;
            _windPhase = WindPhase.Calm;
            AnimateLeavesIn();
        }
        else if (newSeason == Season.WINTER && _currentSeason == Season.AUTUMN)
        {
            //GD.Print("  --> Autumn -> Winter: AnimateLeavesOut()");
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
        }
        else if (seasonVal == (int)Season.AUTUMN)
        {
            if (roll > 0.5f) next = WeatherState.Rain;
            else if (roll > 0.4f) next = WeatherState.Rainstorm;
        }
        else if (seasonVal == (int)Season.SPRING)
        {
            if (roll > 0.7f) next = WeatherState.Rain;
            else if (roll > 0.4f) next = WeatherState.Rainstorm;
        }
        else if (seasonVal == (int)Season.SUMMER)
        {
            if (roll > 0.7f) next = WeatherState.Rain;
            else if (roll > 0.4f) next = WeatherState.Rainstorm;
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
            Callable.From<float>(v => RenderingServer.GlobalShaderParameterSet(ParamSpawnProgress, v)),
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
            Callable.From<float>(v => RenderingServer.GlobalShaderParameterSet(ParamDespawnProgress, v)),
            0.0f, 1.0f, 5.0f
        );
        _currentTransitionTween.TweenCallback(Callable.From(() =>
        {
            RenderingServer.GlobalShaderParameterSet(ParamSpawnProgress, 0.0f);
            RenderingServer.GlobalShaderParameterSet(ParamDespawnProgress, 0.0f);
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
        var p = (_weatherPresets != null && (int)newState < _weatherPresets.Length && _weatherPresets[(int)newState] != null)
            ? _weatherPresets[(int)newState] : new WeatherPreset();
        _activePreset = p;

        GD.Print($"[Weather] {newState} | rain={p.RainAmount} snow={p.SnowAmount} ice={p.IceAmount} " +
                $"cov={p.CloudCoverage} dark={p.StormDarkness} fogD={p.FogDensity} fogF={p.FogFalloff} " +
                $"wind={p.WindSpeed} auto={p.AutoWind}");

        // --- particles: independent booleans, no > 0.1 threshold ---
        if (IsInstanceValid(_rainParticles))
        {
            SetParticlesActive(_rainParticles, p.RainParticles && p.RainAmount > 0f);
            SetShaderParameter(_rainParticles, _rainProcessMaterial, ParamDoRecycle, p.RainParticles ? 1f : 0f);
        }
        if (IsInstanceValid(_rainSplashParticles))
            SetParticlesActive(_rainSplashParticles, p.SplashParticles && p.RainAmount > 0f);
        if (IsInstanceValid(_snowParticles))
        {
            SetParticlesActive(_snowParticles, p.SnowParticles && p.SnowAmount > 0f);
            SetShaderParameter(_snowParticles, _snowProcessMaterial, ParamIsSnow, 1f);
            SetShaderParameter(_snowParticles, _snowProcessMaterial, ParamDoRecycle, p.SnowParticles ? 1f : 0f);
        }

        // --- wind: only touch the scheduler if the preset wants it off ---
        if (!p.AutoWind)
        {
            _manualWindOverride = true;
            float rad = Mathf.DegToRad(p.WindDirectionDeg);
            _targetWindVelocity = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * p.WindSpeed;
        }
        else
        {
            _manualWindOverride = false;
            _targetWindVelocity = new Vector3(Mathf.Cos(_targetWindAngle), 0, Mathf.Sin(_targetWindAngle)) * p.WindSpeed;
        }

        // --- shader globals + SkyWeaver: tween or snap ---
        if (_activeTween != null && _activeTween.IsValid()) _activeTween.Kill();

        if (immediate)
        {
            ApplyPrecipitationInstant(p);
        }
        else
        {
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);
            _activeTween.TweenMethod(Callable.From<float>(v => { _currentRainVal = v; RenderingServer.GlobalShaderParameterSet(ParamRainAmount, v); }), _currentRainVal, p.RainAmount, 3.0f);
            _activeTween.TweenMethod(Callable.From<float>(v => { _currentSnowVal = v; RenderingServer.GlobalShaderParameterSet(ParamSnowAmount, v); }), _currentSnowVal, p.SnowAmount, 3.0f);
            _activeTween.TweenMethod(Callable.From<float>(v => { _currentIceVal  = v; RenderingServer.GlobalShaderParameterSet(ParamIceAmount,  v); }), _currentIceVal,  p.IceAmount,  3.0f);
        }

        // Everything else is pushed every frame from _Process via the feed.
        UpdateCanopyLeavesState();
    }

    private void ApplyPrecipitationInstant(WeatherPreset p)
    {
        _currentRainVal = p.RainAmount;  RenderingServer.GlobalShaderParameterSet(ParamRainAmount, _currentRainVal);
        _currentSnowVal = p.SnowAmount;  RenderingServer.GlobalShaderParameterSet(ParamSnowAmount, _currentSnowVal);
        _currentIceVal  = p.IceAmount;   RenderingServer.GlobalShaderParameterSet(ParamIceAmount,  _currentIceVal);
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

    private void SetMultiMeshShaderParam(StringName param, Variant val)
    {
        if (_canopyOverrideMaterial != null)
            _canopyOverrideMaterial.SetShaderParameter(param, val);
        else if (_canopySurfaceMaterial != null)
            _canopySurfaceMaterial.SetShaderParameter(param, val);
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

        // [SKY3D] Locate the SkyDome node created by Sky3D. It's a direct child
        // of the Sky3D node. Adjust SKYDOME_NAME if you renamed it.
        if (_skyDome == null)
            _skyDome = GetTree().Root.FindChild(SKYDOME_NAME, true, false);
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
            Timer timer = new Timer();
            timer.OneShot = true;
            timer.ProcessMode = ProcessModeEnum.Always; // Not affected by TimeScale
            timer.WaitTime = soundDelay;
            timer.Timeout += () =>
            {
                if (IsInstanceValid(_thunderPlayer))
                    _thunderPlayer.Play();
                timer.QueueFree();
            };
            AddChild(timer);
            timer.Start();
        }
    }

    private void TryResolveWorldReferences(float dt)
    {
        if (_worldResolved) return;
        _worldResolveTimer -= dt;
        if (_worldResolveTimer > 0f) return;
        _worldResolveTimer = 1.0f;   // retry once per second until it works

        ResolveReferences();
        CacheMaterials();

        _worldResolved = _rainParticles != null
                    && _rainSplashParticles != null
                    && _snowParticles != null
                    && _player != null;

        if (_worldResolved && !_weatherReapplied)
        {
            _weatherReapplied = true;
            GD.Print($"[Weather] World resolved — re-applying {CurrentState}");
            ChangeWeather(CurrentState, immediate: true);
        }
    }

    private void ValidatePresetArray()
    {
        string[] expected = { "Clear", "Rain", "Snow", "Rainstorm" };
        if (_weatherPresets == null)
        {
            GD.PushError("WeatherManager: _weatherPresets is NULL — assign it in the inspector!");
            return;
        }
        if (_weatherPresets.Length != expected.Length)
            GD.PushWarning($"WeatherManager: {_weatherPresets.Length} presets set, expected {expected.Length}");

        for (int i = 0; i < _weatherPresets.Length; i++)
        {
            string label = i < expected.Length ? expected[i] : $"Slot{i}";
            var p = _weatherPresets[i];
            GD.Print($"  preset[{i}] {label,-11} = " +
                    (p == null ? "NULL !!!" : System.IO.Path.GetFileName(p.ResourcePath)));
        }
    }
}