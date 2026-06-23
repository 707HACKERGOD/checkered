using Godot;
using System.Collections.Generic;

public partial class MeleeHitbox : Area3D
{
    public ItemData CurrentWeapon;
    
    // Prevent hitting the same enemy twice in one swing
    private List<Rid> _hitBodies = new List<Rid>();

    public override void _Ready()
    {
        Monitoring = false; // Off by default
        BodyEntered += OnBodyEntered;
    }

    public void StartSwing(ItemData weapon)
    {
        CurrentWeapon = weapon;
        _hitBodies.Clear();
        Monitoring = true;
    }

    public void EndSwing()
    {
        Monitoring = false;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is CharacterBody3D npc)
        {
            if (_hitBodies.Contains(npc.GetRid())) return;
            _hitBodies.Add(npc.GetRid());

            // Find hit limb (assuming your Area3D colliders are named "Head", "Torso", etc.)
            string limb = "Torso"; // Default fallback
            // You can raycast from this hitbox to the NPC to find the exact Area3D limb name here
            
            var health = npc.GetNodeOrNull<Health>("Health");
            if (health != null)
            {
                float damage = CurrentWeapon.Damage ?? 10f; // Default to 10 if null
                health.TakeDamage(damage, limb);
                
                // Apply Knockback & Reactions
                Vector3 knockbackDir = (npc.GlobalPosition - GlobalPosition).Normalized();
                knockbackDir.Y = 0.5f; // Slight upward pop
                
                var npcReact = npc.GetNodeOrNull<NpcController>("ReactionController");
                if (npcReact != null)
                {
                    npcReact.ApplyHit(CurrentWeapon, knockbackDir, limb);
                }

                // TIME EFFECTS
                TimeManager.Instance.TriggerHitstop(
                    CurrentWeapon.HitstopDuration ?? 0.05f, 
                    CurrentWeapon.CameraShake ?? 0.1f
                );
            }
        }
    }
}