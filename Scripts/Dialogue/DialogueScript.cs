using Godot;
using System.Collections.Generic;

public enum GazeMode { Unset, Off, Player, Away }

public class DialogueCommand
{
    public string Name;
    public string[] Args = System.Array.Empty<string>();
}

public class DialogueFlagOp { public string Flag; public bool Value; }
public class DialogueCondition { public string Flag; public bool Required; }

public class SpeakerInfo
{
    public string Name;
    public string ColorHex = "#e0d5c7";
    public Color Color => new(ColorHex);
}

public class DialogueParagraph
{
    public string Text = "";
    public GazeMode Eyes = GazeMode.Unset;
    public GazeMode Head = GazeMode.Unset;
    public string Speaker = null;                       // null = tree owner (group chats override per paragraph)
    public List<DialogueCommand> Commands = new();      // fire when this paragraph starts
}

public class DialogueResponse
{
    public string Text = "";
    public string NextNodeId = null;
    public List<DialogueCondition> Requirements = new();
    public List<DialogueFlagOp> SetFlags = new();       // applied when chosen
}

public class DialogueNode
{
    public string Id = "";
    public List<DialogueParagraph> Paragraphs = new();
    public int TimeUseIndex = -1;
    public GazeMode NodeEyes = GazeMode.Unset;
    public GazeMode NodeHead = GazeMode.Unset;
    public string DefaultSpeaker = null;
    public List<DialogueCondition> Conditions = new();      // unmet -> node skipped
    public List<DialogueFlagOp> EnterFlags = new();         // set when node is entered
    public List<(string Target, float Delta)> RelDeltas = new();
    public List<DialogueCommand> EnterCommands = new();     // fire on node enter
    public List<DialogueCommand> ParagraphCommands = new(); // attach to @usetime paragraphs
    public List<DialogueResponse> Responses = new();
}