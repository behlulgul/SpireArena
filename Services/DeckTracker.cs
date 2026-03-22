namespace SpireArena;

/// <summary>
/// Tracks the state of the player's deck during combat.
/// Knows which cards are in draw pile, hand, discard, exhaust.
/// </summary>
public static class DeckTracker
{
    /// <summary>
    /// Represents the tracked state of a card in the deck.
    /// </summary>
    public class TrackedCard
    {
        public string CardId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int EnergyCost { get; set; }
        public string CardType { get; set; } = ""; // Attack, Skill, Power
        public CardLocation Location { get; set; } = CardLocation.DrawPile;
        public int CopiesInDeck { get; set; } = 1;
        public int CopiesPlayed { get; set; }
        public int CopiesExhausted { get; set; }
    }

    public enum CardLocation
    {
        DrawPile,
        Hand,
        DiscardPile,
        Exhausted
    }

    private static readonly List<TrackedCard> _masterDeck = new();
    private static readonly List<string> _playedThisCombat = new();
    private static bool _inCombat;

    public static IReadOnlyList<TrackedCard> MasterDeck => _masterDeck;
    public static IReadOnlyList<string> PlayedThisCombat => _playedThisCombat;
    public static bool InCombat => _inCombat;

    // Counters
    public static int DrawPileCount { get; private set; }
    public static int DiscardPileCount { get; private set; }
    public static int ExhaustPileCount { get; private set; }
    public static int HandCount { get; private set; }

    /// <summary>
    /// Called when a new combat starts. Loads the full deck.
    /// </summary>
    public static void OnCombatStart(List<(string id, string name, int cost, string type)> deckCards)
    {
        _masterDeck.Clear();
        _playedThisCombat.Clear();
        _inCombat = true;

        // Group by card id to aggregate copies
        var grouped = new Dictionary<string, TrackedCard>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, name, cost, type) in deckCards)
        {
            if (grouped.TryGetValue(id, out var existing))
            {
                existing.CopiesInDeck++;
            }
            else
            {
                grouped[id] = new TrackedCard
                {
                    CardId = id,
                    DisplayName = name,
                    EnergyCost = cost,
                    CardType = type,
                    CopiesInDeck = 1
                };
            }
        }

        _masterDeck.AddRange(grouped.Values);
        UpdateCounts(deckCards.Count, 0, 0, 0);

        MainFile.Logger.Info($"DeckTracker: Combat started with {deckCards.Count} cards.");
    }

    /// <summary>
    /// Called when a card is played.
    /// </summary>
    public static void OnCardPlayed(string cardId)
    {
        _playedThisCombat.Add(cardId);

        var tracked = _masterDeck.Find(c => c.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
        if (tracked != null)
        {
            tracked.CopiesPlayed++;
        }
    }

    /// <summary>
    /// Called when a card is exhausted.
    /// </summary>
    public static void OnCardExhausted(string cardId)
    {
        var tracked = _masterDeck.Find(c => c.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
        if (tracked != null)
        {
            tracked.CopiesExhausted++;
        }
    }

    /// <summary>
    /// Update pile counts from the game state.
    /// </summary>
    public static void UpdateCounts(int drawPile, int hand, int discard, int exhaust)
    {
        DrawPileCount = drawPile;
        HandCount = hand;
        DiscardPileCount = discard;
        ExhaustPileCount = exhaust;
    }

    /// <summary>
    /// Called when combat ends.
    /// </summary>
    public static void OnCombatEnd()
    {
        _inCombat = false;
        _playedThisCombat.Clear();

        // Reset played/exhausted counters
        foreach (var card in _masterDeck)
        {
            card.CopiesPlayed = 0;
            card.CopiesExhausted = 0;
        }

        MainFile.Logger.Info("DeckTracker: Combat ended.");
    }

    /// <summary>
    /// Called when a new card is added to the deck (from rewards, shops, events).
    /// </summary>
    public static void OnCardAdded(string cardId, string name, int cost, string type)
    {
        var existing = _masterDeck.Find(c => c.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.CopiesInDeck++;
        }
        else
        {
            _masterDeck.Add(new TrackedCard
            {
                CardId = cardId,
                DisplayName = name,
                EnergyCost = cost,
                CardType = type,
                CopiesInDeck = 1
            });
        }
    }

    /// <summary>
    /// Called when a card is removed from the deck (shop removal, events).
    /// </summary>
    public static void OnCardRemoved(string cardId)
    {
        var existing = _masterDeck.Find(c => c.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.CopiesInDeck--;
            if (existing.CopiesInDeck <= 0)
                _masterDeck.Remove(existing);
        }
    }

    /// <summary>
    /// Checks if ALL copies of a card have been played/exhausted in current combat.
    /// </summary>
    public static bool IsFullyUsed(string cardId)
    {
        var tracked = _masterDeck.Find(c => c.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
        if (tracked == null) return false;
        return (tracked.CopiesPlayed + tracked.CopiesExhausted) >= tracked.CopiesInDeck;
    }

    /// <summary>
    /// Returns deck card IDs for synergy calculation.
    /// </summary>
    public static List<string> GetDeckCardIds()
    {
        var ids = new List<string>();
        foreach (var card in _masterDeck)
        {
            for (int i = 0; i < card.CopiesInDeck; i++)
                ids.Add(card.CardId);
        }
        return ids;
    }
}
