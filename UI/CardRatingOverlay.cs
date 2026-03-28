using Godot;
using System.Collections.Generic;

namespace SpireArena;

/// <summary>
/// Draws HearthArena-style card tier ratings above cards during reward/draft screens.
/// </summary>
public partial class CardRatingOverlay : Control
{
    /// <summary>
    /// Cards currently being offered for selection (set by GameSceneWatcher or CardRewardHook).
    /// </summary>
    public static readonly List<OfferedCard> CurrentOfferedCards = new();

    /// <summary>
    /// Set to true only when a confirmed card reward/draft screen is active.
    /// Prevents badges from appearing on combat hand cards.
    /// </summary>
    public static bool IsRewardScreenActive { get; set; }

    public class OfferedCard
    {
        public string CardId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public Vector2 ScreenPosition { get; set; }
        public Vector2 CardSize { get; set; }
        public int Rating { get; set; }
        public int ContextualRating { get; set; }
        public bool DetectedFromScene { get; set; }
        /// <summary>Rank among offered cards (1 = best pick).</summary>
        public int Rank { get; set; }
        /// <summary>Relative score 0-100 showing strength compared to other offered cards.</summary>
        public int PickScore { get; set; } = 50;
    }

    // Badge dimensions
    private const float BadgeWidth = 130f;
    private const float BadgeHeight = 44f;
    private const float BadgeGap = 4f;
    private const float AccentWidth = 6f;

    private int _prevCardCount;
    private string? _lastArchetypeId;
    private int _lastPickedCardCount;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        // Recompute ratings when the active build/class changes
        var currentArchetypeId = ArchetypeSystem.ActiveArchetype?.Id;
        int currentPickedCount = ArchetypeSystem.PickedCards.Count;

        if (currentArchetypeId != _lastArchetypeId || currentPickedCount != _lastPickedCardCount)
        {
            _lastArchetypeId = currentArchetypeId;
            _lastPickedCardCount = currentPickedCount;
            RecomputeRatings();
        }

        int count = CurrentOfferedCards.Count;
        bool shouldShow = ModConfig.ShowCardRatings && IsRewardScreenActive && count > 0;

        // Always redraw once more when cards are cleared or ratings toggled off
        // so the canvas is wiped clean (Godot keeps the last _Draw output).
        if (!shouldShow)
        {
            if (_prevCardCount > 0)
            {
                _prevCardCount = 0;
                QueueRedraw();
            }
            return;
        }

