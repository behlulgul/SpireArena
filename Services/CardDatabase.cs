using System.Text.Json;
using Godot;

namespace SpireArena;

/// <summary>
/// Loads and queries the card tier list from embedded JSON data.
/// </summary>
public static class CardDatabase
{
    private static readonly Dictionary<string, CardTierEntry> _cards = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;
    private static int _loadAttempts;
    private const int MaxLoadAttempts = 5;

    public static void Load()
    {
        if (_loaded) return;
        if (_loadAttempts >= MaxLoadAttempts) return;
        _loadAttempts++;

        try
        {
            var jsonPath = FindCardTierListPath();

            if (jsonPath == null)
            {
                MainFile.Logger.Warn($"CardTierList.json not found (attempt {_loadAttempts}/{MaxLoadAttempts}). Will retry later.");
                return;
            }

            MainFile.Logger.Info($"CardDatabase loading from: {jsonPath}");

            var json = System.IO.File.ReadAllText(jsonPath);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("cards", out var cardsArray))
            {
                foreach (var cardEl in cardsArray.EnumerateArray())
                {
                    var entry = new CardTierEntry
                    {
                        Id = cardEl.GetProperty("id").GetString() ?? "",
                        Name = cardEl.GetProperty("name").GetString() ?? "",
                        Cost = cardEl.TryGetProperty("cost", out var costEl) ? costEl.GetInt32() : 1,
                        Type = cardEl.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "" : "",
                        Rarity = cardEl.TryGetProperty("rarity", out var rarityEl) ? rarityEl.GetString() ?? "" : "",
                        Character = cardEl.TryGetProperty("character", out var charEl) ? charEl.GetString() ?? "" : "",
                        BaseRating = cardEl.GetProperty("baseRating").GetInt32(),
                        Tags = DeserializeTags(cardEl)
                    };
                    _cards[entry.Id] = entry;
                }
            }

            MainFile.Logger.Info($"CardDatabase loaded: {_cards.Count} cards.");
            _loaded = true;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Error loading CardDatabase (attempt {_loadAttempts}): {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures the database is loaded before querying. Retries loading if previous attempts failed.
    /// </summary>
    private static void EnsureLoaded()
    {
        if (!_loaded && _cards.Count == 0)
            Load();
    }

    /// <summary>
    /// Resolves the CardTierList.json path relative to the mod's assembly location.
    /// The DLL is deployed at mods/SpireArena/SpireArena.dll, so the tier list
    /// is always at mods/SpireArena/Data/CardTierList.json next to it.
    /// </summary>
    private static string? FindCardTierListPath()
    {
        try
        {
            var asmLocation = typeof(CardDatabase).Assembly.Location;
            if (string.IsNullOrEmpty(asmLocation))
            {
                MainFile.Logger.Warn("CardDatabase: Assembly.Location is empty.");
                return null;
            }

            var modDir = System.IO.Path.GetDirectoryName(asmLocation) ?? "";
            var jsonPath = System.IO.Path.Combine(modDir, "Data", "CardTierList.json");

            MainFile.Logger.Info($"CardDatabase path: {jsonPath}");

            if (System.IO.File.Exists(jsonPath))
                return jsonPath;

            MainFile.Logger.Warn($"CardTierList.json not found at: {jsonPath}");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"CardDatabase: Failed to resolve path: {ex.Message}");
        }

        return null;
    }

    private static string[] DeserializeTags(JsonElement cardEl)
    {
        if (!cardEl.TryGetProperty("tags", out var tagsEl))
            return [];

        var tags = new List<string>();
        foreach (var tag in tagsEl.EnumerateArray())
        {
            var val = tag.GetString();
            if (val != null)
                tags.Add(val);
        }
        return tags.ToArray();
    }

    /// <summary>
    /// Look up a card's tier entry by its game ID (case-insensitive).
    /// </summary>
    public static CardTierEntry? GetByCardId(string cardId)
    {
        EnsureLoaded();
        _cards.TryGetValue(cardId, out var entry);
        return entry;
    }

    /// <summary>
    /// Try to find a card by partial name match (for display names).
    /// Supports exact match first, then contains match as fallback.
    /// </summary>
    public static CardTierEntry? GetByName(string cardName)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(cardName)) return null;

        // First: exact match
        foreach (var entry in _cards.Values)
        {
            if (entry.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        // Second: contains match (e.g. "Setup Strike" matches "SetupStrike" or vice versa)
        var normalizedSearch = cardName.Replace(" ", "").Replace("_", "");
        foreach (var entry in _cards.Values)
        {
            var normalizedEntry = entry.Name.Replace(" ", "").Replace("_", "");
            if (normalizedEntry.Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        // Third: partial match (search term contained in card name or vice versa)
        foreach (var entry in _cards.Values)
        {
            if (entry.Name.Contains(cardName, StringComparison.OrdinalIgnoreCase) ||
                cardName.Contains(entry.Name, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Calculate a contextual rating considering current deck synergies.
    /// Returns the base rating plus a synergy bonus (-2 to +2).
    /// </summary>
    public static int GetContextualRating(string cardId, List<string> currentDeckCardIds)
    {
        var entry = GetByCardId(cardId);
        if (entry == null) return 5; // Unknown card defaults to average

        int synergy = CalculateSynergy(entry, currentDeckCardIds);
        return Math.Clamp(entry.BaseRating + synergy, 1, 10);
    }

    private static int CalculateSynergy(CardTierEntry candidate, List<string> deckCardIds)
    {
        if (deckCardIds.Count == 0) return 0;

        int bonus = 0;
        var deckTags = new HashSet<string>();
        foreach (var id in deckCardIds)
        {
            var deckCard = GetByCardId(id);
            if (deckCard != null)
            {
                foreach (var tag in deckCard.Tags)
                    deckTags.Add(tag);
            }
        }

        // Reward synergistic picks
        foreach (var tag in candidate.Tags)
        {
            if (tag.EndsWith("-synergy"))
            {
                string baseTag = tag.Replace("-synergy", "");
                if (deckTags.Contains(baseTag))
                    bonus++;
            }
        }

        // If deck has exhaust cards, exhaust-synergy cards are better
        if (deckTags.Contains("exhaust") && candidate.Tags.Contains("exhaust-synergy"))
            bonus++;

        // Strength scaling bonus if deck already has strength
        if (deckTags.Contains("strength") && candidate.Tags.Contains("strength-synergy"))
            bonus++;

        return Math.Clamp(bonus, -2, 2);
    }

    public static bool IsLoaded => _loaded;
    public static int CardCount => _cards.Count;
    public static IReadOnlyDictionary<string, CardTierEntry> AllCards => _cards;
}
