using Godot;
using System.Collections.Generic;

public partial class MeleeHitbox : Area3D
{
    [Export] public float HitboxLifetime = 0.3f; 

    public ItemData CurrentWeapon;

    private List<Rid> _hitBodies = new List<Rid>();
    private Timer _lifetimeTimer;
    private CharacterBody3D _owner; 

    public override void _Ready()
    {
        Monitoring = false;
        CollisionMask = uint.MaxValue; 
        
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered; 
        // FIX: Walk up the tree to find CharacterBody3D owner
        _owner = FindOwnerInParents();
        if (_owner == null)
            GD.PrintErr($"MeleeHitbox '{Name}': Could not find CharacterBody3D owner!");

        _lifetimeTimer = new Timer { OneShot = true, WaitTime = HitboxLifetime };
        _lifetimeTimer.Timeout += EndSwing;
        AddChild(_lifetimeTimer);
        // Safety check: warn if the hitbox has no shape
        bool hasShape = false;
        foreach (Node child in GetChildren())
        {
            if (child is CollisionShape3D cs && cs.Shape != null) hasShape = true;
        }
        if (!hasShape) GD.PrintErr($"MeleeHitbox '{Name}' has no CollisionShape3D!");
    }

    private CharacterBody3D FindOwnerInParents()
    {
        Node current = GetParent();
        while (current != null)
        {
            if (current is CharacterBody3D cb) return cb;
            current = current.GetParent();
        }
        return null;
    }
    public void StartSwing(ItemData weapon)
    {
        CurrentWeapon = weapon;
        _hitBodies.Clear();
        Monitoring = true;
        _lifetimeTimer.Start(HitboxLifetime);
        CallDeferred(nameof(CheckInitialOverlaps));

        // FIX: Force-check overlaps immediately in case the swing starts while already intersecting
        foreach (Node node in GetOverlappingBodies())
        {
            if (node is Node3D body) OnBodyEntered(body);
        }
        foreach (Node node in GetOverlappingAreas())
        {
            if (node is Area3D area) OnAreaEntered(area);
        }
    }

    private void CheckInitialOverlaps()
    {
        if (!Monitoring) return;
        foreach (Node node in GetOverlappingBodies())
        {
            if (node is Node3D body) OnBodyEntered(body);
        }
        foreach (Node node in GetOverlappingAreas())
        {
            if (node is Area3D area) OnAreaEntered(area);
        }
    }

    public void EndSwing()
    {
        Monitoring = false;
        _lifetimeTimer.Stop();
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body == _owner) return;
        if (body is not CharacterBody3D npc) return;

        var npcCtrl = npc.GetNodeOrNull<NpcController>(".");
        if (npcCtrl != null && npcCtrl.IsDead) return;

        if (_hitBodies.Contains(npc.GetRid())) return;
        _hitBodies.Add(npc.GetRid());

        string limb = DetermineHitLimb(npc);
        ApplyDamage(npc, limb);
    }

    private void OnAreaEntered(Area3D area)
    {
        CharacterBody3D npc = FindNpcFromLimbArea(area);
        if (npc == null || npc == _owner) return;

        var npcCtrl = npc.GetNodeOrNull<NpcController>(".");
        if (npcCtrl != null && npcCtrl.IsDead) return;

        if (_hitBodies.Contains(npc.GetRid())) return;
        _hitBodies.Add(npc.GetRid());

        string limb = area.Name;
        ApplyDamage(npc, limb);
    }

    private CharacterBody3D FindNpcFromLimbArea(Area3D area)
    {
        Node current = area;
        while (current != null)
        {
            if (current is CharacterBody3D npc) return npc;
            current = current.GetParent();
        }
        return null;
    }

    private void ApplyDamage(CharacterBody3D npc, string limb)
    {
        var health = npc.GetNodeOrNull<Health>("Health");
        if (health != null)
        {
            float damage = CurrentWeapon?.Damage ?? 10f;
            health.TakeDamage(damage, limb);

            Vector3 knockbackDir = (npc.GlobalPosition - GlobalPosition).Normalized();
            knockbackDir.Y = 0; 

            var npcCtrl = npc.GetNodeOrNull<NpcController>(".");
            if (npcCtrl != null)
            {
                npcCtrl.ApplyHit(CurrentWeapon, knockbackDir, limb);
            }

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.TriggerHitstop(
                    CurrentWeapon?.HitstopDuration ?? 0.05f,
                    CurrentWeapon?.CameraShake ?? 0.1f
                );
            }

            FlashLimbRed(npc);
        }
    }

    private async void FlashLimbRed(CharacterBody3D npc)
    {
        var meshes = new List<MeshInstance3D>();
        FindAllMeshes(npc, meshes);
        if (meshes.Count == 0) return;

        var redMat = new StandardMaterial3D { AlbedoColor = Colors.Red, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var originalMats = new Dictionary<MeshInstance3D, Material[]>();

        foreach (var mesh in meshes)
        {
            int count = mesh.Mesh.GetSurfaceCount();
            var mats = new Material[count];
            for (int i = 0; i < count; i++)
            {
                mats[i] = mesh.GetActiveMaterial(i);
                mesh.SetSurfaceOverrideMaterial(i, redMat);
            }
            originalMats[mesh] = mats;
        }

        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");

        foreach (var kvp in originalMats)
        {
            if (!IsInstanceValid(kvp.Key)) continue;
            for (int i = 0; i < kvp.Value.Length; i++)
            {
                kvp.Key.SetSurfaceOverrideMaterial(i, kvp.Value[i]);
            }
        }
    }

    private void FindAllMeshes(Node node, List<MeshInstance3D> list)
    {
        if (node is MeshInstance3D mi) list.Add(mi);
        foreach (Node child in node.GetChildren())
            FindAllMeshes(child, list);
    }

    private string DetermineHitLimb(CharacterBody3D npc)
    {
        var skeleton = npc.FindChildOfTypeRecursive<Skeleton3D>(false);
        if (skeleton == null) return "Torso";

        float closestDist = float.MaxValue;
        string closestLimb = "Torso";

        for (int i = 0; i < skeleton.GetBoneCount(); i++)
        {
            string boneName = skeleton.GetBoneName(i);
            Vector3 boneWorldPos = skeleton.GetBoneGlobalPose(i).Origin;
            float dist = GlobalPosition.DistanceTo(boneWorldPos);

            if (dist < closestDist)
            {
                closestDist = dist;
                closestLimb = BoneNameToLimb(boneName);
            }
        }
        return closestLimb;
    }

    private string BoneNameToLimb(string boneName)
    {
        string lower = boneName.ToLower();
        if (lower.Contains("head") || lower.Contains("neck")) return "Head";
        if (lower.Contains("hand") || lower.Contains("forearm") || lower.Contains("upper_arm")) return "Arms";
        if (lower.Contains("foot") || lower.Contains("shin") || lower.Contains("thigh") || lower.Contains("calf")) return "Legs";
        return "Torso";
    }
}

public static class ScriptExtensions
{
    public static T FindChildOfTypeRecursive<T>(this Node node, bool includeSelf = true) where T : Node
    {
        if (includeSelf && node is T found) return found;
        foreach (Node child in node.GetChildren())
        {
            T result = child.FindChildOfTypeRecursive<T>(true);
            if (result != null) return result;
        }
        return null;
    }
}