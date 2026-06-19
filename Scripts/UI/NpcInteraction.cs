using Godot;

public partial class NpcInteraction : Node
{
    [Export] public NpcNavCombat NavCombat;
    public string NpcName;
    public bool IsDead;
    public bool IsInDialogue { get; private set; }

    private NpcController _npcController;

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

            // UNIFIED: All NPCs use StartDialogue now
            TimePeriod currentTime = TimeManager.Instance?.CurrentPeriod ?? TimePeriod.AFTERNOON;
            DialogueManager.Instance.StartDialogue(npcId, currentTime, npcNode);

            // Subscribe to DialogueManager's end event instead of UI signal
            DialogueManager.Instance.DialogueEnded -= OnDialogueEnded;
            DialogueManager.Instance.DialogueEnded += OnDialogueEnded;
        }
        else
        {
            GD.Print($"Talked to {NpcName}");
        }
    }

    private void OnDialogueEnded()
    {
        IsInDialogue = false;
        NavCombat?.EndDialogue();

        DialogueManager.Instance.DialogueEnded -= OnDialogueEnded;
    }

    public void ForceEndDialogue()
    {
        if (IsInDialogue)
        {
            IsInDialogue = false;
            NavCombat?.EndDialogue();
        }

        DialogueManager.Instance.DialogueEnded -= OnDialogueEnded;
    }
}