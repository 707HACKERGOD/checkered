// Scripts/NpcInteraction.cs
using Godot;
public partial class NpcInteraction : Node
{
    public string NpcName;
    public bool IsDead;
    
    public void Interact()
    {
        GD.Print($"Talked to {NpcName}");
        // Later: start dialogue
    }
}