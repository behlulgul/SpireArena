using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SpireArena;

/// <summary>
/// Scans the Godot scene tree every few frames to detect card reward screens
/// and other game states. This works independently of Harmony patches, making
/// it resilient to game assembly changes.
///
/// Detection strategy:
///   1. Walk the scene tree looking for visible "Choose a Card" text (Label/RichTextLabel)
///   2. Navigate to the parent container to find individual card nodes
///   3. Extract card positions for overlay placement
///   4. Try to extract card IDs/names from node properties or child labels
/// </summary>
public partial class GameSceneWatcher : Node
{
    private bool _rewardScreenActive;
    private ulong _lastScanFrame;
    private const int ScanInterval = 12; // scan every N frames (~5 times/sec at 60fps)

    /// <summary>
    /// Set to true after first successful detection to reduce logging noise.
    /// </summary>
    private bool _hasEverDetected;

    /// <summary>
    /// Set to true after first overlay population to log diagnostic info once.
    /// </summary>
    private bool _hasLoggedOverlayDiag;

    public override void _Process(double delta)
    {
        var frame = Engine.GetProcessFrames();
        if (frame - _lastScanFrame < ScanInterval) return;
        _lastScanFrame = frame;

        try
        {
            var root = GetTree()?.Root;
            if (root == null) return;

            DetectCardRewardScreen(root);
        }
        catch (System.Exception ex)
        {
            // Never let the scene watcher crash the game
            if (!_hasEverDetected)
            {
                MainFile.Logger.Warn($"[SceneWatcher] Scan error: {ex.Message}");
                _hasEverDetected = true; // Suppress repeated logging
            }
        }
    }

    private void DetectCardRewardScreen(Node root)
    {
        var chooseLabel = FindChooseCardLabel(root);

        if (chooseLabel != null && !_rewardScreenActive)
        {
            _rewardScreenActive = true;
            if (!_hasEverDetected)
            {
                MainFile.Logger.Info("[SceneWatcher] Card reward screen detected!");
                _hasEverDetected = true;
            }
            PopulateOverlay(chooseLabel);
        }
        else if (chooseLabel != null && _rewardScreenActive)
        {
            // Still active — update positions in case cards animated
            PopulateOverlay(chooseLabel);
        }
        else if (chooseLabel == null && _rewardScreenActive)
        {
            _rewardScreenActive = false;
            _hasLoggedOverlayDiag = false;
            CardRatingOverlay.ClearOfferedCards();
        }
    }

    /// <summary>
    /// Recursively search for a visible Label containing "Choose a Card" or similar text.
    /// </summary>
    private Node? FindChooseCardLabel(Node node)
    {
        if (IsChooseCardLabel(node))
            return node;

        foreach (var child in node.GetChildren())
        {
            var found = FindChooseCardLabel(child);
            if (found != null) return found;
        }
        return null;
    }

    private static bool IsChooseCardLabel(Node node)
    {
        string? text = null;
        bool visible = false;

        if (node is Label label && label.IsVisibleInTree())
        {
            text = label.Text;
            visible = true;
        }
        else if (node is RichTextLabel rich && rich.IsVisibleInTree())
        {
            text = rich.GetParsedText();
            visible = true;
        }

        if (!visible || string.IsNullOrEmpty(text)) return false;

        var lower = text.ToLowerInvariant();
        return (lower.Contains("choose") && lower.Contains("card")) ||
               lower.Contains("pick a card") ||
               lower.Contains("select a card") ||
               lower.Contains("card reward");
    }

