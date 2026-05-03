using Godot;

public partial class HealthBar : ProgressBar
{
    [Export] public Health TargetHealth { get; set; }

    public override void _Ready()
    {
        if (TargetHealth != null)
        {
            TargetHealth.Damaged += OnHealthChanged;
            TargetHealth.Healed += OnHealthChanged;
            CallDeferred(nameof(UpdateDisplay));
        }
    }

    private void OnHealthChanged(float amount, float current)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (TargetHealth == null) return;
        MaxValue = TargetHealth.MaxHealth;
        Value = TargetHealth.CurrentHealth;
    }
}