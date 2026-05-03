// Scripts/UI/MobileUIController.cs
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
    }

    private void BuildJoystick()
    {
        _joystick = new VirtualJoystick { Name = "Joystick", OuterSize = 360 };
        Joystick = _joystick;
        AddChild(_joystick);
        _joystick.JoystickMoved += OnJoystickMoved;
    }

    // ----- Menu bar (Inv, Health, Debug, Pause) -----
    private void BuildMenuBar()
    {
        _menuButtonBar = new VBoxContainer();
            _menuButtonBar.AddThemeConstantOverride("separation", 12);
            AddChild(_menuButtonBar);
            ButtonContainers.Add(_menuButtonBar);

            AddMenuButton("Inv",    () => { GD.Print("[MOBILE] Tapped Inv");    GetHud()?.ToggleInventory(); });
            AddMenuButton("Health", () => { GD.Print("[MOBILE] Tapped Health"); GetHud()?.ToggleHealth(); });
            AddMenuButton("Debug",  () => { GD.Print("[MOBILE] Tapped Debug");  GetHud()?.ToggleDebug(); });
            AddMenuButton("Pause",  () => { GD.Print("[MOBILE] Tapped Pause");  GetHud()?.TogglePause(); });

            _menuButtonBar.Size = new Vector2(140, 4 * 60 + 3 * 12); // same as before
    }

    private HUD GetHud()
    {
        if (_hud == null || !IsInstanceValid(_hud))
            _hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
        return _hud;
    }

    private void AddMenuButton(string label, Action onTap)
    {
        var btn = new MobileButton { Text = label, TapOnce = true };
        btn.CustomMinimumSize = new Vector2(140, 60);  // was 140x110
        btn.AddThemeFontSizeOverride("font_size", 20); // smaller font
        btn.Tapped += onTap;
        StyleButton(btn, new Color(0.12f, 0.12f, 0.14f, 0.85f));
        _menuButtonBar.AddChild(btn);
    }

    // ----- Action bar (Jump, Interact, Camera) -----
    private void BuildActionBar()
    {
        _actionButtonBar = new Control { CustomMinimumSize = new Vector2(280, 280) };
        _actionButtonBar.Size = new Vector2(280, 280);
        AddChild(_actionButtonBar);

        // Jump (hold)
        var jumpBtn = new MobileButton
        {
            Text = "⤒",
            ActionName = "jump",
            TapOnce = false,
            CustomMinimumSize = new Vector2(180, 180),
            Position = new Vector2(100, 100)
        };
        jumpBtn.Size = new Vector2(180, 180);
        StyleButton(jumpBtn, new Color(0.15f, 0.45f, 0.15f, 0.85f));
        _actionButtonBar.AddChild(jumpBtn);

        // Interact (tap)
        var interactBtn = new MobileButton
        {
            Text = "Interact",
            ActionName = "interact",
            TapOnce = true,
            CustomMinimumSize = new Vector2(110, 110),
            Position = new Vector2(0, 170)
        };
        interactBtn.Size = new Vector2(110, 110);
        StyleButton(interactBtn, new Color(0.5f, 0.4f, 0.1f, 0.85f));
        _actionButtonBar.AddChild(interactBtn);

        // Camera (tap)
        var camBtn = new MobileButton
        {
            Text = "Camera",
            TapOnce = true,
            CustomMinimumSize = new Vector2(110, 110),
            Position = new Vector2(170, 0)
        };
        camBtn.Size = new Vector2(110, 110);
        camBtn.Tapped += () => { GD.Print("[MOBILE] Tapped Camera"); GetHud()?.ToggleCamera(); };
        StyleButton(camBtn, new Color(0.15f, 0.25f, 0.45f, 0.85f));
        _actionButtonBar.AddChild(camBtn);
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
        float margin = 30f;

        // ---------- Joystick (bottom-left) ----------
        _joystick.Position = new Vector2(70, vp.Y - _joystick.OuterSize - 70);

        // ---------- Menu bar (right side, centered vertically) ----------
        Vector2 menuSize = new Vector2(140, 4 * 60 + 3 * 12); // match forced size
        float menuX = vp.X - menuSize.X - margin;
        float menuY = Mathf.Clamp((vp.Y - menuSize.Y) * 0.5f, margin, vp.Y - menuSize.Y - margin);
        _menuButtonBar.Position = new Vector2(menuX, menuY);

        // ---------- Action bar (bottom-right) ----------
        Vector2 actionSize = new Vector2(280, 280);
        _actionButtonBar.Position = new Vector2(
            vp.X - actionSize.X - margin,
            vp.Y - actionSize.Y - margin
        );

        // Diagnostic: print every position so you can see if they’re off-screen
        GD.Print($"[MOBILE] Viewport: {vp.X}x{vp.Y}");
        GD.Print($"[MOBILE] Menu bar   pos: {_menuButtonBar.Position}  size: {menuSize}");
        GD.Print($"[MOBILE] Action bar pos: {_actionButtonBar.Position}  size: {actionSize}");
        GD.Print($"[MOBILE] Joystick   pos: {_joystick.Position}");
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
        bool menuOpen = _hud.IsInventoryOpen || _hud.IsHealthPanelOpen || _hud.IsDebugOpen;
        _actionButtonBar.Visible = !menuOpen;
        _joystick.Visible = !menuOpen;
    }

    private void OnJoystickMoved(Vector2 dir)
    {
        MobileInput.MovementDirection = dir;
    }
}