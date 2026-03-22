using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class HUD : Control
{
    // ========== Main HUD ==========
    [ExportCategory("Main HUD")]
    [Export] private Label _timeLabel;
    [Export] private ProgressBar _sanityBar;

    // ========== Menus ==========
    [ExportCategory("Menus")]
    [Export] private Control _pauseMenu;
    [Export] private Control _debugContainer;
    [Export] private VBoxContainer _debugList;

    // ========== Interaction Prompt ==========
    [ExportCategory("Tooltip")]
    [Export] private Panel _tooltipPanel;      // will auto‑create if null
    [Export] private Label _tooltipKeyLabel;   // auto‑created
    [Export] private Label _tooltipTextLabel;  // auto‑created

    // ========== Crosshair ==========
    private ColorRect _crosshair;

    // ========== Inventory UI ==========
    private Control _inventoryPanel;
    private Control _hotbarPanel;
    private Control _creativePanel;
    private Label _inventoryTooltipLabel;   // separate from interaction tooltip
    private ColorRect _analysisPanel;

    // ========== References ==========
    private TimeManager _timeManager;
    private WeatherManager _weatherManager;
    private Tween _tooltipTween;

    // ========== Particle & Debug Data ==========
    private GpuParticles3D _rainNode;
    private GpuParticles3D _splashNode;
    private GpuParticles3D _snowNode;

    private Dictionary<string, float> _trackedValues = new Dictionary<string, float>() {
        { "rain_amount", 0f }, { "snow_amount", 0f }, { "ice_amount", 0f },
        { "wind_speed", 0f },  { "wind_angle", 0f }
    };

    // ========== Debug Menu ==========
    private List<DebugItem> _menuItems = new List<DebugItem>();
    private List<Label> _labelPool = new List<Label>();
    private int _selectedIdx = 0;
    private bool _isDebugOpen = false;
    private bool _isPaused = false;

    // ========== Debug Item Classes ==========
    private abstract class DebugItem { public string Name; public Color Color = Colors.White; }
    private class HeaderItem : DebugItem {
        public HeaderItem(string title) { Name = $"--- {title} ---"; Color = Colors.Gold; }
    }
    private class ActionItem : DebugItem {
        public Action OnExecute;
        public Action<int> OnAdjust;
        public Func<string> GetDisplayValue;
        public ActionItem() { Color = Colors.LightGreen; }
    }

    // ========== Lifecycle ==========
    public override void _Ready()
    {
        _timeManager = TimeManager.Instance;
        _weatherManager = GetNodeOrNull<WeatherManager>("/root/WeatherManager");

        // Find particles
        _rainNode = GetTree().Root.FindChild("RainParticles", true, false) as GpuParticles3D;
        _splashNode = GetTree().Root.FindChild("RainSplashParticles", true, false) as GpuParticles3D;
        _snowNode = GetTree().Root.FindChild("SnowParticles", true, false) as GpuParticles3D;

        if (_debugContainer != null) _debugContainer.Visible = false;
        if (_pauseMenu != null) _pauseMenu.Visible = false;

        ProcessMode = ProcessModeEnum.Always;
        SetupShowcaseMenu();

        // Build all UI elements
        BuildInteractionTooltip();   // creates the prompt panel
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
        BuildCrosshair();            // creates the center dot
        BuildInventoryUI();          // creates the inventory panels

        // Initially hide interaction prompt
        HideTooltip();
    }

    // ========== Interaction Prompt ==========
    private void BuildInteractionTooltip()
    {
        if (_tooltipPanel == null)
        {
            _tooltipPanel = new Panel();
            _tooltipPanel.Name = "InteractionTooltip";
            _tooltipPanel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_tooltipPanel);
        }
        if (_tooltipKeyLabel == null)
        {
            _tooltipKeyLabel = new Label();
            _tooltipKeyLabel.Name = "KeyLabel";
            _tooltipKeyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _tooltipKeyLabel.VerticalAlignment = VerticalAlignment.Center;
            _tooltipKeyLabel.MouseFilter = MouseFilterEnum.Ignore;
        }
        if (_tooltipTextLabel == null)
        {
            _tooltipTextLabel = new Label();
            _tooltipTextLabel.Name = "TextLabel";
            _tooltipTextLabel.HorizontalAlignment = HorizontalAlignment.Left;
            _tooltipTextLabel.VerticalAlignment = VerticalAlignment.Center;
            _tooltipTextLabel.MouseFilter = MouseFilterEnum.Ignore;
        }

        StyleInteractionTooltip();

        //_tooltipPanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _tooltipPanel.Position = new Vector2(0, -100);
        _tooltipPanel.Hide();
    }

    private void StyleInteractionTooltip()
    {
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color("#14100f") { A = 0.95f };
        panelStyle.BorderColor = new Color("#4a3030");
        panelStyle.SetBorderWidthAll(2);
        panelStyle.SetCornerRadiusAll(0);
        panelStyle.ContentMarginLeft = 10;
        panelStyle.ContentMarginRight = 10;
        panelStyle.ContentMarginTop = 5;
        panelStyle.ContentMarginBottom = 5;
        _tooltipPanel.AddThemeStyleboxOverride("panel", panelStyle);

        // Clear existing children
        foreach (Node child in _tooltipPanel.GetChildren())
            _tooltipPanel.RemoveChild(child);

        var hbox = new HBoxContainer();
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _tooltipPanel.AddChild(hbox);
        hbox.AddChild(_tooltipKeyLabel);
        hbox.AddChild(_tooltipTextLabel);

        var keyStyle = new StyleBoxFlat();
        keyStyle.BgColor = new Color("#8b0000");
        keyStyle.SetCornerRadiusAll(3);
        _tooltipKeyLabel.AddThemeStyleboxOverride("normal", keyStyle);
        _tooltipKeyLabel.AddThemeColorOverride("font_color", new Color("#e0d5c7"));
        _tooltipKeyLabel.AddThemeFontSizeOverride("font_size", 16);
        _tooltipKeyLabel.AddThemeConstantOverride("outline_size", 1);
        _tooltipKeyLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _tooltipKeyLabel.CustomMinimumSize = new Vector2(40, 30);
        _tooltipKeyLabel.Text = "E";

        _tooltipTextLabel.AddThemeColorOverride("font_color", new Color("#e0d5c7"));
        _tooltipTextLabel.AddThemeFontSizeOverride("font_size", 16);
        _tooltipTextLabel.AddThemeConstantOverride("outline_size", 1);
        _tooltipTextLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _tooltipTextLabel.CustomMinimumSize = new Vector2(200, 30);
    }

    public void ShowTooltip(string text, string key = "E")
    {
        if (_tooltipPanel == null || _tooltipKeyLabel == null || _tooltipTextLabel == null)
            return;

        _tooltipKeyLabel.Text = key;
        _tooltipTextLabel.Text = text;
        _tooltipPanel.Show();

        // DEBUG: Force panel to appear at center with a fixed size
        var viewportSize = GetViewportRect().Size;
        _tooltipPanel.Size = new Vector2(200, 50);
        _tooltipPanel.Position = new Vector2(viewportSize.X / 2 - 100, viewportSize.Y / 2 - 25);
        _tooltipPanel.Modulate = Colors.White;

        // Pulse animation
        if (_tooltipTween != null && _tooltipTween.IsValid())
            _tooltipTween.Kill();

        _tooltipTween = CreateTween();
        _tooltipTween.SetLoops();
        _tooltipTween.TweenProperty(_tooltipPanel, "modulate:a", 0.7f, 0.75f)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
        _tooltipTween.TweenProperty(_tooltipPanel, "modulate:a", 1.0f, 0.75f)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
    }
    public void HideTooltip()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.Hide();
        if (_tooltipTween != null && _tooltipTween.IsValid())
            _tooltipTween.Kill();
    }

    // ========== Crosshair ==========
    private void BuildCrosshair()
    {
        _crosshair = new ColorRect
        {
            CustomMinimumSize = new Vector2(4, 4),
            Color = new Color(1, 1, 1, 0.8f)
        };
        _crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        _crosshair.Position = new Vector2(-2, -2);
        AddChild(_crosshair);
    }

    // ========== Inventory UI ==========
    private void BuildInventoryUI()
    {
        // Hotbar (always visible)
        _hotbarPanel = new HBoxContainer();
        _hotbarPanel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        AddChild(_hotbarPanel);
        for (int i = 0; i < 9; i++)
            _hotbarPanel.AddChild(CreateSlot(null));

        // Main inventory panel (hidden initially)
        _inventoryPanel = new Panel();
        _inventoryPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _inventoryPanel.CustomMinimumSize = new Vector2(800, 600);
        AddChild(_inventoryPanel);
        _inventoryPanel.Visible = false;

        // Inventory grid (20 slots)
        var invGrid = new GridContainer { Columns = 5, Position = new Vector2(20, 20) };
        _inventoryPanel.AddChild(invGrid);
        for (int i = 0; i < 20; i++)
            invGrid.AddChild(CreateSlot(null));

        // Crafting grid (2x2)
        var craftGrid = new GridContainer { Columns = 2, Position = new Vector2(20, 300) };
        _inventoryPanel.AddChild(craftGrid);
        for (int i = 0; i < 4; i++)
            craftGrid.AddChild(CreateSlot(null));

        // Analysis panel (right side)
        _analysisPanel = new ColorRect
        {
            Color = new Color(0.1f, 0.1f, 0.1f, 0.8f),
            Size = new Vector2(250, 400),
            Position = new Vector2(500, 20)
        };
        _inventoryPanel.AddChild(_analysisPanel);

        // Inventory tooltip (for item details)
        _inventoryTooltipLabel = new Label
        {
            Position = new Vector2(20, 500),
            CustomMinimumSize = new Vector2(400, 80),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _inventoryPanel.AddChild(_inventoryTooltipLabel);

        // Creative debug panel (hidden initially)
        _creativePanel = new GridContainer { Columns = 4, Position = new Vector2(100, 100) };
        AddChild(_creativePanel);
        _creativePanel.Visible = false;
        foreach (var item in ItemRegistry.Items.Values)
            _creativePanel.AddChild(CreateSlot(item));
    }

    private Control CreateSlot(ItemData itemData)
    {
        var slot = new ColorRect
        {
            CustomMinimumSize = new Vector2(50, 50),
            Color = itemData != null ? itemData.ThemeColor : new Color(0.2f, 0.2f, 0.2f, 0.5f)
        };

        if (itemData != null)
        {
            var text = new Label
            {
                Text = itemData.Abbreviation,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            text.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            slot.AddChild(text);

            slot.GuiInput += (InputEvent e) =>
            {
                if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    ShowItemDetails(itemData);
            };
        }
        return slot;
    }

    private void ShowItemDetails(ItemData item)
    {
        string props = string.Join(", ", item.Properties);
        _inventoryTooltipLabel.Text = $"Name: {item.Name}\nProperties: [{props}]";

        // Placeholder for diagrams
        if (item.IsSolid)
            GD.Print($"Displaying Crystallography diagram for {item.Name}");
        else
            GD.Print($"Displaying Thermodynamics UI for {item.Name}");
    }

    // ========== Input ==========
    public override void _Input(InputEvent @event)
    {
        // Toggle console (debug menu)
        if (@event.IsActionPressed("toggle_console"))
        {
            ToggleDebug();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Inventory toggle
        if (@event.IsActionPressed("toggle_inventory"))
        {
            if (_inventoryPanel != null)
            {
                _inventoryPanel.Visible = !_inventoryPanel.Visible;
                if (!_inventoryPanel.Visible && _creativePanel != null)
                    _creativePanel.Visible = false;
            }
            // Optionally change mouse mode
            Input.MouseMode = _inventoryPanel.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
            return;
        }

        // Creative debug panel toggle
        if (@event.IsActionPressed("debug_creative"))
        {
            if (_creativePanel != null)
                _creativePanel.Visible = !_creativePanel.Visible;
            GetViewport().SetInputAsHandled();
            return;
        }

        // Debug menu navigation (only when open)
        if (_isDebugOpen)
        {
            if (@event.IsActionPressed("ui_down")) Navigate(1);
            else if (@event.IsActionPressed("ui_up")) Navigate(-1);
            else if (@event.IsActionPressed("ui_accept")) ExecuteCurrent();
            else if (@event.IsActionPressed("ui_left")) AdjustCurrent(-1);
            else if (@event.IsActionPressed("ui_right")) AdjustCurrent(1);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ui_cancel"))
        {
            TogglePause();
        }
    }

    // ========== Debug Menu Implementation (fully copied from your HUD.cs) ==========
    private void SetupShowcaseMenu()
    {
        _menuItems.Clear();

        // --- SECTION 1: WEATHER PRESETS ---
        _menuItems.Add(new HeaderItem("PRESETS (Enter)"));
        AddPreset("CLEAR", WeatherManager.WeatherState.Clear);
        AddPreset("RAIN (Gloomy)", WeatherManager.WeatherState.Rain);
        AddPreset("SUMMER RAIN (Bright)", WeatherManager.WeatherState.SummerRain);
        AddPreset("STORM (Grey Sky)", WeatherManager.WeatherState.Storm);
        AddPreset("SNOW (Standard)", WeatherManager.WeatherState.Snow);
        AddPreset("SUNNY SNOW (Bright)", WeatherManager.WeatherState.SunnySnow);
        AddPreset("ICE", WeatherManager.WeatherState.Ice);
        AddPreset("MIXED", WeatherManager.WeatherState.Mixed);

        // --- SECTION 2: MANUAL PARTICLES ---
        _menuItems.Add(new HeaderItem("PARTICLES (On/Off)"));
        AddToggle("Rain Particles", _rainNode);
        AddToggle("Splash Particles", _splashNode);
        AddToggle("Snow Particles", _snowNode);

        // --- SECTION 3: GROUND & WIND ---
        _menuItems.Add(new HeaderItem("GROUND & WIND (< >)"));
        AddSlider("Puddle Coverage", "rain_amount", 0.05f);
        AddSlider("Snow Coverage", "snow_amount", 0.05f);
        AddSlider("Ice Coverage", "ice_amount", 0.05f);

        _menuItems.Add(new ActionItem {
            Name = "Wind Speed",
            OnAdjust = (dir) => AdjustWind("wind_speed", dir * 1.0f),
            GetDisplayValue = () => _trackedValues["wind_speed"].ToString("0.0")
        });
        _menuItems.Add(new ActionItem {
            Name = "Wind Angle",
            OnAdjust = (dir) => AdjustWind("wind_angle", dir * 15.0f),
            GetDisplayValue = () => _trackedValues["wind_angle"].ToString("0") + "°"
        });
        _menuItems.Add(new ActionItem {
            Name = "Auto Wind",
            OnExecute = () => {
                if (_weatherManager != null) {
                    bool newState = !_weatherManager.IsAutoWindEnabled();
                    _weatherManager.SetAutoWindEnabled(newState);
                }
            },
            GetDisplayValue = () => _weatherManager != null ? (_weatherManager.IsAutoWindEnabled() ? "ON" : "OFF") : "N/A"
        });
        _menuItems.Add(new ActionItem {
            Name = "Fog Density",
            OnAdjust = (dir) => AdjustFogDensity(dir * 0.005f),
            GetDisplayValue = () => GetFogDensity().ToString("0.000")
        });

        // --- SECTION 4: TIME CONTROL ---
        _menuItems.Add(new HeaderItem("TIME CONTROL"));
        _menuItems.Add(new ActionItem {
            Name = "Time Scale",
            OnAdjust = (dir) => {
                if (_timeManager != null) {
                    float next = Mathf.Max(0, _timeManager.TimeScale + (dir * 1.0f));
                    _timeManager.TimeScale = next;
                }
            },
            GetDisplayValue = () => _timeManager?.TimeScale.ToString("0.0") + "x"
        });
        _menuItems.Add(new ActionItem {
            Name = "Set Hour",
            OnAdjust = (dir) => {
                if (_timeManager != null) {
                    int next = _timeManager.Hour + dir;
                    if (next > 23) next = 0; if (next < 0) next = 23;
                    ForceSetTime(next, _timeManager.Minute);
                }
            },
            GetDisplayValue = () => _timeManager?.GetTimeString()
        });
        _menuItems.Add(new ActionItem {
            Name = "Jump to Next Season",
            OnExecute = () => JumpToNextSeason(),
            GetDisplayValue = () => "-->"
        });
    }

    private void AddPreset(string name, WeatherManager.WeatherState state)
    {
        _menuItems.Add(new ActionItem {
            Name = name,
            OnExecute = () => {
                _weatherManager?.ChangeWeather(state);
                _trackedValues["rain_amount"] = 0.0f;
                _trackedValues["snow_amount"] = 0.0f;
                _trackedValues["ice_amount"] = 0.0f;
                _trackedValues["wind_speed"] = 0.0f;

                switch (state)
                {
                    case WeatherManager.WeatherState.Rain:
                    case WeatherManager.WeatherState.SummerRain:
                        _trackedValues["rain_amount"] = 1.0f;
                        break;
                    case WeatherManager.WeatherState.Snow:
                    case WeatherManager.WeatherState.SunnySnow:
                        _trackedValues["snow_amount"] = 1.0f;
                        break;
                    case WeatherManager.WeatherState.Ice:
                        _trackedValues["ice_amount"] = 1.0f;
                        break;
                    case WeatherManager.WeatherState.Storm:
                        _trackedValues["rain_amount"] = 1.0f;
                        _trackedValues["wind_speed"] = 20.0f;
                        break;
                    case WeatherManager.WeatherState.Mixed:
                        _trackedValues["rain_amount"] = 0.5f;
                        _trackedValues["snow_amount"] = 0.5f;
                        _trackedValues["wind_speed"] = 5.0f;
                        break;
                }
            },
            GetDisplayValue = () => _weatherManager?.CurrentState == state ? "ACTIVE" : ""
        });
    }

    private void AddSlider(string name, string key, float step)
    {
        _menuItems.Add(new ActionItem {
            Name = name,
            OnAdjust = (dir) => {
                float current = _trackedValues.ContainsKey(key) ? _trackedValues[key] : 0f;
                float next = Mathf.Clamp(current + (dir * step), 0.0f, 1.0f);
                _trackedValues[key] = next;
                RenderingServer.GlobalShaderParameterSet(key, next);
            },
            GetDisplayValue = () => _trackedValues[key].ToString("0.00")
        });
    }

    private void AddToggle(string name, GpuParticles3D node)
    {
        _menuItems.Add(new ActionItem {
            Name = name,
            OnExecute = () => { if (node != null) node.Emitting = !node.Emitting; },
            GetDisplayValue = () => (node != null && node.Emitting) ? "ON" : "OFF"
        });
    }

    private void AdjustWind(string key, float change)
    {
        float val = _trackedValues[key] + change;
        if (key == "wind_angle") {
            if (val >= 360) val -= 360;
            if (val < 0) val += 360;
        }
        else {
            val = Mathf.Max(0, val);
        }
        _trackedValues[key] = val;

        float angleRad = Mathf.DegToRad(_trackedValues["wind_angle"]);
        float speed = _trackedValues["wind_speed"];
        Vector3 windVec = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)) * speed;

        if (_weatherManager != null)
            _weatherManager.SetManualWind(windVec);
    }

    private void JumpToNextSeason()
    {
        if (_timeManager == null) return;
        var startSeason = _timeManager.CurrentSeason;
        int safetyLimit = 0;
        while (_timeManager.CurrentSeason == startSeason && safetyLimit < 100)
        {
            var method = typeof(TimeManager).GetMethod("AdvanceDay", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(_timeManager, null);
            safetyLimit++;
        }
    }

    private void ForceSetTime(int h, int m)
    {
        if (_timeManager == null) return;
        var type = typeof(TimeManager);
        type.GetProperty("Hour")?.SetValue(_timeManager, h);
        type.GetProperty("Minute")?.SetValue(_timeManager, m);
        var updateMethod = type.GetMethod("UpdatePeriod", BindingFlags.NonPublic | BindingFlags.Instance);
        updateMethod?.Invoke(_timeManager, null);
        _timeManager.EmitSignal(TimeManager.SignalName.ClockTick, h, m);
        _timeManager.EmitSignal(TimeManager.SignalName.TimeUpdated, (h + (m / 60.0f)) / 24.0f);
    }

    private void AdjustFogDensity(float change)
    {
        var env = GetEnvironment();
        if (env != null && env.VolumetricFogEnabled)
            env.VolumetricFogDensity = Mathf.Clamp(env.VolumetricFogDensity + change, 0.0f, 0.5f);
    }

    private float GetFogDensity()
    {
        var env = GetEnvironment();
        return env != null ? env.VolumetricFogDensity : 0.0f;
    }

    private Godot.Environment GetEnvironment()
    {
        var node = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
        return node?.Environment;
    }

    // ========== Debug Menu Navigation ==========
    private void Navigate(int dir)
    {
        _selectedIdx += dir;
        if (_selectedIdx < 0) _selectedIdx = _menuItems.Count - 1;
        if (_selectedIdx >= _menuItems.Count) _selectedIdx = 0;
        if (_menuItems[_selectedIdx] is HeaderItem) Navigate(dir);
        RenderDebugMenu();
    }

    private void ExecuteCurrent()
    {
        if (_menuItems[_selectedIdx] is ActionItem item) item.OnExecute?.Invoke();
        RenderDebugMenu();
    }

    private void AdjustCurrent(int dir)
    {
        if (_menuItems[_selectedIdx] is ActionItem item) item.OnAdjust?.Invoke(dir);
        RenderDebugMenu();
    }

    private void RenderDebugMenu()
    {
        if (_debugList == null) return;
        while (_labelPool.Count < _menuItems.Count)
        {
            Label l = new Label();
            l.LabelSettings = new LabelSettings() { OutlineSize = 4, OutlineColor = Colors.Black };
            _debugList.AddChild(l);
            _labelPool.Add(l);
        }
        for (int i = 0; i < _labelPool.Count; i++)
        {
            var lbl = _labelPool[i];
            if (i >= _menuItems.Count) { lbl.Visible = false; continue; }
            var item = _menuItems[i];
            lbl.Visible = true;
            bool isSelected = (i == _selectedIdx);

            if (item is HeaderItem)
            {
                lbl.Text = item.Name;
                lbl.Modulate = item.Color;
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else if (item is ActionItem action)
            {
                string val = action.GetDisplayValue != null ? $" [{action.GetDisplayValue()}]" : "";
                lbl.Text = $"{(isSelected ? "> " : "  ")}{item.Name}{val}";
                lbl.Modulate = isSelected ? Colors.GreenYellow : Colors.LightGray;
                lbl.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
    }

    private void ToggleDebug()
    {
        _isDebugOpen = !_isDebugOpen;
        if (_debugContainer != null) _debugContainer.Visible = _isDebugOpen;
        if (_isDebugOpen) { GetTree().Paused = true; RenderDebugMenu(); }
        else if (!_isPaused) GetTree().Paused = false;
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        GetTree().Paused = _isPaused;
        if (_pauseMenu != null) _pauseMenu.Visible = _isPaused;
        Input.MouseMode = _isPaused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    // ========== Update ==========
    public override void _Process(double delta)
    {
        if (_timeManager != null && _timeLabel != null)
            _timeLabel.Text = $"{_timeManager.CurrentSeason} | {_timeManager.GetDateString()} | {_timeManager.GetTimeString()}";
    }

    public void ShowTooltipAtWorldPosition(string text, Vector3 worldPos, string key = "E")
    {
        if (_tooltipPanel == null || _tooltipKeyLabel == null || _tooltipTextLabel == null)
            return;

        _tooltipKeyLabel.Text = key;
        _tooltipTextLabel.Text = text;

        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        // Get screen coordinates (pixel coordinates relative to viewport)
        Vector2 screenPos = camera.UnprojectPosition(worldPos);

        // Get the panel's natural size
        Vector2 panelSize = _tooltipPanel.GetCombinedMinimumSize();
        if (panelSize == Vector2.Zero) panelSize = new Vector2(200, 50);
        _tooltipPanel.Size = panelSize; // set it so we can read later

        // Position above the item
        Vector2 offset = new Vector2(0, -panelSize.Y - 10);
        Vector2 finalPos = screenPos + offset;

        // Clamp to screen edges
        var viewportSize = GetViewportRect().Size;
        finalPos.X = Mathf.Clamp(finalPos.X, 0, viewportSize.X - panelSize.X);
        finalPos.Y = Mathf.Clamp(finalPos.Y, 0, viewportSize.Y - panelSize.Y);

        _tooltipPanel.Position = finalPos;
        _tooltipPanel.Show();

        // Pulse animation
        if (_tooltipTween != null && _tooltipTween.IsValid())
            _tooltipTween.Kill();

        _tooltipTween = CreateTween();
        _tooltipTween.SetLoops();
        _tooltipTween.TweenProperty(_tooltipPanel, "modulate:a", 0.7f, 0.75f)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
        _tooltipTween.TweenProperty(_tooltipPanel, "modulate:a", 1.0f, 0.75f)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
    }
}