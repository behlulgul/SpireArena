<p align="center">
<img src="<img width="1024" height="572" alt="image" src="<img width="1024" height="572" alt="image" src="https://github.com/user-attachments/assets/9f107f2b-9c94-4c09-acd3-ea5ab1cd713f" />
" />
" alt="SpireArena Banner">
</p>

<h1 align="center">SpireArena</h1>

<p align="center">
<i>A mod for Slay the Spire 2 (STS2) that provides dynamic card rating overlays and build/archetype guidance during card reward and draft screens. Built with Godot and C#.</i>
</p>

<p align="center">
<img src="https://img.shields.io/badge/.NET-10.0-blue.svg" alt=".NET 10">
<img src="https://img.shields.io/badge/Godot-C%23-478cbf.svg" alt="Godot C#">
<img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License MIT">
</p>

🎮 See It In Action
✨ Features
🃏 Card Rating Overlay
Shows HearthArena-style ratings above each card during reward/draft screens. Ratings dynamically update based on your current deck and build.

🧠 Archetype Detection & Synergy
Detects your current build/archetype and adjusts ratings contextually. Ratings reflect deck synergies, archetype bonuses, and per-run card picks.

⚙️ Dynamic Card Analysis & Modular Design
Handles both 3-card and 4-card reward screens. Built with robust scene tree scanning to avoid false positives. Uses Godot's scene tree and Harmony patches for game-version-resilient detection.

🛠️ How It Works
Scans the Godot scene tree to detect card reward/draft screens and extract offered card names/positions.

Looks up each card in a local tier list database (JSON) and computes a contextual rating based on your deck and archetype.

Draws overlay badges above each card, showing rating, tier, and relative pick rank.

Supports both upgraded (e.g., Piercing Wail+) and base card names.

📂 Project Structure
Services/ — Core logic: scene watcher, card database, archetype system, deck tracker
UI/       — Overlay rendering, build guide, deck tracker UI, styles
Hooks/    — Harmony patches for game event integration
Config/   — Mod configuration
MainFile.cs — Entry point and logger

🚀 Requirements
.NET 10

Godot (with C# support)

Slay the Spire 2 (STS2)

BepInEx (for mod loading)

📦 Installation
Build the project with .NET 10 and Godot.

Place the compiled DLL and Data/CardTierList.json in your STS2 mods directory.

Ensure BepInEx is installed and enabled.

Launch the game. Card ratings will appear during reward/draft screens!

🤝 Contributing
Pull requests and issues are welcome! Please ensure your code follows the existing style and includes relevant test coverage.

Examples from in game
Values changes based on the build 

<img width="2556" height="1165" alt="image" src="https://github.com/user-attachments/assets/16ebffcc-3c11-47e5-8c73-75aa42ed8ac9" />

