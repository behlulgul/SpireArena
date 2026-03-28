# SpireArena

A mod for Slay the Spire 2 (STS2) that provides dynamic card rating overlays and build/archetype guidance during card reward and draft screens. Built with Godot and C#.

## Features
- **Card Rating Overlay:** Shows HearthArena-style ratings above each card during reward/draft screens, dynamically updating based on your current deck and build.
- **Archetype Detection:** Detects your current build/archetype and adjusts ratings contextually.
- **Dynamic Card Analysis:** Handles both 3-card and 4-card reward screens, with robust scene tree scanning to avoid false positives.
- **Synergy & Context:** Ratings reflect deck synergies, archetype bonuses, and per-run card picks.
- **Modular Design:** Uses Godot's scene tree and Harmony patches for robust, game-version-resilient detection.

## How It Works
- Scans the Godot scene tree to detect card reward/draft screens and extract offered card names/positions.
- Looks up each card in a local tier list database (JSON) and computes a contextual rating based on your deck and archetype.
- Draws overlay badges above each card, showing rating, tier, and relative pick rank.
- Supports both upgraded (e.g. `Piercing Wail+`) and base card names.

## Project Structure
- `Services/` — Core logic: scene watcher, card database, archetype system, deck tracker
- `UI/` — Overlay rendering, build guide, deck tracker UI, styles
- `Hooks/` — Harmony patches for game event integration
- `Config/` — Mod configuration
- `MainFile.cs` — Entry point and logger

## Requirements
- .NET 10
- Godot (C# support)
- Slay the Spire 2 (STS2)
- BepInEx (for mod loading)

## Installation
1. Build the project with .NET 10 and Godot.
2. Place the compiled DLL and `Data/CardTierList.json` in your STS2 mods directory.
3. Ensure BepInEx is installed and enabled.
4. Launch the game. Card ratings will appear during reward/draft screens.

## Contributing
Pull requests and issues are welcome! Please ensure your code follows the existing style and includes relevant test coverage.

## License
MIT License

Examples from game
Values changes based on the build 

<img width="2556" height="1165" alt="image" src="https://github.com/user-attachments/assets/16ebffcc-3c11-47e5-8c73-75aa42ed8ac9" />
![Uploading image.png…]()

