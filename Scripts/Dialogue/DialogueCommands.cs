using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Event hub for dialogue-triggered game events. Subscribe from any system.</summary>
public static class DialogueEvents
{
    public static event Action<Node3D, string> EmotionChanged;   // npc, emotion
    public static event Action<Node3D, string> StateChanged;     // npc, "hostile"/"friendly"/...
    public static event Action<Node3D, string, string[]> AiCommand; // npc, verb, args
    public static event Action<string> ShopRequested;            // shopId
    public static event Action<Node3D> HangoutRequested;          // npc

    public static void FireEmotion(Node3D npc, string e) => EmotionChanged?.Invoke(npc, e);
    public static void FireState(Node3D npc, string s) => StateChanged?.Invoke(npc, s);
    public static void FireAi(Node3D npc, string verb, string[] a) => AiCommand?.Invoke(npc, verb, a);
    public static void FireShop(string id) => ShopRequested?.Invoke(id);
    public static void FireHangout(Node3D npc) => HangoutRequested?.Invoke(npc);
}

public static class DialogueCommands
{
    public delegate void Handler(Node3D npc, string[] args);
    private static readonly Dictionary<string, Handler> _handlers = new();

    static DialogueCommands()
    {
        // anim/emotion/state: optional first arg = target NPC name (group chats), else conversation owner
        Register("anim", (npc, a) =>
        {
            if (a.Length == 0) return;
            var target = a.Length >= 2 ? ResolveNpc(npc, a[0]) : npc;
            string anim = a.Length >= 2 ? a[1] : a[0];
            var ap = target.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
            if (ap == null) return;
            if (ap.HasAnimation(anim)) ap.Play(anim);
            else GD.PushWarning($"DialogueCommands: no animation '{anim}' on {target.Name}");
        });

        Register("emotion", (npc, a) =>
        {
            if (a.Length == 0) return;
            var target = a.Length >= 2 ? ResolveNpc(npc, a[0]) : npc;
            target.SetMeta("emotion", a[a.Length - 1]);
            DialogueEvents.FireEmotion(target, a[a.Length - 1]);
        });

        Register("state", (npc, a) =>
        {
            if (a.Length == 0) return;
            var target = a.Length >= 2 ? ResolveNpc(npc, a[0]) : npc;
            target.SetMeta("dialogue_state", a[a.Length - 1]);
            DialogueEvents.FireState(target, a[a.Length - 1]);   // e.g. "hostile" -> start battle
        });

        Register("ai", (npc, a) =>
        {
            if (a.Length == 0) return;
            DialogueEvents.FireAi(npc, a[0], a.Skip(1).ToArray());
        });

        Register("teleport", (npc, a) =>
        {
            if (a.Length < 2) { GD.PushWarning("DialogueCommands: teleport needs who + marker name"); return; }
            var scene = npc.GetTree().CurrentScene;
            var marker = scene?.FindChild(a[1], true, false) as Node3D;
            var who = a[0] == "player" ? npc.GetTree().Root.FindChild("Player", true, false) as Node3D : npc;
            if (marker == null || who == null) { GD.PushWarning($"DialogueCommands: teleport target '{a[1]}' not found"); return; }
            if (who is CharacterBody3D cb) cb.Velocity = Vector3.Zero;
            who.GlobalPosition = marker.GlobalPosition;
        });

        Register("shop", (npc, a) => DialogueEvents.FireShop(a.Length > 0 ? a[0] : ""));
        Register("hangout", (npc, a) => DialogueEvents.FireHangout(npc));
    }

    public static void Register(string name, Handler h) => _handlers[name.ToLowerInvariant()] = h;

    public static void Run(DialogueCommand cmd, Node3D npc)
    {
        if (cmd == null || npc == null) return;
        if (!_handlers.TryGetValue(cmd.Name.ToLowerInvariant(), out var h))
        { GD.PushWarning($"DialogueCommands: no handler for '{cmd.Name}'"); return; }
        h(npc, cmd.Args ?? Array.Empty<string>());
    }

    private static Node3D ResolveNpc(Node3D owner, string name)
    {
        if (string.IsNullOrEmpty(name)) return owner;
        foreach (var n in owner.GetTree().GetNodesInGroup("NPC"))
        {
            if (n is not Node3D nd) continue;
            if (nd.Name.ToString().Equals(name, StringComparison.OrdinalIgnoreCase)) return nd;
            if (n is NpcController c && c.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)) return nd;
        }
        return owner;
    }
}