using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class DialogueManager : Node
{
    public static DialogueManager Instance { get; private set; }
    private DialogueUI _ui;

    private Node3D _currentNpc;
    private Node3D _currentLinearNpc;
    private string _currentTreeId;
    private string _currentNodeId;
    private DialogueNode _currentNode;
    private Timer _distanceTimer;
    private Timer _totalTimeoutTimer;
    private Timer _repeatTimer;
    private bool _repeatInserted = false;
    private float _maxDistance = 10f;
    private string _nextNodeAfterAuto = null;

    public bool IsDialogueActive => _currentNpc != null || _currentLinearNpc != null;

    private List<DialogueResponse> _originalResponses;

    public override void _Ready()
    {
        Instance = this;
        
        // NEW: build all branching trees from pure data
        Dialogues.RegisterAllTrees();

        _ui = new DialogueUI();
        AddChild(_ui);
        _ui.Hide();

        _distanceTimer = new Timer { WaitTime = 10f, OneShot = false };
        _distanceTimer.Timeout += CheckDistance;
        AddChild(_distanceTimer);
    }

    // LINEAR DIALOGUE
    public void StartDialogue(string npcId, TimePeriod time, Node3D npc)
    {
        if (_currentLinearNpc != null && _currentLinearNpc != npc)
        {
            var oldInteraction = _currentLinearNpc.GetNodeOrNull<NpcInteraction>("Interaction");
            oldInteraction?.ForceEndDialogue();
        }

        if (_currentNpc != null)
        {
            ResetDialogueSystem();
        }

        _ui.DialogueClosed -= OnLinearDialogueClosed;
        _ui.DialogueClosed += OnLinearDialogueClosed;
        _currentLinearNpc = npc;

        if (_ui == null) return;

        List<string> lines;
        if (Dialogues.TimeBased.TryGetValue(npcId, out var timeMap) && timeMap.TryGetValue(time, out var timeLines))
        {
            lines = timeLines;
        }
        else if (Dialogues.Fallback.TryGetValue(npcId, out var fallbackLines))
        {
            lines = fallbackLines;
        }
        else
        {
            lines = new() { "Hello." };
        }

        _ui.ShowDialogue(lines);
    }

    // BRANCHING DIALOGUE
    // NEW: takes treeId (e.g. "kendall") instead of startNodeId, looks up the tree
    public void StartBranchingDialogue(string treeId, Node3D npc, float maxDistance = 10f, float totalTimeout = 60f)
    {
        if (!Dialogues.BranchingTrees.TryGetValue(treeId, out var tree))
        {
            GD.PushError($"Dialogue tree '{treeId}' not found!");
            return;
        }

        if (_currentNpc != null)
        {
            ResetDialogueSystem();
        }

        if (_currentLinearNpc != null)
        {
            var oldInteraction = _currentLinearNpc.GetNodeOrNull<NpcInteraction>("Interaction");
            oldInteraction?.ForceEndDialogue();
            _currentLinearNpc = null;
        }

        _currentNpc = npc;
        _currentTreeId = treeId;
        _maxDistance = maxDistance;
        _nextNodeAfterAuto = null;
        _repeatInserted = false;

        _repeatTimer?.Stop();
        _totalTimeoutTimer?.Stop();

        _totalTimeoutTimer = new Timer { WaitTime = totalTimeout, OneShot = true };
        _totalTimeoutTimer.Timeout += OnTotalTimeout;
        AddChild(_totalTimeoutTimer);
        _totalTimeoutTimer.Start();

        _distanceTimer.Start();

        // NEW: every tree must have an "intro" or "start" node, or we look for treeId + "_intro1"
        string startNode = tree.ContainsKey($"{treeId}_intro1") ? $"{treeId}_intro1" : $"{treeId}_start";
        ShowNode(treeId, startNode);
    }

    private void OnLinearDialogueClosed()
    {
        _ui.DialogueClosed -= OnLinearDialogueClosed;
        _currentLinearNpc = null;
    }

    private void ShowNode(string treeId, string nodeId)
    {
        _ui.ResponseChosen -= OnResponseChosen;
        _ui.DialogueAdvanced -= OnAutoAdvanceFinished;

        if (!Dialogues.BranchingTrees.TryGetValue(treeId, out var tree) || !tree.TryGetValue(nodeId, out var node))
        {
            EndDialogue();
            return;
        }

        // NEW: generic time-text injection, no hardcoded IDs
        if (node.InjectTimeText && Dialogues.TimeBased.TryGetValue(treeId, out var timeMap))
        {
            TimePeriod currentTime = TimeManager.Instance?.CurrentPeriod ?? TimePeriod.MORNING;
            if (timeMap.TryGetValue(currentTime, out var lines) && node.TimeTextIndex < lines.Count)
            {
                node.Text = lines[node.TimeTextIndex];
            }
        }

        _currentNodeId = nodeId;
        _currentNode = node;

        bool isDummyAuto = node.Responses.Count == 1 && string.IsNullOrEmpty(node.Responses[0].Text);
        if (isDummyAuto)
        {
            _nextNodeAfterAuto = node.Responses[0].NextNodeId;
            _ui.ShowAutoAdvanceLine(node.Text, 3f);
            _ui.DialogueAdvanced += OnAutoAdvanceFinished;
        }
        else if (node.Responses.Count == 0)
        {
            _nextNodeAfterAuto = null;
            _ui.ShowAutoAdvanceLine(node.Text, 3f);
            _ui.DialogueAdvanced += OnAutoAdvanceFinished;
        }
        else
        {
            _nextNodeAfterAuto = null;
            _originalResponses = new List<DialogueResponse>(node.Responses);
            _ui.ShowBranchingQuestion(node.Text, _originalResponses);
            _ui.ResponseChosen += OnResponseChosen;

            _repeatInserted = false;
            StartRepeatTimer();
        }
    }

    private void StartRepeatTimer()
    {
        _repeatTimer?.Stop();
        _repeatTimer = new Timer { WaitTime = 15f, OneShot = true };
        _repeatTimer.Timeout += OnRepeatTimeout;
        AddChild(_repeatTimer);
        _repeatTimer.Start();
    }

    private void OnAutoAdvanceFinished()
    {
        _ui.DialogueAdvanced -= OnAutoAdvanceFinished;
        if (!string.IsNullOrEmpty(_nextNodeAfterAuto))
        {
            string next = _nextNodeAfterAuto;
            _nextNodeAfterAuto = null;
            ShowNode(_currentTreeId, next);
        }
        else
        {
            EndDialogue();
        }
    }

    private void OnResponseChosen(int index)
    {
        if (_currentNode == null || _currentTreeId == null) return;

        var currentResponses = _ui.GetCurrentResponses();

        // Check if repeat option was chosen
        if (index < currentResponses.Count && currentResponses[index].Text == "Sorry, what did you ask?")
        {
            var withoutRepeat = currentResponses.Where(r => r.Text != "Sorry, what did you ask?").ToList();
            _ui.UpdateCurrentResponses(withoutRepeat);

            _ui.ShowAutoAdvanceLine(_currentNode.Text, 3f);
            _ui.DialogueAdvanced += () => {
                _ui.DialogueAdvanced -= OnAutoAdvanceFinished;
                _ui.ShowBranchingQuestion(_currentNode.Text, _originalResponses);
                _repeatInserted = false;
                StartRepeatTimer();
            };
            return;
        }

        if (index >= _currentNode.Responses.Count) return;
        var response = _currentNode.Responses[index];
        _repeatTimer?.Stop();
        if (string.IsNullOrEmpty(response.NextNodeId))
            EndDialogue();
        else
            ShowNode(_currentTreeId, response.NextNodeId);
    }

    private void OnRepeatTimeout()
    {
        if (_repeatInserted) return;
        _repeatInserted = true;

        var newResponses = new List<DialogueResponse> { new DialogueResponse { Text = "Sorry, what did you ask?", NextNodeId = null } };
        newResponses.AddRange(_originalResponses);
        _ui.UpdateCurrentResponses(newResponses);
    }

    private void CheckDistance()
    {
        if (_currentNpc == null) return;
        var player = GetTree().Root.FindChild("Player", true, false) as Node3D;
        if (player == null) return;
        if (player.GlobalPosition.DistanceTo(_currentNpc.GlobalPosition) > _maxDistance)
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
        _totalTimeoutTimer?.Stop();
        _repeatTimer?.Stop();
        _ui.CloseDialogue();

        if (_currentNpc != null)
        {
            var interaction = _currentNpc.GetNodeOrNull<NpcInteraction>("Interaction");
            interaction?.ForceEndDialogue();
            _currentNpc = null;
        }

        if (_currentLinearNpc != null)
        {
            var interaction = _currentLinearNpc.GetNodeOrNull<NpcInteraction>("Interaction");
            interaction?.ForceEndDialogue();
            _currentLinearNpc = null;
        }

        _currentTreeId = null;
        _currentNode = null;
        _currentNodeId = null;
        _nextNodeAfterAuto = null;
        _repeatInserted = false;
        _originalResponses = null;
    }

    public DialogueUI GetUI() => _ui;

    private void ResetDialogueSystem()
    {
        _distanceTimer?.Stop();
        _totalTimeoutTimer?.Stop();
        _repeatTimer?.Stop();

        if (_ui != null)
        {
            _ui.QueueFree();
            _ui = new DialogueUI();
            AddChild(_ui);
            _ui.Hide();
        }

        if (_currentNpc != null)
        {
            var oldInteraction = _currentNpc.GetNodeOrNull<NpcInteraction>("Interaction");
            oldInteraction?.ForceEndDialogue();
            _currentNpc = null;
        }

        if (_currentLinearNpc != null)
        {
            var oldInteraction = _currentLinearNpc.GetNodeOrNull<NpcInteraction>("Interaction");
            oldInteraction?.ForceEndDialogue();
            _currentLinearNpc = null;
        }

        _currentTreeId = null;
        _currentNode = null;
        _currentNodeId = null;
        _nextNodeAfterAuto = null;
        _repeatInserted = false;
        _originalResponses = null;
    }
}