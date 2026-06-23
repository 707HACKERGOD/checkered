using Godot;
using System.Collections.Generic;

public partial class Health : Node
{
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void DamagedEventHandler(float amount, float currentHealth);
    [Signal] public delegate void HealedEventHandler(float amount, float currentHealth);
    [Signal] public delegate void LimbDamagedEventHandler(string limbName, float amount, float limbHealth);

    public float MaxHealth { get; private set; } = 100f;   
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    private Dictionary<string, LimbHealth> _limbs = new();

    public override void _Ready()
    {
        foreach (Node child in GetChildren())
        {
            if (child is LimbHealth limb)
            {
                _limbs[limb.LimbName] = limb;
                limb.Damaged += (amount, current) =>
                    EmitSignal(SignalName.LimbDamaged, limb.LimbName, amount, current);
            }
        }
        RecalculateTotalHealth();
        CurrentHealth = MaxHealth;   
    }

    public void TakeDamage(float amount, string limbName = null)
    {
        if (IsDead) return;

        // FIX: Proper fallback if an entity has no limbs assigned to it (prevents divide by zero & invincibility)
        if (_limbs.Count == 0)
        {
            CurrentHealth -= amount;
            EmitSignal(SignalName.Damaged, amount, CurrentHealth);
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = 0;
                EmitSignal(SignalName.Died);
                Die();
            }
            return;
        }

        if (!string.IsNullOrEmpty(limbName) && _limbs.TryGetValue(limbName, out LimbHealth limb))
        {
            limb.TakeDamage(amount);

            if ((limbName == "Head" || limbName == "Torso") && limb.IsDestroyed)
            {
                CurrentHealth = 0;
                EmitSignal(SignalName.Died);
                Die();
                return;
            }
        }
        else
        {
            float perLimb = amount / _limbs.Count;
            foreach (var l in _limbs.Values)
                l.TakeDamage(perLimb);
        }

        RecalculateTotalHealth();

        if (CurrentHealth <= 0f)
        {
            EmitSignal(SignalName.Died);
            Die();
        }
    }

    private void RecalculateTotalHealth()
    {
        if (!_limbs.TryGetValue("Head", out LimbHealth head) ||
            !_limbs.TryGetValue("Torso", out LimbHealth torso))
        {
            CurrentHealth = MaxHealth;
            return;
        }

        float headRatio  = head.CurrentHealth  / head.MaxHealth;
        float torsoRatio = torso.CurrentHealth / torso.MaxHealth;

        float headCriticality = headRatio * headRatio;
        float lethalScore = headCriticality * torsoRatio;

        float limbSum = 0f;
        int limbCount = 0;
        foreach (var name in new[] { "LeftArm", "RightArm", "LeftLeg", "RightLeg" })
        {
            if (_limbs.TryGetValue(name, out var limb))
            {
                limbSum += limb.CurrentHealth / limb.MaxHealth;
                limbCount++;
            }
        }
        float avgLimbRatio = limbCount > 0 ? limbSum / limbCount : 1f;

        const float limbInfluence = 0.40f;
        float limbMultiplier = 1f - limbInfluence + (limbInfluence * avgLimbRatio);

        CurrentHealth = Mathf.Clamp(lethalScore * limbMultiplier * MaxHealth, 0f, MaxHealth);

        EmitSignal(SignalName.Damaged, 0, CurrentHealth);
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        EmitSignal(SignalName.Healed, amount, CurrentHealth);
    }

    protected virtual void Die() { /* override if needed */ }
}