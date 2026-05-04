using Godot;
using System;

public partial class MobileUIController : CanvasLayer
{
    public static VirtualJoystick Joystick;
    public static System.Collections.Generic.List<Control> ButtonContainers = new();

    private HUD _hud;
    private VirtualJoystick _joystick;
    private Control _menuButtonBar; // right edge: Inv/Health/Debug/Pause
    private Control _actionButtonBar;   // bottom-right: Jump/Interact/Camera

    private MobileButton _invBtn;
    private MobileButton _healthBtn;
    private MobileButton _debugBtn;
    private MobileButton _pauseBtn;

    public override void _Ready()
    {
        if (!DisplayServer.IsTouchscreenAvailable()) { QueueFree(); return; }
        Layer = 128;   // above overlays (25), above game
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = 1000;
        ProjectSettings.SetSetting("input_devices/pointing/emulate_mouse_from_touch", false);
        ProjectSettings.SetSetting("input_devices/pointing/emulate_touch_from_mouse", false);

        BuildJoystick();
        BuildMenuBar();
        BuildActionBar();
        CallDeferred(nameof(WireHUD));
        GetViewport().SizeChanged += Relayout;
        CallDeferred(nameof(Relayout));

        GD.Print($"MobileUI layer: {Layer}");
    }

    private void BuildJoystick()
    {
        _joystick = new VirtualJoystick { Name = "Joystick", OuterSize = 360 };
        Joystick = _joystick;
        AddChild(_joystick);
        _joystick.JoystickMoved += OnJoystickMoved;
    }

    // ----- Menu bar (Inv, Health, Debug, Pause) -----
    private HUD GetHud()
    {
        if (_hud == null || !IsInstanceValid(_hud))
            _hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
        return _hud;
    }

    private void BuildMenuBar()
    {
        _menuButtonBar = new VBoxContainer();
        _menuButtonBar.AddThemeConstantOverride("separation", 12);
        _menuButtonBar.MouseFilter = Control.MouseFilterEnum.Pass;
        AddChild(_menuButtonBar);
        ButtonContainers.Add(_menuButtonBar);

        _invBtn   = CreateMenuButton("Inv",    () => GetHud()?.ToggleInventory());
        _healthBtn= CreateMenuButton("Health", () => GetHud()?.ToggleHealth());
        _debugBtn = CreateMenuButton("Debug",  () => GetHud()?.ToggleDebug());
        _pauseBtn = CreateMenuButton("Pause",  () => GetHud()?.TogglePause());

        // Add them in order
        AddMenuButtonToBar(_invBtn);
        AddMenuButtonToBar(_healthBtn);
        AddMenuButtonToBar(_debugBtn);
        AddMenuButtonToBar(_pauseBtn);

        // Force the VBoxContainer to measure itself and then set its size.
        // 4 buttons × 80 height + 3 × 12 spacing = 356, width 170.
        _menuButtonBar.Size = new Vector2(170, 4 * 80 + 3 * 12);
    }

    // Helper to create a button without adding to bar yet
    private MobileButton CreateMenuButton(string label, Action onTap)
    {
        var btn = new MobileButton { Text = label, TapOnce = true };
        btn.CustomMinimumSize = new Vector2(170, 80);
        btn.SizeFlagsVertical = Control.SizeFlags.Expand;
        btn.Tapped += onTap;
        StyleButton(btn, new Color(0.12f, 0.12f, 0.14f, 0.85f));
        return btn;
    }

    // Helper to add a pre‑created button
    private void AddMenuButtonToBar(MobileButton btn)
    {
        _menuButtonBar.AddChild(btn);
    }

