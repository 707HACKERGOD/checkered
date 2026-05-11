using Godot;
using System.Collections.Generic;

public partial class Health : Node
{
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void DamagedEventHandler(float amount, float currentHealth);
    [Signal] public delegate void HealedEventHandler(float amount, float currentHealth);
    [Signal] public delegate void LimbDamagedEventHandler(string limbName, float amount, float limbHealth);

    public float MaxHealth { get; private set; } = 100f;   // fixed scale
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
        CurrentHealth = MaxHealth;   // start at full
    }

    public void TakeDamage(float amount, string limbName = null)
    {
        if (IsDead) return;

        if (!string.IsNullOrEmpty(limbName) && _limbs.TryGetValue(limbName, out LimbHealth limb))
        {
            limb.TakeDamage(amount);

            // Instant death when a vital core is destroyed
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
            // Distributed damage (fallback) – rarely used
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
        // Only work if we have Head and Torso – otherwise treat as full health
        if (!_limbs.TryGetValue("Head", out LimbHealth head) ||
            !_limbs.TryGetValue("Torso", out LimbHealth torso))
        {
            CurrentHealth = MaxHealth;
            return;
        }

        // 1) Core ratios (0.0 – 1.0)
        float headRatio  = head.CurrentHealth  / head.MaxHealth;
        float torsoRatio = torso.CurrentHealth / torso.MaxHealth;

        // HEAD is squared because it's fragile – one punch from death feels critical
        float headCriticality = headRatio * headRatio;

        // Lethal score = product of critical parts
        float lethalScore = headCriticality * torsoRatio;

        // 2) Limbs buffer – average integrity of non‑vital parts
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

        // Limbs modulate final health by up to 40% (all broken → multiplier 0.6)
        const float limbInfluence = 0.40f;
        float limbMultiplier = 1f - limbInfluence + (limbInfluence * avgLimbRatio);

        // Final health (clamped 0-100)
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