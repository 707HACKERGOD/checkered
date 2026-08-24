using System.Collections.Generic;

public enum GazeMode { Unset, Off, Player, Away }

public class DialogueParagraph
{
    public string Text = "";
    public GazeMode Eyes = GazeMode.Unset;
    public GazeMode Head = GazeMode.Unset;
}

public class DialogueResponse
{
    public string Text = "";
    public string NextNodeId = null; // null = end dialogue
}

public class DialogueNode
{
    public string Id = "";
    public List<DialogueParagraph> Paragraphs = new();
    public int TimeUseIndex = -1;                 // >= 0 → text injected from the @time block
    public GazeMode NodeEyes = GazeMode.Unset;    // node-level default (used by @usetime nodes)
    public GazeMode NodeHead = GazeMode.Unset;
    public List<DialogueResponse> Responses = new();
}