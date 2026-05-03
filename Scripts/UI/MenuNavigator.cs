using Godot;
using System;
using System.Collections.Generic;

public abstract partial class MenuNavigator : Control
{
    [Signal] public delegate void SelectionChangedEventHandler(int index);
    [Signal] public delegate void ItemActivatedEventHandler(int index);

    protected int _selectedIndex = -1;
    protected List<Control> _items = new();
    protected int _columns = 1;
    protected bool _isActive = false;

    public virtual void Activate()
    {
        _isActive = true;
        if (_items.Count > 0 && _selectedIndex < 0)
            SetSelectedIndex(0);
        UpdateSelectionVisual();
    }

    public virtual void Deactivate()
    {
        _isActive = false;
        UpdateSelectionVisual();
    }

    public virtual void HandleInput(InputEvent @event)
    {
        if (!_isActive) return;

        if (@event.IsActionPressed("ui_left"))  MoveSelection(-1, 0);
        if (@event.IsActionPressed("ui_right")) MoveSelection(1, 0);
        if (@event.IsActionPressed("ui_up"))    MoveSelection(0, -1);
        if (@event.IsActionPressed("ui_down"))  MoveSelection(0, 1);
        if (@event.IsActionPressed("ui_accept")) ActivateCurrent();
        if (@event.IsActionPressed("ui_cancel")) OnCancel();
    }

    protected virtual void MoveSelection(int dx, int dy)
    {
        if (_items.Count == 0) return;
        int total = _items.Count;
        int columns = Math.Max(1, _columns);
        int row = _selectedIndex / columns;
        int col = _selectedIndex % columns;

        // Horizontal wrap within row
        if (dx != 0)
        {
            int rowStart = row * columns;
            int rowEnd = Math.Min(rowStart + columns, total) - 1;
            int rowLength = rowEnd - rowStart + 1;
            col = (col + dx) % rowLength;
            if (col < 0) col += rowLength;
        }

        // Vertical wrap
        if (dy != 0)
        {
            int totalRows = Mathf.CeilToInt((float)total / columns);
            row = (row + dy) % totalRows;
            if (row < 0) row += totalRows;

            // Clamp column if new row is shorter
            int rowStart = row * columns;
            int rowEnd = Math.Min(rowStart + columns, total) - 1;
            int rowLength = rowEnd - rowStart + 1;
            if (col >= rowLength)
                col = rowLength - 1;
        }

        int newIndex = row * columns + col;
        SetSelectedIndex(newIndex);
    }

    protected virtual void SetSelectedIndex(int index)
    {
        if (index == _selectedIndex) return;
        _selectedIndex = index;
        UpdateSelectionVisual();
        EmitSignal(SignalName.SelectionChanged, index);
    }

    protected virtual void ActivateCurrent()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            EmitSignal(SignalName.ItemActivated, _selectedIndex);
    }

    protected virtual void OnCancel()
    {
        Deactivate();
    }

    protected abstract void UpdateSelectionVisual();
}