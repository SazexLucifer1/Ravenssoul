using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Utopia;

/// <summary>
/// The "Combine" verb: one hero paired with one battalion.
///
/// Pure data logic. Produces the combined stats and the combined 20-card deck
/// the battlefield unit uses. Saved as ids only (see ToSaveData).
/// </summary>
public class UnitLoadout
{
    public HeroData Hero { get; set; }
    public BattalionData Battalion { get; set; }

    public UnitLoadout(HeroData hero = null, BattalionData battalion = null)
    {
        Hero = hero;
        Battalion = battalion;
    }

    public bool IsComplete() => Hero != null && Battalion != null;

    public UnitStats CombinedStats()
    {
        Debug.Assert(IsComplete(), "UnitLoadout needs both a hero and a battalion");
        return UnitStats.Combine(Hero.Stats, Battalion.Stats);
    }

    /// <summary>Hero cards first, then battalion cards. Order is stable for deterministic tests.</summary>
    public List<CardData> CombinedDeck()
    {
        Debug.Assert(IsComplete(), "UnitLoadout needs both a hero and a battalion");
        var cards = new List<CardData>(Hero.Deck);
        cards.AddRange(Battalion.Deck);
        return cards;
    }

    public JsonObject ToSaveData() => new()
    {
        ["hero_id"] = Hero != null ? (string)Hero.Id : "",
        ["battalion_id"] = Battalion != null ? (string)Battalion.Id : "",
    };

    public static UnitLoadout FromSaveData(JsonObject data, RosterCatalog catalog) => new(
        catalog.FindHero(JsonNodes.Str(data["hero_id"])),
        catalog.FindBattalion(JsonNodes.Str(data["battalion_id"])));
}
