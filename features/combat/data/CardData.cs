using Godot;

namespace Utopia;

/// <summary>One authored card. Effects are data; combat rules interpret them.</summary>
[GlobalClass]
public partial class CardData : Resource
{
    public enum CardKind { Move, Attack, Defend, Buff, Debuff, Special }

    /// <summary>Stable, language-independent identifier (saves, analytics, tests).</summary>
    [Export] public StringName Id { get; set; } = "";
    [Export] public string NameKey { get; set; } = "";
    [Export] public string DescriptionKey { get; set; } = "";
    [Export] public CardKind Kind { get; set; } = CardKind.Attack;
    [Export(PropertyHint.Range, "0,10")] public int ApCost { get; set; } = 1;
    /// <summary>Generic magnitude: damage, block, tiles moved, or buff amount depending on kind.</summary>
    [Export(PropertyHint.Range, "0,99")] public int Power { get; set; }
    [Export(PropertyHint.Range, "0,12")] public int RangeTiles { get; set; } = 1;
}
