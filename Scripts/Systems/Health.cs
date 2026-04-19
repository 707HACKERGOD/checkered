using Godot;
using System.Collections.Generic;

public partial class Health : Node
{
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void DamagedEventHandler(float amount, float currentHealth);
    [Signal] public delegate void HealedEventHandler(float amount, float currentHealth);
    [Signal] public delegate void LimbDamagedEventHandler(string limbName, float amount, float limbHealth);

    [Export] public float MaxHealth { get; set; } = 100f;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    private Dictionary<string, LimbHealth> _limbs = new();

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        // Find all LimbHealth children
        foreach (Node child in GetChildren())
        {
            if (child is LimbHealth limb)
            {
                _limbs[limb.LimbName] = limb;
                limb.Damaged += (amount, current) =>
                    EmitSignal(SignalName.LimbDamaged, limb.LimbName, amount, current);
            }
        }
    }

    // Health.cs
    public void TakeDamage(float amount, string limbName = null)
    {
        if (IsDead) return;

        if (!string.IsNullOrEmpty(limbName) && _limbs.TryGetValue(limbName, out LimbHealth limb))
        {
            limb.TakeDamage(amount);
        }
        else
        {
            // If no limb specified, spread damage across all limbs proportionally (optional)
            foreach (var l in _limbs.Values)
                l.TakeDamage(amount / _limbs.Count);
        }

        // Recalculate total health from limbs
        RecalculateTotalHealth();

        if (CurrentHealth <= 0f)
        {
            EmitSignal(SignalName.Died);
            Die();
        }
    }

    private void RecalculateTotalHealth()
    {
        float totalMax = 0;
        float totalCurrent = 0;
        foreach (var limb in _limbs.Values)
        {
            totalMax += limb.MaxHealth;
            totalCurrent += limb.CurrentHealth;
        }
        MaxHealth = totalMax;
        CurrentHealth = totalCurrent;
        EmitSignal(SignalName.Damaged, 0, CurrentHealth); // Notify UI
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        EmitSignal(SignalName.Healed, amount, CurrentHealth);
    }

    protected virtual void Die()
    {
        // Override in derived classes if needed
    }
}