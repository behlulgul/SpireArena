using Godot;

namespace SpireArena;

/// <summary>
/// Hooks into the game's main scene to inject SpireArena's UI nodes.
/// Also handles keyboard shortcuts for toggling features.
/// Added to the scene tree by MainFile during initialization.
/// </summary>
public partial class UIManager : Node
{
    private static DeckTrackerUI? _deckTracker;
    private static CardRatingOverlay? _ratingOverlay;
    private static GameSceneWatcher? _sceneWatcher;
    private static BuildGuidePanel? _buildGuide;
    private static Label? _statusLabel;
    private static bool _uiInitialized;

    public override void _Ready()
    {
        InitializeUI();
    }

    public override void _Process(double delta)
    {
        if (!_uiInitialized)
        {
            InitializeUI();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            switch (keyEvent.Keycode)
            {
                case Key.Q:
                    ModConfig.ToggleDeckTracker();
                    break;
                case Key.W:
                    ModConfig.ToggleCardRatings();
                    break;
                case Key.E:
                    ArchetypeSystem.CycleCharacter();
                    break;
                case Key.R:
                    ArchetypeSystem.CycleArchetype();
                    break;
                case Key.T:
                    ArchetypeSystem.ClearPickedCards();
                    ArchetypeSystem.SetActiveArchetype(null);
                    break;
                case Key.F9:
                    // Debug: dump scene tree structure to log
                    _sceneWatcher?.DumpSceneTree();
                    MainFile.Logger.Info("[Debug] Scene tree dump requested. Check log output.");
                    break;
            }
        }
    }

    private void InitializeUI()
    {
        if (_uiInitialized) return;

        var tree = GetTree();
        if (tree?.Root == null) return;

        try
        {
            // Create and add DeckTrackerUI
            _deckTracker = new DeckTrackerUI();
            _deckTracker.Name = "SpireArena_DeckTracker";
            _deckTracker.ZIndex = 100; // Render on top
            tree.Root.CallDeferred("add_child", _deckTracker);

            // Create and add CardRatingOverlay
            _ratingOverlay = new CardRatingOverlay();
            _ratingOverlay.Name = "SpireArena_CardRating";
            _ratingOverlay.ZIndex = 100;
            tree.Root.CallDeferred("add_child", _ratingOverlay);

            // Create and add GameSceneWatcher — scene-tree-based game state detection
            // This works independently of Harmony patches for card reward detection
            _sceneWatcher = new GameSceneWatcher();
            _sceneWatcher.Name = "SpireArena_SceneWatcher";
            tree.Root.CallDeferred("add_child", _sceneWatcher);
            MainFile.Logger.Info("GameSceneWatcher registered.");

            // Create and add BuildGuidePanel
            _buildGuide = new BuildGuidePanel();
            _buildGuide.Name = "SpireArena_BuildGuide";
            _buildGuide.ZIndex = 100;
            tree.Root.CallDeferred("add_child", _buildGuide);
            MainFile.Logger.Info("BuildGuidePanel registered.");

            // Status indicator — confirms mod UI layer is active
            _statusLabel = new Label();
            _statusLabel.Name = "SpireArena_Status";
            _statusLabel.Text = "SpireArena ✓";
            _statusLabel.ZIndex = 100;
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1.0f, 0.5f, 0.5f));
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            _statusLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _statusLabel.Position = new Vector2(10, 60);
            tree.Root.CallDeferred("add_child", _statusLabel);

            _uiInitialized = true;
            MainFile.Logger.Info("SpireArena UI initialized.");
        }
        catch (System.Exception ex)
        {
            MainFile.Logger.Error($"UI initialization error: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleanup when the mod is unloaded.
    /// </summary>
    public override void _ExitTree()
    {
        _deckTracker?.QueueFree();
        _ratingOverlay?.QueueFree();
        _sceneWatcher?.QueueFree();
        _buildGuide?.QueueFree();
        _statusLabel?.QueueFree();
        _uiInitialized = false;
    }
}
