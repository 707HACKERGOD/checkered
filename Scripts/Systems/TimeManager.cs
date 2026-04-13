using Godot;
using System;

public enum TimePeriod { NIGHT, MORNING, AFTERNOON, EVENING, NIGHT2 }
public enum Season { SPRING, SUMMER, AUTUMN, WINTER }

public partial class TimeManager : Node
{
    public static TimeManager Instance { get; private set; }

    [Signal] public delegate void TimeUpdatedEventHandler(float timeOfDay);
    [Signal] public delegate void DayChangedEventHandler(int totalDays, int month, int calendarDay, int season);
    [Signal] public delegate void ClockTickEventHandler(int hour, int minute);

    [Export] public float DayDuration = 600.0f; // Real seconds per game day
    [Export] public float TimeScale = 1.0f;
    
    public int Month { get; private set; } = 9;  
    public int Day { get; private set; } = 1;    
    public int Year { get; private set; } = 2004;
    public int Hour { get; private set; } = 6;   
    public int Minute { get; private set; } = 0;

    public int CurrentDay { get; private set; } = 1;
    public TimePeriod CurrentPeriod { get; private set; } = TimePeriod.MORNING;
    public Season CurrentSeason => GetSeason(Month);
    
    public bool IsWeekend => DayOfWeek == 6 || DayOfWeek == 7;
    public int DayOfWeek => ((CurrentDay - 1) % 7) + 1; 
    public float TimeOfDay => (Hour + (Minute / 60.0f)) / 24.0f;

    private double _timer = 0.0;
    private double _realSecondsPerGameMinute; 

    public override void _Ready()
    {
        Instance = this;
        _realSecondsPerGameMinute = DayDuration / 1440.0;
        UpdatePeriod();
    }

    public override void _Process(double delta)
    {
        if (Mathf.IsZeroApprox(TimeScale)) return;

        float dt = (float)delta * TimeScale * GameState.Instance.GameSpeed;
        _timer += dt * TimeScale;

        if (_timer >= _realSecondsPerGameMinute)
        {
            while (_timer >= _realSecondsPerGameMinute)
            {
                _timer -= _realSecondsPerGameMinute;
                AdvanceMinute();
            }
        }
    }

    // --- SMOOTH TIME ---
    // Returns continuous time (0.0 to 24.0) for smooth shader interpolation
    public float GetSmoothHour()
    {
        float baseTime = Hour + (Minute / 60.0f);
        float minuteProgress = (float)(_timer / _realSecondsPerGameMinute);
        return baseTime + (minuteProgress / 60.0f);
    }

    // --- DYNAMIC SEASONAL SUN CYCLE (SINE WAVE) ---
    // Returns 0.0 to 1.0 Gradient Position
    // 0.25 is ALWAYS Sunrise. 0.75 is ALWAYS Sunset.
    public float GetSeasonalSunProgress()
    {
        float currentSmoothTime = GetSmoothHour(); // 0-24
        
        // 1. Calculate Day of Year (Approx 1-360)
        int dayOfYear = ((Month - 1) * 30) + Day;
        
        // 2. Normalize to 0-1 (0 = Winter, 1 = Summer)
        // -Cos formula creates a wave that is -1 in Jan/Dec and +1 in June/July
        float seasonWave = -(float)Math.Cos(((dayOfYear - 15) / 360.0f) * Mathf.Tau);

        // 3. Define Extremes (Based on your request)
        // Winter (Mid-Jan): Rise 7:00, Set 18:00
        float winterRise = 7.0f; 
        float winterSet  = 18.0f;
        
        // Summer (Mid-July): Rise 5:00, Set 21:00
        float summerRise = 5.0f; 
        float summerSet  = 21.0f;

        // 4. Interpolate based on the Wave (-1 to 1 mapped to 0 to 1)
        float alpha = (seasonWave + 1.0f) * 0.5f; 
        
        float sunriseHour = Mathf.Lerp(winterRise, summerRise, alpha);
        float sunsetHour = Mathf.Lerp(winterSet, summerSet, alpha);

        // --- Standard Cycle Logic ---
        if (currentSmoothTime >= sunriseHour && currentSmoothTime <= sunsetHour)
        {
            // DAYTIME (0.25 -> 0.75)
            float dayLength = sunsetHour - sunriseHour;
            float progress = (currentSmoothTime - sunriseHour) / dayLength;
            return 0.25f + (progress * 0.5f);
        }
        else
        {
            // NIGHTTIME (0.75 -> 1.25, wrap to 0.25)
            if (currentSmoothTime > sunsetHour)
            {
                // Evening (Sunset -> Midnight)
                float nightDuration = (24.0f - sunsetHour) + sunriseHour;
                float timePassed = currentSmoothTime - sunsetHour;
                return 0.75f + (timePassed / nightDuration) * 0.5f;
            }
            else
            {
                // Morning (Midnight -> Sunrise)
                float nightDuration = (24.0f - sunsetHour) + sunriseHour;
                float timePassed = (24.0f - sunsetHour) + currentSmoothTime;
                float val = 0.75f + (timePassed / nightDuration) * 0.5f;
                return val > 1.0f ? val - 1.0f : val;
            }
        }
    }

    private void AdvanceMinute()
    {
        Minute++;
        if (Minute >= 60)
        {
            Minute = 0;
            Hour++;
            UpdatePeriod();
            if (Hour >= 24)
            {
                Hour = 0;
                AdvanceDay();
            }
        }
        EmitSignal(SignalName.ClockTick, Hour, Minute);
        EmitSignal(SignalName.TimeUpdated, TimeOfDay);
    }

    private void AdvanceDay()
    {
        CurrentDay++;
        Day++;
        if (Day > 30)
        {
            Day = 1;
            Month++;
            if (Month > 12) { Month = 1; Year++; }
        }
        EmitSignal(SignalName.DayChanged, CurrentDay, Month, Day, (int)CurrentSeason);
    }

    private void UpdatePeriod()
    {
        if (Hour < 6) CurrentPeriod = TimePeriod.NIGHT;
        else if (Hour < 12) CurrentPeriod = TimePeriod.MORNING;
        else if (Hour < 17) CurrentPeriod = TimePeriod.AFTERNOON;
        else if (Hour < 21) CurrentPeriod = TimePeriod.EVENING;
        else CurrentPeriod = TimePeriod.NIGHT2;
    }

    public static Season GetSeason(int month)
    {
        if (month >= 3 && month <= 5) return Season.SPRING;
        if (month >= 6 && month <= 8) return Season.SUMMER;
        if (month >= 9 && month <= 11) return Season.AUTUMN;
        return Season.WINTER;
    }

    public int GetTimeID() => Hour * 100 + Minute;
    public string GetDateString() => $"{Month:D2}/{Day:D2}/{Year}";
    public string GetTimeString() => $"{Hour:D2}:{Minute:D2}";
    
    public void SkipToNextMorning()
    {
        AdvanceDay();
        Hour = 6; Minute = 0;
        _timer = 0;
        UpdatePeriod();
        EmitSignal(SignalName.ClockTick, Hour, Minute);
        EmitSignal(SignalName.TimeUpdated, TimeOfDay);
    }
}