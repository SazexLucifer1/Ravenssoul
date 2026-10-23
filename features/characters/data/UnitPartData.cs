using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>Shared base for the two halves of a battlefield unit: heroes and battalions.</summary>
[GlobalClass]
public partial class UnitPartData : Resource
{
    /// <summary>Stable, language-independent identifier. Used in saves; never shown to players.</summary>
    [Export] public StringName Id { get; set; } = "";
    /// <summary>Translation key for the display name.</summary>
    [Export] public string NameKey { get; set; } = "";
    [Export] public UnitStats Stats { get; set; }
    [Export] public Godot.Collections.Array<CardData> Deck { get; set; } = new();

    /// <summary>Overridden by subclasses with the design-mandated deck size.</summary>
    public virtual int ExpectedDeckSize() => 0;

    /// <summary>Returns developer-facing problems (empty when valid). Used by content tests.</summary>
    public List<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrEmpty(Id))
            problems.Add("missing id");
        if (string.IsNullOrEmpty(NameKey))
            problems.Add($"{Id}: missing name_key");
        if (Stats == null)
            problems.Add($"{Id}: missing stats");
        if (Deck.Count != ExpectedDeckSize())
            problems.Add($"{Id}: deck has {Deck.Count} cards, expected {ExpectedDeckSize()}");
        foreach (CardData card in Deck)
            if (card == null)
                problems.Add($"{Id}: deck contains an empty slot");
        return problems;
    }
}