    /// <summary>
    /// Given the "Choose a Card" label node, find card names and positions,
    /// then populate the CardRatingOverlay.
    /// 
    /// Strategy: Collect ALL visible text labels from the reward screen subtree.
    /// Filter for card-name-like text. Match them to detected card positions by X order.
    /// </summary>
    private void PopulateOverlay(Node chooseLabel)
    {
        CardRatingOverlay.ClearOfferedCards();

        // Navigate up to reward screen root (go up enough levels to capture everything)
        var rewardRoot = chooseLabel;
        for (int i = 0; i < 4; i++)
        {
            var parent = rewardRoot.GetParent();
            if (parent == null || parent == GetTree()?.Root) break;
            rewardRoot = parent;
        }

        // Step 1: Collect all visible text labels with their positions
        var allLabels = new List<TextLabel>();
        CollectVisibleLabels(rewardRoot, allLabels, 0);

        // Step 2: Extract card names from all labels
        var cardNameLabels = new List<TextLabel>();
        foreach (var lbl in allLabels)
        {
            if (IsLikelyCardName(lbl.Text))
                cardNameLabels.Add(lbl);
        }

        // Sort card names left-to-right by X position
        cardNameLabels.Sort((a, b) => a.Position.X.CompareTo(b.Position.X));

        // Step 3: Also find card-sized node positions
        var cardNodes = new List<CardCandidate>();
        FindCardNodesRecursive(rewardRoot, cardNodes, 0);
        cardNodes.Sort((a, b) => a.Position.X.CompareTo(b.Position.X));

        // Step 4: Build overlay entries
        var deckCardIds = DeckTracker.GetDeckCardIds();

        if (!_hasLoggedOverlayDiag)
        {
            MainFile.Logger.Info($"[SceneWatcher] PopulateOverlay: cardNameLabels={cardNameLabels.Count}, cardNodes={cardNodes.Count}, DB loaded={CardDatabase.IsLoaded} ({CardDatabase.CardCount} cards)");
            foreach (var lbl in cardNameLabels)
                MainFile.Logger.Info($"[SceneWatcher]   Label: \"{lbl.Text}\" at ({lbl.Position.X:F0},{lbl.Position.Y:F0})");
            _hasLoggedOverlayDiag = true;
        }

        if (cardNodes.Count >= 2 && cardNodes.Count <= 5)
        {
            // We have card node positions — use them for placement
            for (int i = 0; i < cardNodes.Count; i++)
            {
                // Try global label match first, then extract from card node children
                string cardName = (i < cardNameLabels.Count) ? cardNameLabels[i].Text : "";
                if (string.IsNullOrEmpty(cardName))
                    cardName = TryExtractCardNameFromNode(cardNodes[i].Node);
                AddOverlayCard(cardNodes[i].Position, cardNodes[i].Size, cardName, deckCardIds, true);
            }
        }
        else if (cardNameLabels.Count >= 2)
        {
            // No card nodes found but we have names — estimate positions from name label positions.
            // The card name label sits near the top of the card, so estimate the card top-left
            // from the label position and compute the actual card center from that.
            foreach (var nameLabel in cardNameLabels)
            {
                var estimatedSize = new Vector2(200, 300);
                var estimatedTopLeft = new Vector2(nameLabel.Position.X - 100, nameLabel.Position.Y - 40);
                var estimatedCenter = estimatedTopLeft + estimatedSize / 2f;
                AddOverlayCard(estimatedCenter, estimatedSize, nameLabel.Text, deckCardIds, true);
            }
        }
        else
        {
            // Fallback: estimated positions
            PopulateFromEstimatedPositions();
        }
    }

    private void AddOverlayCard(Vector2 center, Vector2 size, string cardName, List<string> deckCardIds, bool fromScene)
    {
        string cardId = cardName;
        var tierEntry = CardDatabase.GetByCardId(cardId) ?? CardDatabase.GetByName(cardName);
        int baseRating = tierEntry?.BaseRating ?? 5;
        int contextRating = baseRating > 0
            ? CardDatabase.GetContextualRating(tierEntry?.Id ?? cardId, deckCardIds)
            : 0;
        string displayName = tierEntry?.Name ?? cardName;

        if (!_hasLoggedOverlayDiag || tierEntry == null)
        {
            MainFile.Logger.Info($"[SceneWatcher] AddOverlayCard: name=\"{cardName}\", tierEntry={(tierEntry != null ? tierEntry.Id : "NULL")}, rating={baseRating}, fromScene={fromScene}");
        }

        CardRatingOverlay.CurrentOfferedCards.Add(new CardRatingOverlay.OfferedCard
        {
            CardId = tierEntry?.Id ?? cardId,
            DisplayName = displayName,
            ScreenPosition = new Vector2(center.X - size.X / 2f, center.Y - size.Y / 2f),
            CardSize = size,
            Rating = baseRating,
            ContextualRating = contextRating,
            DetectedFromScene = fromScene
        });
    }

