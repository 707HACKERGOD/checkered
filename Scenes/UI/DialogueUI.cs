using Godot;
using System.Collections.Generic;

public partial class DialogueUI : CanvasLayer
{
    [Signal] public delegate void DialogueClosedEventHandler();
    [Signal] public delegate void DialogueAdvancedEventHandler();
    [Signal] public delegate void ResponseChosenEventHandler(int index);

    [Export] public float LineDuration = 3.0f;
    private const float HOLD_DURATION = 0.5f;

    private Label _label;
    private Timer _timer;
    private Tween _fadeTween;
    private List<string> _lines;
    private int _currentLine;
    private bool _isBranching = false;
    private List<DialogueResponse> _pendingResponses;
    private bool _waitingForOptions = false;

    private Timer _holdTimer;
    private int _heldKeyIndex = -1;
    private bool _keyReleased = false;

    public override void _Ready()
    {
        _label = new Label();
        _label.Name = "DialogueLabel";
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.AnchorLeft = 0.5f;
        _label.AnchorRight = 0.5f;
        _label.AnchorTop = 1.0f;
        _label.AnchorBottom = 1.0f;
        _label.OffsetLeft = -400;
        _label.OffsetRight = 400;
        _label.OffsetTop = -200;
        _label.OffsetBottom = -100;
        _label.Modulate = Colors.Transparent;

        _label.AutowrapMode = TextServer.AutowrapMode.Word;
        _label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        _label.AddThemeColorOverride("font_color", new Color(0.88f, 0.84f, 0.78f));
        _label.AddThemeFontSizeOverride("font_size", 28);
        var font = ResourceLoader.Load<Font>("res://Assets/UI/PublicPixel-rv0pA.ttf");
        if (font != null) _label.AddThemeFontOverride("font", font);
        _label.AddThemeConstantOverride("outline_size", 2);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(_label);

        _timer = new Timer();
        _timer.OneShot = true;
        _timer.Timeout += NextLine;
        AddChild(_timer);

        _holdTimer = new Timer { OneShot = true, WaitTime = HOLD_DURATION };
        _holdTimer.Timeout += OnHoldComplete;
        AddChild(_holdTimer);

        Hide();
    }

    // Linear dialogue
    public void ShowDialogue(List<string> lines)
    {
        ResetUI();
        _isBranching = false;
        _waitingForOptions = false;
        _lines = lines;
        _currentLine = 0;
        ShowLine();
        FadeIn();
    }

    private void ShowLine()
    {
        if (_lines == null || _currentLine >= _lines.Count)
        {
            EndLinear();
            return;
        }
        _label.Text = _lines[_currentLine];
        _timer.Start(LineDuration);
    }

    private void NextLine()
    {
        if (_waitingForOptions) return;

        if (_isBranching && _pendingResponses != null)
        {
            ShowOptionsParagraph();
            return;
        }
        else if (_isBranching)
        {
            EmitSignal(SignalName.DialogueAdvanced);
            return;
        }
        else
        {
            _currentLine++;
            ShowLine();
        }
    }

