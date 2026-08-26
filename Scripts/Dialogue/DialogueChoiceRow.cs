using Godot;

/// <summary>One player choice: [progress ring] [number] [text] — outlined text, no box.
/// Tap = accent flash. Hold = ring fills, text highlights.
/// Confirm = white flash + scale pop.</summary>
public partial class DialogueChoiceRow : HBoxContainer
{
    private static readonly Color AccentCol = new("#ff4444");
    private static readonly Color TextCol = new("#e0d5c7");

    private HoldRing _ring;
    private Label _number;
    private Label _text;

    private float _progress;
    private bool _held;
    private bool _draining;

    public override void _Ready()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0, 32);
        AddThemeConstantOverride("separation", 12);

        _ring = new HoldRing { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        AddChild(_ring);

        _number = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        Style(_number, Ps1Ui.HintGray, 15);
        AddChild(_number);

        _text = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        Style(_text, TextCol, 17);
        AddChild(_text);
    }

    private static void Style(Label l, Color color, int size)
    {
        l.AddThemeFontOverride("font", Ps1Ui.GetFont(1));
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        l.AddThemeConstantOverride("outline_size", 5);
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

    /// <summary>Hold completed: text goes white, scale pop.</summary>
    public void PlaySelected()
    {
        SetProgress(1f);
        _draining = false;
        _text.AddThemeColorOverride("font_color", Colors.White);
        _number.AddThemeColorOverride("font_color", Colors.White);

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
}