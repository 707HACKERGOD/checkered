using Godot;

public partial class NpcInteraction : Node
{
    [Export] public NpcNavCombat NavCombat;
    public string NpcName;
    public bool IsDead;
    public bool IsInDialogue { get; private set; }

    private NpcController _npcController;
    private DialogueUI _subscribedUI;

    public override void _Ready()
    {
        if (NavCombat == null)
        {
            var parent = GetParent<CharacterBody3D>();
            NavCombat = parent?.GetNodeOrNull<NpcNavCombat>("NPCNavCombat");
        }
        _npcController = GetParent<NpcController>();
    }

    public void Interact()
    {
        if (IsDead || IsInDialogue) return;

        if (DialogueManager.Instance.IsDialogueActive)
        {
            DialogueManager.Instance.CancelDialogue();
        }

        if (NavCombat != null && !NavCombat.IsInDialogue)
        {
            IsInDialogue = true;
            NavCombat.StartDialogue();

            string npcId = NpcName.Trim().ToLower();
            var npcNode = GetParent<Node3D>();

            if (npcId == "kendall")
            {
                // NEW: pass tree ID, not raw node ID. Manager finds intro/start automatically.
                DialogueManager.Instance.StartBranchingDialogue("kendall", npcNode, 10f, 60f);
            }
            else
            {
                TimePeriod currentTime = TimeManager.Instance?.CurrentPeriod ?? TimePeriod.AFTERNOON;
                DialogueManager.Instance.StartDialogue(npcId, currentTime, npcNode);
            }

            _subscribedUI = DialogueManager.Instance.GetUI();

            if (_subscribedUI != null)
            {
                _subscribedUI.DialogueClosed -= OnDialogueClosed;
                _subscribedUI.DialogueClosed += OnDialogueClosed;
            }
        }
        else
        {
            GD.Print($"Talked to {NpcName}");
        }
    }

    private void OnDialogueClosed()
    {
        IsInDialogue = false;
        NavCombat?.EndDialogue();

        if (_subscribedUI != null)
        {
            _subscribedUI.DialogueClosed -= OnDialogueClosed;
            _subscribedUI = null;
        }
    }

    public void ForceEndDialogue()
    {
        if (IsInDialogue)
        {
            IsInDialogue = false;
            NavCombat?.EndDialogue();
        }

        if (_subscribedUI != null)
        {
            _subscribedUI.DialogueClosed -= OnDialogueClosed;
            _subscribedUI = null;
        }
    }
}