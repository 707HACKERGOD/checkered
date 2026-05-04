using Godot;
using System.Collections.Generic;

public partial class LimbHealthUI : Control
{
    [Export] public Health TargetHealth { get; set; }

    // UI containers
    private Panel _panelBackground;
    private Panel _bodyDiagram;
    private Panel _partStatusPanel;
    private Label _partNameLabel;
    private ProgressBar _healthBar;
    private Label _conditionLabel;
    private float _lastBackgroundWidth = -1f;   // used to detect size changes
    private float _bodyScale = 1f;              // current scale factor for the body diagram

    // Limb controls (created dynamically)
    private Dictionary<string, SelectableBodyPart> _limbControls = new();
    
    // Navigation state
    private enum SelectionMode { FullBody, Limb }
    private SelectionMode _selectionMode = SelectionMode.FullBody;
    private string _selectedLimb = "Torso";
    
    private bool _isActive = false;

    // Visual styles
    private StyleBoxFlat _selectedOutlineStyle;
    
    // SVG‑based layout constants
    private static readonly Vector2 BodyDiagramSize = new(200, 400);

    private struct LimbDef
    {
        public string Name;
        public Vector2 Position;
        public Vector2 Size;
        public int CornerRadius;
    }

    private readonly LimbDef[] _limbDefinitions = new[]
    {
        new LimbDef { Name = "Head",     Position = new Vector2(70, 5),   Size = new Vector2(60, 70),  CornerRadius = 30 },
        new LimbDef { Name = "LeftEye",  Position = new Vector2(83, 30),  Size = new Vector2(10, 10),  CornerRadius = 5 },
        new LimbDef { Name = "RightEye", Position = new Vector2(107, 30), Size = new Vector2(10, 10),  CornerRadius = 5 },
        new LimbDef { Name = "Torso",    Position = new Vector2(70, 80),  Size = new Vector2(60, 100), CornerRadius = 10 },
        new LimbDef { Name = "LeftArm",  Position = new Vector2(40, 85),  Size = new Vector2(25, 80),  CornerRadius = 5 },
        new LimbDef { Name = "RightArm", Position = new Vector2(135, 85), Size = new Vector2(25, 80),  CornerRadius = 5 },
        new LimbDef { Name = "LeftLeg",  Position = new Vector2(72, 185), Size = new Vector2(25, 100), CornerRadius = 5 },
        new LimbDef { Name = "RightLeg", Position = new Vector2(103, 185),Size = new Vector2(25, 100), CornerRadius = 5 }
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;

        BuildUI();
        CreateSelectionStyle();
        ConnectSignals();
        UpdateAllLimbs();
        SetSelectionMode(SelectionMode.FullBody);
        Visible = false;
    }

    private void BuildUI()
    {
        _panelBackground = new Panel
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -480, OffsetRight = 480, OffsetTop = -320, OffsetBottom = 320
        };
        AddChild(_panelBackground);
        ApplyPanelStyle(_panelBackground, new Color(0.078f, 0.063f, 0.059f, 0.925f));
        _panelBackground.Resized += () => CallDeferred(nameof(UpdateLayout));

