using Godot;

namespace Utopia;

/// <summary>
/// Authored enemy type. Enemies follow the same AP/draw/deck rules as player
/// units. Scouting intel is tracked per enemy type id (see features/scouting).
/// </summary>
[GlobalClass]
public partial class EnemyConfig : Resource
{
    [Export] public StringName Id { get; set; } = "";
    [Export] public string NameKey { get; set; } = "";
    [Export] public UnitStats Stats { get; set; }
    [Export] public Godot.Collections.Array<CardData> Deck { get; set; } = new();
    /// <summary>Stable id of the AI behaviour script/profile that picks cards.</summary>
    [Export] public StringName BehaviorId { get; set; } = "aggressive";
}
