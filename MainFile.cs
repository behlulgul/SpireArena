using Godot;
using HarmonyLib;
using System.Linq;
using MegaCrit.Sts2.Core.Modding;

namespace SpireArena;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SpireArena";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    private static UIManager? _uiManager;

    public static void Initialize()
    {
        Logger.Info("SpireArena initializing...");

        // === Harmony patches (optional — scene tree watcher is the primary detection) ===
        try
        {
            Harmony harmony = new(ModId);
            harmony.PatchAll();
            Logger.Info("Harmony patches applied.");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"Harmony patching failed (non-fatal, scene watcher will be used): {ex.Message}");
        }

        // === Assembly type scan (diagnostic only) ===
        try
        {
            LogGameAssemblyTypes();
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"Assembly type scan failed (non-fatal): {ex.Message}");
        }

        // === Card database ===
        try
        {
            CardDatabase.Load();
        }
        catch (System.Exception ex)
        {
            Logger.Error($"CardDatabase failed to load: {ex.Message}");
        }

        ModConfig.Initialize();

        // === UI setup — defer if scene tree root isn't ready yet ===
        try
        {
            var sceneTree = Engine.GetMainLoop() as SceneTree;
            if (sceneTree != null)
            {
                if (sceneTree.Root != null)
                {
                    AddUIManager(sceneTree.Root);
                }
                else
                {
                    Logger.Warn("Scene tree root not yet ready, deferring UI setup via ProcessFrame...");
                    void OnFrame()
                    {
                        var tree = Engine.GetMainLoop() as SceneTree;
                        if (tree?.Root != null && _uiManager == null)
                        {
                            tree.ProcessFrame -= OnFrame;
                            AddUIManager(tree.Root);
                        }
                    }
                    sceneTree.ProcessFrame += OnFrame;
                }
            }
            else
            {
                Logger.Error("SceneTree not available. SpireArena UI cannot initialize.");
            }
        }
        catch (System.Exception ex)
        {
            Logger.Error($"UI setup failed: {ex.Message}");
        }

        Logger.Info("SpireArena initialized successfully.");
    }

    public override void _Ready()
    {
        if (_uiManager == null)
        {
            var tree = GetTree();
            if (tree?.Root != null)
            {
                AddUIManager(tree.Root);
            }
        }
    }

    private static void AddUIManager(Node root)
    {
        if (_uiManager != null) return;
        _uiManager = new UIManager();
        _uiManager.Name = "SpireArena_UIManager";
        root.CallDeferred("add_child", _uiManager);
        Logger.Info("UIManager added to scene tree.");
    }

    /// <summary>
    /// Scans loaded assemblies for game types relevant to Harmony patching.
    /// Results are logged so correct type/method names can be identified.
    /// Dumps ALL public types from game assemblies to help with patch targeting.
    /// </summary>
    private static void LogGameAssemblyTypes()
    {
        // Keywords for matching game-related assemblies
        string[] asmKeywords = ["sts2", "MegaCrit", "SlayTheSpire", "slay", "spire"];

        // Keywords for highlighting relevant types (logged with [!!])
        string[] highlightKeywords = [
            "Card", "Combat", "Reward", "Deck", "Play", "Action",
            "Encounter", "Battle", "Fight", "Hand", "Draw", "Discard",
            "Exhaust", "Screen", "UI", "Panel", "Display", "Select",
            "Choose", "Pick", "Offer", "Player", "Character", "Run"
        ];

        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var asmName = asm.GetName().Name ?? "";
                bool isGameAssembly = false;
                foreach (var kw in asmKeywords)
                {
                    if (asmName.Contains(kw, System.StringComparison.OrdinalIgnoreCase))
                    {
                        isGameAssembly = true;
                        break;
                    }
                }
                if (!isGameAssembly) continue;

                Logger.Info($"[TypeScan] ===== Assembly: {asmName} =====");

                System.Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    Logger.Warn($"[TypeScan] Partial load for {asmName}");
                    types = ex.Types?.Where(t => t != null).ToArray()!
                            ?? System.Array.Empty<System.Type>();
                }

                Logger.Info($"[TypeScan] Total types: {types.Length}");

                foreach (var type in types)
                {
                    try
                    {
                        if (type == null) continue;
                        if (type.IsNotPublic && !type.IsNestedPublic && !type.IsNestedFamily)
                            continue;

                        var fullName = type.FullName ?? type.Name;

                        bool isHighlight = false;
                        foreach (var kw in highlightKeywords)
                        {
                            if (type.Name.Contains(kw, System.StringComparison.OrdinalIgnoreCase))
                            {
                                isHighlight = true;
                                break;
                            }
                        }

                        if (isHighlight)
                        {
                            Logger.Info($"[TypeScan] [!!] {fullName}");
                            try
                            {
                                var methods = type.GetMethods(
                                    System.Reflection.BindingFlags.Public |
                                    System.Reflection.BindingFlags.Instance |
                                    System.Reflection.BindingFlags.DeclaredOnly);
                                foreach (var m in methods)
                                {
                                    try
                                    {
                                        if (m.IsSpecialName) continue;
                                        var paramNames = string.Join(", ",
                                            m.GetParameters().Select(p =>
                                            {
                                                try { return p.ParameterType.Name; }
                                                catch { return "?"; }
                                            }));
                                        Logger.Info($"[TypeScan]       -> {m.Name}({paramNames})");
                                    }
                                    catch { /* skip this method */ }
                                }
                            }
                            catch { /* skip method enumeration */ }
                        }
                    }
                    catch { /* skip this type entirely */ }
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warn($"[TypeScan] Assembly scan error: {ex.Message}");
            }
        }
    }
}
