using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Runtime draw/hand/discard piles for one battlefield unit.
///
/// Seeded RandomNumberGenerator keeps shuffles reproducible for tests,
/// replays, and the scouting "see their hand" intel tier.
/// </summary>
public class Deck
{
    public List<CardData> DrawPile { get; private set; }
    public List<CardData> Hand { get; } = new();
    public List<CardData> DiscardPile { get; } = new();

    private readonly RandomNumberGenerator _rng = new();

    public Deck(IEnumerable<CardData> cards = null, ulong rngSeed = 0)
    {
        _rng.Seed = rngSeed;
        DrawPile = new List<CardData>(cards ?? new List<CardData>());
        ShuffleDrawPile();
    }

    public void ShuffleDrawPile()
    {
        // Fisher-Yates with our own RNG (reproducible per seed).
        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = _rng.RandiRange(0, i);
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }
    }

    /// <summary>
    /// Draws up to <paramref name="count"/> cards without exceeding <paramref name="maxHandSize"/>.
    /// Reshuffles the discard pile when the draw pile runs out.
    /// Returns the cards drawn.
    /// </summary>
    public List<CardData> Draw(int count, int maxHandSize)
    {
        var drawn = new List<CardData>();
        while (drawn.Count < count && Hand.Count < maxHandSize)
        {
            if (DrawPile.Count == 0)
            {
                if (DiscardPile.Count == 0)
                    break;
                DrawPile = new List<CardData>(DiscardPile);
                DiscardPile.Clear();
                ShuffleDrawPile();
            }
            CardData card = DrawPile[^1];
            DrawPile.RemoveAt(DrawPile.Count - 1);
            Hand.Add(card);
            drawn.Add(card);
        }
        return drawn;
    }

    public bool DiscardFromHand(CardData card)
    {
        int index = Hand.IndexOf(card);
        if (index < 0)
            return false;
        Hand.RemoveAt(index);
        DiscardPile.Add(card);
        return true;
    }

    public int TotalCards() => DrawPile.Count + Hand.Count + DiscardPile.Count;
}