    // ----- Action bar (Jump, Interact, Camera) -----
    private void BuildActionBar()
    {
        // Button dimensions
        float smallBtn = 140;
        float bigBtn   = 200;
        float gap      = 12;

        // Create the container (no anchors – we’ll place it later)
        _actionButtonBar = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        AddChild(_actionButtonBar);

        // Camera (top‑left)
        var camBtn = new MobileButton
        {
            Text = "Camera",
            TapOnce = true,
            CustomMinimumSize = new Vector2(smallBtn, smallBtn),
            Size = new Vector2(smallBtn, smallBtn),
            Position = new Vector2(0, 0)
        };
        camBtn.Tapped += () => GetHud()?.ToggleCamera();
        StyleButton(camBtn, new Color(0.15f, 0.25f, 0.45f, 0.85f));
        _actionButtonBar.AddChild(camBtn);

        // Interact (bottom‑left)
        var interactBtn = new MobileButton
        {
            Text = "Interact",
            ActionName = "interact",
            TapOnce = true,
            CustomMinimumSize = new Vector2(smallBtn, smallBtn),
            Size = new Vector2(smallBtn, smallBtn),
            Position = new Vector2(0, bigBtn + gap)      // 180+10 = 190
        };
        StyleButton(interactBtn, new Color(0.5f, 0.4f, 0.1f, 0.85f));
        _actionButtonBar.AddChild(interactBtn);

        // Jump (right side)
        var jumpBtn = new MobileButton
        {
            Text = "⤒",
            ActionName = "jump",
            TapOnce = false,
            CustomMinimumSize = new Vector2(bigBtn, bigBtn),
            Size = new Vector2(bigBtn, bigBtn),
            Position = new Vector2(smallBtn + gap, 0)    // 120, 0
        };
        StyleButton(jumpBtn, new Color(0.15f, 0.45f, 0.15f, 0.85f));
        _actionButtonBar.AddChild(jumpBtn);

        // Calculate the exact container size
        float containerWidth  = smallBtn + gap + bigBtn;          // 300
        float containerHeight = bigBtn + gap + smallBtn;          // 180+10+110 = 300
        _actionButtonBar.CustomMinimumSize = new Vector2(containerWidth, containerHeight);
        _actionButtonBar.Size = new Vector2(containerWidth, containerHeight);
    }

    private MobileButton CreateActionButton(string text, string action, bool tapOnce, float size, float? height = null)
    {
        float h = height ?? size;
        var btn = new MobileButton
        {
            Text = text,
            ActionName = action ?? "",
            TapOnce = tapOnce,
            CustomMinimumSize = new Vector2(size, h),
            Size = new Vector2(size, h)
        };
        StyleButton(btn, GetButtonColor(text));
        return btn;
    }

    private Color GetButtonColor(string text)
    {
        return text switch
        {
            "Camera" => new Color(0.15f, 0.25f, 0.45f, 0.85f),
            "Interact" => new Color(0.5f, 0.4f, 0.1f, 0.85f),
            "⤒" => new Color(0.15f, 0.45f, 0.15f, 0.85f),
            _ => new Color(0.2f, 0.2f, 0.2f, 0.85f)
        };
    }

    private void StyleButton(Button b, Color bg)
    {
        var normal = new StyleBoxFlat { BgColor = bg };
        normal.SetCornerRadiusAll(14);
        normal.SetContentMarginAll(12);

        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = bg.Lightened(0.25f);   // built‑in Godot 4

        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("hover", pressed);
        b.AddThemeColorOverride("font_color", Colors.White);
    }

    // ----- Layout -----
    private void Relayout()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        float margin = 30;
        float bottomMargin = 70;

        _joystick.Position = new Vector2(70, vp.Y - _joystick.OuterSize - bottomMargin);

        _menuButtonBar.Position = new Vector2(vp.X - _menuButtonBar.Size.X - margin, 50);

        _actionButtonBar.Position = new Vector2(
            vp.X - _actionButtonBar.Size.X - margin,
            vp.Y - _actionButtonBar.Size.Y - bottomMargin
        );
    }

    // ----- HUD wiring -----
    private void WireHUD()
    {
        _hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
        if (_hud != null)
        {
            _hud.MenuStateChanged += UpdateVisibility;
            _hud.MenuStateChanged += () => MobileInput.MovementDirection = Vector2.Zero;
            UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        if (_hud == null) return;

        bool invOpen = _hud.IsInventoryOpen;
        bool healthOpen = _hud.IsHealthPanelOpen;
        bool debugOpen = _hud.IsDebugOpen;
        bool anyMenuOpen = invOpen || healthOpen || debugOpen;
        bool paused = _hud.IsGamePaused && !anyMenuOpen;   // only standalone pause (no other menus)

        // If a menu (inv/health/debug) is active, show ONLY its toggle button
        if (invOpen || healthOpen || debugOpen)
        {
            _invBtn.Visible = invOpen;
            _healthBtn.Visible = healthOpen;
            _debugBtn.Visible = debugOpen;
            _pauseBtn.Visible = false;
        }
        else if (paused)
        {
            // Standalone pause menu (no other menus open) – hide everything
            _invBtn.Visible = false;
            _healthBtn.Visible = false;
            _debugBtn.Visible = false;
            _pauseBtn.Visible = false;
        }
        else
        {
            // Normal gameplay – show all buttons
            _invBtn.Visible = true;
            _healthBtn.Visible = true;
            _debugBtn.Visible = true;
            _pauseBtn.Visible = true;
        }

        // Action bar and joystick are hidden whenever any menu or pause is active
        _actionButtonBar.Visible = !anyMenuOpen && !paused;
        _joystick.Visible        = !anyMenuOpen && !paused;
    }

    private void OnJoystickMoved(Vector2 dir)
    {
        MobileInput.MovementDirection = dir;
    }
    
}