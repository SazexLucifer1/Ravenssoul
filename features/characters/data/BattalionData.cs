using Godot;

namespace Utopia;

/// <summary>A battalion type. Carries the 15-card backbone deck of a unit.</summary>
[GlobalClass]
public partial class BattalionData : UnitPartData
{
    public const int DeckSize = 15;

    public override int ExpectedDeckSize() => DeckSize;
}