    /// <summary>
    /// Collect ALL visible Label and RichTextLabel text from the subtree.
    /// </summary>
    private void CollectVisibleLabels(Node node, List<TextLabel> results, int depth)
    {
        if (depth > 8) return;

        try
        {
            if (node is Label label && label.IsVisibleInTree() && !string.IsNullOrWhiteSpace(label.Text))
            {
                results.Add(new TextLabel
                {
                    Text = label.Text.Trim(),
                    Position = label.GlobalPosition + label.Size / 2f
                });
            }
            else if (node is RichTextLabel rich && rich.IsVisibleInTree())
            {
                var text = rich.GetParsedText()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add(new TextLabel
                    {
                        Text = text,
                        Position = rich.GlobalPosition + rich.Size / 2f
                    });
                }
            }
        }
        catch { /* skip problematic nodes */ }

        foreach (var child in node.GetChildren())
        {
            CollectVisibleLabels(child, results, depth + 1);
        }
    }

    /// <summary>
    /// Determine if a text string is likely a card name (not description, cost, or UI text).
    /// </summary>
    private static bool IsLikelyCardName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();

        // Length filter: card names are typically 3-25 chars
        if (text.Length < 3 || text.Length > 30) return false;

        // Skip pure numbers (energy costs)
        if (int.TryParse(text, out _)) return false;
        if (float.TryParse(text, out _)) return false;

        // Skip text with special characters that aren't in card names
        if (text.Contains('!') || text.Contains('?') || text.Contains(':') ||
            text.Contains('(') || text.Contains(')') || text.Contains('['))
            return false;

        var lower = text.ToLowerInvariant();

        // Skip known UI / mod / game chrome text
        string[] exactSkip = [
            "attack", "skill", "power", "curse", "status",
            "loot", "loot!", "skip", "confirm", "cancel", "close", "back",
            "profile", "continue", "abandon", "abandon run",
            "multiplayer", "settings", "quit", "new run",
            "spirearena", "modded", "loaded", "baselib",
            "click to edit", "choose a card", "card reward",
            "common", "uncommon", "rare", "basic", "special"
        ];
        foreach (var skip in exactSkip)
        {
            if (lower == skip) return false;
        }

        // Skip descriptions (contains sentence-like phrases)
        string[] descriptionPhrases = [
            "deal ", "gain ", "apply ", "lose ", "draw ", "add ",
            "damage", "block", "this turn", " hp", " hp.",
            "vulnerable", "weak", "strength", "dexterity",
            "energy", "exhaust", "ethereal", "innate", "retain",
            "upgrade", "unplayable", "cost", "discard",
            "all enemies", "random enemy", "times", "each",
            "cards", "card to", "hand", "pile",
            "choose", "select", "pick a",
            "running modded"
        ];
        foreach (var phrase in descriptionPhrases)
        {
            if (lower.Contains(phrase))
                return false;
        }

        // Card names don't have periods, newlines, or long sentences
        if (text.Contains('.') || text.Contains('\n'))
            return false;

        // Card names typically start with an uppercase letter
        if (!char.IsUpper(text[0]))
            return false;

        // If it has more than 4 words, probably a description
        if (text.Split(' ').Length > 4)
            return false;

        return true;
    }

    private void FindCardNodesRecursive(Node node, List<CardCandidate> results, int depth)
    {
        if (depth > 4) return; // Don't go too deep

        foreach (var child in node.GetChildren())
        {
            var name = child.Name.ToString().ToLowerInvariant();

            // Skip UI elements that aren't cards
            if (name.Contains("skip") || name.Contains("close") || name.Contains("back") ||
                name.Contains("header") || name.Contains("banner") || name.Contains("title") ||
                name.Contains("choose"))
                continue;

            if (child is Control ctrl && ctrl.IsVisibleInTree())
            {
                var size = ctrl.Size;

                // Card-like: significant rectangle, not too small (buttons) or too large (background)
                bool isCardSized = size.X > 120 && size.X < 500 && size.Y > 180 && size.Y < 700;

                // Name hints
                bool hasCardName = name.Contains("card") || name.Contains("reward") ||
                                   name.Contains("choice") || name.Contains("option") ||
                                   name.Contains("offer");

                if (isCardSized || hasCardName)
                {
                    results.Add(new CardCandidate
                    {
                        Node = ctrl,
                        Position = ctrl.GlobalPosition + size / 2f,
                        Size = size
                    });
                }
            }

            FindCardNodesRecursive(child, results, depth + 1);
        }
    }

    private void PopulateFromEstimatedPositions()
    {
        var vp = GetViewport();
        if (vp == null) return;
        var viewport = vp.GetVisibleRect().Size;
        if (viewport.X < 1 || viewport.Y < 1) return;

        if (CardRatingOverlay.CurrentOfferedCards.Count > 0)
            return;

        var deckCardIds = DeckTracker.GetDeckCardIds();
        float cardW = 200f;
        float cardH = 300f;

        float[] xCenters = [viewport.X * 0.26f, viewport.X * 0.50f, viewport.X * 0.74f];
        float yCenter = viewport.Y * 0.62f;

        for (int i = 0; i < 3; i++)
        {
            AddOverlayCard(new Vector2(xCenters[i], yCenter), new Vector2(cardW, cardH), "", deckCardIds, false);
        }
    }

    /// <summary>
    /// Dump the current scene tree structure to the log for debugging.
    /// Called via F3 keypress.
    /// </summary>
    public void DumpSceneTree()
    {
        var root = GetTree()?.Root;
        if (root == null)
        {
            MainFile.Logger.Info("[SceneWatcher] No scene tree root.");
            return;
        }
        MainFile.Logger.Info("[SceneWatcher] === SCENE TREE DUMP ===");
        DumpNodeRecursive(root, 0);
        MainFile.Logger.Info("[SceneWatcher] === END SCENE TREE DUMP ===");
    }

    private void DumpNodeRecursive(Node node, int depth)
    {
        if (depth > 6) return;

        try
        {
            var indent = new string(' ', depth * 2);
            var typeName = node.GetType().Name;
            var name = node.Name;
            var extra = "";

            if (node is Control ctrl)
            {
                extra = $" Pos=({ctrl.GlobalPosition.X:F0},{ctrl.GlobalPosition.Y:F0}) Size=({ctrl.Size.X:F0},{ctrl.Size.Y:F0}) Vis={ctrl.IsVisibleInTree()}";
            }
            if (node is Label label)
            {
                var text = label.Text?.Length > 50 ? label.Text[..50] + "..." : label.Text;
                extra += $" Text=\"{text}\"";
            }
            if (node is RichTextLabel rich)
            {
                var text = rich.GetParsedText();
                text = text?.Length > 50 ? text[..50] + "..." : text;
                extra += $" Text=\"{text}\"";
            }

            MainFile.Logger.Info($"[SceneWatcher] {indent}{typeName}: {name}{extra}");
        }
        catch { /* skip */ }

        foreach (var child in node.GetChildren())
        {
            DumpNodeRecursive(child, depth + 1);
        }
    }

    /// <summary>
    /// Try to extract a card name from a card node's children (Label/RichTextLabel).
    /// Falls back to the node's own name if no suitable label child is found.
    /// </summary>
    private string TryExtractCardNameFromNode(Node cardNode)
    {
        // Search immediate children and one level deeper for a name label
        foreach (var child in cardNode.GetChildren())
        {
            var name = TryGetLabelText(child);
            if (!string.IsNullOrEmpty(name) && IsLikelyCardName(name))
                return name;

            foreach (var grandchild in child.GetChildren())
            {
                name = TryGetLabelText(grandchild);
                if (!string.IsNullOrEmpty(name) && IsLikelyCardName(name))
                    return name;
            }
        }

        // Fallback: try to read the node name itself (some games use descriptive node names)
        var nodeName = cardNode.Name.ToString();
        if (!string.IsNullOrEmpty(nodeName) && nodeName.Length >= 3)
        {
            var entry = CardDatabase.GetByCardId(nodeName) ?? CardDatabase.GetByName(nodeName);
            if (entry != null)
                return entry.Name;
        }

        return "";
    }

    private static string TryGetLabelText(Node node)
    {
        if (node is Label label && label.IsVisibleInTree())
            return label.Text?.Trim() ?? "";
        if (node is RichTextLabel rich && rich.IsVisibleInTree())
            return rich.GetParsedText()?.Trim() ?? "";
        return "";
    }

    private class TextLabel
    {
        public string Text { get; set; } = "";
        public Vector2 Position { get; set; }
    }

    private class CardCandidate
    {
        public Node Node { get; set; } = null!;
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
    }
}
