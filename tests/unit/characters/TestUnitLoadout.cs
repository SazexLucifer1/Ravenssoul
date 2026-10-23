using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia.Tests.Unit.Characters;

public class TestUnitLoadout : TestCase
{
    private static RosterCatalog Roster => GameSession.Roster;

    public void TestStatsCombineByAddition()
    {
        var a = new UnitStats { MaxHealth = 8, Speed = 6, MaxAp = 1 };
        var b = new UnitStats { MaxHealth = 12, Speed = 2, MaxAp = 2 };
        UnitStats c = UnitStats.Combine(a, b);
        AssertEq(c.MaxHealth, 20);
        AssertEq(c.Speed, 8);
        AssertEq(c.MaxAp, 3);
        AssertEq(a.MaxHealth, 8, "inputs are not mutated");
    }

    public void TestCombinedDeckIsTwentyCardsHeroFirst()
    {
        var loadout = new UnitLoadout(Roster.DefaultHero, Roster.DefaultBattalion);
        List<CardData> deck = loadout.CombinedDeck();
        AssertEq(deck.Count, HeroData.DeckSize + BattalionData.DeckSize);
        AssertEq(deck[0], Roster.DefaultHero.Deck[0]);
    }

    public void TestAllAuthoredUnitsAreValid()
    {
        foreach (HeroData hero in Roster.Heroes)
            AssertEmpty(hero.Validate(), $"hero {hero.Id}");
        foreach (BattalionData battalion in Roster.Battalions)
            AssertEmpty(battalion.Validate(), $"battalion {battalion.Id}");
    }

    public void TestIdsAreUnique()
    {
        var seen = new HashSet<string>();
        foreach (UnitPartData part in Roster.Heroes.Cast<UnitPartData>().Concat(Roster.Battalions))
            AssertTrue(seen.Add(part.Id), $"duplicate id {part.Id}");
    }

    public void TestSaveDataRoundTripsThroughIds()
    {
        var loadout = new UnitLoadout(Roster.DefaultHero, Roster.DefaultBattalion);
        JsonObject data = loadout.ToSaveData();
        AssertEq(data["hero_id"], "hero_deserter");
        UnitLoadout restored = UnitLoadout.FromSaveData(data, Roster);
        AssertTrue(restored.IsComplete());
        AssertEq(restored.Hero, Roster.DefaultHero);
    }

    public void TestUnknownIdsProduceIncompleteLoadout()
    {
        UnitLoadout restored = UnitLoadout.FromSaveData(new JsonObject { ["hero_id"] = "nobody", ["battalion_id"] = "battalion_militia" }, Roster);
        AssertFalse(restored.IsComplete());
    }
}
