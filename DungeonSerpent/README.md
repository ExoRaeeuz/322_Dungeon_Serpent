# Dungeon Serpent — MonoGame / C# / .NET 8

A polished Snake game with dungeon aesthetics, ported from the HTML/JS prototype
into a full MonoGame DesktopGL project.

---

## Setup — Visual Studio 2022

### Prerequisites
1. **Visual Studio 2022** (Community or better)
2. **.NET 8 SDK** — [download](https://dotnet.microsoft.com/download)
3. **MonoGame extension for VS 2022** — install from Visual Studio Marketplace
   (search *MonoGame Framework*) OR let NuGet restore packages automatically.

### Open the project
1. Double-click `DungeonSerpent.sln` (or `DungeonSerpent/DungeonSerpent.csproj`)
2. Wait for NuGet to restore `MonoGame.Framework.DesktopGL 3.8.1`
3. Press **F5** (or **Ctrl+F5**) to build and run

> If the MGCB editor is not installed, the content pipeline step is a no-op
> (the project generates all textures and audio programmatically — no asset
> files are required).

---

## Controls

| Key | Action |
|-----|--------|
| WASD / Arrow keys | Move snake |
| P / Space | Pause / Resume |
| Enter | Start / Resume / Restart from overlay |
| Escape | Pause |

---

## Rubric Coverage

| Requirement | Points | Status |
|---|---|---|
| Game is playable, no glitches / crashes | 50 | ✅ Full playtest |
| **Game Menu** (start, pause, death screens) | +10 | ✅ Three overlays |
| **High Score** save / load / display | +10 | ✅ `dungeon_serpent_hi.txt` next to exe |
| **Levels** (speed increase) | +10 | ✅ 10 levels, speed 160 ms → 40 ms |
| **Special pellets** | +10 | ✅ Shrinker (-3 segs) + Slow (5 s) |
| **Sprite for pellet** | +5 | ✅ Programmatic 32×32 glow sprites |
| **Sounds** | +5 | ✅ PCM-synthesised SFX (no WAV files needed) |
| **Total** | **100** | ✅ |

---

## Food Types

| Pellet | Colour | Effect |
|--------|--------|--------|
| Gold Coin | Yellow | +10 pts |
| Ruby Gem | Red | +25 pts |
| Diamond | Cyan | +50 pts |
| Shrinker | Purple | Removes 3 tail segments (available Level 3+) |
| Slow | Green | Halves snake speed for 5 seconds (Level 2+) |

---

## File Overview

```
DungeonSerpent/
├── Program.cs               — Entry point
├── DungeonSerpentGame.cs    — Main Game class (Update + Draw)
├── GameTypes.cs             — Enums, structs, FoodItem, Popup
├── Constants.cs             — All tuning values in one place
├── HighScoreManager.cs      — File-based high score persistence
├── SoundManager.cs          — Runtime PCM synthesis, no WAV files
├── TextureFactory.cs        — Runtime sprite generation, no PNG files
├── BitmapFont.cs            — 8×8 bitmap font renderer
└── Content/
    └── Content.mgcb         — Empty pipeline (no external assets)
```
