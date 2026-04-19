using Godot;

public partial class LimbHealth : Node
{
    [Signal] public delegate void DamagedEventHandler(float amount, float currentHealth);
    [Signal] public delegate void DestroyedEventHandler();

    [Export] public string LimbName { get; set; } = "Limb";
    [Export] public float MaxHealth { get; set; } = 30f;
    [Export] public bool Critical { get; set; } = false; // If true, destroying kills character

    public float CurrentHealth { get; private set; }
    public bool IsDestroyed => CurrentHealth <= 0f;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        EmitSignal(SignalName.Damaged, amount, CurrentHealth);
        if (CurrentHealth <= 0f)
        {
            EmitSignal(SignalName.Destroyed);
            if (Critical && GetParent() is Health parentHealth)
                parentHealth.TakeDamage(parentHealth.CurrentHealth); // Kill instantly
        }
    }
}