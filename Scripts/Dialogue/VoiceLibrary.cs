using Godot;
using System.Collections.Generic;

public enum PersonalityType { Rude, Friendly, Neutral, Stoic, Eccentric, Paranoid, Cheerful, Grim, Cynical, Idealistic, Professional, Casual, Aggressive, Timid, Sarcastic, Wise }
public enum VoiceGender { Female, Male }

public static class VoiceLibrary
{
    // Folder names must match your Godot project exactly
    private static readonly Dictionary<string, string> _folderNames = new()
    {
        ["friendly_f_3"] = "Friendly_F_ESFP",
        ["neutral_m_2"]  = "Neutral_M_ENFP",
        ["rude_f_4"]     = "Rude_F_ESFJ",
    };

    // Cache: [fullPath] = AudioStream (optional, for performance)
    private static readonly Dictionary<string, AudioStream> _cache = new();

    /// <summary>
    /// Creates a phrase key like "friendly_f_3_Part_1" from prefix and number.
    /// Dialogues.cs calls this when building nodes.
    /// </summary>
    public static string MakeAutoKey(string filePrefix, int partNumber)
    {
        return $"{filePrefix}_Part_{partNumber}";
    }

    /// <summary>
    /// Resolves an auto-key to a full file path, then loads the AudioStream.
    /// Called by DialogueManager when showing a node.
    /// </summary>
    public static string ResolveAutoKey(string autoKey)
    {
        // autoKey format: "friendly_f_3_Part_1"
        int lastUnderscore = autoKey.LastIndexOf("_Part_");
        if (lastUnderscore == -1) return null;

        string prefix = autoKey.Substring(0, lastUnderscore);
        string partSuffix = autoKey.Substring(lastUnderscore + 1); // "Part_1"

        if (!_folderNames.TryGetValue(prefix, out var folderName))
        {
            GD.PushWarning($"Unknown voice prefix: {prefix}");
            return null;
        }

        // Build path: res://Assets/Audio/Voice_lines/Friendly_F_ESFP/friendly_f_3 - Part_1.ogg
        string fileName = $"{prefix} - {partSuffix}.ogg";
        string path = $"res://Assets/Audio/Voice_lines/{folderName}/{fileName}";

        return path;
    }

    /// <summary>
    /// Load and cache an AudioStream. Returns null if file not found.
    /// </summary>
    public static AudioStream LoadStream(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
            return cached;

        var stream = GD.Load<AudioStream>(path);
        if (stream != null)
            _cache[path] = stream;

        return stream;
    }

    public static void ClearCache()
    {
        _cache.Clear();
    }
}