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

    /// <summary>
    /// Fingerprint of the last offered card set that was recorded as picked.
    /// Prevents duplicate picks when the same reward screen flickers or reopens.
    /// </summary>
    private string _lastRecordedOfferFingerprint = "";

    /// <summary>
    /// Tracks the card IDs from the previous scan while the reward screen is active.
    /// When a card disappears from the offer (count drops), it means the user picked it.
    /// </summary>
    private readonly List<string> _previousOfferedCardIds = [];

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

        // Fallback: find reward screen by node name if label detection fails
        Node? rewardNode = chooseLabel;
        if (rewardNode == null)
            rewardNode = FindRewardScreenByNodeName(root);

        if (rewardNode != null && !_rewardScreenActive)
        {
            _rewardScreenActive = true;
            CardRatingOverlay.IsRewardScreenActive = true;
            _previousOfferedCardIds.Clear();
            if (!_hasEverDetected)
            {
                MainFile.Logger.Info($"[SceneWatcher] Card reward screen detected! (via {(chooseLabel != null ? "label" : "node name")})");
                _hasEverDetected = true;
            }
            PopulateOverlay(rewardNode);
            SnapshotOfferedCards();
        }
        else if (rewardNode != null && _rewardScreenActive)
        {
            // Still active — update positions in case cards animated
            PopulateOverlay(rewardNode);

            // Detect pick: if a card disappeared from the offered set, the user picked it
            DetectPickedCard();
            SnapshotOfferedCards();
        }
        else if (rewardNode == null && _rewardScreenActive)
        {
            // Reward screen closed — detect last-moment pick if any
            DetectPickedCard();

            _rewardScreenActive = false;
            _hasLoggedOverlayDiag = false;
            _previousOfferedCardIds.Clear();

            CardRatingOverlay.ClearOfferedCards();
        }
    }

    /// <summary>
    /// Snapshot current offered card IDs for next-frame comparison.
    /// </summary>
    private void SnapshotOfferedCards()
    {
        _previousOfferedCardIds.Clear();
        foreach (var card in CardRatingOverlay.CurrentOfferedCards)
        {
            if (card.DetectedFromScene && !string.IsNullOrEmpty(card.CardId))
                _previousOfferedCardIds.Add(card.CardId);
        }
    }

    /// <summary>
    /// Compare current offered cards against the previous snapshot.
    /// If a card disappeared (user clicked it), record it as picked.
    /// </summary>
    private void DetectPickedCard()
    {
        if (_previousOfferedCardIds.Count == 0) return;

        var currentIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var card in CardRatingOverlay.CurrentOfferedCards)
        {
            if (card.DetectedFromScene && !string.IsNullOrEmpty(card.CardId))
                currentIds.Add(card.CardId);
        }

        // If current set is empty or same size, no pick detected yet
        // (screen may have just closed, or cards haven't changed)
        // We only record a pick when exactly one card disappears from the offer.
        foreach (var prevId in _previousOfferedCardIds)
        {
            if (!currentIds.Contains(prevId))
            {
                // This card was in the previous offer but is gone now — it was picked
                var entry = CardDatabase.GetByCardId(prevId);
                if (entry == null) continue;

                // Skip starter cards
                var lower = entry.Name.ToLowerInvariant();
                if (lower is "strike" or "defend" or "bash" or "zap" or "dualcast"
                    or "neutralize" or "survive" or "snap" or "authority")
                    continue;

                // Avoid duplicate recording of the same pick
                var fingerprint = $"pick:{prevId}:{_previousOfferedCardIds.Count}";
                if (fingerprint == _lastRecordedOfferFingerprint)
                    continue;
                _lastRecordedOfferFingerprint = fingerprint;

                var deckCardIds = DeckTracker.GetDeckCardIds();
                int rating = CardDatabase.GetContextualRating(entry.Id, deckCardIds);
                ArchetypeSystem.OnCardPicked(entry.Id, entry.Name, rating);
                MainFile.Logger.Info($"[SceneWatcher] Card picked detected: {entry.Name} (rating={rating})");
                return; // Only record one pick per transition
            }
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

    /// <summary>
    /// Fallback detection: find the reward/draft screen by looking for
    /// visible nodes whose name contains reward/draft-related keywords.
    /// </summary>
    private Node? FindRewardScreenByNodeName(Node node, int depth = 0)
    {
        if (depth > 10) return null;

        try
        {
            var name = node.Name.ToString().ToLowerInvariant();
            bool isRewardNode = name.Contains("cardreward") || name.Contains("card_reward") ||
                                name.Contains("draftscreen") || name.Contains("draft_screen") ||
                                name.Contains("rewardscreen") || name.Contains("reward_screen") ||
                                name.Contains("cardchoice") || name.Contains("card_choice") ||
                                name.Contains("cardselect") || name.Contains("card_select");

            if (isRewardNode && node is Control ctrl && ctrl.IsVisibleInTree())
                return node;
        }
        catch { /* skip */ }

        foreach (var child in node.GetChildren())
        {
            var found = FindRewardScreenByNodeName(child, depth + 1);
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
        // Only clear the card list, NOT the IsRewardScreenActive flag.
        // ClearOfferedCards() also resets IsRewardScreenActive which would
        // prevent _Draw from rendering badges in the same frame.
        CardRatingOverlay.CurrentOfferedCards.Clear();

        // Navigate up to reward screen root (go up enough levels to capture everything)
        var rewardRoot = chooseLabel;
        for (int i = 0; i < 6; i++)
        {
            var parent = rewardRoot.GetParent();
            if (parent == null || parent == GetTree()?.Root) break;
            rewardRoot = parent;
        }

        // Step 1: Collect all visible text labels with their positions
        var allLabels = new List<TextLabel>();
        CollectVisibleLabels(rewardRoot, allLabels, 0);

        // Step 2: Extract card names from all labels, validate against DB, and deduplicate
        var cardNameLabels = new List<TextLabel>();
        var seenCardIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var lbl in allLabels)
        {
            if (!IsLikelyCardName(lbl.Text))
                continue;

            // Require exact DB match to avoid false positives from game keywords
            // (e.g., "Poison" from card descriptions matching "Deadly Poison" via partial match)
            var entry = CardDatabase.GetByExactName(lbl.Text);
            if (entry == null && CardDatabase.IsLoaded)
                continue;

            // Deduplicate by resolved card ID
            string resolvedId = entry?.Id ?? lbl.Text;
            if (!seenCardIds.Add(resolvedId))
                continue;

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
            MainFile.Logger.Info($"[SceneWatcher] PopulateOverlay: cardNameLabels={cardNameLabels.Count}, cardNodes={cardNodes.Count}, allLabels={allLabels.Count}, DB loaded={CardDatabase.IsLoaded} ({CardDatabase.CardCount} cards)");
            foreach (var lbl in allLabels)
                MainFile.Logger.Info($"[SceneWatcher]   AllLabel: \"{lbl.Text}\" at ({lbl.Position.X:F0},{lbl.Position.Y:F0}) isCardName={IsLikelyCardName(lbl.Text)}");
            foreach (var cn in cardNodes)
                MainFile.Logger.Info($"[SceneWatcher]   CardNode: \"{cn.Node.Name}\" type={cn.Node.GetType().Name} at ({cn.Position.X:F0},{cn.Position.Y:F0}) size=({cn.Size.X:F0},{cn.Size.Y:F0})");
            _hasLoggedOverlayDiag = true;
        }

        // Get viewport size for standard position estimation
        var vp = GetViewport();
        var viewport = vp?.GetVisibleRect().Size ?? new Vector2(1920, 1080);

        if (cardNameLabels.Count >= 2)
        {
            // Card names detected — use viewport-proportional standard positions.
            // Card node positions are unreliable (can match containers/banners/panels),
            // so we always use standard card layout positions when names are known.
            float cardW = 200f;
            float cardH = 300f;
            float yCenter = viewport.Y * 0.56f;
            float[] xCenters = GetStandardCardXPositions(viewport.X, cardNameLabels.Count);

            for (int i = 0; i < cardNameLabels.Count && i < xCenters.Length; i++)
            {
                AddOverlayCard(new Vector2(xCenters[i], yCenter), new Vector2(cardW, cardH), cardNameLabels[i].Text, deckCardIds, true);
            }
        }
        else if (cardNodes.Count >= 2 && cardNodes.Count <= 5)
        {
            // No card names found but card-sized nodes detected — use node positions as fallback.
            for (int i = 0; i < cardNodes.Count; i++)
            {
                string cardName = TryExtractCardNameFromNode(cardNodes[i].Node);
                AddOverlayCard(cardNodes[i].Position, cardNodes[i].Size, cardName, deckCardIds, true);
            }
        }
        else
        {
            // Reward screen confirmed but no card names/nodes found via tree walking.
            // Use estimated positions based on viewport size. This is safe because
            // IsRewardScreenActive prevents badges during combat.
            PopulateFromEstimatedPositions(cardNameLabels, allLabels, deckCardIds);
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

        // Calculate archetype bonus for display
        int archBonus = tierEntry != null
            ? ArchetypeSystem.GetArchetypeBonus(tierEntry.Id, tierEntry.Tags)
            : 0;

        if (!_hasLoggedOverlayDiag || tierEntry == null)
        {
            MainFile.Logger.Info($"[SceneWatcher] AddOverlayCard: name=\"{cardName}\", tierEntry={(tierEntry != null ? tierEntry.Id : "NULL")}, rating={baseRating}, archBonus={archBonus}, fromScene={fromScene}");
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
    /// Get standard X center positions for cards based on card count.
    /// STS2 reward screens lay cards out evenly across the screen center.
    /// </summary>
    private static float[] GetStandardCardXPositions(float viewportWidth, int count)
    {
        return count switch
        {
            2 => [viewportWidth * 0.35f, viewportWidth * 0.65f],
            4 => [viewportWidth * 0.20f, viewportWidth * 0.40f, viewportWidth * 0.60f, viewportWidth * 0.80f],
            5 => [viewportWidth * 0.15f, viewportWidth * 0.30f, viewportWidth * 0.50f, viewportWidth * 0.70f, viewportWidth * 0.85f],
            _ => count == 3
                ? [viewportWidth * 0.30f, viewportWidth * 0.50f, viewportWidth * 0.70f]
                : Enumerable.Range(0, count).Select(i => viewportWidth * (0.2f + 0.6f * i / (count - 1))).ToArray()
        };
    }

    /// <summary>
    /// Collect ALL visible Label and RichTextLabel text from the subtree.
    /// </summary>
    private void CollectVisibleLabels(Node node, List<TextLabel> results, int depth)
    {
        if (depth > 12) return;

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

        // Fast path: if the text matches a known card name EXACTLY in the database, it IS a card name
        if (CardDatabase.IsLoaded && CardDatabase.GetByExactName(text) != null)
            return true;

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
        if (depth > 8) return; // Search deeper to find card nodes

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

    private void PopulateFromEstimatedPositions(List<TextLabel> cardNameLabels, List<TextLabel> allLabels, List<string> deckCardIds)
    {
        var vp = GetViewport();
        if (vp == null) return;
        var viewport = vp.GetVisibleRect().Size;
        if (viewport.X < 1 || viewport.Y < 1) return;

        if (CardRatingOverlay.CurrentOfferedCards.Count > 0)
            return;

        // Try to match any label text to known cards in the database (exact match only)
        var matchedCards = new List<(string name, float x)>();
        var seenIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var lbl in allLabels)
        {
            var entry = CardDatabase.GetByExactName(lbl.Text);
            if (entry != null && seenIds.Add(entry.Id))
                matchedCards.Add((entry.Name, lbl.Position.X));
        }
        matchedCards.Sort((a, b) => a.x.CompareTo(b.x));

        float cardW = 200f;
        float cardH = 300f;
        float yCenter = viewport.Y * 0.56f;

        if (matchedCards.Count >= 2)
        {
            // We found card names via DB match — use standard positions
            float[] xCenters = GetStandardCardXPositions(viewport.X, matchedCards.Count);

            for (int i = 0; i < matchedCards.Count && i < xCenters.Length; i++)
            {
                AddOverlayCard(new Vector2(xCenters[i], yCenter), new Vector2(cardW, cardH), matchedCards[i].name, deckCardIds, true);
            }

            if (!_hasLoggedOverlayDiag)
            {
                MainFile.Logger.Info($"[SceneWatcher] Fallback: matched {matchedCards.Count} cards from all labels via DB lookup.");
                foreach (var m in matchedCards)
                    MainFile.Logger.Info($"[SceneWatcher]   Matched: \"{m.name}\" (label X={m.x:F0})");
            }
        }
        else
        {
            // Last resort: 3 unknown cards at estimated positions
            float[] xCenters = GetStandardCardXPositions(viewport.X, 3);

            for (int i = 0; i < 3; i++)
            {
                AddOverlayCard(new Vector2(xCenters[i], yCenter), new Vector2(cardW, cardH), "", deckCardIds, false);
            }

            if (!_hasLoggedOverlayDiag)
                MainFile.Logger.Info("[SceneWatcher] Fallback: using estimated positions (no card names found).");
        }
    }

    /// <summary>
    /// Dump the current scene tree structure to both the game log and a file for debugging.
    /// Called via F9 keypress.
    /// </summary>
    public void DumpSceneTree()
    {
        var root = GetTree()?.Root;
        if (root == null)
        {
            MainFile.Logger.Info("[SceneWatcher] No scene tree root.");
            return;
        }

        var lines = new System.Collections.Generic.List<string>();
        lines.Add($"[SceneWatcher] === SCENE TREE DUMP === RewardActive={_rewardScreenActive} OfferedCards={CardRatingOverlay.CurrentOfferedCards.Count} IsRewardScreen={CardRatingOverlay.IsRewardScreenActive}");
        DumpNodeRecursive(root, 0, lines);
        lines.Add("[SceneWatcher] === END SCENE TREE DUMP ===");

        foreach (var line in lines)
            MainFile.Logger.Info(line);

        // Also write to a file next to the mod DLL for easy access
        try
        {
            var asmLocation = typeof(GameSceneWatcher).Assembly.Location;
            if (!string.IsNullOrEmpty(asmLocation))
            {
                var modDir = System.IO.Path.GetDirectoryName(asmLocation) ?? "";
                var dumpPath = System.IO.Path.Combine(modDir, "scene_dump.txt");
                System.IO.File.WriteAllLines(dumpPath, lines);
                MainFile.Logger.Info($"[SceneWatcher] Scene tree dumped to: {dumpPath}");
            }
        }
        catch (System.Exception ex)
        {
            MainFile.Logger.Warn($"[SceneWatcher] Failed to write dump file: {ex.Message}");
        }
    }

    private void DumpNodeRecursive(Node node, int depth, System.Collections.Generic.List<string> lines)
    {
        if (depth > 8) return;

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

            var line = $"{indent}{typeName}: {name}{extra}";
            lines.Add(line);
        }
        catch { /* skip */ }

        foreach (var child in node.GetChildren())
        {
            DumpNodeRecursive(child, depth + 1, lines);
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
