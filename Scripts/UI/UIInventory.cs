using Godot;
using System.Collections.Generic;

public partial class UIInventory : CanvasLayer
{
    private Control _inventoryPanel;
    private Control _hotbarPanel;
    private Control _creativePanel;
    private Label _tooltipLabel;
    private ColorRect _analysisPanel;

    public override void _Ready()
    {
        BuildUI();
        _inventoryPanel.Visible = false;
        _creativePanel.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_inventory"))
        {
            _inventoryPanel.Visible = !_inventoryPanel.Visible;
            if (!_inventoryPanel.Visible) _creativePanel.Visible = false;
        }

        if (@event.IsActionPressed("debug_creative"))
        {
            _creativePanel.Visible = !_creativePanel.Visible;
        }
    }

    private void BuildUI()
    {
        // Hotbar (always visible)
        _hotbarPanel = new HBoxContainer();
        _hotbarPanel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        AddChild(_hotbarPanel);
        for (int i = 0; i < 9; i++)
            _hotbarPanel.AddChild(CreateSlot(null));

        // Main inventory panel
        _inventoryPanel = new Panel();
        _inventoryPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _inventoryPanel.CustomMinimumSize = new Vector2(800, 600);
        AddChild(_inventoryPanel);

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

        // Tooltip
        _tooltipLabel = new Label
        {
            Position = new Vector2(20, 500),
            CustomMinimumSize = new Vector2(400, 80),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _inventoryPanel.AddChild(_tooltipLabel);

        // Creative debug panel
        _creativePanel = new GridContainer { Columns = 4, Position = new Vector2(100, 100) };
        AddChild(_creativePanel);
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
        _tooltipLabel.Text = $"Name: {item.Name}\nProperties: [{props}]";

        // Placeholder for crystallography / thermodynamics diagrams
        if (item.IsSolid)
            GD.Print($"Displaying Crystallography diagram for {item.Name}");
        else
            GD.Print($"Displaying Thermodynamics UI for {item.Name}");
    }
}