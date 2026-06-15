using Godot;
using System.Collections.Generic;

// ============================================================
// PURE DATA CLASSES — no behavior, just structure
// Writers only touch this file and the Dialogues static class below
// ============================================================

public class DialogueResponse
{
    public string Text { get; set; }
    public string NextNodeId { get; set; } // null/empty = end dialogue
}

public class DialogueNode
{
    public string Id { get; set; }
    public string Text { get; set; }
    public List<DialogueResponse> Responses { get; set; } = new();
    
    // NEW: if true, DialogueManager will replace {0} with time-based text
    public bool InjectTimeText { get; set; } = false;
    // NEW: which line index from TimeBased to inject (0 or 1)
    public int TimeTextIndex { get; set; } = 0;
}

// ============================================================
// WRITER-FRIENDLY DIALOGUE DATABASE
// Add new NPCs, lines, and branching trees here. No code logic.
// ============================================================

public static class Dialogues
{
    // ------------------------------------------------------------------
    // 1. TIME-BASED GREETINGS (for all NPCs, linear dialogue)
    // ------------------------------------------------------------------
    public static readonly Dictionary<string, Dictionary<TimePeriod, List<string>>> TimeBased = new()
    {
        ["stranger"] = new()
        {
            [TimePeriod.MORNING]   = new() { "Good morning.", "The world feels quiet." },
            [TimePeriod.AFTERNOON] = new() { "It's afternoon.", "I have so much to do." },
            [TimePeriod.EVENING]   = new() { "Evening already?", "Time flies." },
            [TimePeriod.NIGHT]     = new() { "Late night thoughts.", "Can't sleep either?" }
        },
        ["kendall"] = new()
        {
            [TimePeriod.MORNING]   = new() { "Good morning!", "Beautiful day ahead!" },
            [TimePeriod.AFTERNOON] = new() { "Good afternoon.", "What a day." },
            [TimePeriod.EVENING]   = new() { "Evening!", "Time to relax." },
            [TimePeriod.NIGHT]     = new() { "Good night.", "See you tomorrow!" }
        },
        ["strong"] = new()
        {
            [TimePeriod.MORNING]   = new() { "Morning.", "Don't waste my time." },
            [TimePeriod.AFTERNOON] = new() { "Afternoon.", "You lost?" },
            [TimePeriod.EVENING]   = new() { "Evening.", "Stay out of my way." },
            [TimePeriod.NIGHT]     = new() { "Night.", "Go away." }
        }
    };

    public static readonly Dictionary<string, List<string>> Fallback = new()
    {
        ["stranger"] = new() { "Hello." },
        ["kendall"]  = new() { "Hi there!" },
        ["strong"]   = new() { "What do you want?" }
    };

    // ------------------------------------------------------------------
    // 2. BRANCHING DIALOGUE TREES
    // Each tree is a flat dictionary of nodes. Connect via NodeId strings.
    // ------------------------------------------------------------------
    public static readonly Dictionary<string, Dictionary<string, DialogueNode>> BranchingTrees = new();

    // ------------------------------------------------------------------
    // 3. TREE REGISTRATION (called once at startup by DialogueManager)
    // Writers: add your new tree here, copy the kendall pattern
    // ------------------------------------------------------------------
    public static void RegisterAllTrees()
    {
        RegisterKendallTree();
        // RegisterNewNpcTree(); // <-- add more here
    }

    private static void RegisterKendallTree()
    {
        var tree = new Dictionary<string, DialogueNode>();

        // Intro nodes — text injected at runtime from TimeBased
        tree["kendall_intro1"] = new DialogueNode
        {
            Id = "kendall_intro1",
            Text = "{0}", // placeholder, replaced by DialogueManager
            InjectTimeText = true,
            TimeTextIndex = 0,
            Responses = new() { new DialogueResponse { Text = "", NextNodeId = "kendall_intro2" } }
        };

        tree["kendall_intro2"] = new DialogueNode
        {
            Id = "kendall_intro2",
            Text = "{0}",
            InjectTimeText = true,
            TimeTextIndex = 1,
            Responses = new() { new DialogueResponse { Text = "", NextNodeId = "kendall_start" } }
        };

        // Question node
        tree["kendall_start"] = new DialogueNode
        {
            Id = "kendall_start",
            Text = "What is the best shark?",
            Responses = new()
            {
                new() { Text = "Thresher", NextNodeId = "kendall_correct" },
                new() { Text = "Pocket", NextNodeId = "kendall_wrong_pocket" },
                new() { Text = "Bull shark", NextNodeId = "kendall_wrong_bull" },
                new() { Text = "Hammerhead", NextNodeId = "kendall_wrong_hammer" }
            }
        };

        // Answer nodes (auto-advance, no responses = end dialogue)
        tree["kendall_correct"] = new DialogueNode
        {
            Id = "kendall_correct",
            Text = "Correct! Thresher sharks are awesome!",
            Responses = new()
        };

        tree["kendall_wrong_pocket"] = new DialogueNode
        {
            Id = "kendall_wrong_pocket",
            Text = "Pocket sharks are cute, but not the best.",
            Responses = new()
        };

        tree["kendall_wrong_bull"] = new DialogueNode
        {
            Id = "kendall_wrong_bull",
            Text = "Bull sharks are aggressive, but not the best.",
            Responses = new()
        };

        tree["kendall_wrong_hammer"] = new DialogueNode
        {
            Id = "kendall_wrong_hammer",
            Text = "Hammerheads are cool, but not the best.",
            Responses = new()
        };

        BranchingTrees["kendall"] = tree;
    }
}