        _bodyDiagram = new Panel
        {
            Position = new Vector2(20, 20),
            Size = BodyDiagramSize
        };
        var bodyStyle = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidthLeft = 0, BorderWidthRight = 0, BorderWidthTop = 0, BorderWidthBottom = 0
        };
        _bodyDiagram.AddThemeStyleboxOverride("panel", bodyStyle);
        _panelBackground.AddChild(_bodyDiagram);

        foreach (var def in _limbDefinitions)
        {
            var limb = CreateLimb(def);
            _bodyDiagram.AddChild(limb);
            _limbControls[def.Name] = limb;
        }

        _partStatusPanel = new Panel();
        _partStatusPanel.Name = "StatusPanel";
        // position and size it dynamically in UpdateLayout
        _panelBackground.AddChild(_partStatusPanel);
        ApplyPanelStyle(_partStatusPanel, new Color(0.102f, 0.082f, 0.082f, 1.0f));

        _partNameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _partNameLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.267f, 0.267f));
        _partNameLabel.AddThemeFontSizeOverride("font_size", 24);
        _partStatusPanel.AddChild(_partNameLabel);

        _healthBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100
        };
        ApplyHealthBarStyle(_healthBar);
        _partStatusPanel.AddChild(_healthBar);

        _conditionLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _conditionLabel.AddThemeColorOverride("font_color", new Color(0.878f, 0.835f, 0.78f));
        _conditionLabel.AddThemeFontSizeOverride("font_size", 16);
        _partStatusPanel.AddChild(_conditionLabel);

        // Use anchors to fill the status panel
        _partNameLabel.AnchorLeft = 0; _partNameLabel.AnchorRight = 1;
        _partNameLabel.AnchorTop = 0; _partNameLabel.AnchorBottom = 0;
        _partNameLabel.OffsetLeft = 10; _partNameLabel.OffsetRight = -10;
        _partNameLabel.OffsetTop = 10; _partNameLabel.OffsetBottom = 40;

        _healthBar.AnchorLeft = 0; _healthBar.AnchorRight = 1;
        _healthBar.AnchorTop = 0; _healthBar.AnchorBottom = 0;
        _healthBar.OffsetLeft = 10; _healthBar.OffsetRight = -10;
        _healthBar.OffsetTop = 50; _healthBar.OffsetBottom = 74;

        _conditionLabel.AnchorLeft = 0; _conditionLabel.AnchorRight = 1;
        _conditionLabel.AnchorTop = 0; _conditionLabel.AnchorBottom = 1;
        _conditionLabel.OffsetLeft = 10; _conditionLabel.OffsetRight = -10;
        _conditionLabel.OffsetTop = 90; _conditionLabel.OffsetBottom = -10;
    }

    private void CreateSelectionStyle()
    {
        _selectedOutlineStyle = new StyleBoxFlat
        {
            BorderColor = new Color(1, 0, 0),
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            BgColor = Colors.Transparent
        };
    }

    private SelectableBodyPart CreateLimb(LimbDef def)
    {
        var part = new SelectableBodyPart
        {
            Name = def.Name,
            Position = def.Position,
            Size = def.Size,
            MouseFilter = MouseFilterEnum.Ignore,
            FillColor = new Color(0.133f, 0.545f, 0.133f),
            BorderColor = new Color(0.29f, 0.188f, 0.188f),
            BorderWidth = 2,
            CornerRadius = def.CornerRadius
        };
        if (def.Name == "Head")
        {
            part.IsEllipse = true;
        }
        return part;
    }

    private void ApplyPanelStyle(Panel panel, Color bgColor)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = new Color(0.29f, 0.188f, 0.188f),
            BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    private void ApplyHealthBarStyle(ProgressBar bar)
    {
        var bgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.039f, 0.039f, 0.039f),
            BorderColor = new Color(0.29f, 0.188f, 0.188f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1
        };
        bar.AddThemeStyleboxOverride("background", bgStyle);

        var fillStyle = new StyleBoxFlat { BgColor = new Color(0.133f, 0.545f, 0.133f) };
        bar.AddThemeStyleboxOverride("fill", fillStyle);
    }

    private void ConnectSignals()
    {
        if (TargetHealth != null)
        {
            TargetHealth.LimbDamaged += OnLimbDamaged;
            TargetHealth.Damaged += (_, _) => UpdateAllLimbs();
            TargetHealth.Healed += (_, _) => UpdateAllLimbs();
        }
    }

    public void Activate()
    {
        _isActive = true;
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;

        // Auto‑run: capture current movement input
        GameState.Instance.AutoRunActive = Input.IsActionPressed("move_forward");
        if (DisplayServer.IsTouchscreenAvailable())
        {
            GameState.Instance.AutoRunDirection = MobileInput.MovementDirection;
            GameState.Instance.AutoRunSprinting = Input.IsActionPressed("sprint");
        }
        else
        {
            GameState.Instance.AutoRunDirection = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            GameState.Instance.AutoRunSprinting = Input.IsActionPressed("sprint");
        }

        UpdateAllLimbs();
        if (_selectionMode == SelectionMode.FullBody)
            ShowFullBodyStatus();
        else if (!string.IsNullOrEmpty(_selectedLimb))
            ShowLimbStatus(_selectedLimb);

        GameState.Instance.GameSpeed = 0.2f;

        // Notify the HUD about the menu state change
        HUD.Instance?.OnHealthPanelToggled();
        CallDeferred(nameof(UpdateLayout));
    }

    public void Deactivate()
    {
        _isActive = false;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        ClearSelectionOutline();

        // Turn off auto‑run
        GameState.Instance.AutoRunActive = false;
        GameState.Instance.AutoRunDirection = Vector2.Zero;

        GameState.Instance.GameSpeed = 1.0f;

        // Notify the HUD about the menu state change
        HUD.Instance?.OnHealthPanelToggled();
    }

    public void HandleInput(InputEvent @event)
    {
        if (!_isActive) return;

        // Allow H to close the panel at any time
        if (@event.IsActionPressed("toggle_health"))
        {
            Deactivate();
            return;
        }

        if (_selectionMode == SelectionMode.FullBody)
        {
            if (@event.IsActionPressed("move_forward"))
            {
                SetSelectionMode(SelectionMode.Limb, "Head");
            }
            else if (@event.IsActionPressed("move_left") || 
                    @event.IsActionPressed("move_back") || 
                    @event.IsActionPressed("move_right"))
            {
                SetSelectionMode(SelectionMode.Limb, "Torso");
            }
            else if (@event.IsActionPressed("ui_cancel"))
            {
                Cancel();
            }
        }
        else // Limb mode
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                // If we are on an eye, go back to Head; otherwise go to Full Body
                if (_selectedLimb == "LeftEye" || _selectedLimb == "RightEye")
                {
                    SetSelectionMode(SelectionMode.Limb, "Head");
                }
                else
                {
                    SetSelectionMode(SelectionMode.FullBody);
                }
            }
            else
            {
                HandleLimbNavigation(@event);
            }
        }
    }

    private void HandleLimbNavigation(InputEvent @event)
    {
        string nextLimb = null;

        if (@event.IsActionPressed("move_forward"))
            nextLimb = GetUpNeighbor(_selectedLimb);
        else if (@event.IsActionPressed("move_left"))
            nextLimb = GetLeftNeighbor(_selectedLimb);
        else if (@event.IsActionPressed("move_back"))
            nextLimb = GetDownNeighbor(_selectedLimb);
        else if (@event.IsActionPressed("move_right"))
            nextLimb = GetRightNeighbor(_selectedLimb);

        if (!string.IsNullOrEmpty(nextLimb))
            SetSelectionMode(SelectionMode.Limb, nextLimb);
    }

    private string GetUpNeighbor(string current) => current switch
    {
        "Torso" => "Head",
        "LeftArm" => "Torso",
        "RightArm" => "Torso",
        "LeftLeg" => "Torso",
        "RightLeg" => "Torso",
        "Head" => "RightLeg",   // W from Head goes to Right Leg
        _ => null
    };

    private string GetDownNeighbor(string current) => current switch
    {
        "Torso" => "LeftLeg",
        "Head" => "Torso",      // S from Head goes to Torso
        _ => null
    };

    private string GetLeftNeighbor(string current) => current switch
    {
        "Head" => "LeftEye",      // A from Head → Left Eye
        "RightEye" => "Head",     // A from Right Eye → Head (optional)
        "Torso" => "LeftArm",
        "RightArm" => "Torso",
        "RightLeg" => "LeftLeg",
        _ => null
    };

    private string GetRightNeighbor(string current) => current switch
    {
        "Head" => "RightEye",     // D from Head → Right Eye
        "LeftEye" => "Head",      // D from Left Eye → Head (optional)
        "Torso" => "RightArm",
        "LeftArm" => "Torso",
        "LeftLeg" => "RightLeg",
        _ => null
    };

    private void SetSelectionMode(SelectionMode mode, string limb = null)
    {
        _selectionMode = mode;
        if (mode == SelectionMode.FullBody)
        {
            _selectedLimb = null;
            ApplyFullBodyOutline();
            ShowFullBodyStatus();
        }
        else
        {
            _selectedLimb = limb ?? "Torso";
            ApplyLimbOutline(_selectedLimb);
            ShowLimbStatus(_selectedLimb);
        }
    }

    private void ClearSelectionOutline()
    {
        foreach (var part in _limbControls.Values)
            part.IsSelected = false;

        _bodyDiagram.RemoveThemeStyleboxOverride("panel");
        var bodyStyle = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidthLeft = 0, BorderWidthRight = 0, BorderWidthTop = 0, BorderWidthBottom = 0
        };
        _bodyDiagram.AddThemeStyleboxOverride("panel", bodyStyle);
        _bodyDiagram.QueueRedraw();
    }

    private void ApplyFullBodyOutline()
    {
        ClearSelectionOutline();
        _bodyDiagram.AddThemeStyleboxOverride("panel", _selectedOutlineStyle);
        _bodyDiagram.QueueRedraw();
    }

    private void ApplyLimbOutline(string limbName)
    {
        ClearSelectionOutline();
        if (_limbControls.TryGetValue(limbName, out var part))
            part.IsSelected = true;
    }

    private void ShowFullBodyStatus()
    {
        if (TargetHealth == null) return;

        _partNameLabel.Text = "FULL BODY";

        // Calculate average limb health
        float totalMax = 0;
        float totalCurrent = 0;
        bool anyLimbFound = false;

        foreach (var limbName in _limbControls.Keys)
        {
            var limb = TargetHealth.GetNodeOrNull<LimbHealth>(limbName);
            if (limb != null)
            {
                totalMax += limb.MaxHealth;
                totalCurrent += limb.CurrentHealth;
                anyLimbFound = true;
            }
        }

        float percent = anyLimbFound ? (totalCurrent / totalMax) * 100f : 100f;
        _healthBar.Value = percent;

        // Condition based on lowest limb
        float minLimbHealth = 1.0f;
        foreach (var limbName in _limbControls.Keys)
        {
            var limb = TargetHealth.GetNodeOrNull<LimbHealth>(limbName);
            if (limb != null && !limb.IsDestroyed)
                minLimbHealth = Mathf.Min(minLimbHealth, limb.CurrentHealth / limb.MaxHealth);
        }

        string condition = minLimbHealth >= 1.0f ? "Healthy" :
                        minLimbHealth > 0.6f ? "Healthy" :
                        minLimbHealth > 0.3f ? "Damaged" : "Critical";
        _conditionLabel.Text = $"Overall: {condition}";
    }

    private void ShowLimbStatus(string limbName)
    {
        LimbHealth limb = TargetHealth?.GetNodeOrNull<LimbHealth>(limbName);
        if (limb == null) return;

        _partNameLabel.Text = limbName;
        float percent = limb.IsDestroyed ? 0 : (limb.CurrentHealth / limb.MaxHealth) * 100f;
        _healthBar.Value = percent;

        string condition;
        if (limb.IsDestroyed || percent <= 0)
            condition = "Crippled";
        else if (percent < 20)
            condition = "Critical";
        else if (percent < 50)
            condition = "Damaged";
        else if (percent < 100)
            condition = "Bruised";
        else
            condition = "Healthy";

        _conditionLabel.Text = condition;
    }

    private void OnLimbDamaged(string limbName, float amount, float currentHealth)
    {
        UpdateLimbVisual(limbName);
        if (_isActive)
        {
            if (_selectionMode == SelectionMode.FullBody)
                ShowFullBodyStatus();
            else if (_selectedLimb == limbName)
                ShowLimbStatus(limbName);
        }
    }

    private void UpdateAllLimbs()
    {
        foreach (var limbName in _limbControls.Keys)
            UpdateLimbVisual(limbName);
        if (_isActive)
        {
            if (_selectionMode == SelectionMode.FullBody)
                ShowFullBodyStatus();
            else if (!string.IsNullOrEmpty(_selectedLimb))
                ShowLimbStatus(_selectedLimb);
        }
    }

    private void UpdateLimbVisual(string limbName)
    {
        if (!_limbControls.TryGetValue(limbName, out var part)) return;
        LimbHealth limb = TargetHealth?.GetNodeOrNull<LimbHealth>(limbName);
        if (limb == null) return;

        float percent = limb.IsDestroyed ? 0 : (limb.CurrentHealth / limb.MaxHealth) * 100f;
        Color color;

        if (limb.IsDestroyed || percent <= 0)
            color = new Color(0.5f, 0.5f, 0.5f); // Gray
        else if (percent < 20)
            color = new Color(1, 0, 0);          // Red
        else if (percent < 50)
            color = new Color(1, 0.5f, 0);       // Orange
        else if (percent >= 100)
            color = Colors.White;
        else
            color = new Color(0, 1, 0);          // Green

        part.FillColor = color;
    }

    public void Cancel()
    {
        if (!_isActive) return;

        if (_selectionMode == SelectionMode.Limb)
        {
            if (_selectedLimb == "LeftEye" || _selectedLimb == "RightEye")
                SetSelectionMode(SelectionMode.Limb, "Head");
            else
                SetSelectionMode(SelectionMode.FullBody);
        }
        else
        {
            Deactivate();
        }
    }

    private void UpdateLayout()
    {
        if (_panelBackground == null || _bodyDiagram == null || _partStatusPanel == null)
            return;

        Vector2 bgSize = _panelBackground.Size;
        if (bgSize.X <= 0 || bgSize.Y <= 0) return;

        // Margins
        const float margin = 20f;
        float availWidth = bgSize.X - margin * 2;
        float availHeight = bgSize.Y - margin * 2;

        // Body diagram takes left 35% of available width, maintaining aspect ratio
        float maxBodyWidth = availWidth * 0.35f;
        float maxBodyHeight = availHeight * 0.9f;

        // Original diagram size (from constant)
        Vector2 origSize = BodyDiagramSize; // 200x400
        _bodyScale = Mathf.Min(maxBodyWidth / origSize.X, maxBodyHeight / origSize.Y);

        Vector2 newBodySize = origSize * _bodyScale;

        // Position the body diagram – left aligned, vertically centered
        _bodyDiagram.Position = new Vector2(margin, (bgSize.Y - newBodySize.Y) * 0.5f);
        _bodyDiagram.Size = newBodySize;

        // Resize and reposition all limb controls
        for (int i = 0; i < _limbDefinitions.Length; i++)
        {
            var def = _limbDefinitions[i];
            if (_limbControls.TryGetValue(def.Name, out var part))
            {
                part.Position = def.Position * _bodyScale;
                part.Size = def.Size * _bodyScale;
                if (part.IsEllipse)
                {
                    // For ellipse (head), corner radius should scale too
                    part.CornerRadius = Mathf.RoundToInt(def.CornerRadius * _bodyScale);
                }
            }
        }

        // Position the status panel to the right of the body diagram
        float statusPanelX = margin + newBodySize.X + margin;  // small gap
        float statusPanelWidth = bgSize.X - statusPanelX - margin;
        _partStatusPanel.Position = new Vector2(statusPanelX, margin);
        _partStatusPanel.Size = new Vector2(statusPanelWidth, availHeight);

        // The status panel children will automatically fill their parent thanks to anchors – no further action needed.
    }
}