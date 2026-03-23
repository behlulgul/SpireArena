using Godot;
using System.Collections.Generic;

namespace SpireArena;

/// <summary>
/// Draws the Build Guide panel on the right side of the screen.
/// Shows: active archetype, synergy indicator, and list of picked cards.
/// </summary>
public partial class BuildGuidePanel : Control
{
    private const float PanelWidth = 230f;
    private const float PanelPadding = 8f;
    private const float HeaderHeight = 56f;
    private const float CardRowH = 20f;
    private const float SectionGap = 6f;

    private float _scrollOffset;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        var viewport = GetViewportRect().Size;
        float panelX = viewport.X - PanelWidth - 12f;
        float panelY = 80f;

        var font = ThemeDB.FallbackFont;
        var archetype = ArchetypeSystem.ActiveArchetype;
        var picked = ArchetypeSystem.PickedCards;

        // Calculate panel height
        float contentH = HeaderHeight + SectionGap;
        if (picked.Count > 0)
            contentH += 20f + (picked.Count * CardRowH) + SectionGap;
        contentH += 32f; // footer hint (two lines)
        float panelH = Mathf.Max(contentH, 80f);
        float maxH = viewport.Y - 160f;
        panelH = Mathf.Min(panelH, maxH);

        // ── Panel background ──
        var panelRect = new Rect2(panelX, panelY, PanelWidth, panelH);
        DrawRect(panelRect, new Color(0.06f, 0.06f, 0.10f, 0.88f));
        DrawRect(panelRect, new Color(0.4f, 0.35f, 0.6f, 0.7f), false, 1.5f);

        float y = panelY;

        // ── Header: archetype name + description ──
        var headerRect = new Rect2(panelX, y, PanelWidth, HeaderHeight);
        DrawRect(headerRect, new Color(0.10f, 0.08f, 0.16f, 0.95f));

