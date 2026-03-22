using Godot;

namespace SpireArena;

/// <summary>
/// Draws the deck tracker panel on the right side of the screen.
/// Shows all cards in deck, dims played/exhausted ones.
/// Shows draw/discard/exhaust pile counts.
/// </summary>
public partial class DeckTrackerUI : Control
{
    private float _scrollOffset;
    private bool _isHovered;

    public override void _Ready()
    {
        // Position at right side of screen
        SetAnchorsPreset(LayoutPreset.RightWide);
        MouseFilter = MouseFilterEnum.Pass;
    }

    public override void _Process(double delta)
    {
        if (!ModConfig.ShowDeckTracker) return;
        if (!DeckTracker.InCombat && DeckTracker.MasterDeck.Count == 0) return;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!ModConfig.ShowDeckTracker) return;

        var deck = DeckTracker.MasterDeck;
        if (deck.Count == 0) return;

        var viewport = GetViewportRect().Size;
        float panelX = viewport.X - UIStyles.PanelWidth - 10;
        float panelY = 80;
        float maxHeight = viewport.Y - 160;

        // Calculate total height
        float contentHeight = UIStyles.HeaderHeight + UIStyles.CounterHeight + (deck.Count * UIStyles.CardRowHeight) + UIStyles.Padding * 2;
        float panelHeight = Mathf.Min(contentHeight, maxHeight);

        // === Draw Panel Background ===
        var panelRect = new Rect2(panelX, panelY, UIStyles.PanelWidth, panelHeight);
        DrawRect(panelRect, UIStyles.PanelBackground);
        DrawRect(panelRect, UIStyles.PanelBorder, false, 2.0f);

        // === Draw Header ===
        var headerRect = new Rect2(panelX, panelY, UIStyles.PanelWidth, UIStyles.HeaderHeight);
        DrawRect(headerRect, UIStyles.HeaderBackground);

        var headerFont = ThemeDB.FallbackFont;
        DrawString(headerFont, new Vector2(panelX + UIStyles.Padding, panelY + 20),
            "DECK TRACKER", HorizontalAlignment.Left, UIStyles.PanelWidth - UIStyles.Padding * 2,
            UIStyles.FontSizeLarge, UIStyles.TextWhite);

        float y = panelY + UIStyles.HeaderHeight + UIStyles.Padding;

        // === Draw Pile Counters ===
        DrawPileCounters(panelX, y, headerFont);
        y += UIStyles.CounterHeight;

        // === Draw Card List ===
        float listTop = y;
        float listBottom = panelY + panelHeight - UIStyles.Padding;

        // Clamp scroll
        float maxScroll = Mathf.Max(0, contentHeight - maxHeight);
        _scrollOffset = Mathf.Clamp(_scrollOffset, 0, maxScroll);

        float cardY = listTop - _scrollOffset;

        foreach (var card in deck)
        {
            if (cardY + UIStyles.CardRowHeight < listTop)
            {
                cardY += UIStyles.CardRowHeight;
                continue;
            }
            if (cardY > listBottom) break;

            DrawCardRow(panelX, cardY, card, headerFont);
            cardY += UIStyles.CardRowHeight;
        }
    }

    private void DrawPileCounters(float x, float y, Font font)
    {
        float colWidth = (UIStyles.PanelWidth - UIStyles.Padding * 2) / 3f;

        // Draw pile
        DrawString(font, new Vector2(x + UIStyles.Padding, y + 16),
            $"D:{DeckTracker.DrawPileCount}", HorizontalAlignment.Left,
            (int)colWidth, UIStyles.FontSizeSmall, UIStyles.DrawPileColor);

        // Discard pile
        DrawString(font, new Vector2(x + UIStyles.Padding + colWidth, y + 16),
            $"X:{DeckTracker.DiscardPileCount}", HorizontalAlignment.Left,
            (int)colWidth, UIStyles.FontSizeSmall, UIStyles.DiscardColor);

        // Exhaust pile
        DrawString(font, new Vector2(x + UIStyles.Padding + colWidth * 2, y + 16),
            $"E:{DeckTracker.ExhaustPileCount}", HorizontalAlignment.Left,
            (int)colWidth, UIStyles.FontSizeSmall, UIStyles.ExhaustColor);
    }

    private void DrawCardRow(float panelX, float y, DeckTracker.TrackedCard card, Font font)
    {
        bool isUsed = DeckTracker.IsFullyUsed(card.CardId);
        float alpha = isUsed ? ModConfig.DimmedCardOpacity : 1.0f;

        // Row background (alternating)
        int idx = ((System.Collections.Generic.IList<DeckTracker.TrackedCard>)DeckTracker.MasterDeck).IndexOf(card);
        if (idx % 2 == 0)
        {
            var rowBg = new Color(1, 1, 1, 0.03f);
            DrawRect(new Rect2(panelX + 2, y, UIStyles.PanelWidth - 4, UIStyles.CardRowHeight), rowBg);
        }

        float textX = panelX + UIStyles.Padding;

        // Energy cost
        string costStr = card.EnergyCost >= 0 ? card.EnergyCost.ToString() : "X";
        var costColor = new Color(0.8f, 0.8f, 0.2f, alpha);
        DrawString(font, new Vector2(textX, y + 16), costStr,
            HorizontalAlignment.Left, 16, UIStyles.FontSizeSmall, costColor);
        textX += 18;

        // Card name
        var nameColor = UIStyles.GetCardTypeColor(card.CardType);
        nameColor.A = alpha;
        string displayName = card.DisplayName;
        if (card.CopiesInDeck > 1)
            displayName += $" x{card.CopiesInDeck}";

        DrawString(font, new Vector2(textX, y + 16), displayName,
            HorizontalAlignment.Left, UIStyles.PanelWidth - 50, UIStyles.FontSizeSmall, nameColor);

        // Play count indicator
        if (card.CopiesPlayed > 0)
        {
            var checkColor = new Color(0.3f, 0.8f, 0.3f, alpha);
            string playedStr = card.CopiesPlayed > 1 ? $"✓{card.CopiesPlayed}" : "✓";
            DrawString(font, new Vector2(panelX + UIStyles.PanelWidth - 30, y + 16),
                playedStr, HorizontalAlignment.Right, 25, UIStyles.FontSizeSmall, checkColor);
        }

        // Exhausted indicator
        if (card.CopiesExhausted > 0)
        {
            var exhColor = new Color(0.6f, 0.3f, 0.3f, alpha);
            DrawString(font, new Vector2(panelX + UIStyles.PanelWidth - 30, y + 16),
                "✗", HorizontalAlignment.Right, 25, UIStyles.FontSizeSmall, exhColor);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                _scrollOffset -= UIStyles.CardRowHeight * 2;
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                _scrollOffset += UIStyles.CardRowHeight * 2;
            }
        }
    }
}
