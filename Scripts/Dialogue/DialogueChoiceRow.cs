using Godot;

/// <summary>One player choice: [progress ring] [number] [text].
/// Tap = pulse + accent flash. Hold = ring fills, row highlights.
/// Confirm = white burst + scale pop.</summary>
public partial class DialogueChoiceRow : Panel
{
    private static readonly Color BorderCol = new("#4a3030");
    private static readonly Color AccentCol = new("#ff4444");
    private static readonly Color TextCol = new("#e0d5c7");
    private static readonly Color HeldBg = new(0.545f, 0f, 0f, 0.45f);   // #8b0000 @ 45%

    private HoldRing _ring;
    private Label _number;
    private Label _text;
    private ColorRect _burst;

    private float _progress;
    private bool _held;
    private bool _draining;

    public override void _Ready()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0, 40);
        AddThemeStyleboxOverride("panel", MakeSb(new Color(0, 0, 0, 0f), BorderCol));

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        AddChild(margin);

        var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 12);
        margin.AddChild(box);

        _ring = new HoldRing { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        box.AddChild(_ring);

        _number = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _number.AddThemeFontOverride("font", Ps1Ui.GetFont(1));
        _number.AddThemeFontSizeOverride("font_size", 15);
        _number.AddThemeColorOverride("font_color", Ps1Ui.HintGray);
        box.AddChild(_number);

        _text = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _text.AddThemeFontOverride("font", Ps1Ui.GetFont(1));
        _text.AddThemeFontSizeOverride("font_size", 17);
        _text.AddThemeColorOverride("font_color", TextCol);
        box.AddChild(_text);

        _burst = new ColorRect
        {
            Color = AccentCol,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        _burst.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_burst);
    }

    public void Setup(int number, string text)
    {
        _number.Text = $"{number}.";
        _text.Text = text;
    }

    public void SetProgress(float p)
    {
        bool held = p > 0f;
        if (held != _held)
        {
            _held = held;
            _draining = false;
            AddThemeStyleboxOverride("panel", MakeSb(held ? HeldBg : new Color(0, 0, 0, 0f), held ? AccentCol : BorderCol));
            _text.AddThemeColorOverride("font_color", held ? Colors.White : TextCol);
            _number.AddThemeColorOverride("font_color", held ? AccentCol : Ps1Ui.HintGray);
        }
        _progress = Mathf.Clamp(p, 0f, 1f);
        _ring.SetProgress(_progress);
    }

    /// <summary>Short tap: text flashes accent and fades back, ring drains away.</summary>
    public void TapFlash()
    {
        _draining = true;
        _text.AddThemeColorOverride("font_color", AccentCol);
        var tw = CreateTween();
        tw.TweenProperty(_text, "theme_override_colors/font_color", TextCol, 0.35f);
    }

    /// <summary>Instant the key goes down: tiny scale pulse so the press registers.</summary>
    public void Pulse()
    {
        PivotOffset = Size / 2f;
        var tw = CreateTween();
        tw.TweenProperty(this, "scale", new Vector2(1.03f, 1.03f), 0.07f);
        tw.TweenProperty(this, "scale", Vector2.One, 0.12f);
    }

    /// <summary>Hold completed: white border, accent burst overlay, scale pop.</summary>
    public void PlaySelected()
    {
        SetProgress(1f);
        _draining = false;
        _text.AddThemeColorOverride("font_color", Colors.White);
        AddThemeStyleboxOverride("panel", MakeSb(HeldBg, Colors.White));

        var tw = CreateTween();
        tw.TweenProperty(_burst, "modulate:a", 0.55f, 0.09f);
        tw.TweenProperty(_burst, "modulate:a", 0f, 0.35f);

        PivotOffset = Size / 2f;
        var ts = CreateTween();
        ts.TweenProperty(this, "scale", new Vector2(1.05f, 1.05f), 0.08f);
        ts.TweenProperty(this, "scale", Vector2.One, 0.18f);
    }

    public override void _Process(double delta)
    {
        if (!_draining) return;
        _progress = Mathf.Max(0f, _progress - (float)delta / 0.12f);
        _ring.SetProgress(_progress);
        if (_progress <= 0f) { _draining = false; SetProgress(0f); }
    }

    private static StyleBoxFlat MakeSb(Color bg, Color border)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(2);
        return s;
    }
}

/// <summary>The hold-progress ring (faint track + accent arc + dot when full).</summary>
public partial class HoldRing : Control
{
    private static readonly Color TrackCol = new("#4a3030");
    private static readonly Color FillCol = new("#ff4444");
    private float _progress;
    private bool _done;

    public HoldRing()
    {
        CustomMinimumSize = new Vector2(26, 26);
        MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    public void SetProgress(float p)
    {
        p = Mathf.Clamp(p, 0f, 1f);
        if (Mathf.IsEqualApprox(p, _progress) && _done == (p >= 1f)) return;
        _progress = p;
        _done = p >= 1f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) * 0.5f - 3f;
        if (radius < 2f) return;

        DrawArc(center, radius, 0f, Mathf.Tau, 40, TrackCol, 3f, true);
        if (_progress > 0f)
            DrawArc(center, radius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * _progress, 40, FillCol, 3f, true);
        if (_done)
            DrawCircle(center, radius * 0.4f, FillCol);
    }
}