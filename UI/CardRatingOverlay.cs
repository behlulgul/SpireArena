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

    public class OfferedCard
    {
        public string CardId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public Vector2 ScreenPosition { get; set; }
        public Vector2 CardSize { get; set; }
        public int Rating { get; set; }
        public int ContextualRating { get; set; }
        public bool DetectedFromScene { get; set; }
    }

    // Badge dimensions
    private const float BadgeWidth = 130f;
    private const float BadgeHeight = 44f;
    private const float BadgeGap = 10f;
    private const float AccentWidth = 6f;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        if (!ModConfig.ShowCardRatings) return;
        if (CurrentOfferedCards.Count == 0) return;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!ModConfig.ShowCardRatings) return;
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
        //  "BEST" label for top pick
        // ========================================
        if (isBest && rating > 0)
        {
            float bestLabelW = 42f;
            float bestLabelH = 16f;
            float bestX = badgeX + BadgeWidth - bestLabelW - 4;
            float bestY = badgeY - bestLabelH - 2;

            DrawRect(new Rect2(bestX, bestY, bestLabelW, bestLabelH),
                new Color(1.0f, 0.85f, 0.2f, 0.95f));
            DrawString(font, new Vector2(bestX + 4, bestY + 12),
                "BEST", HorizontalAlignment.Left,
                40, 10, new Color(0.1f, 0.08f, 0.05f));
        }

        // ========================================
        //  Card name label (below badge)
        // ========================================
        if (!string.IsNullOrEmpty(card.DisplayName))
        {
            float nameY = badgeY + BadgeHeight + 14f;
            var nameSize = font.GetStringSize(card.DisplayName, HorizontalAlignment.Center, -1, 11);
            float nameBgW = Mathf.Max(nameSize.X + 12f, 60f);
            float nameBgX = cardCenterX - nameBgW / 2f;

            // Name background
            DrawRect(new Rect2(nameBgX, nameY - 12f, nameBgW, 16f),
                new Color(0.06f, 0.05f, 0.10f, 0.85f));
            // Name text
            DrawString(font, new Vector2(cardCenterX, nameY),
                card.DisplayName, HorizontalAlignment.Center,
                (int)nameBgW, 11, new Color(0.9f, 0.9f, 0.9f, 0.85f));
        }

        // ========================================
        //  Connector line (badge → card)
        // ========================================
        var lineColor = new Color(tierColor.R, tierColor.G, tierColor.B, 0.3f);
        DrawLine(
            new Vector2(cardCenterX, badgeY + BadgeHeight),
            new Vector2(cardCenterX, cardTopY),
            lineColor, 1.0f);
    }

    public static void ClearOfferedCards()
    {
        CurrentOfferedCards.Clear();
    }
}