    private void EndLinear()
    {
        _lines = null;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_label, "modulate:a", 0f, 0.5f);
        _fadeTween.TweenCallback(Callable.From(() => {
            Hide();
            EmitSignal(SignalName.DialogueClosed);
        }));
    }

    private void FadeIn()
    {
        Show();
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_label, "modulate:a", 1f, 0.2f);
    }

    // Branching dialogue
    public void ShowBranchingQuestion(string text, List<DialogueResponse> responses)
    {
        ResetUI();
        _isBranching = true;
        _waitingForOptions = false;
        _pendingResponses = responses;
        _label.Text = text;
        FadeIn();
        _timer.Start(LineDuration);
    }

    private void ShowOptionsParagraph()
    {
        string optionsText = "";
        for (int i = 0; i < _pendingResponses.Count; i++)
        {
            optionsText += $"{i + 1}. {_pendingResponses[i].Text}\n";
        }
        _label.Text = optionsText.TrimEnd('\n');
        _timer.Stop();
        _waitingForOptions = true;
    }

    public void SelectResponse(int index)
    {
        if (!_waitingForOptions) return;
        if (index < 0 || index >= _pendingResponses.Count) return;
        _waitingForOptions = false;
        _holdTimer.Stop();
        _heldKeyIndex = -1;
        EmitSignal(SignalName.ResponseChosen, index);
    }

    // Auto-advance line
    public void ShowAutoAdvanceLine(string text, float duration)
    {
        ResetUI();
        _isBranching = true;
        _waitingForOptions = false;
        _pendingResponses = null;
        _label.Text = text;
        FadeIn();
        var t = new Timer { OneShot = true, WaitTime = duration };
        t.Timeout += () => {
            EmitSignal(SignalName.DialogueAdvanced);
            t.QueueFree();
        };
        AddChild(t);
        t.Start();
    }

    // Temporary farewell line
    public void ShowTemporaryLine(string text, float duration)
    {
        _label.Text = text;
        FadeIn();
        var tween = CreateTween();
        tween.TweenInterval(duration);
        tween.TweenCallback(Callable.From(() => {
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(_label, "modulate:a", 0f, 0.5f);
            _fadeTween.TweenCallback(Callable.From(() => Hide()));
        }));
    }

    // Input: X cancels, number keys with hold
    public override void _Input(InputEvent @event)
    {
        // Cancel with X key (always works)
        if (@event.IsActionPressed("cancel_dialogue"))
        {
            DialogueManager.Instance.CancelDialogue();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_waitingForOptions) return;

        for (int i = 0; i < 4; i++)
        {
            var action = $"dialogue_{i + 1}";
            if (@event.IsActionPressed(action))
            {
                _heldKeyIndex = i;
                _keyReleased = false;
                _holdTimer.Start();
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (@event.IsActionReleased(action))
            {
                if (_heldKeyIndex == i)
                {
                    _keyReleased = true;
                    _holdTimer.Stop();
                    _heldKeyIndex = -1;
                }
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    private void OnHoldComplete()
    {
        if (_heldKeyIndex >= 0 && !_keyReleased)
            SelectResponse(_heldKeyIndex);
        _heldKeyIndex = -1;
    }

    public void CloseDialogue()
    {
        EmitSignal(SignalName.DialogueClosed);
        Hide();
    }

    public new void Hide()
    {
        base.Hide();
        _waitingForOptions = false;
        _timer.Stop();
        _holdTimer.Stop();
        _heldKeyIndex = -1;
        _lines = null;
        _pendingResponses = null;
        _isBranching = false;
        _currentLine = 0;
    }

    public List<DialogueResponse> GetCurrentResponses()
    {
        return _pendingResponses;
    }

    public void UpdateCurrentResponses(List<DialogueResponse> newResponses)
    {
        _pendingResponses = newResponses;
        RefreshOptions();
    }

    public void RefreshOptions()
    {
        if (_waitingForOptions && _pendingResponses != null)
        {
            string optionsText = "";
            for (int i = 0; i < _pendingResponses.Count; i++)
            {
                optionsText += $"{i + 1}. {_pendingResponses[i].Text}\n";
            }
            _label.Text = optionsText.TrimEnd('\n');
        }
    }

    public void ResetUI()
    {
        // Stop all timers
        _timer?.Stop();
        _holdTimer?.Stop();
        // Kill tweens
        _fadeTween?.Kill();
        // Clear all dialogue data
        _lines = null;
        _pendingResponses = null;
        _currentLine = 0;
        _isBranching = false;
        _waitingForOptions = false;
        _heldKeyIndex = -1;
        _keyReleased = false;
        // Clear any auto-advance timers created dynamically
        foreach (Node child in GetChildren())
        {
            if (child is Timer t && t != _timer && t != _holdTimer)
                t.QueueFree();
        }
        // Hide the UI completely
        Hide();
    }
}