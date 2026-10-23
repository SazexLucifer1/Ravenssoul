using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Authored layout of one Fire-Emblem-sized battle map (12x12 to 16x16).
/// Logical cells are the source of truth for positions; visuals follow them.
/// </summary>
[GlobalClass]
public partial class BattleMapData : Resource
{
    public const int MinSize = 12;
    public const int MaxSize = 16;

    [Export] public StringName Id { get; set; } = "";
    [Export] public Vector2I Size { get; set; } = new(12, 12);
    [Export] public Godot.Collections.Array<Vector2I> BlockedCells { get; set; } = new();
    [Export] public Godot.Collections.Array<Vector2I> HazardCells { get; set; } = new();
    [Export(PropertyHint.Range, "0,99")] public int HazardDamage { get; set; } = 1;
    [Export] public Vector2I PlayerSpawn { get; set; } = Vector2I.Zero;
    [Export] public Vector2I ObjectiveCell { get; set; } = Vector2I.Zero;

    public bool IsInside(Vector2I cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Size.X && cell.Y < Size.Y;

    public bool IsWalkable(Vector2I cell) => IsInside(cell) && !BlockedCells.Contains(cell);

    public bool IsHazard(Vector2I cell) => HazardCells.Contains(cell);

    public List<string> Validate()
    {
        var problems = new List<string>();
        if (Size.X < MinSize || Size.Y < MinSize || Size.X > MaxSize || Size.Y > MaxSize)
            problems.Add($"{Id}: size {Size} outside {MinSize}..{MaxSize}");
        if (!IsWalkable(PlayerSpawn))
            problems.Add($"{Id}: player_spawn is not walkable");
        if (!IsWalkable(ObjectiveCell))
            problems.Add($"{Id}: objective_cell is not walkable");
        return problems;
    }
}
