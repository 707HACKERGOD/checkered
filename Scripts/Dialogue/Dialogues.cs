using System.Collections.Generic;

public static class Dialogues
{
    public const string RepeatOptionText = "Sorry, what did you ask?";

    public static readonly Dictionary<string, Dictionary<string, DialogueNode>> BranchingTrees = new();
    public static readonly Dictionary<string, Dictionary<TimePeriod, List<string>>> TimeBased = new();
    public static readonly Dictionary<string, List<(string Node, List<DialogueCondition> Conds)>> Entries = new();
    public static readonly Dictionary<string, SpeakerInfo> Speakers = new();

    public static readonly Dictionary<string, List<string>> Fallback = new()
    {
        ["stranger"] = new() { "Hello." },
        ["kendall"]  = new() { "Hi there!" },
        ["strong"]   = new() { "What do you want?" }
    };

    public static readonly Dictionary<string, (MbtiType mbti, Predisposition predis, VoiceGender gender)> NpcVoiceConfig = new()
    {
        ["kendall"]  = (MbtiType.ENFP, Predisposition.Peaceful,   VoiceGender.Female),
        ["strong"]   = (MbtiType.ESTJ, Predisposition.Aggressive, VoiceGender.Male),
        ["stranger"] = (MbtiType.ISTP, Predisposition.Neutral,    VoiceGender.Male),
    };
}