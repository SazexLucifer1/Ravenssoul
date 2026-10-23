using Godot;

namespace Utopia;

/// <summary>
/// Reusable battlefield unit (hero + battalion, or an enemy).
///
/// Composition root only: it wires its child components and exposes a small
/// API. Logic lives in components (HealthComponent, GridMover,
/// CharacterAnimator); decisions live in controllers outside this scene.
/// Logical grid cell is the source of truth; visuals follow it.
/// </summary>
[GlobalClass]
public partial class Character : Node2D
{
    [Signal] public delegate void CellChangedEventHandler(Vector2I cell);
    [Signal] public delegate void DefeatedEventHandler();

    [Export] public HealthComponent Health { get; set; }
    [Export] public GridMover Mover { get; set; }
    [Export] public CharacterAnimator Animator { get; set; }
    [Export] public UnitVisual Visual { get; set; }

    /// <summary>Translation key of the unit's display name.</summary>
    public string DisplayNameKey { get; private set; } = "";
    public UnitStats Stats { get; private set; }
    public Vector2I Cell { get; private set; } = Vector2I.Zero;

    public override void _Ready()
    {
        Health.Died += OnDied;
    }

    /// <summary>Applies combined stats. Call before or after adding to the tree.</summary>
    public void Configure(UnitStats stats, string displayNameKey)
    {
        Stats = stats;
        DisplayNameKey = displayNameKey;
        Health.MaxHealth = Mathf.Max(1, stats.MaxHealth);
        Health.SetCurrentHealth(Health.MaxHealth);
    }

    /// <summary>Instantly places the unit (spawning, loading).</summary>
    public void PlaceAt(Vector2I cell, Vector2 worldPosition)
    {
        Cell = cell;
        Mover.SnapTo(worldPosition);
        EmitSignalCellChanged(Cell);
    }

    /// <summary>
    /// Updates the logical cell immediately and animates the visual toward it.
    /// Gameplay can query <see cref="Cell"/> right away; it never waits for animation.
    /// </summary>
    public void StepTo(Vector2I cell, Vector2 worldPosition)
    {
        Vector2I direction = cell - Cell;
        Cell = cell;
        Mover.MoveTo(worldPosition, direction);
        EmitSignalCellChanged(Cell);
    }

    public bool IsDefeated() => Health.IsDead();

    private void OnDied() => EmitSignalDefeated();
}
