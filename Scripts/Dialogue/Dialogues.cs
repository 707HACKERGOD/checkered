using Godot;
using System.Collections.Generic;

// ============================================================
// VOICE PART CONVENTION (auto-numbered files)
// Part 1-2 = Morning   (line 0, line 1)
// Part 3-4 = Afternoon (line 0, line 1) — some chars skip Part 4
// Part 5-6 = Evening   (line 0, line 1) — some chars skip Part 6
// Part 7-8 = Night     (line 0, line 1) — some chars skip Part 8
// Branching dialogue = no voice yet (WIP)
// ============================================================

public class DialogueResponse
{
    public string Text { get; set; }
    public string NextNodeId { get; set; }
}

public class DialogueNode
{
    public string Id { get; set; }
    public string Text { get; set; }
    public List<DialogueResponse> Responses { get; set; } = new();
    public bool InjectTimeText { get; set; } = false;
    public int TimeTextIndex { get; set; } = 0;
    public string VoicePhraseKey { get; set; } = null;
}

public class NpcVoiceProfile
{
    public PersonalityType Personality { get; set; }
    public VoiceGender Gender { get; set; }
    public string FilePrefix { get; set; }
}

public static class Dialogues
{
    public static readonly Dictionary<string, Dictionary<TimePeriod, List<string>>> TimeBased = new()
    {
        ["stranger"] = new()
        {
            [TimePeriod.MORNING]   = new() { "Good morning.", "The world feels quiet." },
            [TimePeriod.AFTERNOON] = new() { "It's afternoon.", "I have so much to do." },
            [TimePeriod.EVENING]   = new() { "Evening already?", "Time flies." },
            [TimePeriod.NIGHT]     = new() { "Hello.", "Um... It's pretty late." }
        },
        ["kendall"] = new()
        {
            [TimePeriod.MORNING]   = new() { "Good morning!", "Beautiful day ahead!" },
            [TimePeriod.AFTERNOON] = new() { "Good afternoon.", "What a day." },
            [TimePeriod.EVENING]   = new() { "Evening!", "Time to relax." },
            [TimePeriod.NIGHT]     = new() { "Oh!", "Hello..." }
        },
        ["strong"] = new()
        {
            [TimePeriod.MORNING]   = new() { "Morning.", "Don't waste my time." },
            [TimePeriod.AFTERNOON] = new() { "You lost?" },
            [TimePeriod.EVENING]   = new() { "Yes, hello.", "Did something happen?" },
            [TimePeriod.NIGHT]     = new() { "Go away." }
        }
    };

    public static readonly Dictionary<string, List<string>> Fallback = new()
    {
        ["stranger"] = new() { "Hello." },
        ["kendall"]  = new() { "Hi there!" },
        ["strong"]   = new() { "What do you want?" }
    };

    public static readonly Dictionary<string, NpcVoiceProfile> NpcVoiceProfiles = new()
    {
        ["kendall"] = new() { Personality = PersonalityType.Friendly, Gender = VoiceGender.Female, FilePrefix = "friendly_f_3" },
        ["strong"] = new() { Personality = PersonalityType.Rude, Gender = VoiceGender.Male, FilePrefix = "rude_f_4" },
        ["stranger"] = new() { Personality = PersonalityType.Neutral, Gender = VoiceGender.Male, FilePrefix = "neutral_m_2" },
    };

    public static readonly Dictionary<string, Dictionary<string, DialogueNode>> BranchingTrees = new();

    public static void RegisterAllTrees()
    {
        RegisterKendallTree();
        RegisterStrongTree();
        RegisterStrangerTree();
    }

    private static void RegisterKendallTree()
    {
        var tree = new Dictionary<string, DialogueNode>();

        // Time-based intros: NO VoicePhraseKey — computed at runtime from TimePeriod + TimeTextIndex
        tree["kendall_intro1"] = new DialogueNode
        {
            Id = "kendall_intro1",
            Text = "{0}",
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

        // Branching nodes: WIP, no voice yet
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

    private static void RegisterStrongTree()
    {
        var tree = new Dictionary<string, DialogueNode>();

        tree["strong_intro1"] = new DialogueNode
        {
            Id = "strong_intro1",
            Text = "{0}",
            InjectTimeText = true,
            TimeTextIndex = 0,
            Responses = new() { new DialogueResponse { Text = "", NextNodeId = "strong_intro2" } }
        };

        // Final node: no NextNodeId = end dialogue after this line
        tree["strong_intro2"] = new DialogueNode
        {
            Id = "strong_intro2",
            Text = "{0}",
            InjectTimeText = true,
            TimeTextIndex = 1,
            Responses = new() // Empty = end after auto-advance
        };

        BranchingTrees["strong"] = tree;
    }

    private static void RegisterStrangerTree()
    {
        var tree = new Dictionary<string, DialogueNode>();

        tree["stranger_intro1"] = new DialogueNode
        {
            Id = "stranger_intro1",
            Text = "{0}",
            InjectTimeText = true,
            TimeTextIndex = 0,
            Responses = new() { new DialogueResponse { Text = "", NextNodeId = "stranger_intro2" } }
        };

        tree["stranger_intro2"] = new DialogueNode
        {
            Id = "stranger_intro2",
            Text = "{0}",
            InjectTimeText = true,
            TimeTextIndex = 1,
            Responses = new() { new DialogueResponse { Text = "", NextNodeId = "stranger_start" } }
        };

        BranchingTrees["stranger"] = tree;
    }
}