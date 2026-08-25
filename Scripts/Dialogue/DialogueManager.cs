using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class DialogueManager : Node
{
    public static DialogueManager Instance { get; private set; }
    private DialogueUI _ui;
    private ChipVoicePlayer _voice;

    [Signal] public delegate void DialogueEndedEventHandler();

    private Node3D _currentNpc;
    private string _currentTreeId;
    private string _currentNodeId;
    private DialogueNode _currentNode;

    private Timer _distanceTimer;
    private Timer _totalTimeoutTimer;
    private Timer _repeatTimer;
    private bool _repeatInserted = false;
    private float _maxDistance = 10f;
    private List<DialogueResponse> _originalResponses;

    private Node3D _player;
    private Node3D _gazeGhost;
    private NpcEyeTracker _npcTracker;
    private GazeMode _eyesMode = GazeMode.Player;
    private GazeMode _headMode = GazeMode.Player;
    private float _ghostSide = 1f;

    public bool IsDialogueActive => _currentNpc != null;

    public override void _Ready()
    {
        Instance = this;
        DialogueScriptLoader.LoadAll();

        _ui = new DialogueUI();
        AddChild(_ui);
        _ui.Hide();

        _voice = new ChipVoicePlayer();
        AddChild(_voice);
        _ui.SetVoice(_voice);

        _distanceTimer = new Timer { WaitTime = 10f, OneShot = false };
        _distanceTimer.Timeout += CheckDistance;
        AddChild(_distanceTimer);

        _totalTimeoutTimer = new Timer { OneShot = true };
        _totalTimeoutTimer.Timeout += OnTotalTimeout;
        AddChild(_totalTimeoutTimer);

        _repeatTimer = new Timer { OneShot = true };
        _repeatTimer.Timeout += OnRepeatTimeout;
        AddChild(_repeatTimer);
    }

    // Same signature as before — NpcInteraction needs no changes.
    public void StartDialogue(string npcId, TimePeriod time, Node3D npc, float maxDistance = 10f, float totalTimeout = 60f)
    {
        if (_currentNpc != null) ResetDialogueSystem();

        _player = GetTree().Root.FindChild("Player", true, false) as Node3D;
        _currentNpc = npc;
        _currentTreeId = npcId;
        _maxDistance = maxDistance;
        _repeatInserted = false;

        // --- voice ---
        var (mbti, predis, gender) = GetVoiceKey(npcId);
        _voice.SetProfile(VoiceStudio.Get(npcId, mbti, predis, gender));
        _voice.ResetLine();

        // --- gaze ---
        _npcTracker = npc.GetNodeOrNull<NpcEyeTracker>("EyeTrackerComponent");
        EnsureGazeGhost();

        _totalTimeoutTimer.WaitTime = totalTimeout;
        _totalTimeoutTimer.Start();
        _distanceTimer.Start();

        if (!Dialogues.BranchingTrees.TryGetValue(npcId, out var tree))
        {
            GD.PushError($"No dialogue tree found for NPC '{npcId}' (check res://Dialogue/Scripts/)");
            EndDialogue();
            return;
        }

        string startNode =
            Dialogues.StartNodes.TryGetValue(npcId, out var s) ? s :
            tree.ContainsKey($"{npcId}_intro1") ? $"{npcId}_intro1" :
            tree.ContainsKey($"{npcId}_start") ? $"{npcId}_start" :
            tree.Count > 0 ? tree.Keys.First() : null;

        if (startNode == null) { EndDialogue(); return; }
        ShowNode(npcId, startNode);
    }

    private (MbtiType, Predisposition, VoiceGender) GetVoiceKey(string npcId)
    {
        if (Dialogues.NpcVoiceConfig.TryGetValue(npcId, out var cfg)) return cfg;
        uint h = VoiceStudio.Fnv1a(npcId); // deterministic per name (string.GetHashCode is randomized per run!)
        return ((MbtiType)(h % 16), (Predisposition)((h >> 4) % 3), (VoiceGender)((h >> 6) % 2));
    }

    // ---------------- node flow ----------------

    private void ShowNode(string treeId, string nodeId)
    {
        _ui.ResponseChosen -= OnResponseChosen;

        if (!Dialogues.BranchingTrees.TryGetValue(treeId, out var tree) || !tree.TryGetValue(nodeId, out var node))
        {
            EndDialogue();
            return;
        }

        _currentNodeId = nodeId;
        _currentNode = node;

        var paragraphs = new List<DialogueParagraph>();
        if (node.TimeUseIndex >= 0)
        {
            TimePeriod now = TimeManager.Instance?.CurrentPeriod ?? TimePeriod.MORNING;
            if (Dialogues.TimeBased.TryGetValue(treeId, out var perPeriod) &&
                perPeriod.TryGetValue(now, out var lines) &&
                node.TimeUseIndex < lines.Count)
            {
                paragraphs.Add(new DialogueParagraph
                {
                    Text = lines[node.TimeUseIndex],
                    Eyes = node.NodeEyes,
                    Head = node.NodeHead
                });
            }
            else
            {
                // no line for this period → skip node
                string skipTo = node.Responses.Count > 0 ? node.Responses[0].NextNodeId : null;
                if (!string.IsNullOrEmpty(skipTo)) ShowNode(treeId, skipTo); else EndDialogue();
                return;
            }
        }
        else paragraphs.AddRange(node.Paragraphs);

        if (paragraphs.Count == 0)
        {
            string skipTo = node.Responses.Count > 0 ? node.Responses[0].NextNodeId : null;
            if (!string.IsNullOrEmpty(skipTo)) { ShowNode(treeId, skipTo); return; }
            EndDialogue();
            return;
        }

        bool autoNext = node.Responses.Count == 1 && string.IsNullOrEmpty(node.Responses[0].Text);

        _ui.PlayParagraphs(node, paragraphs, ApplyGaze, () =>
        {
            if (autoNext)
            {
                var next = node.Responses[0].NextNodeId;
                if (string.IsNullOrEmpty(next)) EndDialogue();
                else ShowNode(treeId, next);
            }
            else if (node.Responses.Count == 0)
            {
                EndDialogue();
            }
            else
            {
                _originalResponses = new List<DialogueResponse>(node.Responses);
                _ui.ShowOptions(node.Responses);
                _ui.ResponseChosen += OnResponseChosen;
                _repeatInserted = false;
                StartRepeatTimer();
            }
        });
    }

    private void OnResponseChosen(int index)
    {
        _ui.ResponseChosen -= OnResponseChosen;
        var shown = _ui.GetCurrentResponses();
        if (shown == null || index < 0 || index >= shown.Count) return;

        var chosen = shown[index];
        _repeatTimer.Stop();

        if (chosen.Text == Dialogues.RepeatOptionText)
        {
            ShowNode(_currentTreeId, _currentNodeId); // replay the node
            return;
        }

        if (string.IsNullOrEmpty(chosen.NextNodeId)) EndDialogue();
        else ShowNode(_currentTreeId, chosen.NextNodeId);
    }

    private void StartRepeatTimer()
    {
        _repeatTimer.Stop();
        _repeatTimer.WaitTime = 15f;
        _repeatTimer.Start();
    }

    private void OnRepeatTimeout()
    {
        if (_repeatInserted || _originalResponses == null) return;
        _repeatInserted = true;
        var list = new List<DialogueResponse> { new() { Text = Dialogues.RepeatOptionText, NextNodeId = null } };
        list.AddRange(_originalResponses);
        _ui.UpdateCurrentResponses(list);
    }

    // ---------------- gaze ----------------
    // "Away" uses an invisible ghost Node3D as the tracker target
    // If BOTH eyes+head are Away, the ghost
    // sits off in the distance; if only one is Away, the ghost sits beside the
    // player's head → head stays roughly on the player while eyes glance aside.

    private void ApplyGaze(DialogueNode node, DialogueParagraph p)
    {
        var eyes = p.Eyes != GazeMode.Unset ? p.Eyes : node.NodeEyes != GazeMode.Unset ? node.NodeEyes : GazeMode.Player;
        var head = p.Head != GazeMode.Unset ? p.Head : node.NodeHead != GazeMode.Unset ? node.NodeHead : GazeMode.Player;

        _eyesMode = eyes;
        _headMode = head;
        if (eyes == GazeMode.Away || head == GazeMode.Away)
            _ghostSide = -_ghostSide; // alternate the glance side paragraph to paragraph

        if (_npcTracker == null) return;
        _npcTracker.EnableEyeTracking = eyes != GazeMode.Off;
        _npcTracker.EnableHeadTracking = head != GazeMode.Off;

        if (eyes == GazeMode.Away || head == GazeMode.Away)
            _npcTracker.Target = _gazeGhost;
        else if (_player != null)
            _npcTracker.Target = _player;
    }

    private void EnsureGazeGhost()
    {
        if (_gazeGhost != null && GodotObject.IsInstanceValid(_gazeGhost)) return;
        _gazeGhost = new Node3D { Name = "DialogueGazeGhost" };
        (GetTree().CurrentScene ?? (Node)GetTree().Root).AddChild(_gazeGhost);
    }

    public override void _Process(double delta)
    {
        if (_currentNpc == null || _gazeGhost == null || !GodotObject.IsInstanceValid(_gazeGhost)) return;

        bool eyesAway = _eyesMode == GazeMode.Away;
        bool headAway = _headMode == GazeMode.Away;
        if (!eyesAway && !headAway) return;

        if (eyesAway && headAway)
        {
            var b = _currentNpc.GlobalTransform.Basis;
            _gazeGhost.GlobalPosition = _currentNpc.GlobalPosition
                + (-b.Z * 5f + b.X * 2.2f * _ghostSide) + Vector3.Up * 0.6f;
        }
        else if (_player != null && GodotObject.IsInstanceValid(_player))
        {
            _gazeGhost.GlobalPosition = _player.GlobalPosition
                + _player.GlobalTransform.Basis.X * (1.35f * _ghostSide) + Vector3.Up * 0.25f;
        }
    }

    private void RestoreGaze()
    {
        if (_npcTracker != null && GodotObject.IsInstanceValid(_npcTracker))
        {
            _npcTracker.EnableEyeTracking = true;
            _npcTracker.EnableHeadTracking = true;
            if (_player != null && GodotObject.IsInstanceValid(_player))
                _npcTracker.Target = _player;
        }
        _npcTracker = null;
        _eyesMode = GazeMode.Player;
        _headMode = GazeMode.Player;
    }

    // ---------------- lifecycle ----------------

    private void CheckDistance()
    {
        if (_currentNpc == null) return;
        if (_player == null) _player = GetTree().Root.FindChild("Player", true, false) as Node3D;
        if (_player == null) return;
        if (_player.GlobalPosition.DistanceTo(_currentNpc.GlobalPosition) > _maxDistance)
        {
            _ui.ShowTemporaryLine("Hey, where are you going?", 2f);
            EndDialogue();
        }
    }

    private void OnTotalTimeout()
    {
        _ui.ShowTemporaryLine("Nevermind, you're not listening.", 2f);
        EndDialogue();
    }

    public void CancelDialogue() => EndDialogue();

    private void EndDialogue()
    {
        _distanceTimer.Stop();
        _totalTimeoutTimer.Stop();
        _repeatTimer.Stop();
        RestoreGaze();
        _ui.CloseDialogue();
        _currentNpc = null;
        _currentTreeId = null;
        _currentNode = null;
        _currentNodeId = null;
        _repeatInserted = false;
        _originalResponses = null;
        EmitSignal(SignalName.DialogueEnded);
    }

    private void ResetDialogueSystem()
    {
        var npc = _currentNpc;
        EndDialogue();
        npc?.GetNodeOrNull<NpcInteraction>("Interaction")?.ForceEndDialogue();
    }
}