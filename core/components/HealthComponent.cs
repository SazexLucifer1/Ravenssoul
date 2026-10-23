using Godot;

namespace Utopia;

/// <summary>
/// Reusable hit-point owner for any entity (units, destructible props).
///
/// Owns numbers and signals only. It never touches visuals, audio, or UI:
/// listeners (animators, health bars, controllers) subscribe to its signals.
/// </summary>
[GlobalClass]
public partial class HealthComponent : Node
{
    [Signal] public delegate void HealthChangedEventHandler(int current, int maximum, int delta);
    [Signal] public delegate void DamagedEventHandler(int amount, StringName sourceId);
    [Signal] public delegate void HealedEventHandler(int amount);
    [Signal] public delegate void DiedEventHandler();

    private int _maxHealth = 10;

    /// <summary>-1 means "not yet set": the component starts at full health.</summary>
    private int _current = -1;

    [Export(PropertyHint.Range, "1,9999")]
    public int MaxHealth
    {
        get => _maxHealth;
        set
        {
            _maxHealth = Mathf.Max(1, value);
            if (_current > _maxHealth)
                _current = _maxHealth;
        }
    }

    public int CurrentHealth => _current < 0 ? MaxHealth : _current;

    public bool IsDead() => CurrentHealth <= 0;

    /// <summary>Returns the damage actually dealt (clamped to remaining health).</summary>
    public int ApplyDamage(int amount, StringName sourceId = null)
    {
        if (amount <= 0 || IsDead())
            return 0;
        int before = CurrentHealth;
        _current = Mathf.Max(0, before - amount);
        int dealt = before - _current;
        EmitSignalDamaged(dealt, sourceId ?? new StringName(""));
        EmitSignalHealthChanged(_current, MaxHealth, -dealt);
        if (_current == 0)
            EmitSignalDied();
        return dealt;
    }

    /// <summary>
    /// Returns the amount actually healed. Defeated entities cannot be healed;
    /// use <see cref="Revive"/> for that.
    /// </summary>
    public int Heal(int amount)
    {
        if (amount <= 0 || IsDead())
            return 0;
        int before = CurrentHealth;
        _current = Mathf.Min(MaxHealth, before + amount);
        int restored = _current - before;
        if (restored > 0)
        {
            EmitSignalHealed(restored);
            EmitSignalHealthChanged(_current, MaxHealth, restored);
        }
        return restored;
    }

    public void Revive(int health) => SetCurrentHealth(Mathf.Max(1, health));

    /// <summary>
    /// Sets health directly (save loading, setup). Emits HealthChanged but not
    /// Damaged/Healed, because nothing in the game world caused it.
    /// </summary>
    public void SetCurrentHealth(int value)
    {
        int before = CurrentHealth;
        _current = Mathf.Clamp(value, 0, MaxHealth);
        EmitSignalHealthChanged(_current, MaxHealth, _current - before);
    }
}
