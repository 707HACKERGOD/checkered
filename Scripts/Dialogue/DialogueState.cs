using System;
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>Persistent dialogue world-state. Call Save()/Load() from your save
/// system, Reset() on New Game. met_<npcId> is set automatically on first talk.</summary>
public static class DialogueState
{
    public static event Action Changed;

    private static readonly HashSet<string> _flags = new();
    private static readonly Dictionary<string, float> _rel = new();   // "a>b"
    private static readonly Dictionary<string, int> _talks = new();

    private static string RelKey(string a, string b) => $"{a}>{b}";

    public static bool Has(string flag) => _flags.Contains(flag);

    public static void Set(string flag, bool value)
    {
        bool had = _flags.Contains(flag);
        if (value == had) return;
        if (value) _flags.Add(flag); else _flags.Remove(flag);
        Changed?.Invoke();
    }

    public static bool MeetsAll(IEnumerable<DialogueCondition> conds) =>
        conds == null || conds.All(c => Has(c.Flag) == c.Required);

    public static int TalkCount(string npcId) => _talks.TryGetValue(npcId, out var n) ? n : 0;

    public static void BumpTalk(string npcId)
    {
        _talks[npcId] = TalkCount(npcId) + 1;
        if (_talks[npcId] == 1) Set($"met_{npcId}", true);
    }

    /// <summary>Relationship between two actors ("player" is the player's id). Stored symmetrically.</summary>
    public static float GetRel(string a, string b) => _rel.TryGetValue(RelKey(a, b), out var v) ? v : 0f;

    public static void AddRel(string a, string b, float delta)
    {
        _rel[RelKey(a, b)] = GetRel(a, b) + delta;
        _rel[RelKey(b, a)] = GetRel(b, a) + delta;
        Changed?.Invoke();
    }

    public static void Reset()
    {
        _flags.Clear(); _rel.Clear(); _talks.Clear();
        VoiceStudio.RuntimeOverride.Clear();
        Changed?.Invoke();
    }

    // ---------------- persistence ----------------

    public static void Save(string path = "user://dialogue_state.json")
    {
        var root = new Godot.Collections.Dictionary
        {
            ["flags"] = new Godot.Collections.Array<string>(_flags)
        };
        var rel = new Godot.Collections.Dictionary();
        foreach (var kv in _rel) rel[kv.Key] = kv.Value;
        root["rel"] = rel;
        var talks = new Godot.Collections.Dictionary();
        foreach (var kv in _talks) talks[kv.Key] = kv.Value;
        root["talks"] = talks;

        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f?.StoreString(Json.Stringify(root));
    }

    public static void Load(string path = "user://dialogue_state.json")
    {
        if (!FileAccess.FileExists(path)) return;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return;
        var parsed = Json.ParseString(f.GetAsText()) as Godot.Collections.Dictionary;
        if (parsed == null) return;

        _flags.Clear();
        if (parsed.TryGetValue("flags", out var fl) && fl.VariantType == Variant.Type.Array)
            foreach (var v in fl.AsGodotArray()) _flags.Add(v.AsString());

        _rel.Clear();
        if (parsed.TryGetValue("rel", out var r) && r.VariantType == Variant.Type.Dictionary)
            foreach (var kv in r.AsGodotDictionary()) _rel[kv.Key.AsString()] = kv.Value.AsSingle();

        _talks.Clear();
        if (parsed.TryGetValue("talks", out var t) && t.VariantType == Variant.Type.Dictionary)
            foreach (var kv in t.AsGodotDictionary()) _talks[kv.Key.AsString()] = kv.Value.AsInt32();

        Changed?.Invoke();
    }
}