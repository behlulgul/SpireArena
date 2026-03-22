using Godot;

namespace SpireArena;

/// <summary>
/// Centralized UI style definitions: colors, fonts, sizes.
/// </summary>
public static class UIStyles
{
    // === Rating Tier Colors ===
    public static readonly Color TierS = new(0.0f, 0.9f, 0.3f);     // Bright green (9-10)
    public static readonly Color TierA = new(0.4f, 0.85f, 0.2f);    // Green (7-8)
    public static readonly Color TierB = new(1.0f, 0.85f, 0.0f);    // Yellow (5-6)
    public static readonly Color TierC = new(1.0f, 0.5f, 0.0f);     // Orange (3-4)
    public static readonly Color TierD = new(1.0f, 0.2f, 0.2f);     // Red (1-2)

    // === Card Type Colors ===
    public static readonly Color AttackColor = new(0.9f, 0.3f, 0.3f);
    public static readonly Color SkillColor = new(0.3f, 0.6f, 0.9f);
    public static readonly Color PowerColor = new(0.9f, 0.7f, 0.2f);

    // === Panel Colors ===
    public static readonly Color PanelBackground = new(0.08f, 0.08f, 0.12f, 0.85f);
    public static readonly Color PanelBorder = new(0.4f, 0.35f, 0.55f, 0.9f);
    public static readonly Color HeaderBackground = new(0.12f, 0.1f, 0.18f, 0.95f);
    public static readonly Color TextWhite = new(0.95f, 0.95f, 0.95f);
    public static readonly Color TextDimmed = new(0.5f, 0.5f, 0.5f, 0.4f);

    // === Counter Colors ===
    public static readonly Color DrawPileColor = new(0.3f, 0.7f, 0.3f);
    public static readonly Color DiscardColor = new(0.7f, 0.3f, 0.3f);
    public static readonly Color ExhaustColor = new(0.5f, 0.5f, 0.5f);

    // === Sizes ===
    public const int PanelWidth = 220;
    public const int CardRowHeight = 22;
    public const int HeaderHeight = 30;
    public const int CounterHeight = 24;
    public const int RatingBadgeSize = 28;
    public const int FontSizeNormal = 14;
    public const int FontSizeSmall = 12;
    public const int FontSizeLarge = 18;
    public const int Padding = 6;

    /// <summary>
    /// Get tier color based on rating value (1-10).
    /// </summary>
    public static Color GetTierColor(int rating)
    {
        return rating switch
        {
            >= 9 => TierS,
            >= 7 => TierA,
            >= 5 => TierB,
            >= 3 => TierC,
            _ => TierD,
        };
    }

    /// <summary>
    /// Get a letter grade for the rating.
    /// </summary>
    public static string GetTierLabel(int rating)
    {
        return rating switch
        {
            >= 9 => "S",
            >= 7 => "A",
            >= 5 => "B",
            >= 3 => "C",
            _ => "D",
        };
    }

    /// <summary>
    /// Get the color for a card type string.
    /// </summary>
    public static Color GetCardTypeColor(string cardType)
    {
        return cardType.ToLowerInvariant() switch
        {
            "attack" => AttackColor,
            "skill" => SkillColor,
            "power" => PowerColor,
            _ => TextWhite,
        };
    }
}
