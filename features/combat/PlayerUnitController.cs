using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Turns directional input into grid steps for one unit. Owns no visuals and
/// no UI: it asks the grid for legality and reports outcomes through signals.
///
/// First-playable stand-in for card-driven movement: one step per press.
/// Card/AP movement replaces TryStep's caller, not the grid rules.
/// </summary>
[GlobalClass]
public partial class PlayerUnitController : Node
{
    [Signal] public delegate void SteppedEventHandler(Vector2I cell);
    [Signal] public delegate void StepBlockedEventHandler(Vector2I cell);
    [Signal] public delegate void HazardEnteredEventHandler(Vector2I cell, int damage);
    [Signal] public delegate void ObjectiveReachedEventHandler(Vector2I cell);

    public static readonly IReadOnlyDictionary<StringName, Vector2I> Directions = new Dictionary<StringName, Vector2I>
    {
        ["ui_up"] = Vector2I.Up,
        ["ui_down"] = Vector2I.Down,
        ["ui_left"] = Vector2I.Left,
        ["ui_right"] = Vector2I.Right,
    };

    [Export] public BattleGrid Grid { get; set; }

    public Character Unit { get; set; }
    public bool Enabled { get; set; } = true;

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Enabled || Unit == null)
            return;
        foreach ((StringName action, Vector2I direction) in Directions)
        {
            if (inputEvent.IsActionPressed(action, true))
            {
                GetViewport().SetInputAsHandled();
                TryStep(direction);
                return;
            }
        }
    }

    /// <summary>Attempts one orthogonal step. Returns true if the unit moved.</summary>
    public bool TryStep(Vector2I direction)
    {
        if (!Enabled || Unit == null || Unit.IsDefeated())
            return false;
        if (Mathf.Abs(direction.X) + Mathf.Abs(direction.Y) != 1)
            return false;
        Vector2I target = Unit.Cell + direction;
        if (!Grid.Relocate(Unit.Cell, target))
        {
            EmitSignalStepBlocked(target);
            return false;
        }
        Unit.StepTo(target, Grid.CellToLocal(target));
        EmitSignalStepped(target);
        BattleMapData map = Grid.MapData;
        if (map.IsHazard(target))
        {
            int dealt = Unit.Health.ApplyDamage(map.HazardDamage, "hazard");
            EmitSignalHazardEntered(target, dealt);
        }
        if (target == map.ObjectiveCell && !Unit.IsDefeated())
            EmitSignalObjectiveReached(target);
        return true;
    }
}
