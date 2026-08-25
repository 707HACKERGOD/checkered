using Godot;
using System.Collections.Generic;
public static class DialogueScriptLoader
{
    public static void LoadAll(string dir = "res://Assets/Dialogue")
    {
        DirAccess da = DirAccess.Open(dir);
        if (da == null)
        {
            GD.PushError($"DialogueScriptLoader: can't open '{dir}' (create the folder and add .txt files)");
            return;
        }
        da.ListDirBegin();
        string file = da.GetNext();
        while (!string.IsNullOrEmpty(file))
        {
            if (file.EndsWith(".txt")) ParseFile($"{dir}/{file}");
            file = da.GetNext();
        }
        da.ListDirEnd();
        GD.Print($"DialogueScriptLoader: loaded {Dialogues.BranchingTrees.Count} dialogue trees");
    }

    private static void ParseFile(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) { GD.PushError($"DialogueScriptLoader: can't read '{path}'"); return; }

        string treeId = null;
        Dictionary<string, DialogueNode> tree = null;
        DialogueNode node = null;
        List<string> timeList = null;                    // non-null while inside a @time block
        GazeMode pendingEyes = GazeMode.Unset, pendingHead = GazeMode.Unset;

        int lineNo = 0;
        while (!f.EofReached())
        {
            string line = f.GetLine().Trim();
            lineNo++;
            if (line.Length == 0 || line.StartsWith("#")) continue;

            // ---- @commands ----
            if (line.StartsWith("@"))
            {
                string[] parts = line.Substring(1).Split(' ', 2);
                string cmd = parts[0].ToLowerInvariant();
                string arg = parts.Length > 1 ? parts[1].Trim() : "";

                switch (cmd)
                {
                    case "tree":
                        FlushPending(node, ref pendingEyes, ref pendingHead);
                        treeId = arg;
                        tree = new Dictionary<string, DialogueNode>();
                        Dialogues.BranchingTrees[treeId] = tree;
                        node = null; timeList = null;
                        break;

                    case "start":
                        if (treeId != null) Dialogues.StartNodes[treeId] = arg;
                        break;

                    case "node":
                        FlushPending(node, ref pendingEyes, ref pendingHead);
                        if (tree == null) { GD.PushError($"{path}:{lineNo}: '@node' before '@tree'"); break; }
                        node = new DialogueNode { Id = arg };
                        tree[arg] = node;
                        timeList = null;
                        break;

                    case "time":
                        FlushPending(node, ref pendingEyes, ref pendingHead);
                        if (treeId == null) { GD.PushError($"{path}:{lineNo}: '@time' before '@tree'"); break; }
                        if (!Dialogues.TimeBased.TryGetValue(treeId, out var perPeriod))
                        { perPeriod = new Dictionary<TimePeriod, List<string>>(); Dialogues.TimeBased[treeId] = perPeriod; }
                        timeList = new List<string>();
                        perPeriod[ParsePeriod(arg, path, lineNo)] = timeList;
                        node = null;
                        break;

                    case "eyes": pendingEyes = ParseGaze(arg, path, lineNo); break;
                    case "head": pendingHead = ParseGaze(arg, path, lineNo); break;

                    case "usetime":
                        if (node != null && int.TryParse(arg, out int idx)) node.TimeUseIndex = idx;
                        break;

                    case "end": break; // optional, purely for writer readability
                    default:
                        GD.PushWarning($"{path}:{lineNo}: unknown command '@{cmd}'");
                        break;
                }
                continue;
            }

            // ---- links ----
            if (line.StartsWith("->"))
            {
                string body = line.Substring(2).Trim();
                if (node == null) { GD.PushError($"{path}:{lineNo}: '->' outside a node"); continue; }

                if (body.StartsWith("\""))
                {
                    int close = body.IndexOf('"', 1);
                    if (close == -1) { GD.PushError($"{path}:{lineNo}: unterminated option text"); continue; }
                    string optText = body.Substring(1, close - 1);
                    string target = body.Substring(close + 1).Trim();
                    node.Responses.Add(new DialogueResponse { Text = optText, NextNodeId = CleanTarget(target) });
                }
                else
                {
                    node.Responses.Add(new DialogueResponse { Text = "", NextNodeId = CleanTarget(body) });
                }
                continue;
            }

            // ---- text lines ----
            if (line.StartsWith("\"") && line.EndsWith("\"") && line.Length >= 2)
            {
                string text = line.Substring(1, line.Length - 2);

                if (timeList != null) timeList.Add(text);
                else if (node != null)
                {
                    node.Paragraphs.Add(new DialogueParagraph { Text = text, Eyes = pendingEyes, Head = pendingHead });
                    pendingEyes = pendingHead = GazeMode.Unset;
                }
                else GD.PushWarning($"{path}:{lineNo}: text outside a node");
                continue;
            }

            GD.PushWarning($"{path}:{lineNo}: unrecognized line '{line}'");
        }
        FlushPending(node, ref pendingEyes, ref pendingHead);
    }

    private static void FlushPending(DialogueNode node, ref GazeMode eyes, ref GazeMode head)
    {
        // gaze written for a @usetime node (no paragraphs) becomes the node default
        if (node != null)
        {
            if (eyes != GazeMode.Unset && node.NodeEyes == GazeMode.Unset) node.NodeEyes = eyes;
            if (head != GazeMode.Unset && node.NodeHead == GazeMode.Unset) node.NodeHead = head;
        }
        eyes = head = GazeMode.Unset;
    }

    private static string CleanTarget(string t) => t == "end" ? null : t;

    private static TimePeriod ParsePeriod(string s, string path, int lineNo)
    {
        switch (s.ToLowerInvariant())
        {
            case "morning":   return TimePeriod.MORNING;
            case "afternoon": return TimePeriod.AFTERNOON;
            case "evening":   return TimePeriod.EVENING;
            case "night":     return TimePeriod.NIGHT;
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
            case "away":                         return GazeMode.Away;
            case "off":   case "none":           return GazeMode.Off;
            default:
                GD.PushWarning($"{path}:{lineNo}: bad gaze '{s}', defaulting to Player");
                return GazeMode.Player;
        }
    }
}