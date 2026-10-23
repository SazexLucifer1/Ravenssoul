using System.Collections.Generic;
using System.Linq;

namespace Utopia.Tests.Unit.Combat;

public class TestDeck : TestCase
{
    private static List<CardData> Cards(int count) =>
        Enumerable.Range(0, count).Select(i => new CardData { Id = $"card_{i}" }).ToList();

    public void TestDrawRespectsCountAndHandSize()
    {
        var deck = new Deck(Cards(20), 7);
        AssertEq(deck.Draw(4, 6).Count, 4);
        AssertEq(deck.Draw(4, 6).Count, 2, "hand size caps the draw");
        AssertEq(deck.Hand.Count, 6);
        AssertEq(deck.TotalCards(), 20);
    }

    public void TestSameSeedSameOrder()
    {
        var a = new Deck(Cards(20), 42);
        var b = new Deck(Cards(20), 42);
        AssertEq(a.Draw(5, 10).Select(c => (string)c.Id).ToList(), b.Draw(5, 10).Select(c => (string)c.Id).ToList());
    }

    public void TestDiscardReshufflesWhenDrawPileEmpty()
    {
        var deck = new Deck(Cards(3), 1);
        deck.Draw(3, 10);
        foreach (CardData card in deck.Hand.ToList())
            AssertTrue(deck.DiscardFromHand(card));
        AssertEq(deck.Draw(2, 10).Count, 2);
        AssertEq(deck.TotalCards(), 3);
    }

    public void TestEmptyDeckDrawsNothing() => AssertEmpty(new Deck(new List<CardData>(), 1).Draw(3, 5));
}
