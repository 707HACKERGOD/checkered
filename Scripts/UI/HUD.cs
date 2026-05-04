using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class HUD : Control
{
    [Signal] public delegate void MenuStateChangedEventHandler();
    
    // ========== Main HUD ==========
    [ExportCategory("Main HUD")]
    [Export] private Label _timeLabel;
    [Export] private ProgressBar _sanityBar;
    [Export] private CanvasLayer _debugCanvasLayer;   // wraps the debug container
    private ScrollContainer _debugScrollContainer;
    [Export] private CanvasLayer _healthCanvasLayer;  // wraps the health panel
    public bool IsGamePaused => _isPaused || _isDebugOpen;

    public static HUD Instance { get; private set; }
    public bool IsInventoryOpen => _inventoryPanel != null && _inventoryPanel.Visible;

    public bool IsDebugOpen => _isDebugOpen;
    private bool _isPaused = false;
    private TouchScreenButton _mobileResumeButton;
    private TouchScreenButton _mobileDebugButton;
    private TouchScreenButton _mobileHealthButton;
    private TouchScreenButton _mobileInventoryButton;

    private ColorRect _pauseResumeContainer;

    // ========== Menus ==========
    [ExportCategory("Menus")]
    [Export] private CanvasLayer _pauseMenu;
    [Export] private Control _debugControl;
    [Export] private VBoxContainer _debugVBoxContainer;
    [Export] private LimbHealthUI _healthPanel;
    public bool IsHealthPanelOpen => _healthPanel != null && _healthPanel.Visible;

    // ========== Interaction Prompt ==========
    [ExportCategory("Tooltip")]
    [Export] private Panel _tooltipPanel;      
    [Export] private Label _tooltipKeyLabel;   
    [Export] private Label _tooltipTextLabel;  

    // ========== Crosshair ==========
    private ColorRect _crosshair;

    // ========== Inventory UI ==========
    private PackedScene _inventorySlotScene;
    private Control _inventoryPanel;          
    private Panel _inventoryLeftPanel;      
    private Panel _inventoryRightPanel;     
    private GridContainer _inventoryGrid;
    private Label _detailsLabel;
    private Inventory _playerInventory;
    private Control _creativePanel;
    private Label _inventoryTooltipLabel;      
    private ColorRect _analysisPanel;

    // ========== Crystal Diagram ==========
    private CrystalDiagram _crystalDiagram;

    // ========== Layout & Animation ==========
    private enum LayoutState { Default, Expanded }
    private LayoutState _currentLayout = LayoutState.Default;
    private Tween _layoutTween;
    private const float LayoutAnimationDuration = 0.25f;

    // Margins and ratios (relative to viewport)
    private const float MarginHorizontal = 0.05f;
    private const float MarginVertical = 0.08f;
    private const float LeftPanelDefaultWidthRatio = 0.5f;
    private const float PanelHeightRatio = 0.7f;

    // "Remember last clicked item"
    private TextureButton _lockToggle;
    private Texture2D _lockClosedTex;
    private Texture2D _lockOpenTex;
    private bool _rememberSelection = false;
    private int _lastSelectedSlot = -1;

    // ========== References ==========
    private TimeManager _timeManager;
    private WeatherManager _weatherManager;
    private Tween _tooltipTween;

    // ========== Particle & Debug Data ==========
    private GpuParticles3D _rainNode;
    private GpuParticles3D _splashNode;
    private GpuParticles3D _snowNode;

    private Dictionary<string, float> _trackedValues = new Dictionary<string, float>()
    {
        { "rain_amount", 0f }, { "snow_amount", 0f }, { "ice_amount", 0f },
        { "wind_speed", 0f },  { "wind_angle", 0f }
    };

    // ========== Debug Menu ==========
    private List<DebugItem> _menuItems = new List<DebugItem>();
    private List<Label> _labelPool = new List<Label>();
    private int _selectedIdx = 0;
    private bool _isDebugOpen = false;

    private int _selectedSlotIndex = -1;

    private const float GridHeightRatio = 0.8f;      // 80% of left panel height
    private Control _gridArea;

    private const float InventoryTimeScale = 0.2f; // 20% speed
    private Tween _timeScaleTween;

    private bool _lockFocused = false;
    private Panel _lockFocusBorder;

    private List<Tween> _activeTweens = new();

    private abstract class DebugItem { public string Name; public Color Color = Colors.White; }
    private class HeaderItem : DebugItem
    {
        public HeaderItem(string title) { Name = $"--- {title} ---"; Color = Colors.Gold; }
    }
    private class ActionItem : DebugItem
    {
        public Action OnExecute;
        public Action<int> OnAdjust;
        public Func<string> GetDisplayValue;
        public ActionItem() { Color = Colors.LightGreen; }
    }

    [Export] private AudioStream _inventoryOpenSound;
    [Export] private AudioStream _inventoryCloseSound;
    private AudioStreamPlayer _uiAudioPlayer;

    public void OnHealthPanelToggled() => EmitSignal(SignalName.MenuStateChanged);

    // ========== Lifecycle ==========
    public override void _Ready()
    {
        GameState.TimeScaleChanged += OnTimeScaleChanged;
        Instance = this;
        _timeManager = TimeManager.Instance;
        _weatherManager = GetNodeOrNull<WeatherManager>("/root/WeatherManager");

        ProcessMode = ProcessModeEnum.Always;
        SetupShowcaseMenu();

        // Build UI
        BuildInteractionTooltip();
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
        BuildCrosshair();
        HideTooltip();
        BuildInventoryUI();

        // Pause menu (assigned in editor)
        if (_pauseMenu != null)
        {
            _pauseMenu.Layer = 20;
            _pauseMenu.Visible = false;
        }

        // --- Health panel (simple test version) ---
        if (_healthPanel == null)
        {
            _healthPanel = new LimbHealthUI { Visible = false };
            _healthPanel.MouseFilter = MouseFilterEnum.Ignore;  // let mobile buttons stay on top
            _healthPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);

            var hBg = new Panel();
            hBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            hBg.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.4f, 0, 0, 0.95f) });
            _healthPanel.AddChild(hBg);
            _healthPanel.MoveChild(hBg, 0);

            _healthCanvasLayer = new CanvasLayer { Layer = 25 };
            AddChild(_healthCanvasLayer);
            _healthCanvasLayer.AddChild(_healthPanel);
            MoveChild(_healthCanvasLayer, GetChildCount());

            // No tap-to-close on the background; use the menu bar button instead
        }
        else
        {
            _healthPanel.Visible = false;
        }

        // --- Debug container (always exists, starts hidden) ---
        if (_debugControl != null)
            _debugControl.Visible = false;

        // Wrap debug VBoxContainer in a ScrollContainer for scrolling support
        if (_debugVBoxContainer != null)
        {
            // Create ScrollContainer
            _debugScrollContainer = new ScrollContainer();
            _debugScrollContainer.Name = "DebugScroll";
            _debugScrollContainer.FollowFocus = true;               // automatically scroll to focused child
            _debugScrollContainer.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            _debugScrollContainer.SizeFlagsVertical = Control.SizeFlags.Fill;
            _debugScrollContainer.MouseFilter = MouseFilterEnum.Pass; // allow touch drag scrolling on mobile

            // Reparent the VBoxContainer under the ScrollContainer
            _debugVBoxContainer.GetParent().RemoveChild(_debugVBoxContainer);
            _debugScrollContainer.AddChild(_debugVBoxContainer);

            // Add ScrollContainer to the debug control (full‑screen panel)
            _debugControl.AddChild(_debugScrollContainer);

            // Position the ScrollContainer to fill the parent
            _debugScrollContainer.AnchorLeft = 0.0f;
            _debugScrollContainer.AnchorRight = 1.0f;
            _debugScrollContainer.AnchorTop = 0.0f;
            _debugScrollContainer.AnchorBottom = 1.0f;
            _debugScrollContainer.OffsetLeft = 0;
            _debugScrollContainer.OffsetRight = 0;
            _debugScrollContainer.OffsetTop = 0;
            _debugScrollContainer.OffsetBottom = 0;
        }

        if (DisplayServer.IsTouchscreenAvailable() && _debugScrollContainer != null)
        {
            _debugScrollContainer.AnchorLeft = 0.0f;
            _debugScrollContainer.AnchorRight = 0.3f;  // left 30% of screen
            _debugScrollContainer.AnchorTop = 0.0f;
            _debugScrollContainer.AnchorBottom = 1.0f;
            _debugScrollContainer.OffsetLeft = 10;
            _debugScrollContainer.OffsetRight = 0;
            _debugScrollContainer.OffsetTop = 80;
            _debugScrollContainer.OffsetBottom = -20;
        }

        // Player inventory
        var player = GetTree().Root.FindChild("Player", true, false);
        _playerInventory = player?.GetNode<Inventory>("Inventory");
        if (_playerInventory != null)
            _playerInventory.SlotUpdated += OnInventorySlotUpdated;
        PopulateInventorySlots();

        GetViewport().SizeChanged += OnViewportSizeChanged;

        _uiAudioPlayer = new AudioStreamPlayer();
        AddChild(_uiAudioPlayer);

        if (DisplayServer.IsTouchscreenAvailable())
            CreateMobileResumeButton();

        // Input actions
        if (!InputMap.HasAction("toggle_inventory"))
            InputMap.AddAction("toggle_inventory");
        if (!InputMap.HasAction("toggle_health"))
            InputMap.AddAction("toggle_health");
        if (!InputMap.HasAction("toggle_debug"))
            InputMap.AddAction("toggle_debug");
        if (!InputMap.HasAction("interact"))
            InputMap.AddAction("interact");

        // Rain / snow nodes
        _rainNode = GetTree().Root.FindChild("RainParticles", true, false) as GpuParticles3D;
        _splashNode = GetTree().Root.FindChild("RainSplashParticles", true, false) as GpuParticles3D;
        _snowNode = GetTree().Root.FindChild("SnowParticles", true, false) as GpuParticles3D;

        GD.Print($"HUD layers - inventory: 5, debug/health: 25, pause: {_pauseMenu?.Layer}, mobile: 128");
    }

    private void OnViewportSizeChanged()
    {
        if (_inventoryPanel.Visible)
        {
            ApplyLayout(_currentLayout, true);
        }
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

        var viewportSize = GetViewportRect().Size;
        _tooltipPanel.Size = new Vector2(200, 50);
        _tooltipPanel.Position = new Vector2(viewportSize.X / 2 - 100, viewportSize.Y / 2 - 25);
        _tooltipPanel.Modulate = Colors.White;

        if (_tooltipTween != null && _tooltipTween.IsValid())
        {
            _tooltipTween.Kill();
            _activeTweens.Remove(_tooltipTween);
        }

        _tooltipTween = CreateRealTimeTween();
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
            {
                _tooltipTween.Kill();
                _activeTweens.Remove(_tooltipTween);
            }
    }

    // ========== Inventory UI (Dynamic & Responsive) ==========
    private void BuildInventoryUI()
    {
        var inventoryCanvas = new CanvasLayer
        {
            Layer = 5
        };
        AddChild(inventoryCanvas);   // Add canvas to HUD first

        _inventoryPanel = new Panel
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            MouseFilter = MouseFilterEnum.Stop,
            Visible = false
        };
        var bgStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.5f) };
        _inventoryPanel.AddThemeStyleboxOverride("panel", bgStyle);
        
        inventoryCanvas.AddChild(_inventoryPanel);

        // Load custom theme
        var theme = ResourceLoader.Load<Theme>("res://Scenes/UI/CheckeredTheme.tres");

        // --- Left Panel ---
        _inventoryLeftPanel = new Panel();
        _inventoryLeftPanel.Theme = theme;                 
        ApplyInventoryPanelStyle(_inventoryLeftPanel);     
        _inventoryLeftPanel.MouseFilter = MouseFilterEnum.Pass;
        _inventoryPanel.AddChild(_inventoryLeftPanel);

        // --- Lock Toggle (above left panel) ---
        _lockClosedTex = CreateLockIcon(true);
        _lockOpenTex = CreateLockIcon(false);

        _lockToggle = new TextureButton
        {
            TextureNormal = _lockOpenTex,
            TexturePressed = _lockClosedTex,
            TextureHover = _lockOpenTex,
            ToggleMode = true,
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            CustomMinimumSize = new Vector2(40, 40),
            TooltipText = "Remember last selected item"
        };
        _lockToggle.Toggled += (pressed) => _rememberSelection = pressed;
        _inventoryPanel.AddChild(_lockToggle);

        // Create focus border (same size as lock toggle)
        _lockFocusBorder = new Panel
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        var borderStyle = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = Colors.White,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2
        };
        // Make it slightly larger than the button for visibility
        _lockFocusBorder.AddThemeStyleboxOverride("panel", borderStyle);
        _lockFocusBorder.CustomMinimumSize = new Vector2(46, 46);
        _lockFocusBorder.Visible = false;
        _inventoryPanel.AddChild(_lockFocusBorder);

        // Grid Area
        _gridArea = new Control();
        _inventoryLeftPanel.AddChild(_gridArea);

        _inventoryGrid = new GridContainer();
        _inventoryGrid.AddThemeConstantOverride("h_separation", 0);
        _inventoryGrid.AddThemeConstantOverride("v_separation", 0);
        _gridArea.AddChild(_inventoryGrid);

        // Details label
        _detailsLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailsLabel.AddThemeFontSizeOverride("font_size", 25);
        _inventoryLeftPanel.AddChild(_detailsLabel);

        _inventorySlotScene = ResourceLoader.Load<PackedScene>("res://Scenes/UI/InventorySlot.tscn");

        // --- Right Panel ---
        _inventoryRightPanel = new Panel();
        _inventoryRightPanel.Theme = theme;
        ApplyInventoryPanelStyle(_inventoryRightPanel);
        _inventoryRightPanel.MouseFilter = MouseFilterEnum.Pass;
        _inventoryRightPanel.Visible = false;
        _inventoryPanel.AddChild(_inventoryRightPanel);

        _crystalDiagram = new CrystalDiagram
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 10,
            OffsetTop = 10,
            OffsetRight = -10,
            OffsetBottom = -10
        };
        _inventoryRightPanel.AddChild(_crystalDiagram);

        ApplyLayout(LayoutState.Default, true);
    }

    private void ApplyInventoryPanelStyle(Panel panel)
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color("#14100fec");      
        style.BorderColor = new Color("#4a3030");
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(0);
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        style.ContentMarginTop = 5;
        style.ContentMarginBottom = 5;
        panel.AddThemeStyleboxOverride("panel", style);
    }

    private void ApplyLayout(LayoutState state, bool instant = false)
    {
        _currentLayout = state;
        Vector2 viewportSize = GetViewportRect().Size;
        
        float leftWidth = viewportSize.X * LeftPanelDefaultWidthRatio; 
        float panelHeight = viewportSize.Y * PanelHeightRatio;
        float rightWidth = panelHeight;

        float panelY = (viewportSize.Y - panelHeight) * 0.5f;

        float leftX;
        float rightX = 0f;

        if (state == LayoutState.Default)
        {
            leftX = (viewportSize.X - leftWidth) * 0.5f;
            rightX = leftX + leftWidth + 20f; 
        }
        else
        {
            float gap = 20f;
            float combinedWidth = leftWidth + gap + rightWidth;
            
            float blockStartX = (viewportSize.X - combinedWidth) * 0.5f;
            leftX = blockStartX;
            rightX = blockStartX + leftWidth + gap;
        }

        // Lock toggle position
        float toggleHeight = 40f; 
        float toggleGap = 5f;
        Vector2 togglePos = new Vector2(leftX, panelY - toggleHeight - toggleGap);

        // Position border (slightly larger, centered on the button)
        Vector2 borderOffset = new Vector2(-3, -3);
        _lockFocusBorder.Position = togglePos + borderOffset;
        _lockFocusBorder.Size = new Vector2(46, 46);

        if (instant)
        {
            _lockToggle.Position = togglePos;
            SetPanelGeometry(_inventoryLeftPanel, leftX, panelY, leftWidth, panelHeight);
            if (state == LayoutState.Expanded)
            {
                _inventoryRightPanel.Visible = true;
                SetPanelGeometry(_inventoryRightPanel, rightX, panelY, rightWidth, panelHeight);
                _inventoryRightPanel.Modulate = new Color(1, 1, 1, 1);
            }
            else
            {
                _inventoryRightPanel.Visible = false;
                _inventoryRightPanel.Modulate = new Color(1, 1, 1, 0);
            }
            CallDeferred(nameof(UpdateGridLayout));
            return;
        }

        // Animation logic 
        if (_layoutTween != null && _layoutTween.IsValid())
        {
            _layoutTween.Kill();
            _activeTweens.Remove(_layoutTween);
        }
        _layoutTween = CreateRealTimeTween().SetParallel(true).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);

        Vector2 startVec = new Vector2(_inventoryLeftPanel.Position.X, _inventoryLeftPanel.Size.X);
        Vector2 targetVec = new Vector2(leftX, leftWidth);
        
        _layoutTween.TweenMethod(Callable.From<Vector2>(SetLeftPanelRect), startVec, targetVec, LayoutAnimationDuration);
        
        _lockToggle.Position = new Vector2(_lockToggle.Position.X, togglePos.Y);
        _layoutTween.TweenProperty(_lockToggle, "position:x", leftX, LayoutAnimationDuration);
        _layoutTween.TweenProperty(_lockFocusBorder, "position:x", leftX + borderOffset.X, LayoutAnimationDuration);

        if (state == LayoutState.Expanded)
        {
            _inventoryRightPanel.Visible = true;
            SetPanelGeometry(_inventoryRightPanel, _inventoryLeftPanel.Position.X + leftWidth + 20f, panelY, rightWidth, panelHeight);
            
            _layoutTween.TweenMethod(Callable.From<Vector2>(SetRightPanelRect), 
                new Vector2(_inventoryRightPanel.Position.X, rightWidth), 
                new Vector2(rightX, rightWidth), LayoutAnimationDuration);
                
            _layoutTween.TweenProperty(_inventoryRightPanel, "modulate:a", 1.0f, LayoutAnimationDuration);
        }
        else
        {
            float hiddenRightX = leftX + leftWidth + 20f;
            _layoutTween.TweenMethod(Callable.From<Vector2>(SetRightPanelRect), 
                new Vector2(_inventoryRightPanel.Position.X, rightWidth), 
                new Vector2(hiddenRightX, rightWidth), LayoutAnimationDuration);
                
            _layoutTween.TweenProperty(_inventoryRightPanel, "modulate:a", 0.0f, LayoutAnimationDuration);
            _layoutTween.TweenCallback(Callable.From(() => _inventoryRightPanel.Visible = false));
        }
        
        CallDeferred(nameof(UpdateGridLayout)); 
    }

    private void UpdateGridLayout()
    {
        if (_inventoryLeftPanel == null || _inventoryGrid == null || _detailsLabel == null || _gridArea == null)
            return;

        Vector2 panelSize = _inventoryLeftPanel.Size;
        if (panelSize.X <= 0 || panelSize.Y <= 0) return;

        float margin = 20f;
        float splitY = panelSize.Y * GridHeightRatio;

        // Artificial padding added to exactly mirror the lost constraints of the moved CheckButton
        // This ensures the vertical height remains restricted, correctly generating the 7-col layout
        float gridAreaY = margin + 40f;
        float gridAreaHeight = splitY - gridAreaY;
        
        _gridArea.Position = new Vector2(margin, gridAreaY);
        _gridArea.Size = new Vector2(panelSize.X - margin * 2, gridAreaHeight);

        _detailsLabel.Position = new Vector2(margin, splitY);
        _detailsLabel.Size = new Vector2(panelSize.X - margin * 2, panelSize.Y - splitY - margin);

        float availableWidth = _gridArea.Size.X;
        float availableHeight = _gridArea.Size.Y;

        if (availableWidth <= 0 || availableHeight <= 0) return;

        int totalSlots = Inventory.SLOT_COUNT;
        int bestColumns = 1;
        float bestSlotSize = 0f;

        for (int cols = 1; cols <= totalSlots; cols++)
        {
            int rowsRequired = Mathf.CeilToInt((float)totalSlots / cols);
            float slotW = availableWidth / cols;
            float slotH = availableHeight / rowsRequired;
            float slotSize = Mathf.Min(slotW, slotH);
            
            // Prioritize strict sizing, but break math ties by favoring configurations
            // that stretch horizontally to utilize more grid width
            if (slotSize > bestSlotSize + 0.1f)
            {
                bestSlotSize = slotSize;
                bestColumns = cols;
            }
            else if (Mathf.Abs(slotSize - bestSlotSize) <= 0.1f && cols > bestColumns)
            {
                bestSlotSize = slotSize;
                bestColumns = cols;
            }
        }

        // Applied only AFTER the best fit is found to prevent floating point wrap-around snapping
        bestSlotSize = Mathf.Floor(bestSlotSize);
        _inventoryGrid.Columns = bestColumns;

        foreach (Control slot in _inventoryGrid.GetChildren())
        {
            slot.CustomMinimumSize = new Vector2(bestSlotSize, bestSlotSize);
            slot.Size = new Vector2(bestSlotSize, bestSlotSize);
        }

        float gridWidth = bestColumns * bestSlotSize;
        int rows = Mathf.CeilToInt((float)totalSlots / bestColumns);
        float gridHeight = rows * bestSlotSize;

        _inventoryGrid.CustomMinimumSize = new Vector2(gridWidth, gridHeight);
        _inventoryGrid.Size = new Vector2(gridWidth, gridHeight);
        
        _inventoryGrid.Position = new Vector2(
            Mathf.Floor((availableWidth - gridWidth) * 0.5f),
            Mathf.Floor((availableHeight - gridHeight) * 0.5f)
        );
    }
    
    private void SetLeftPanelRect(Vector2 posXWidth)
    {
        float x = posXWidth.X;
        float width = posXWidth.Y;
        float y = _inventoryLeftPanel.Position.Y;
        float height = _inventoryLeftPanel.Size.Y;
        SetPanelGeometry(_inventoryLeftPanel, x, y, width, height);
    }
    
    private void SetRightPanelRect(Vector2 posXWidth)
    {
        float x = posXWidth.X;
        float width = posXWidth.Y;
        float y = _inventoryRightPanel.Position.Y;
        float height = _inventoryRightPanel.Size.Y;
        SetPanelGeometry(_inventoryRightPanel, x, y, width, height);
    }

    private void SetPanelGeometry(Control panel, float x, float y, float width, float height)
    {
        panel.Position = new Vector2(x, y);
        panel.Size = new Vector2(width, height);
    }

    private void PopulateInventorySlots()
    {
        foreach (Node child in _inventoryGrid.GetChildren())
            child.QueueFree();

        for (int i = 0; i < Inventory.SLOT_COUNT; i++)
        {
            Control slot = _inventorySlotScene.Instantiate<Control>();
            slot.SetMeta("slot_index", i);
            _inventoryGrid.AddChild(slot);
            ConnectSlotSignals(slot);
        }

        UpdateGridLayout(); 
    }

    private void ConnectSlotSignals(Control slot)
    {
        //slot.MouseEntered += () => ShowSlotTooltip(slot);
        //slot.MouseExited += () => _detailsLabel.Text = "";

        slot.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                int idx = (int)slot.GetMeta("slot_index");
                SelectSlot(idx);
            }
        };
    }

    private void SelectSlot(int slotIndex)
    {
        _selectedSlotIndex = slotIndex;
        var item = _playerInventory?.GetSlot(slotIndex);
        _lastSelectedSlot = slotIndex;
        UpdateSlotSelectionHighlight(slotIndex);

        if (_detailsLabel != null)
        {
            if (item != null)
            {
                string props = item.Properties.ToString();
                string physics = item.Physics != null
                    ? $"\nLattice: {item.Physics.Lattice}  H: {item.Physics.Enthalpy}  S: {item.Physics.Entropy}"
                    : "";
                _detailsLabel.Text = $"{item.Name}\n{props}{physics}";
            }
            else
            {
                _detailsLabel.Text = "";
            }
        }

        _crystalDiagram?.SetTargetItem(item);

        if (item != null && _currentLayout == LayoutState.Default)
            ApplyLayout(LayoutState.Expanded);
    }

    private void UpdateSlotSelectionHighlight(int selectedIdx)
    {
        foreach (Control slot in _inventoryGrid.GetChildren())
        {
            int idx = (int)slot.GetMeta("slot_index");
            if (slot is Panel panel)
            {
                var style = panel.GetThemeStylebox("panel")?.Duplicate() as StyleBoxFlat;
                if (style == null)
                {
                    style = new StyleBoxFlat();
                    style.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                }
                if (idx == selectedIdx)
                {
                    style.BorderColor = Colors.White;
                    style.SetBorderWidthAll(2);
                }
                else
                {
                    style.BorderColor = Colors.Transparent;
                    style.SetBorderWidthAll(0);
                }
                panel.AddThemeStyleboxOverride("panel", style);
            }
        }
    }

    private void ShowSlotTooltip(Control slot)
    {
        int idx = (int)slot.GetMeta("slot_index");
        var item = _playerInventory?.GetSlot(idx);
        if (item != null)
            _detailsLabel.Text = $"{item.Name} - {item.Properties}";
    }

    private void OnInventorySlotUpdated(int slotIndex)
    {
        if (_inventoryGrid == null || _playerInventory == null) return;

        foreach (Control slot in _inventoryGrid.GetChildren())
        {
            Variant meta = slot.GetMeta("slot_index");
            if (meta.VariantType != Variant.Type.Int) continue;
            int idx = meta.AsInt32();
            if (idx != slotIndex) continue;

            Panel panel = slot as Panel;
            if (panel == null) continue;

            Label countLabel = slot.GetNodeOrNull<Label>("CountLabel");
            Label abbrLabel = slot.GetNodeOrNull<Label>("AbbreviationLabel");

            var stack = _playerInventory.GetSlot(slotIndex);
            if (stack != null)
            {
                panel.Modulate = stack.ThemeColor;
                if (countLabel != null) countLabel.Text = stack.Quantity > 1 ? stack.Quantity.ToString() : "";
                slot.TooltipText = $"{stack.Name}\n{stack.Properties}";
                if (abbrLabel != null) abbrLabel.Text = stack.Abbreviation;
            }
            else
            {
                panel.Modulate = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                if (countLabel != null) countLabel.Text = "";
                if (abbrLabel != null) abbrLabel.Text = "";
                slot.TooltipText = "";
            }

            if (slotIndex == _lastSelectedSlot)
            {
                _crystalDiagram?.SetTargetItem(stack);
                if (stack == null) ApplyLayout(LayoutState.Default);
            }
            break;
        }
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

    // ========== Input handling ==========
    public override void _Input(InputEvent @event)
    {
        // Touch events are handled by MobileButton – ignore them here
        if (@event is InputEventScreenTouch)
            return;

        // ---- MENU IS OPEN: route to the active menu ----
        if (IsHealthPanelOpen)
        {
            // Tab or Escape steps back inside the health panel
            if (@event.IsActionPressed("toggle_inventory") || @event.IsActionPressed("ui_cancel"))
            {
                _healthPanel.Cancel();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Forward other keyboard/gamepad input for limb navigation
            _healthPanel.HandleInput(@event);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_inventoryPanel.Visible)
        {
            // Tab or Escape closes inventory
            if (@event.IsActionPressed("toggle_inventory") || @event.IsActionPressed("ui_cancel"))
            {
                ToggleInventory();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Inventory grid navigation
            if (_lockFocused)
            {
                if (@event.IsActionPressed("ui_accept"))
                {
                    _lockToggle.ButtonPressed = !_lockToggle.ButtonPressed;
                    GetViewport().SetInputAsHandled();
                }
                else if (@event.IsActionPressed("move_back"))
                {
                    _lockFocused = false;
                    UpdateLockFocusVisual();
                    _selectedSlotIndex = 0;
                    UpdateSlotSelectionHighlight(_selectedSlotIndex);
                    ShowSlotTooltipForIndex(_selectedSlotIndex);
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            if (@event.IsActionPressed("move_left"))      { MoveGridSelection(-1, 0); GetViewport().SetInputAsHandled(); }
            else if (@event.IsActionPressed("move_right")) { MoveGridSelection(1, 0); GetViewport().SetInputAsHandled(); }
            else if (@event.IsActionPressed("move_forward"))
            {
                if (_selectedSlotIndex == 0)
                {
                    _lockFocused = true;
                    UpdateLockFocusVisual();
                    UpdateSlotSelectionHighlight(-1);
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    MoveGridSelection(0, -1);
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event.IsActionPressed("move_back"))  { MoveGridSelection(0, 1); GetViewport().SetInputAsHandled(); }
            else if (@event.IsActionPressed("ui_accept"))  { if (_selectedSlotIndex >= 0) SelectSlot(_selectedSlotIndex); GetViewport().SetInputAsHandled(); }
            return;
        }

        if (_isDebugOpen)
        {
            // ` closes debug
            if (@event.IsActionPressed("toggle_debug"))
            {
                ToggleDebug();
                GetViewport().SetInputAsHandled();
                return;
            }
            // Escape also closes debug
            if (@event.IsActionPressed("ui_cancel"))
            {
                ToggleDebug();
                GetViewport().SetInputAsHandled();
                return;
            }
            
            bool consumed = false;
            if (@event.IsActionPressed("ui_down"))      { Navigate(1); consumed = true; }
            else if (@event.IsActionPressed("ui_up"))    { Navigate(-1); consumed = true; }
            else if (@event.IsActionPressed("ui_accept")) { ExecuteCurrent(); consumed = true; }
            else if (@event.IsActionPressed("ui_left"))  { AdjustCurrent(-1); consumed = true; }
            else if (@event.IsActionPressed("ui_right")) { AdjustCurrent(1); consumed = true; }

            if (consumed)
                GetViewport().SetInputAsHandled();
            return;
        }

        // --- Remap keys depending on current menu state ---
        bool healthOpen = IsHealthPanelOpen;
        bool inventoryOpen = _inventoryPanel.Visible;

        // Tab or Escape while Health is open → step back inside the health panel
        if (healthOpen && (@event.IsActionPressed("toggle_inventory") || @event.IsActionPressed("ui_cancel")))
        {
            _healthPanel.Cancel();   // added in LimbHealthUI (see below)
            GetViewport().SetInputAsHandled();
            return;
        }

        // Tab or Escape while Inventory is open → close inventory
        if (inventoryOpen && (@event.IsActionPressed("toggle_inventory") || @event.IsActionPressed("ui_cancel")))
        {
            ToggleInventory();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Escape while Debug is open → close debug
        if (_isDebugOpen && @event.IsActionPressed("ui_cancel"))
        {
            ToggleDebug();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Only if NO menu is open do the global toggles apply
        if (!healthOpen && !inventoryOpen && !_isDebugOpen)
        {
            if (@event.IsActionPressed("toggle_health"))
            {
                ToggleHealth();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event.IsActionPressed("toggle_inventory"))
            {
                ToggleInventory();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event.IsActionPressed("ui_cancel"))
            {
                TogglePause();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event.IsActionPressed("toggle_debug"))
            {
                ToggleDebug();
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }
    // ========== Toggle methods ==========

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        GetTree().Paused = _isPaused || _isDebugOpen;   // keep paused if debug is also open

        if (_pauseMenu != null)
            _pauseMenu.Visible = _isPaused;

        if (_mobileResumeButton != null)
            _mobileResumeButton.Visible = _isPaused;

        if (_pauseResumeContainer != null)
            _pauseResumeContainer.Visible = _isPaused;

        if (!DisplayServer.IsTouchscreenAvailable())
        {
            Input.MouseMode = _isPaused || _isDebugOpen
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
        EmitSignal(SignalName.MenuStateChanged);
    }

    public void ToggleHealth()
    {
        if (_healthPanel == null) { GD.PrintErr("HUD: _healthPanel is null"); return; }

        // Prevent opening health if inventory is already open
        if (!_healthPanel.Visible && IsInventoryOpen)
            return;

        if (_healthPanel.Visible)
        {
            _healthPanel.Deactivate();
            _healthPanel.Visible = false;
        }
        else
        {
            _healthPanel.Visible = true;
            _healthPanel.TargetHealth = GetPlayerHealth();
            _healthPanel.Activate();
        }
        EmitSignal(SignalName.MenuStateChanged);
    }
    private float _savedTimeScale = 1f;
    public void ToggleDebug()
    {
        _isDebugOpen = !_isDebugOpen;
        if (_debugControl != null)
        {
            _debugControl.Visible = _isDebugOpen;
            if (_isDebugOpen)
            {
                _debugControl.GetParent().MoveChild(_debugControl, -1); // bring to front
                RenderDebugMenu();
                GetTree().Paused = true;
            }
            else if (!_isPaused)   // only resume if pause menu isn't also open
            {
                GetTree().Paused = false;
            }
        }

        if (!DisplayServer.IsTouchscreenAvailable())
        {
            Input.MouseMode = _isDebugOpen || _isPaused
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }

        EmitSignal(SignalName.MenuStateChanged);
    }

    public void ToggleInventory()
    {
        if (_inventoryPanel == null || _lockToggle == null) 
        { 
            GD.PrintErr("HUD: inventory not built yet"); 
            return; 
        }
        bool opening = !_inventoryPanel.Visible;
        if (opening && IsHealthPanelOpen)
            return;
        _inventoryPanel.Visible = opening;
        _lockToggle.Visible = opening;

        // Smoothly change game speed (does NOT affect UI)
        if (_timeScaleTween != null && _timeScaleTween.IsValid())
        {
            _timeScaleTween.Kill();
            _activeTweens.Remove(_timeScaleTween);
        }
        _timeScaleTween = CreateRealTimeTween();
        _timeScaleTween.TweenMethod(Callable.From<float>(v => GameState.Instance.GameSpeed = v),
            GameState.Instance.GameSpeed, opening ? InventoryTimeScale : 1.0f, 0.3f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);

        // Keep mouse captured – no change to Input.MouseMode

        if (opening)
        {
            // Capture the current movement input for auto‑run
            if (DisplayServer.IsTouchscreenAvailable())
            {
                GameState.Instance.AutoRunDirection = MobileInput.MovementDirection;
                GameState.Instance.AutoRunSprinting = MobileInput.MovementDirection.Length() > 0.85f;
            }
            else
            {
                GameState.Instance.AutoRunDirection = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
                GameState.Instance.AutoRunSprinting = Input.IsActionPressed("sprint");
            }
            if (_inventoryOpenSound != null)
            {
                _uiAudioPlayer.Stream = _inventoryOpenSound;
                _uiAudioPlayer.Play();
            }
            _lockFocused = false;
            UpdateLockFocusVisual();
            // If the player is holding 'move_forward' right now, engage auto‑run
            GameState.Instance.AutoRunActive = Input.IsActionPressed("move_forward");
            
            // Reset selection to top‑left unless remember is on and item exists
            if (_rememberSelection && _lastSelectedSlot >= 0 && _playerInventory?.GetSlot(_lastSelectedSlot) != null)
            {
                _selectedSlotIndex = _lastSelectedSlot;
                ApplyLayout(LayoutState.Expanded, true);
                SelectSlot(_lastSelectedSlot);
            }
            else
            {
                _selectedSlotIndex = 0;
                _lastSelectedSlot = -1;
                _crystalDiagram?.SetTargetItem(null);
                UpdateSlotSelectionHighlight(-1);
                _detailsLabel.Text = "";
                ApplyLayout(LayoutState.Default, true);
            }
            CallDeferred(nameof(UpdateGridLayout));
            // Ensure initial highlight if top‑left has an item
            CallDeferred(nameof(UpdateInitialSelectionHighlight));
        }
        else
        {
            // Turn off auto‑run when inventory closes
            GameState.Instance.AutoRunActive = false;
            GameState.Instance.AutoRunDirection = Vector2.Zero;

            _lockFocused = false;
            UpdateLockFocusVisual();

            if (_inventoryCloseSound != null)
            {
                _uiAudioPlayer.Stream = _inventoryCloseSound;
                _uiAudioPlayer.Play();
            }
        }
        EmitSignal(SignalName.MenuStateChanged);
    }

    private void OnMobileResumePressed()
    {
        TogglePause(); // This will hide the pause menu and resume
    }

    public void ToggleCamera()
    {
        var player = GetTree().Root.FindChild("Player", true, false) as Player;
        if (player != null)
        {
            player.ToggleCamera();   // we'll add a public method in Player
        }
    }

    private void UpdateInitialSelectionHighlight()
    {
        if (_selectedSlotIndex >= 0)
        {
            UpdateSlotSelectionHighlight(_selectedSlotIndex);
            ShowSlotTooltipForIndex(_selectedSlotIndex);
        }
    }

    // ========== Debug Menu ==========
    private void SetupShowcaseMenu()
    {
        _menuItems.Clear();

        _menuItems.Add(new HeaderItem("PRESETS (Enter)"));
        AddPreset("CLEAR", WeatherManager.WeatherState.Clear);
        AddPreset("RAIN (Gloomy)", WeatherManager.WeatherState.Rain);
        AddPreset("SUMMER RAIN (Bright)", WeatherManager.WeatherState.SummerRain);
        AddPreset("STORM (Grey Sky)", WeatherManager.WeatherState.Storm);
        AddPreset("SNOW (Standard)", WeatherManager.WeatherState.Snow);
        AddPreset("SUNNY SNOW (Bright)", WeatherManager.WeatherState.SunnySnow);
        AddPreset("ICE", WeatherManager.WeatherState.Ice);
        AddPreset("MIXED", WeatherManager.WeatherState.Mixed);

        _menuItems.Add(new HeaderItem("PARTICLES (On/Off)"));
        AddToggle("Rain Particles", _rainNode);
        AddToggle("Splash Particles", _splashNode);
        AddToggle("Snow Particles", _snowNode);

        _menuItems.Add(new HeaderItem("GROUND & WIND (< >)"));
        AddSlider("Puddle Coverage", "rain_amount", 0.05f);
        AddSlider("Snow Coverage", "snow_amount", 0.05f);
        AddSlider("Ice Coverage", "ice_amount", 0.05f);

        _menuItems.Add(new ActionItem
        {
            Name = "Wind Speed",
            OnAdjust = (dir) => AdjustWind("wind_speed", dir * 1.0f),
            GetDisplayValue = () => _trackedValues["wind_speed"].ToString("0.0")
        });
        _menuItems.Add(new ActionItem
        {
            Name = "Wind Angle",
            OnAdjust = (dir) => AdjustWind("wind_angle", dir * 15.0f),
            GetDisplayValue = () => _trackedValues["wind_angle"].ToString("0") + "°"
        });
        _menuItems.Add(new ActionItem
        {
            Name = "Auto Wind",
            OnExecute = () => {
                if (_weatherManager != null)
                {
                    bool newState = !_weatherManager.IsAutoWindEnabled();
                    _weatherManager.SetAutoWindEnabled(newState);
                }
            },
            GetDisplayValue = () => _weatherManager != null ? (_weatherManager.IsAutoWindEnabled() ? "ON" : "OFF") : "N/A"
        });
        _menuItems.Add(new ActionItem
        {
            Name = "Fog Density",
            OnAdjust = (dir) => AdjustFogDensity(dir * 0.005f),
            GetDisplayValue = () => GetFogDensity().ToString("0.000")
        });

        _menuItems.Add(new HeaderItem("TIME CONTROL"));
        _menuItems.Add(new ActionItem
        {
            Name = "Time Scale",
            OnAdjust = (dir) => {
                if (_timeManager != null)
                {
                    float next = Mathf.Max(0, _timeManager.TimeScale + (dir * 1.0f));
                    _timeManager.TimeScale = next;
                }
            },
            GetDisplayValue = () => _timeManager?.TimeScale.ToString("0.0") + "x"
        });
        _menuItems.Add(new ActionItem
        {
            Name = "Set Hour",
            OnAdjust = (dir) => {
                if (_timeManager != null)
                {
                    int next = _timeManager.Hour + dir;
                    if (next > 23) next = 0; if (next < 0) next = 23;
                    ForceSetTime(next, _timeManager.Minute);
                }
            },
            GetDisplayValue = () => _timeManager?.GetTimeString()
        });
        _menuItems.Add(new ActionItem
        {
            Name = "Jump to Next Season",
            OnExecute = () => JumpToNextSeason(),
            GetDisplayValue = () => "-->"
        });
    }

    private void AddPreset(string name, WeatherManager.WeatherState state)
    {
        _menuItems.Add(new ActionItem
        {
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
        _menuItems.Add(new ActionItem
        {
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
        _menuItems.Add(new ActionItem
        {
            Name = name,
            OnExecute = () => { if (node != null) node.Emitting = !node.Emitting; },
            GetDisplayValue = () => (node != null && node.Emitting) ? "ON" : "OFF"
        });
    }

    private void AdjustWind(string key, float change)
    {
        float val = _trackedValues[key] + change;
        if (key == "wind_angle")
        {
            if (val >= 360) val -= 360;
            if (val < 0) val += 360;
        }
        else
        {
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

    private void Navigate(int dir)
    {
        _selectedIdx += dir;
        if (_selectedIdx < 0) _selectedIdx = _menuItems.Count - 1;
        if (_selectedIdx >= _menuItems.Count) _selectedIdx = 0;
        if (_menuItems[_selectedIdx] is HeaderItem) Navigate(dir);
        RenderDebugMenu();

        // Auto‑scroll to keep the selected label visible
        if (_debugScrollContainer != null && _selectedIdx >= 0 && _selectedIdx < _labelPool.Count)
            _debugScrollContainer.EnsureControlVisible(_labelPool[_selectedIdx]);
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
        if (_debugVBoxContainer == null) return;
        while (_labelPool.Count < _menuItems.Count)
        {
            Label l = new Label();
            l.LabelSettings = new LabelSettings() { OutlineSize = 4, OutlineColor = Colors.Black };
            l.AddThemeFontSizeOverride("font_size", 26);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.SizeFlagsHorizontal = Control.SizeFlags.Fill;
            _debugVBoxContainer.AddChild(l);
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

        Vector2 screenPos = camera.UnprojectPosition(worldPos);
    
        Vector2 panelSize = _tooltipPanel.GetCombinedMinimumSize();
        if (panelSize == Vector2.Zero) panelSize = new Vector2(200, 50);
        _tooltipPanel.Size = panelSize;

        Vector2 offset = new Vector2(0, -panelSize.Y - 10);
        Vector2 finalPos = screenPos + offset;

        var viewportSize = GetViewportRect().Size;
        finalPos.X = Mathf.Clamp(finalPos.X, 0, viewportSize.X - panelSize.X);
        finalPos.Y = Mathf.Clamp(finalPos.Y, 0, viewportSize.Y - panelSize.Y);

        _tooltipPanel.Position = finalPos;
        _tooltipPanel.Show();
        _tooltipPanel.GetParent().MoveChild(_tooltipPanel, -1);

        if (_tooltipTween != null && _tooltipTween.IsValid())
        {
            _tooltipTween.Kill();
            _activeTweens.Remove(_tooltipTween);
        }

        _tooltipTween = CreateRealTimeTween();
        _tooltipTween.SetLoops();
        _tooltipTween.TweenProperty(_tooltipPanel, "modulate:a", 0.7f, 0.75f)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
        _tooltipTween.TweenProperty(_tooltipPanel, "modulate:a", 1.0f, 0.75f)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
    }
    
    private Texture2D CreateLockIcon(bool locked)
    {
        int width = 7;
        int height = 8;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);

        // Row 0
        image.SetPixel(1, 0, Colors.White);
        image.SetPixel(2, 0, Colors.White);
        image.SetPixel(3, 0, Colors.White);
        image.SetPixel(4, 0, Colors.White);
        image.SetPixel(5, 0, Colors.White);

        // Row 1
        image.SetPixel(1, 1, Colors.White);
        image.SetPixel(5, 1, Colors.White);

        // Row 2
        image.SetPixel(1, 2, Colors.White);
        if (locked)
            image.SetPixel(5, 2, Colors.White);

        // Row 3
        image.SetPixel(1, 3, Colors.White);
        if (locked)
            image.SetPixel(5, 3, Colors.White);

        // Row 4
        for (int x = 0; x <= 6; x++)
            image.SetPixel(x, 4, Colors.White);

        // Row 5
        image.SetPixel(0, 5, Colors.White);
        image.SetPixel(1, 5, Colors.White);
        image.SetPixel(2, 5, Colors.White);
        image.SetPixel(4, 5, Colors.White);
        image.SetPixel(5, 5, Colors.White);
        image.SetPixel(6, 5, Colors.White);

        // Row 6
        image.SetPixel(0, 6, Colors.White);
        image.SetPixel(1, 6, Colors.White);
        image.SetPixel(2, 6, Colors.White);
        image.SetPixel(4, 6, Colors.White);
        image.SetPixel(5, 6, Colors.White);
        image.SetPixel(6, 6, Colors.White);

        // Row 7
        for (int x = 0; x <= 6; x++)
            image.SetPixel(x, 7, Colors.White);

        return ImageTexture.CreateFromImage(image);
    }

    private void MoveGridSelection(int dx, int dy)
    {
        if (_inventoryGrid == null || !_inventoryPanel.Visible) return;
        int columns = _inventoryGrid.Columns;
        if (columns == 0) return;
        int total = Inventory.SLOT_COUNT;

        if (_selectedSlotIndex < 0)
            _selectedSlotIndex = 0;

        int row = _selectedSlotIndex / columns;
        int col = _selectedSlotIndex % columns;

        // Total rows (including incomplete last row)
        int totalRows = Mathf.CeilToInt((float)total / columns);

        // Horizontal wrap within the current row
        if (dx != 0)
        {
            int rowStart = row * columns;
            int rowEnd = Mathf.Min(rowStart + columns, total) - 1;
            int rowLength = rowEnd - rowStart + 1;

            col = (col + dx) % rowLength;
            if (col < 0) col += rowLength;
        }

        // Vertical wrap
        if (dy != 0)
        {
            row = (row + dy) % totalRows;
            if (row < 0) row += totalRows;

            // Only clamp column if the target row is shorter than the current column
            int rowStart = row * columns;
            int rowEnd = Mathf.Min(rowStart + columns, total) - 1;
            int rowLength = rowEnd - rowStart + 1;
            if (col >= rowLength)
                col = rowLength - 1;
        }

        int newIndex = row * columns + col;
        _selectedSlotIndex = newIndex;
        UpdateSlotSelectionHighlight(_selectedSlotIndex);
        ShowSlotTooltipForIndex(_selectedSlotIndex);
    }

    private void ShowSlotTooltipForIndex(int index)
    {
        var item = _playerInventory?.GetSlot(index);
        if (item != null)
            _detailsLabel.Text = $"{item.Name} - {item.Properties}";
        else
            _detailsLabel.Text = "";
    }

    public override void _ExitTree()
    {
        GameState.TimeScaleChanged -= OnTimeScaleChanged;
        if (Instance == this)
            Instance = null;
    }
    private void UpdateLockFocusVisual()
    {
        if (_lockFocusBorder != null)
            _lockFocusBorder.Visible = _lockFocused;
    }

    private Health GetPlayerHealth()
    {
        var player = GetTree().Root.FindChild("Player", true, false);
        return player?.GetNode<Health>("Health");
    }

    private Tween CreateRealTimeTween()
    {
        var tween = CreateTween();
        tween.SetSpeedScale(1.0f / GameState.Instance.GameSpeed);
        _activeTweens.Add(tween);
        tween.Finished += () => _activeTweens.Remove(tween);
        return tween;
    }

    private void OnTimeScaleChanged(float newScale)
    {
        // Iterate over a copy to avoid issues if a tween finishes during the loop
        var tweensCopy = _activeTweens.ToArray();
        foreach (var tween in tweensCopy)
        {
            if (tween.IsValid())
                tween.SetSpeedScale(1.0f / newScale);
        }
    }

    private void CreateMobileResumeButton()
    {
        if (_pauseMenu == null) return;

        var container = new ColorRect
        {
            Color = new Color(0.2f, 0.2f, 0.2f, 0.8f),
            Size = new Vector2(300, 120)
        };
        _pauseResumeContainer = container;   // <--- store reference

        var style = new StyleBoxFlat
        {
            BgColor = container.Color,
            BorderColor = Colors.White
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(10);
        container.AddThemeStyleboxOverride("panel", style);

        container.SetAnchorsPreset(Control.LayoutPreset.Center);
        container.Position = new Vector2(-150, 80);

        _pauseMenu.AddChild(container);

        var label = new Label
        {
            Text = "Resume",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        container.AddChild(label);

        _mobileResumeButton = new TouchScreenButton
        {
            TextureNormal = CreateEmptyTexture(),
            TexturePressed = CreateEmptyTexture(),
            Action = "",
            Scale = container.Size,
            Position = container.Position
        };
        _mobileResumeButton.Pressed += OnMobileResumePressed;
        _pauseMenu.AddChild(_mobileResumeButton);

        _mobileResumeButton.Visible = false;
        container.Visible = false;
    }

    // Helper to create an empty texture (from limbhealth)
    private Texture2D CreateEmptyTexture()
    {
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        return ImageTexture.CreateFromImage(image);
    }
}