using Godot;
using System;

public partial class MobileButton : Button
{
    [Export] public string ActionName = "";
    [Export] public bool TapOnce = true;
    public event Action Tapped;

    private int _activeTouchIndex = -1;
    private bool _actionPressed = false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(140, 110);
        AddThemeFontSizeOverride("font_size", 30);
    }

    public override void _GuiInput(InputEvent @event)
    {
        // Handle touch
        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed && _activeTouchIndex == -1)
            {
                _activeTouchIndex = touch.Index;
                OnPressDown();
                AcceptEvent();
            }
            else if (!touch.Pressed && touch.Index == _activeTouchIndex)
            {
                _activeTouchIndex = -1;
                OnPressUp(insideButton: GetGlobalRect().HasPoint(GlobalPosition + touch.Position));
                AcceptEvent();
            }
            return;
        }

        // Handle mouse (editor/desktop testing)
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                OnPressDown();
                AcceptEvent();
            }
            else
            {
                OnPressUp(insideButton: true);
                AcceptEvent();
            }
        }
    }

    private void OnPressDown()
    {
        if (!TapOnce && !string.IsNullOrEmpty(ActionName))
        {
            Input.ActionPress(ActionName);
            _actionPressed = true;
        }
    }

    private void OnPressUp(bool insideButton)
    {
        // Diagnostics – show actual rect and touch position
        var globalRect = GetGlobalRect();
        GD.Print($"[{Text}] Rect: {globalRect}  Size: {Size}  GlobalPos: {GlobalPosition}  TouchPos: {GetGlobalMousePosition()}  inside: {insideButton}");

        if (!TapOnce)
        {
            if (_actionPressed && !string.IsNullOrEmpty(ActionName))
            {
                Input.ActionRelease(ActionName);
                _actionPressed = false;
            }
            return;
        }

        if (!insideButton) return;

        Tapped?.Invoke();

        if (!string.IsNullOrEmpty(ActionName))
        {
            Input.ActionPress(ActionName);
            CallDeferred(nameof(ReleaseActionDeferred));
        }
    }

    private void ReleaseActionDeferred()
    {
        if (!string.IsNullOrEmpty(ActionName))
            Input.ActionRelease(ActionName);
    }

    public override void _ExitTree()
    {
        // Safety: release any held action
        if (_actionPressed && !string.IsNullOrEmpty(ActionName))
            Input.ActionRelease(ActionName);
    }
}