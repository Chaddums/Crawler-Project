# Junkbot Arena

Dungeon crawler roguelike built in **Godot 4.6 (C#)**. Reality TV themed — robots fight through procedurally generated dungeons while alien audiences watch, AI commentators snark, and sponsors drop loot boxes.

**Run structure:** 5 sectors x 3 areas = 15 floors per run. Players reach ~level 15.

## Quick Start

1. Open `Godot/project.godot` in Godot 4.6
2. Build C#: `dotnet build` (or from Godot editor)
3. Run — starts at Main Menu

### Controls

| Action | Keyboard | Gamepad |
|--------|----------|---------|
| Move | WASD | Left Stick |
| Basic Attack | Left Click | Y |
| Abilities 1-6 | 1-6 | X, B, LB, RB |
| Interact | E | A |
| Dash | Shift | L-Stick Click |
| Use Health Potion | Q | LT |
| Use Mana Potion | F | RT |
| Inventory | I | - |
| Character Sheet | C | - |
| Passive Tree | P | - |
| Pause | ESC | - |
| Zoom | Scroll Wheel | - |
| Click-to-Move | Right Click | - |

### Debug Keys

| Key | Action |
|-----|--------|
| ~ (tilde) | Debug console (god mode, spawn enemies, etc.) |
| F9 | Freecam (WASD to pan, click to teleport player) |
| F12 | In-game editor suite (balance, dungeon, VFX, etc.) |

### CLI Flags

```
--test       Automated screenshot test run
--autoplay   Autonomous bot plays the game (for remote observation)
```

## Project Structure

```
Godot/
  project.godot          # Engine config, input mappings, autoloads
  Scripts/
    Abilities/           # 28 abilities across 6 classes
    Achievements/        # ~25 AXIS-themed achievements
    Assets/              # Model loading, character animation
    Audio/               # Audio manager, TTS helper
    Camera/              # Isometric camera with zoom, shake, freecam
    Classes/             # 6 bot frames, passive tree, salvage cores (grafts)
    Combat/              # Damage calc, hitbox/hurtbox, projectiles, status effects
    Commentary/          # AXIS AI commentary system
    Companion/           # AI companion (BIT) with pathfinding + combat
    Core/                # Game manager, save system, service locator, constants
    Debug/               # Console, auto-player, error catcher, bug reporter
    Dungeon/             # Procedural dungeon gen, room building, sector management
    Editor/              # F12 in-game editor suite (9 modules)
    Enemies/             # 16 enemy types + 5 bosses with AI behaviors
    Items/               # Equipment, consumables, relics, affixes, loot boxes
    Player/              # Controller, movement, combat, stats, inventory, input
    Tests/               # Class body test helpers
    UI/                  # 27 UI scripts (HUD, menus, tooltips, passive tree canvas)
    Utils/               # Extensions, health component, interfaces, registry overrides
    VFX/                 # Visual effects factory, shaders, animators, screen shake
  Scenes/                # .tscn scene files
  Shaders/               # 6 GDSL shaders (burn, freeze, dissolve, flash, barrier, outline)
  Data/                  # JSON data (strings, icons, audio, relics)
  Models/                # FBX 3D models (Dungeon/{Floors,Walls,Props,Details,Doors})
  Icons/                 # PNG icons (Abilities, Items, UI, BotFrames, Enemies, LootBoxes)
```

## Core Systems

### Game Flow
`GameManager` (autoload) runs the state machine: Main Menu -> Character Creation -> Sector (dungeon) -> Victory/Death. Each sector has 3 areas of procedurally generated rooms.

### 6 Bot Frame Classes
| Class | Role | Signature |
|-------|------|-----------|
| Tin Can | Tank | High armor, shield abilities |
| Scrapheap | Melee DPS | Berserker, lifesteal |
| Spark Plug | Caster | Lightning/energy abilities |
| Rust Bucket | Support | Healing, buffs, debuffs |
| Noise Box | Crowd Control | AoE, stuns, area denial |
| Clunker | Hybrid | Jack-of-all-trades |

### Passive Tree & Grafts
Each class has a passive tree (allocated with skill points). **Salvage Cores** (grafts) socket into allocated CoreSocket nodes for extra perks — 4 Rare, 4 Epic, 7 Legendary cores available.

### Combat
- Damage formula: `base x stat_scaling x (1 + affixes) x crit x armor_reduction`
- 7 AI archetypes: Melee, Ranged, Flanker, Charger, Tank, Healer, Swarm
- Status effects: Burn, Freeze, Poison + custom debuffs
- 24 perks with combat hooks (lifesteal, chain lightning, thorns, etc.)

### Items & Loot
- Equipment with random affixes (50+ prefixes/suffixes)
- 10 equipment slots: MainHand, OffHand, Chest, Head, Feet, Hands, Back, Amulet, Ring1, Ring2
- Consumables: potions, flasks, repair kits, scrolls
- 15+ unique relics with absurd mechanics
- Loot boxes: 6 tiers (Bronze → Celestial) with in-world holographic ceremony

### Safe Rooms
Respite zones between areas with:
- **Couch + Holographic Display** — Sit down to open loot boxes via 3D in-world ceremony (visible to co-op partners)
- **Healing Station** — Passive HP regen nearby, one-time full heal via [E] interaction
- **Atmosphere** — Workbench, shelves, pipes, crates, BIT drone idle, crystal wall lights
- **Continue Portal** — Proceed to next area

### Dungeon Generation
Seed-based procedural layout. `DungeonGenerator` creates the floor grid, `RoomBuilder` assembles rooms from `RoomLayoutLibrary` templates, `RoomDresser` adds decorative props. FBX models used for floor/wall tiles with procedural fallback.

### Save System
- `SaveManager` + `SaveData` handle per-run state (inventory, position, progress)
- `MetaSaveManager` + `MetaSaveData` handle persistent progression (ascension perks, achievements)
- Save file: `junkbot_save.json`

## In-Game Editor (F12)

9 specialized modules for live game data editing without restarting:

| Tab | What It Does |
|-----|-------------|
| **Balance** | Edit abilities, enemies, equipment, consumables, relics, bot frames |
| **Bosses** | Boss preview with phase/attack inspection |
| **Characters** | 3D model viewer for all bot frames and weapon types |
| **Dungeon** | 2D sector map + 3D room viewer with object transform editing |
| **Assets** | Browse dungeon assets, configure collision shapes |
| **Sectors** | Tune enemy pools, room distributions, difficulty curves |
| **Strings** | Edit all localized UI text with live reload |
| **VFX** | Particle parameter editing with live 3D preview |
| **Sound** | Procedural sound design and preview |
| **UI/UX** | UI layout and theme editor |
| **Bugs** | File bug reports with auto-captured context |

Changes save to JSON override files in `Data/` and auto-sync to git. Apply to live registries immediately.

## Multiplayer

Co-op support for 2 players. P1 uses keyboard+mouse, P2 uses gamepad. Camera tracks midpoint with adaptive zoom based on player spread.

## Registries

Static registries initialize on startup and hold all game data:

| Registry | Content |
|----------|---------|
| AbilityRegistry | 28 abilities |
| EnemyRegistry | 16 enemies |
| BossRegistry | 5 bosses |
| BotFrameRegistry | 6 classes |
| AffixRegistry | 50+ item affixes |
| ConsumableRegistry | 20+ consumables |
| RelicRegistry | 15+ unique relics |
| SalvageCoreRegistry | 15+ graft cores |
| CompanionRegistry | Companion definitions |
| AchievementRegistry | ~25 achievements |
| PerkRegistry | 9 meta-progression perks |
| SectorDataRegistry | 5 sector configs |

Registries load JSON overrides from `Data/` at the end of `Initialize()` via `RegistryOverrides.cs`.

## Physics Layers

| Layer | Name | Used By |
|-------|------|---------|
| 1 | Default | World geometry |
| 2 | Player | Player character |
| 3 | Companion | AI companion |
| 4 | Enemy | Enemy characters |
| 5 | PlayerProjectile | Player-fired projectiles |
| 6 | EnemyProjectile | Enemy-fired projectiles |
| 7 | Interactable | Loot, NPCs, doors |
| 8 | Ground | Floor/terrain for raycasts |

## Autoloads

| Name | Purpose |
|------|---------|
| GameManager | Main game state machine (scene: Main.tscn) |
| DebugTestRunner | `--test` automated screenshot runs |
| AutoPlayer | `--autoplay` autonomous bot |
| DebugMenu | ~ console with cheat commands |
| PlaytestReporter | Bug/feature report launcher |
| ErrorCatcher | Log monitor, auto-generates bug reports |
| EditorManager | F12 in-game editor suite |

## Architecture Patterns

- **Service Locator** — `ServiceLocator.Register<T>()` / `ServiceLocator.TryGet<T>()` for core systems
- **Static Registries** — All game data in static classes, initialized once at startup
- **JSON Overrides** — Balance editor saves to JSON, registries load overrides on init
- **Event Bus** — `GameEvents.OnEnemyKilled`, `OnDamageDealt`, `OnComboHit` for loose coupling
- **StatBlock Stacking** — Flat + percent modifiers from items, affixes, perks, passive tree, status effects
- **IInteractable** — Interface for world objects (couch, healing station, item pickups). Physics query on E key finds nearest interactable within 2m
- **Growth Tiers** — Characters scale visually through 5 growth tiers with socketed salvage cores changing appearance

## Git Workflow

- **Branch:** `dev`
- **Push:** `git -c core.hooksPath=/dev/null push origin dev` (LFS workaround)
- **Build from WSL:** `"/mnt/c/Program Files/dotnet/dotnet.exe" build`

## Known Gotcha

Godot 4's C# runtime **silently catches exceptions** in `_PhysicsProcess`, `_Process`, and `_Ready`. The method just stops executing — no error in logs. If something "stops working" with no errors, wrap suspicious code in try-catch to find the hidden exception.
