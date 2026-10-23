using System.Linq;
using Godot;

namespace Utopia;

/// <summary>
/// Lookup table of all authored heroes and battalions, by stable id.
/// Saves store ids; this catalog turns them back into resources.
/// </summary>
[GlobalClass]
public partial class RosterCatalog : Resource
{
    [Export] public Godot.Collections.Array<HeroData> Heroes { get; set; } = new();
    [Export] public Godot.Collections.Array<BattalionData> Battalions { get; set; } = new();
    [Export] public HeroData DefaultHero { get; set; }
    [Export] public BattalionData DefaultBattalion { get; set; }

    public HeroData FindHero(StringName id) => Heroes.FirstOrDefault(hero => hero.Id == id);

    public BattalionData FindBattalion(StringName id) => Battalions.FirstOrDefault(battalion => battalion.Id == id);
}
