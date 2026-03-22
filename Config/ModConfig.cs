namespace SpireArena;

/// <summary>
/// Runtime configuration toggles for SpireArena features.
/// </summary>
public static class ModConfig
{
    public static bool ShowDeckTracker { get; set; } = true;
    public static bool ShowCardRatings { get; set; } = true;
    public static float TrackerOpacity { get; set; } = 0.85f;
    public static float DimmedCardOpacity { get; set; } = 0.40f;

    public static void Initialize()
    {
        // Default values are set above. Future: read from a config file.
        MainFile.Logger.Info("ModConfig loaded.");
    }

    public static void ToggleDeckTracker()
    {
        ShowDeckTracker = !ShowDeckTracker;
        MainFile.Logger.Info($"Deck Tracker: {(ShowDeckTracker ? "ON" : "OFF")}");
    }

    public static void ToggleCardRatings()
    {
        ShowCardRatings = !ShowCardRatings;
        MainFile.Logger.Info($"Card Ratings: {(ShowCardRatings ? "ON" : "OFF")}");
    }
}
