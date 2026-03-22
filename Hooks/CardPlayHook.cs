using HarmonyLib;

namespace SpireArena;

/// <summary>
/// Harmony patches for tracking card play, exhaust, and combat state events.
/// Hooks into the game's combat system to feed DeckTracker.
///
/// NOTE: Method targets depend on StS2's decompiled assembly.
/// These use reflection-based discovery to be resilient to minor API changes.
/// </summary>
public static class CardPlayHook
{
    /// <summary>
    /// Patch: Detect when combat starts and initialize the deck tracker.
    /// </summary>
    [HarmonyPatch]
    public static class CombatStartPatch
    {
        public static bool Prepare()
        {
            var type = FindCombatType();
            return type != null;
        }

        public static System.Reflection.MethodBase? TargetMethod()
        {
            var type = FindCombatType();
            if (type == null) return null;

            return AccessTools.Method(type, "StartCombat")
                   ?? AccessTools.Method(type, "Begin")
                   ?? AccessTools.Method(type, "Initialize")
                   ?? AccessTools.Method(type, "OnCombatStart");
        }

        public static void Postfix(object __instance)
        {
            try
            {
                InitializeDeckFromCombat(__instance);
            }
            catch (System.Exception ex)
            {
                MainFile.Logger.Error($"CombatStartPatch error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch: Detect when a card is played.
    /// </summary>
    [HarmonyPatch]
    public static class CardPlayedPatch
    {
        public static bool Prepare()
        {
            var type = FindCardActionType();
            return type != null;
        }

        public static System.Reflection.MethodBase? TargetMethod()
        {
            var type = FindCardActionType();
            if (type == null) return null;

            return AccessTools.Method(type, "PlayCard")
                   ?? AccessTools.Method(type, "UseCard")
                   ?? AccessTools.Method(type, "OnCardPlayed");
        }

        public static void Postfix(object __instance, object[] __args)
        {
            try
            {
                if (__args.Length > 0 && __args[0] != null)
                {
                    string? cardId = ExtractCardId(__args[0]);
                    if (cardId != null)
                    {
                        DeckTracker.OnCardPlayed(cardId);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MainFile.Logger.Error($"CardPlayedPatch error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch: Detect when a card is exhausted.
    /// </summary>
    [HarmonyPatch]
    public static class CardExhaustedPatch
    {
        public static bool Prepare()
        {
            var type = FindCardActionType();
            return type != null;
        }

        public static System.Reflection.MethodBase? TargetMethod()
        {
            var type = FindCardActionType();
            if (type == null) return null;

            return AccessTools.Method(type, "ExhaustCard")
                   ?? AccessTools.Method(type, "OnCardExhausted")
                   ?? AccessTools.Method(type, "Exhaust");
        }

        public static void Postfix(object __instance, object[] __args)
        {
            try
            {
                if (__args.Length > 0 && __args[0] != null)
                {
                    string? cardId = ExtractCardId(__args[0]);
                    if (cardId != null)
                    {
                        DeckTracker.OnCardExhausted(cardId);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MainFile.Logger.Error($"CardExhaustedPatch error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch: Detect when combat ends.
    /// </summary>
    [HarmonyPatch]
    public static class CombatEndPatch
    {
        public static bool Prepare()
        {
            var type = FindCombatType();
            return type != null;
        }

        public static System.Reflection.MethodBase? TargetMethod()
        {
            var type = FindCombatType();
            if (type == null) return null;

            return AccessTools.Method(type, "EndCombat")
                   ?? AccessTools.Method(type, "OnCombatEnd")
                   ?? AccessTools.Method(type, "Finish");
        }

        public static void Postfix()
        {
            try
            {
                DeckTracker.OnCombatEnd();
                CardRatingOverlay.ClearOfferedCards();
            }
            catch (System.Exception ex)
            {
                MainFile.Logger.Error($"CombatEndPatch error: {ex.Message}");
            }
        }
    }

    // === Helper methods ===

    private static System.Type? FindCombatType()
    {
        var type = AccessTools.TypeByName("MegaCrit.Sts2.Combat.CombatManager")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Combat.CombatRoom")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Combat.NCombatManager")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Combat.CombatManager")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
        if (type == null)
            MainFile.Logger.Warn("[CardPlayHook] Could not find any combat type. Patch will be skipped.");
        else
            MainFile.Logger.Info($"[CardPlayHook] Found combat type: {type.FullName}");
        return type;
    }

    private static System.Type? FindCardActionType()
    {
        var type = AccessTools.TypeByName("MegaCrit.Sts2.Cards.CardActions")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Combat.CardManager")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Cards.CardManager")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Cards.NCardManager")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Combat.CardManager")
               ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Cards.CardManager");
        if (type == null)
            MainFile.Logger.Warn("[CardPlayHook] Could not find any card action type. Patch will be skipped.");
        else
            MainFile.Logger.Info($"[CardPlayHook] Found card action type: {type.FullName}");
        return type;
    }

    private static void InitializeDeckFromCombat(object combatInstance)
    {
        var type = combatInstance.GetType();

        // Try to find the player's deck/masterDeck
        var deckField = AccessTools.Field(type, "masterDeck")
                        ?? AccessTools.Field(type, "deck")
                        ?? AccessTools.Field(type, "_deck")
                        ?? AccessTools.Field(type, "playerDeck");

        if (deckField == null)
        {
            // Try via a Player property
            var playerProp = AccessTools.Property(type, "Player")
                             ?? AccessTools.Property(type, "player");
            if (playerProp != null)
            {
                var player = playerProp.GetValue(combatInstance);
                if (player != null)
                {
                    var playerType = player.GetType();
                    deckField = AccessTools.Field(playerType, "masterDeck")
                                ?? AccessTools.Field(playerType, "deck")
                                ?? AccessTools.Field(playerType, "_deck");
                    if (deckField != null)
                    {
                        var deckObj = deckField.GetValue(player);
                        ParseDeckObject(deckObj);
                        return;
                    }
                }
            }

            MainFile.Logger.Warn("CombatStart: Could not find deck field.");
            return;
        }

        var deckValue = deckField.GetValue(combatInstance);
        ParseDeckObject(deckValue);
    }

    private static void ParseDeckObject(object? deckObj)
    {
        if (deckObj is not System.Collections.IEnumerable deckCards) return;

        var cardList = new List<(string id, string name, int cost, string type)>();

        foreach (var cardObj in deckCards)
        {
            if (cardObj == null) continue;
            var cardType = cardObj.GetType();

            string id = ExtractCardId(cardObj) ?? "Unknown";
            string name = GetStringValue(cardObj, cardType, "Name", "CardName", "name") ?? id;
            int cost = GetIntValue(cardObj, cardType, "Cost", "EnergyCost", "cost");
            string type = GetStringValue(cardObj, cardType, "Type", "CardType", "type") ?? "Unknown";

            cardList.Add((id, name, cost, type));
        }

        DeckTracker.OnCombatStart(cardList);
    }

    private static string? ExtractCardId(object cardObj)
    {
        var type = cardObj.GetType();
        return GetStringValue(cardObj, type, "Id", "CardId", "id", "ID");
    }

    private static string? GetStringValue(object obj, System.Type type, params string[] names)
    {
        foreach (var name in names)
        {
            var prop = AccessTools.Property(type, name);
            if (prop != null) return prop.GetValue(obj)?.ToString();

            var field = AccessTools.Field(type, name);
            if (field != null) return field.GetValue(obj)?.ToString();
        }
        return null;
    }

    private static int GetIntValue(object obj, System.Type type, params string[] names)
    {
        foreach (var name in names)
        {
            var prop = AccessTools.Property(type, name);
            if (prop != null && prop.GetValue(obj) is int val) return val;

            var field = AccessTools.Field(type, name);
            if (field != null && field.GetValue(obj) is int fval) return fval;
        }
        return 0;
    }
}
