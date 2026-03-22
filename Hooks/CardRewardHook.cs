using Godot;
using HarmonyLib;

namespace SpireArena;

/// <summary>
/// Harmony patches for hooking into the card reward/selection screens.
/// Detects when cards are offered and populates the CardRatingOverlay.
/// 
/// NOTE: The exact class/method names depend on Slay the Spire 2's decompiled code.
/// These patches target the known game API. If the game updates, method signatures
/// may change and patches will need updating.
/// </summary>
public static class CardRewardHook
{
    /// <summary>
    /// Patch: When the card reward screen opens and shows cards.
    /// Target: The game's card reward display method.
    /// </summary>
    [HarmonyPatch]
    public static class CardRewardScreenPatch
    {
        // The target method will be resolved at runtime.
        // StS2 uses Godot + C#, card rewards are shown via a screen/panel node.
        // We try to patch the method that sets up the card choices.

        /// <summary>
        /// Attempts to find and patch the card reward setup method.
        /// Uses manual patching if automatic targeting fails.
        /// </summary>
        public static bool Prepare()
        {
            // Check if the target type exists in the game assembly
            var targetType = AccessTools.TypeByName("MegaCrit.Sts2.Cards.CardRewardScreen");
            if (targetType == null)
            {
                targetType = AccessTools.TypeByName("MegaCrit.Sts2.UI.CardRewardScreen");
            }
            if (targetType == null)
            {
                MainFile.Logger.Warn("[CardRewardHook] Could not find CardRewardScreen type. Patch will be skipped.");
            }
            else
            {
                MainFile.Logger.Info($"[CardRewardHook] Found reward screen type: {targetType.FullName}");
            }
            return targetType != null;
        }

        public static System.Reflection.MethodBase? TargetMethod()
        {
            // Try known method names for showing card rewards
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Cards.CardRewardScreen")
                       ?? AccessTools.TypeByName("MegaCrit.Sts2.UI.CardRewardScreen");

            if (type == null)
            {
                MainFile.Logger.Warn("CardRewardHook: Could not find CardRewardScreen type.");
                return null;
            }

            // Look for the method that opens/shows the reward screen with card choices
            var method = AccessTools.Method(type, "ShowCards")
                         ?? AccessTools.Method(type, "Open")
                         ?? AccessTools.Method(type, "SetCards")
                         ?? AccessTools.Method(type, "_Ready");

            if (method == null)
            {
                MainFile.Logger.Warn("CardRewardHook: Could not find target method on CardRewardScreen.");
            }
            return method;
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ProcessCardReward(__instance);
            }
            catch (System.Exception ex)
            {
                MainFile.Logger.Error($"CardRewardHook error: {ex.Message}");
            }
        }
    }

    private static void ProcessCardReward(object screenInstance)
    {
        CardRatingOverlay.ClearOfferedCards();

        // Attempt to read offered cards from the screen via reflection
        var type = screenInstance.GetType();

        // Try common field names for the card list
        var cardsField = AccessTools.Field(type, "cards")
                         ?? AccessTools.Field(type, "_cards")
                         ?? AccessTools.Field(type, "cardChoices")
                         ?? AccessTools.Field(type, "rewardCards");

        if (cardsField == null)
        {
            MainFile.Logger.Warn("CardRewardHook: Could not find cards field.");
            return;
        }

        var cardsObj = cardsField.GetValue(screenInstance);
        if (cardsObj is not System.Collections.IEnumerable cards) return;

        var deckCardIds = DeckTracker.GetDeckCardIds();
        float cardSpacing = 250f;
        float startX = 400f;
        int index = 0;

        foreach (var cardObj in cards)
        {
            if (cardObj == null) continue;
            var cardType = cardObj.GetType();

            // Try to get card ID and name
            string cardId = GetStringProperty(cardObj, cardType, "Id", "CardId", "id") ?? "Unknown";
            string cardName = GetStringProperty(cardObj, cardType, "Name", "CardName", "name") ?? cardId;

            var tierEntry = CardDatabase.GetByCardId(cardId) ?? CardDatabase.GetByName(cardName);
            int baseRating = tierEntry?.BaseRating ?? 5;
            int contextRating = CardDatabase.GetContextualRating(cardId, deckCardIds);

            // Estimate screen position (cards are typically spread horizontally)
            var pos = new Vector2(startX + (index * cardSpacing), 300);

            // Try to get actual position if it's a Godot node
            if (cardObj is Node2D node2D)
            {
                pos = node2D.GlobalPosition;
            }
            else if (cardObj is Control control)
            {
                pos = control.GlobalPosition;
            }

            CardRatingOverlay.CurrentOfferedCards.Add(new CardRatingOverlay.OfferedCard
            {
                CardId = cardId,
                DisplayName = cardName,
                ScreenPosition = pos,
                CardSize = new Godot.Vector2(200, 300),
                Rating = baseRating,
                ContextualRating = contextRating
            });

            index++;
        }

        MainFile.Logger.Info($"CardRewardHook: {index} cards detected for rating.");
    }

    private static string? GetStringProperty(object obj, System.Type type, params string[] names)
    {
        foreach (var name in names)
        {
            var prop = AccessTools.Property(type, name);
            if (prop != null)
                return prop.GetValue(obj)?.ToString();

            var field = AccessTools.Field(type, name);
            if (field != null)
                return field.GetValue(obj)?.ToString();
        }
        return null;
    }
}
