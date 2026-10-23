using Godot;

namespace Utopia;

/// <summary>A named hero. Carries a small 5-card deck that defines its identity.</summary>
[GlobalClass]
public partial class HeroData : UnitPartData
{
    public const int DeckSize = 5;

    public override int ExpectedDeckSize() => DeckSize;
}
