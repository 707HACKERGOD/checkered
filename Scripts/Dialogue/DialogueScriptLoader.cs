using Godot;
using System.Collections.Generic;
using System.Linq;

public static class DialogueScriptLoader
{
    public static void LoadAll(string dir = "res://Assets/Dialogue/Scripts")
    {
        DirAccess da = DirAccess.Open(dir);
        if (da == null) { GD.PushError($"DialogueScriptLoader: can't open '{dir}'"); return; }
        da.ListDirBegin();
        string file = da.GetNext();
        while (!string.IsNullOrEmpty(file))
        {
            if (file.EndsWith(".txt")) ParseFile($"{dir}/{file}");
            file = da.GetNext();
        }
        da.ListDirEnd();
        GD.Print($"DialogueScriptLoader: loaded {Dialogues.BranchingTrees.Count} trees");
    }

    private static void ParseFile(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) { GD.PushError($"DialogueScriptLoader: can't read '{path}'"); return; }

        string treeId = null;
        Dictionary<string, DialogueNode> tree = null;
        DialogueNode node = null;
        List<string> timeList = null;

        GazeMode pEyes = GazeMode.Unset, pHead = GazeMode.Unset;
        string pSpeaker = null;
        List<DialogueCommand> pCmds = new();

        int lineNo = 0;
        while (!f.EofReached())
        {
            string line = f.GetLine().Trim();
            lineNo++;
            if (line.Length == 0 || line.StartsWith("#")) continue;

            if (line.StartsWith("@"))
            {
                string[] parts = line.Substring(1).Split(' ', 2);
                string cmd = parts[0].ToLowerInvariant();
                string arg = parts.Length > 1 ? parts[1].Trim() : "";
                string[] tokens = arg.Length > 0 ? arg.Split(' ') : new string[0];

                switch (cmd)
                {
                    case "tree":
                        Flush(node, ref pEyes, ref pHead, ref pSpeaker, ref pCmds);
                        treeId = arg;
                        tree = new Dictionary<string, DialogueNode>();
                        Dialogues.BranchingTrees[treeId] = tree;
                        node = null; timeList = null;
                        break;

                    case "speaker":
                        if (tokens.Length == 1)
                        {
                            if (node != null) pSpeaker = tokens[0];
                        }
                        else
                        {
                            string colorHex = tokens[1].StartsWith("#") ? tokens[1] : "#e0d5c7";
                            string name = tokens[1].StartsWith("#")
                                ? string.Join(" ", tokens.Skip(2)) : string.Join(" ", tokens.Skip(1));
                            if (name.Length == 0) name = tokens[0];
                            Dialogues.Speakers[tokens[0]] = new SpeakerInfo { Name = name, ColorHex = colorHex };
                        }
                        break;

                    case "entry":
                    case "start":   // legacy = unconditional entry
                        if (treeId == null) { GD.PushError($"{path}:{lineNo}: '@{cmd}' before '@tree'"); break; }
                        if (!Dialogues.Entries.TryGetValue(treeId, out var elist))
                        { elist = new List<(string, List<DialogueCondition>)>(); Dialogues.Entries[treeId] = elist; }
                        elist.Add((tokens[0], ParseConditions(tokens.Skip(1))));
                        break;

                    case "node":
                        Flush(node, ref pEyes, ref pHead, ref pSpeaker, ref pCmds);
                        if (tree == null) { GD.PushError($"{path}:{lineNo}: '@node' before '@tree'"); break; }
                        node = new DialogueNode { Id = tokens[0] };
                        node.Conditions.AddRange(ParseConditions(tokens.Skip(1)));
                        tree[tokens[0]] = node;
                        timeList = null;
                        break;

                    case "time":
                        Flush(node, ref pEyes, ref pHead, ref pSpeaker, ref pCmds);
                        if (treeId == null) { GD.PushError($"{path}:{lineNo}: '@time' before '@tree'"); break; }
                        if (!Dialogues.TimeBased.TryGetValue(treeId, out var perPeriod))
                        { perPeriod = new Dictionary<TimePeriod, List<string>>(); Dialogues.TimeBased[treeId] = perPeriod; }
                        timeList = new List<string>();
                        perPeriod[ParsePeriod(arg, path, lineNo)] = timeList;
                        node = null;
                        break;

                    case "eyes": pEyes = ParseGaze(arg, path, lineNo); break;
                    case "head": pHead = ParseGaze(arg, path, lineNo); break;

                    case "emotion":
                    case "anim":
                    case "state":
                        pCmds.Add(new DialogueCommand { Name = cmd, Args = new[] { arg } });
                        break;

                    case "do":
                        var dt = arg.Split(' ');
                        if (dt.Length > 0)
                            pCmds.Add(new DialogueCommand { Name = dt[0], Args = dt.Skip(1).ToArray() });
                        break;

                    case "set":
                        if (node != null) node.EnterFlags.Add(new DialogueFlagOp { Flag = arg, Value = true });
                        break;
                    case "unset":
                        if (node != null) node.EnterFlags.Add(new DialogueFlagOp { Flag = arg, Value = false });
                        break;

                    case "rel":
                        if (node != null && tokens.Length == 2 && float.TryParse(tokens[1], out var d))
                            node.RelDeltas.Add((tokens[0], d));
                        break;

                    case "usetime":
                        if (node != null && int.TryParse(arg, out var idx)) node.TimeUseIndex = idx;
                        break;

                    case "end": break;
                    default:
                        GD.PushWarning($"{path}:{lineNo}: unknown command '@{cmd}'");
                        break;
                }
                continue;
            }

            if (line.StartsWith("->"))
            {
                string body = line.Substring(2).Trim();
                if (node == null) { GD.PushError($"{path}:{lineNo}: '->' outside a node"); continue; }

                var resp = new DialogueResponse();
                if (body.StartsWith("\""))
                {
                    int close = body.IndexOf('"', 1);
                    if (close == -1) { GD.PushError($"{path}:{lineNo}: unterminated option text"); continue; }
                    resp.Text = body.Substring(1, close - 1);
                    body = body.Substring(close + 1).Trim();
                }
                var toks = body.Split(' ').Where(t => t.Length > 0).ToArray();
                if (toks.Length == 0) { GD.PushError($"{path}:{lineNo}: option missing target"); continue; }
                resp.NextNodeId = toks[0] == "end" ? null : toks[0];
                ApplyResponseMods(resp, toks.Skip(1), path, lineNo);
                node.Responses.Add(resp);
                continue;
            }

            if (line.StartsWith("\"") && line.EndsWith("\"") && line.Length >= 2)
            {
                string text = line.Substring(1, line.Length - 2);
                if (timeList != null) timeList.Add(text);
                else if (node != null)
                {
                    node.Paragraphs.Add(new DialogueParagraph
                    {
                        Text = text, Eyes = pEyes, Head = pHead,
                        Speaker = pSpeaker, Commands = pCmds
                    });
                    pCmds = new List<DialogueCommand>();
                    pEyes = pHead = GazeMode.Unset;
                    pSpeaker = null;
                }
                else GD.PushWarning($"{path}:{lineNo}: text outside a node");
                continue;
            }

            GD.PushWarning($"{path}:{lineNo}: unrecognized line '{line}'");
        }
        Flush(node, ref pEyes, ref pHead, ref pSpeaker, ref pCmds);
    }

    private static void Flush(DialogueNode node, ref GazeMode eyes, ref GazeMode head,
                              ref string speaker, ref List<DialogueCommand> cmds)
    {
        if (node != null)
        {
            if (eyes != GazeMode.Unset && node.NodeEyes == GazeMode.Unset) node.NodeEyes = eyes;
            if (head != GazeMode.Unset && node.NodeHead == GazeMode.Unset) node.NodeHead = head;
            if (speaker != null && node.DefaultSpeaker == null) node.DefaultSpeaker = speaker;
            (node.TimeUseIndex >= 0 ? node.ParagraphCommands : node.EnterCommands).AddRange(cmds);
        }
        eyes = head = GazeMode.Unset;
        speaker = null;
        cmds = new List<DialogueCommand>();
    }

    private static List<DialogueCondition> ParseConditions(IEnumerable<string> toks)
    {
        var list = new List<DialogueCondition>();
        foreach (var t in toks)
        {
            if (t.StartsWith("?")) list.Add(new DialogueCondition { Flag = t.Substring(1), Required = true });
            else if (t.StartsWith("!")) list.Add(new DialogueCondition { Flag = t.Substring(1), Required = false });
        }
        return list;
    }

    private static void ApplyResponseMods(DialogueResponse r, IEnumerable<string> toks, string path, int lineNo)
    {
        foreach (var t in toks)
        {
            if (t.StartsWith("?")) r.Requirements.Add(new DialogueCondition { Flag = t.Substring(1), Required = true });
            else if (t.StartsWith("!")) r.Requirements.Add(new DialogueCondition { Flag = t.Substring(1), Required = false });
            else if (t.StartsWith("+")) r.SetFlags.Add(new DialogueFlagOp { Flag = t.Substring(1), Value = true });
            else if (t.StartsWith("-")) r.SetFlags.Add(new DialogueFlagOp { Flag = t.Substring(1), Value = false });
            else GD.PushWarning($"{path}:{lineNo}: unknown option modifier '{t}'");
        }
    }

    private static TimePeriod ParsePeriod(string s, string path, int lineNo)
    {
        switch (s.ToLowerInvariant())
        {
            case "morning": return TimePeriod.MORNING;
            case "afternoon": return TimePeriod.AFTERNOON;
            case "evening": return TimePeriod.EVENING;
            case "night": return TimePeriod.NIGHT;
            default:
                GD.PushWarning($"{path}:{lineNo}: bad period '{s}', defaulting to MORNING");
                return TimePeriod.MORNING;
        }
    }

    private static GazeMode ParseGaze(string s, string path, int lineNo)
    {
        switch (s.ToLowerInvariant())
        {
            case "player": case "on": case "at": return GazeMode.Player;
            case "away": return GazeMode.Away;
            case "off": case "none": return GazeMode.Off;
            default:
                GD.PushWarning($"{path}:{lineNo}: bad gaze '{s}', defaulting to Player");
                return GazeMode.Player;
        }
    }
}