        _prevCardCount = count;
        QueueRedraw();
    }

    /// <summary>
    /// Recompute Rating and ContextualRating for all offered cards
    /// based on the currently active archetype/build, then update
    /// relative scores (Rank, PickScore) among the current offer set.
    /// </summary>
    private static void RecomputeRatings()
    {
        if (CurrentOfferedCards.Count == 0) return;
        var deckCardIds = DeckTracker.GetDeckCardIds();
        foreach (var card in CurrentOfferedCards)
        {
            var tierEntry = CardDatabase.GetByCardId(card.CardId);
            if (tierEntry != null)
            {
                card.Rating = tierEntry.BaseRating;
                card.ContextualRating = CardDatabase.GetContextualRating(tierEntry.Id, deckCardIds);
            }
        }
        ComputeRelativeScores();
    }

    /// <summary>
    /// Compute Rank and PickScore for each offered card relative to the others.
    /// Called after contextual ratings are computed so the comparison is up-to-date.
    /// </summary>
    public static void ComputeRelativeScores()
    {
        if (CurrentOfferedCards.Count < 2)
        {
            if (CurrentOfferedCards.Count == 1)
            {
                CurrentOfferedCards[0].Rank = 1;
                CurrentOfferedCards[0].PickScore = 100;
            }
            return;
        }

        // Sort by effective rating descending to assign ranks
        var sorted = new List<OfferedCard>(CurrentOfferedCards);
        sorted.Sort((a, b) =>
        {
            int ra = a.ContextualRating > 0 ? a.ContextualRating : a.Rating;
            int rb = b.ContextualRating > 0 ? b.ContextualRating : b.Rating;
            return rb.CompareTo(ra);
        });

        int maxR = EffectiveRating(sorted[0]);
        int minR = EffectiveRating(sorted[^1]);
        int range = maxR - minR;

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
            sorted[i].PickScore = range > 0
                ? (int)((EffectiveRating(sorted[i]) - minR) / (float)range * 100)
                : 50;
        }
    }

    private static int EffectiveRating(OfferedCard c)
        => c.ContextualRating > 0 ? c.ContextualRating : c.Rating;

    public override void _Draw()
    {
        if (!ModConfig.ShowCardRatings) return;
        if (!IsRewardScreenActive) return;
        if (CurrentOfferedCards.Count == 0) return;

        var font = ThemeDB.FallbackFont;

        // Find the best card (highest rating) for highlighting
        int bestRating = 0;
        foreach (var c in CurrentOfferedCards)
        {
            int r = c.ContextualRating > 0 ? c.ContextualRating : c.Rating;
            if (r > bestRating) bestRating = r;
        }

        foreach (var card in CurrentOfferedCards)
        {
            if (card.Rating <= 0 && string.IsNullOrEmpty(card.DisplayName))
                continue;

            int effectiveRating = card.ContextualRating > 0 ? card.ContextualRating : card.Rating;
            bool isBest = effectiveRating > 0 && effectiveRating == bestRating && CurrentOfferedCards.Count > 1;
            DrawBadge(card, font, isBest);
        }
    }

    private void DrawBadge(OfferedCard card, Font font, bool isBest)
    {
        int rating = card.ContextualRating > 0 ? card.ContextualRating : card.Rating;
        var tierColor = UIStyles.GetTierColor(rating);
        string tierLabel = UIStyles.GetTierLabel(rating);

        // Position: centered above card
        float cardCenterX = card.ScreenPosition.X + card.CardSize.X / 2f;
        float cardTopY = card.ScreenPosition.Y;
        float badgeX = cardCenterX - BadgeWidth / 2f;
        float badgeY = cardTopY - BadgeHeight - BadgeGap;

        // ========================================
        //  BEST PICK: Golden outer glow
        // ========================================
        if (isBest)
        {
            var glowColor = new Color(1.0f, 0.85f, 0.2f, 0.18f);
            DrawRect(new Rect2(badgeX - 5, badgeY - 5, BadgeWidth + 10, BadgeHeight + 10), glowColor);
            var glowColor2 = new Color(1.0f, 0.85f, 0.2f, 0.10f);
            DrawRect(new Rect2(badgeX - 8, badgeY - 8, BadgeWidth + 16, BadgeHeight + 16), glowColor2);
        }

        // ========================================
        //  Shadow
        // ========================================
        DrawRect(new Rect2(badgeX + 3, badgeY + 3, BadgeWidth, BadgeHeight),
            new Color(0, 0, 0, 0.6f));

        // ========================================
        //  Main background
        // ========================================
        DrawRect(new Rect2(badgeX, badgeY, BadgeWidth, BadgeHeight),
            new Color(0.08f, 0.07f, 0.12f, 0.95f));

        // ========================================
        //  Score section (left ~50px) — tier-colored background
        // ========================================
        float scoreSectionW = 50f;
        var scoreBgColor = new Color(tierColor.R * 0.25f, tierColor.G * 0.25f, tierColor.B * 0.25f, 0.95f);
        DrawRect(new Rect2(badgeX, badgeY, scoreSectionW, BadgeHeight), scoreBgColor);

        // Left accent bar
        DrawRect(new Rect2(badgeX, badgeY, AccentWidth, BadgeHeight), tierColor);

        // Rating number — big & centered in score section
        string ratingText = rating > 0 ? rating.ToString() : "?";
        float numX = badgeX + AccentWidth + (scoreSectionW - AccentWidth) / 2f;
        DrawString(font, new Vector2(numX - 8, badgeY + 30),
            ratingText, HorizontalAlignment.Center,
            (int)(scoreSectionW - AccentWidth), 26, Colors.White);

        // ========================================
        //  Tier label section (right side)
        // ========================================
        float labelX = badgeX + scoreSectionW + 8f;
        float labelY = badgeY + 18f;

        // Tier letter — large
        DrawString(font, new Vector2(labelX, labelY + 2),
            tierLabel, HorizontalAlignment.Left,
            24, 18, tierColor);

        // "TIER" text — small, below tier letter
        DrawString(font, new Vector2(labelX, labelY + 16),
            "TIER", HorizontalAlignment.Left,
            30, 9, new Color(0.6f, 0.6f, 0.6f, 0.8f));

        // Synergy indicator (right side of tier section)
        if (card.ContextualRating > card.Rating && card.Rating > 0)
        {
            int diff = card.ContextualRating - card.Rating;
            DrawString(font, new Vector2(labelX + 36, labelY + 2),
                $"▲{diff}", HorizontalAlignment.Left,
                30, 13, new Color(0.3f, 1.0f, 0.4f, 0.9f));
        }
        else if (card.ContextualRating < card.Rating && card.ContextualRating > 0)
        {
            int diff = card.Rating - card.ContextualRating;
            DrawString(font, new Vector2(labelX + 36, labelY + 2),
                $"▼{diff}", HorizontalAlignment.Left,
                30, 13, new Color(1.0f, 0.4f, 0.3f, 0.9f));
        }

        // ========================================
        //  Borders
        // ========================================
        Color borderColor = isBest ? new Color(1.0f, 0.85f, 0.2f, 0.9f) : tierColor;
        float borderW = isBest ? 2.0f : 1.5f;
        // Top
        DrawRect(new Rect2(badgeX, badgeY, BadgeWidth, borderW), borderColor);
        // Bottom
        DrawRect(new Rect2(badgeX, badgeY + BadgeHeight - borderW, BadgeWidth, borderW), borderColor);
        // Right
        DrawRect(new Rect2(badgeX + BadgeWidth - borderW, badgeY, borderW, BadgeHeight), borderColor);

        // ========================================
        //  Rank label (BEST / #2 / #3 / #4)
        // ========================================
        if (rating > 0 && CurrentOfferedCards.Count > 1)
        {
            float rankLabelW = isBest ? 42f : 28f;
            float rankLabelH = 16f;
            float rankX = badgeX + BadgeWidth - rankLabelW - 4;
            float rankY = badgeY - rankLabelH - 2;

            if (isBest)
            {
                DrawRect(new Rect2(rankX, rankY, rankLabelW, rankLabelH),
                    new Color(1.0f, 0.85f, 0.2f, 0.95f));
                DrawString(font, new Vector2(rankX + 4, rankY + 12),
                    "BEST", HorizontalAlignment.Left,
                    40, 10, new Color(0.1f, 0.08f, 0.05f));
            }
            else if (card.Rank >= 2)
            {
                string rankText = card.Rank switch
                {
                    2 => "#2",
                    3 => "#3",
                    4 => "#4",
                    _ => $"#{card.Rank}"
                };
                var rankBgColor = new Color(0.3f, 0.3f, 0.35f, 0.85f);
                DrawRect(new Rect2(rankX, rankY, rankLabelW, rankLabelH), rankBgColor);
                DrawString(font, new Vector2(rankX + 4, rankY + 12),
                    rankText, HorizontalAlignment.Left,
                    26, 10, new Color(0.8f, 0.8f, 0.85f, 0.9f));
            }
        }
    }

    public static void ClearOfferedCards()
    {
        CurrentOfferedCards.Clear();
        IsRewardScreenActive = false;
    }
}