        if (archetype != null)
        {
            // Archetype name
            var nameColor = GetArchetypeColor(archetype.Id);
            DrawString(font, new Vector2(panelX + PanelPadding, y + 18f),
                $"▸ {archetype.Name}", HorizontalAlignment.Left,
                (int)(PanelWidth - PanelPadding * 2), 16, nameColor);

            // Character label
            DrawString(font, new Vector2(panelX + PanelWidth - PanelPadding - 60, y + 18f),
                archetype.Character, HorizontalAlignment.Right,
                60, 10, new Color(0.6f, 0.6f, 0.6f, 0.7f));

            // Description (truncated)
            string desc = archetype.Description.Length > 45
                ? archetype.Description[..42] + "..."
                : archetype.Description;
            DrawString(font, new Vector2(panelX + PanelPadding, y + 36f),
                desc, HorizontalAlignment.Left,
                (int)(PanelWidth - PanelPadding * 2), 9, new Color(0.7f, 0.7f, 0.7f, 0.8f));

            // Synergy tag icons
            DrawString(font, new Vector2(panelX + PanelPadding, y + 50f),
                string.Join(" · ", archetype.SynergyTags), HorizontalAlignment.Left,
                (int)(PanelWidth - PanelPadding * 2), 8, new Color(0.5f, 0.8f, 0.5f, 0.6f));
        }
        else
        {
            DrawString(font, new Vector2(panelX + PanelPadding, y + 20f),
                "No Build Selected", HorizontalAlignment.Left,
                (int)(PanelWidth - PanelPadding * 2), 14, new Color(0.5f, 0.5f, 0.5f, 0.7f));

            DrawString(font, new Vector2(panelX + PanelPadding, y + 38f),
                "E: class  R: build", HorizontalAlignment.Left,
                (int)(PanelWidth - PanelPadding * 2), 10, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        y += HeaderHeight + SectionGap;

        // ── Picked cards section ──
        if (picked.Count > 0)
        {
            // Section header
            DrawString(font, new Vector2(panelX + PanelPadding, y + 14f),
                $"PICKED CARDS ({picked.Count})", HorizontalAlignment.Left,
                (int)(PanelWidth - PanelPadding * 2), 10, new Color(0.7f, 0.7f, 0.7f, 0.6f));

            // Separator line
            DrawLine(
                new Vector2(panelX + PanelPadding, y + 18f),
                new Vector2(panelX + PanelWidth - PanelPadding, y + 18f),
                new Color(0.4f, 0.35f, 0.6f, 0.4f), 1f);

            y += 20f;

            float listBottom = panelY + panelH - 24f;

            for (int i = 0; i < picked.Count; i++)
            {
                if (y > listBottom) break;

                var card = picked[i];

                // Alternating row background
                if (i % 2 == 0)
                    DrawRect(new Rect2(panelX + 2, y, PanelWidth - 4, CardRowH), new Color(1, 1, 1, 0.03f));

                float rowX = panelX + PanelPadding;

                // Synergy indicator
                if (card.IsSynergy)
                {
                    DrawString(font, new Vector2(rowX, y + 14f),
                        "★", HorizontalAlignment.Left, 14, 11,
                        new Color(0.3f, 1.0f, 0.4f, 0.9f));
                    rowX += 14f;
                }
                else
                {
                    rowX += 14f;
                }

                // Rating badge
                var tierColor = UIStyles.GetTierColor(card.Rating);
                DrawString(font, new Vector2(rowX, y + 14f),
                    card.Rating.ToString(), HorizontalAlignment.Left, 16, 11, tierColor);
                rowX += 18f;

                // Card name
                DrawString(font, new Vector2(rowX, y + 14f),
                    card.DisplayName, HorizontalAlignment.Left,
                    (int)(PanelWidth - rowX + panelX - PanelPadding), 11,
                    new Color(0.9f, 0.9f, 0.9f, 0.85f));

                y += CardRowH;
            }

            y += SectionGap;
        }

        // ── Footer hint ──
        var hintColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        float footerY = panelY + panelH - 28f;
        DrawString(font, new Vector2(panelX + PanelPadding, footerY),
            "Q: deck  W: ratings  E: class", HorizontalAlignment.Left,
            (int)(PanelWidth - PanelPadding * 2), 8, hintColor);
        DrawString(font, new Vector2(panelX + PanelPadding, footerY + 12f),
            "R: build   T: clear", HorizontalAlignment.Left,
            (int)(PanelWidth - PanelPadding * 2), 8, hintColor);
    }

    private static Color GetArchetypeColor(string archetypeId)
    {
        // ── Silent ──
        if (archetypeId.Contains("shiv")) return new Color(1.0f, 0.85f, 0.2f);
        if (archetypeId.Contains("poison")) return new Color(0.3f, 0.9f, 0.3f);
        if (archetypeId.Contains("sly")) return new Color(0.5f, 0.7f, 1.0f);

        // ── Ironclad ──
        if (archetypeId.Contains("strength")) return new Color(1.0f, 0.4f, 0.3f);
        if (archetypeId.Contains("exhaust")) return new Color(0.8f, 0.5f, 0.8f);
        if (archetypeId.Contains("block")) return new Color(0.3f, 0.6f, 0.9f);
        if (archetypeId.Contains("bloodletting")) return new Color(0.9f, 0.2f, 0.2f);

        // ── Defect ──
        if (archetypeId.Contains("claw")) return new Color(0.9f, 0.7f, 0.2f);
        if (archetypeId.Contains("orb")) return new Color(0.4f, 0.8f, 1.0f);

        // ── Regent ──
        if (archetypeId.Contains("cosmic")) return new Color(0.7f, 0.5f, 1.0f);

        // ── Necrobinder ──
        if (archetypeId.Contains("doom")) return new Color(0.6f, 0.1f, 0.6f);
        if (archetypeId.Contains("osty")) return new Color(0.85f, 0.75f, 0.5f);

        return new Color(0.8f, 0.8f, 0.8f);
    }
}
