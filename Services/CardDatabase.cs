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
    /// Try to find a card by exact or normalized name match (no partial/substring matching).
    /// Handles upgraded card names with "+" suffix (e.g. "Piercing Wail+" → "Piercing Wail").
    /// Use this for card name label detection to avoid false positives from game keywords.
    /// </summary>
    public static CardTierEntry? GetByExactName(string cardName)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(cardName)) return null;

        var result = MatchExactOrNormalized(cardName);
        if (result != null) return result;

        // Strip upgrade suffix "+" (e.g. "Piercing Wail+" → "Piercing Wail")
        if (cardName.EndsWith('+'))
        {
            var baseName = cardName[..^1].Trim();
            if (baseName.Length > 0)
                return MatchExactOrNormalized(baseName);
        }

        return null;
    }

    /// <summary>
    /// Attempt exact match, then normalized match (spaces/underscores removed).
    /// </summary>
    private static CardTierEntry? MatchExactOrNormalized(string name)
    {
        // Exact match
        foreach (var entry in _cards.Values)
        {
            if (entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        // Normalized match (e.g. "Setup Strike" matches "SetupStrike")
        var normalizedSearch = name.Replace(" ", "").Replace("_", "");
        foreach (var entry in _cards.Values)
        {
            var normalizedEntry = entry.Name.Replace(" ", "").Replace("_", "");
            if (normalizedEntry.Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Try to find a card by partial name match (for display names).
    /// Supports exact match first, then contains match as fallback.
    /// </summary>
    public static CardTierEntry? GetByName(string cardName)
    {
        // Try exact/normalized first
        var exact = GetByExactName(cardName);
        if (exact != null) return exact;

        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(cardName)) return null;

        // Partial match (search term contained in card name or vice versa)
        foreach (var entry in _cards.Values)
        {
            if (entry.Name.Contains(cardName, StringComparison.OrdinalIgnoreCase) ||
                cardName.Contains(entry.Name, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Calculate a contextual rating considering the active build's
    /// per-card overrides, current deck synergies, picked card synergies,
    /// and fallback archetype bonus.
    /// Priority: BuildRating override > BaseRating + synergy + archetypeBonus.
    /// Picked cards from this run are included in synergy calculation so that
    /// each new reward screen reflects the user's previous choices.
    /// </summary>
    public static int GetContextualRating(string cardId, List<string> currentDeckCardIds)
    {
        var entry = GetByCardId(cardId);
        if (entry == null) return 5; // Unknown card defaults to average

        // Merge deck card IDs with picked card IDs for comprehensive synergy
        var allCardIds = GetMergedDeckAndPickedIds(currentDeckCardIds);

        // If a build is active and defines a specific rating for this card, use it.
        int? buildRating = ArchetypeSystem.GetBuildRating(entry.Id);
        if (buildRating.HasValue)
        {
            // Still apply deck synergy on top of the build rating (but smaller range)
            int synergy = CalculateSynergy(entry, allCardIds);
            return Math.Clamp(buildRating.Value + synergy, 1, 10);
        }

        // Fallback: global BaseRating + synergy + old archetype bonus
        int baseSynergy = CalculateSynergy(entry, allCardIds);
        int archetypeBonus = ArchetypeSystem.GetArchetypeBonus(entry.Id, entry.Tags);
        return Math.Clamp(entry.BaseRating + baseSynergy + archetypeBonus, 1, 10);
    }

    /// <summary>
    /// Merge the current deck card IDs with the picked card IDs from this run.
    /// This ensures synergy calculations account for cards the user already chose
    /// in previous reward screens, even if DeckTracker doesn't track them yet.
    /// </summary>
    private static List<string> GetMergedDeckAndPickedIds(List<string> deckCardIds)
    {
        var pickedCards = ArchetypeSystem.PickedCards;
        if (pickedCards.Count == 0)
            return deckCardIds;

        var merged = new List<string>(deckCardIds.Count + pickedCards.Count);
        merged.AddRange(deckCardIds);

        var existing = new HashSet<string>(deckCardIds, StringComparer.OrdinalIgnoreCase);
        foreach (var picked in pickedCards)
        {
            if (!string.IsNullOrEmpty(picked.CardId) && existing.Add(picked.CardId))
                merged.Add(picked.CardId);
        }

        return merged;
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
