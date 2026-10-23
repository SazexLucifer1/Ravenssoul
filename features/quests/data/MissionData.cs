using Godot;

namespace Utopia;

/// <summary>One selectable mission: objective, map, and rewards.</summary>
[GlobalClass]
public partial class MissionData : Resource
{
    public enum MissionObjective { Defeat, Hold, Escort, Reach }

    [Export] public StringName Id { get; set; } = "";
    [Export] public string TitleKey { get; set; } = "";
    [Export] public string ObjectiveKey { get; set; } = "";
    [Export] public MissionObjective Objective { get; set; } = MissionObjective.Reach;
    [Export] public BattleMapData Map { get; set; }
    [Export] public Godot.Collections.Dictionary<StringName, int> Rewards { get; set; } = new();
}
