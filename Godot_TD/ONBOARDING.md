# Vine Logic TD — Developer Onboarding

*For new developers joining the weekend sprint. Read this before writing any code.*

---

## Quick Setup

```bash
# Clone and build
git clone https://github.com/Chaddums/Crawler-Project.git
cd Crawler-Project
git checkout dev

# Build C# project
cd Godot_TD && dotnet build

# Open in Godot 4.6
# File > Open Project > navigate to Godot_TD/project.godot
# Press F5 to run
```

**Requirements:** Godot 4.6.1 with .NET, .NET 8 SDK, Windows (primary dev platform).

---

## What Is This Game?

A **programmable-logic tower defense** where you build a network of sensors, routing nodes, and turrets. The network IS the maze — enemies pathfind around your nodes. You're a probe sent by AXIS (an evil AI) to strip planets of resources.

**Core loop:** Place Mining Building → build sensor-to-turret signal chains → survive 3 waves → choose perk → repeat for 6 floors → win.

**Unique mechanic:** Sensors have a **power budget** (3-4). Each effect node (turret, slow field) costs 1 power. Routing nodes (extender, junction, switch) pass signals free. So one sensor can power 3 turrets max.

---

## How to Play

### Starting a Run
1. **Main Menu** → Click "Planet 1: Grid Prime" or "Planet 2: Scrapyard"
2. **Intro Cinematic** → 30-second AXIS scene (press any key to skip)
3. **Draft Screen** → Choose role (Scrapwright / Arcanist / Bruteforge) — determines your 8 available node types

### Each Floor
1. **Flyover** — 5s camera orbit showing the battlefield layout (skip with any key)
2. **Place Mining Building** — Mandatory first action. Click to place on any empty cell
3. **Choose Magic Type** — Popup: Chaos, Power, or Environment (locked for the run)
4. **Build Phase** — Place nodes from the bottom bar. Connect sensors → routing → turrets
5. **Wave Phase** — Press Space (or wait 30s). Enemies spawn and march to your Mining Building
6. **Between Waves** — Place more nodes, toggle Mining Building between Resources/Materials mode
7. **Floor Complete** → Choose 1 of 3 perks → Next floor

### Controls
| Key | Action |
|-----|--------|
| WASD | Pan camera (build phase) / Move player (wave phase) |
| Scroll | Zoom in/out |
| Left-click | Place node / Select |
| Right-click | Cancel placement / Sell node / Toggle Mining Building mode |
| Space | Start wave / Send next wave early |
| Shift+Space | Send all remaining waves at once (bonus resources) |
| Tab | Speed toggle (1x → 2x → 3x) |
| Q/E/R | Player abilities (Shock Blast / Repair Pulse / Overclock) |
| T | Toggle terrain overlay |
| H | Help overlay |
| F12 | Developer editor |
| ESC | Return to menu |
| ~ | Debug console |
| Insert | Debug panel |

### Player Abilities (During Waves)
- **Q — Shock Blast**: AoE damage around player (15 materials, 4s cooldown)
- **E — Repair Pulse**: Heal Mining Building for 40 HP (25 materials, 8s cooldown)
- **R — Overclock**: Buff all towers in radius (40 materials, 15s cooldown)

### Mining Building
- **Resources Mode** (default): Generates resources every 5s (spend on building nodes)
- **Materials Mode**: Accumulates materials (spent on player abilities and upgrades)
- Toggle with the HUD button or right-click the building on the map
- Strategic decision: more towers vs more ability power

### Signal Chain Basics
```
Sensor → [optional routing nodes] → Effect Node (turret/slow/etc.)
```
- Sensors fire signals when they detect enemies
- Signals travel along vine connections to reach effect nodes
- Each effect node costs 1 power from the signal
- Routing nodes (Extender, Junction, Switch, Gate) pass signals FREE
- A Motion Detector (power 3) can activate up to 3 turrets in a chain

### Connection Colors
| Color | Meaning |
|-------|---------|
| Green | From sensor (signal source) |
| Cyan | Effect-to-effect (powered chain) |
| Orange | Route-to-effect |
| Blue | Route-to-route |

---

## The 18 Node Types

### Structural / Routing (Free signal pass-through)
| Node | Cost | What it does |
|------|------|-------------|
| Extender | 3 | Passes signals to all connections |
| Junction | 5 | Same as Extender but 4 connections max |
| Switch | 8 | Alternates between two output paths |
| Gate (AND) | 15 | Only opens when 2+ signals arrive within timing window |
| Inverter | 6 | Flips signal type (Trigger ↔ Reset) |
| Delay | 7 | Holds signal for configurable duration, then releases |
| Latch | 10 | Stays open until Reset signal received |

### Sensors (Generate signals with power budget)
| Node | Cost | Power | Trigger |
|------|------|-------|---------|
| Proximity Sensor | 8 | 3 | Enemy within range |
| Type Sensor | 10 | 3 | Specific enemy faction (WIP) |
| HP Sensor | 8 | 3 | Wounded enemy (< 50% HP) |
| Count Sensor | 10 | 4 | 5+ enemies in range |
| Timer | 12 | 4 | Fires every N seconds (no enemy needed) |

### Effects (Cost 1 power each, activate + pass signal onward)
| Node | Cost | What it does |
|------|------|-------------|
| Damage Tower | 15 | Shoots closest enemy in range |
| Slow Field | 12 | Slows enemies in area |
| Push/Pull | 10 | Displaces enemies (WIP — needs fix) |
| Loop Anchor | 8 | Pathfinding anchor point (partial) |
| Buff Emitter | 10 | Buffs adjacent towers |
| Signal Cannon | 12 | Player-triggered manual fire (middle-click) |

### Draft Roles (8 nodes each)
| Role | Nodes | Playstyle |
|------|-------|-----------|
| **Scrapwright** | Extender, Junction, Switch, Gate, Delay, Inverter, ProxSensor, DamageTower | Maze builder — complex routing |
| **Arcanist** | ProxSensor, Timer, CountSensor, HPSensor, TypeSensor, Extender, DamageTower, SlowField | Signal specialist — sensor chains |
| **Bruteforge** | DamageTower, SlowField, BuffEmitter, PushPull, SignalCannon, Extender, Junction, ProxSensor | Damage dealer — raw firepower |

---

## Project Architecture

```
Godot_TD/
├── Scripts/
│   ├── Core/           GameManager, ServiceLocator, GameEvents, Constants, Enums
│   ├── VineLogic/      ALL gameplay: grid, nodes, enemies, player, waves, placer, harvester, dome
│   ├── Camera/         TDCamera (flyover, orbit, shake)
│   ├── Audio/          AudioManager (3-tier: manifest → convention → PCM synthesis)
│   ├── VFX/            VfxFactory (projectiles, death bursts, pulses)
│   ├── Editor/         F12 editor suite (6 modules)
│   ├── Debug/          DebugMenu (~ console + Insert panel)
│   └── UI/             MainMenuUI
├── Data/
│   ├── Waves/          P1-F1.json through P1-F6.json (P2 missing!)
│   ├── Levels/         floor_1.json through floor_4.json
│   ├── audio.json      Sound manifest
│   └── difficulty_scaling.json
├── Audio/              WAV files (SFX, Abilities, Ambient — no Music yet)
├── Models/             FBX/GLB models (player, enemies, props)
├── Scenes/             .tscn scene files (11 total)
└── CLAUDE.md           Single source of truth for project context
```

### Key Patterns
- **Code-built UI** — No .tscn for UI. Everything builds CanvasLayer + Controls in `_Ready()`.
- **ServiceLocator** — Singletons register/unregister. Access via `ServiceLocator.Get<T>()`.
- **GameEvents** — Static event bus. `GameEvents.OnWaveStarted?.Invoke(waveNum)`.
- **PlanetTheme.Current** — Polymorphic theme. `TronPlanetTheme` or `ScrapyardPlanetTheme`.
- **BitPalette** — Player/tower colors (white/silver). Same on every planet.
- **Constants.cs** — All tuning numbers. Never use magic numbers.
- **JSON-first data** — Wave data, difficulty scaling, audio manifest all in `Data/`.

### Terminology (Use These Everywhere)
| Term | NOT this | Meaning |
|------|----------|---------|
| Surge | ~~Group~~ | A batch of enemies within a wave |
| Commander | ~~Miniboss~~ | Named enemy with special behavior |
| Resources | ~~Gold/Scrap~~ | Currency for building nodes |
| Materials | ~~Mana/Magic~~ | Resource for player abilities and upgrades |
| Address | — | `P#-F#-W#-S#` format in logs/comments |

---

## Developer Tools

### F12 Editor
Press F12 in-game to open the dev editor. Pauses the game.

| Tab | What it does |
|-----|-------------|
| Asset Sandbox | Browse all 42 imported 3D assets, preview with orbit camera, apply themes |
| Node Balance | Tune node costs, ranges, damage values live |
| Wave Editor | Edit wave/surge configurations |
| Signal Tuning | Tune player stats, enemy stats, harvester stats, sell refund — persists across floors |
| Characters | View/test character animations, split monolithic FBX clips |
| Sound Designer | Browse and test audio system |

### Debug Console (~)
Press tilde in-game. Quick commands:

| Command | Effect |
|---------|--------|
| `scrap 500` | Add 500 resources |
| `kill` | Kill all enemies |
| `god` | Toggle god mode |
| `instakill` | Toggle instant kill |
| `heal` | Full heal player + harvester |
| `speed 5` | Set game speed to 5x |
| `floor 3` | Jump to floor 3 |
| `boss` | Skip to boss wave |
| `win` | Complete current floor |
| `chaos` | Trigger AXIS corruption event |
| `names` | Toggle enemy name labels |
| `hp` | Toggle HP bars on everything |
| `freecam` | Toggle free camera (WASD+QE fly) |

### Debug Panel (Insert)
Press Insert for a categorized panel with buttons for all debug actions.

### Bug Reporter (Ctrl+Shift+B)
Opens screenshot capture + bug report dialog. Saves to `bugs/` directory.

---

## Git Workflow for Multi-Claude Development

### Branch Strategy
We're working on `dev` branch. With 3 Claude instances + 2 humans, coordinate:

1. **Before starting work:** `git pull origin dev`
2. **Communicate your task** — Say which files you're touching in the coordination doc
3. **Commit frequently** — Small, descriptive commits
4. **Push often** — Don't accumulate hours of uncommitted work
5. **If merge conflicts:** Pull first, resolve conflicts, commit the resolution

### File Ownership to Avoid Conflicts
Assign files to Claude instances:
- **Claude A** owns: `Data/Waves/*.json`, `VineNode.cs`, `VineEnemy.cs`, `VineWaveManager.cs`, `DifficultyScaler.cs`
- **Claude B** owns: `AudioManager.cs`, `audio.json`, `VineHUD.cs`, `VinePerkData.cs`, `CorruptionManager.cs`
- **Claude C** owns: `MainMenuUI.cs`, `ConversionDome.cs`, `DebugMenu.cs`, `VfxFactory.cs`
- **Shared** (coordinate before touching): `Constants.cs`, `GameManager.cs`, `GameEvents.cs`, `Enums.cs`

### CLAUDE.md
Read `CLAUDE.md` before making any design decisions. It's the single source of truth for project context, architecture, and conventions.

---

## Game Systems Quick Reference

### Economy
- Starting resources: 90
- Harvester income: 3 resources every 5s
- Wave completion bonus: 15 resources
- Sell refund: 60% of node cost
- Resources per enemy kill: varies (3-16 based on enemy type and floor)

### Difficulty Scaling (Per-Floor)
| Floor | HP Mult | Speed Mult | Count Mult |
|-------|---------|------------|------------|
| 1 | 1.0x | 1.0x | 1.0x |
| 2 | 1.1x | 1.0x | 1.05x |
| 3 | 1.15x | 1.0x | 1.1x |
| 4 | 1.3x | 1.0x | 1.2x |
| 5 | 1.5x | 1.05x | 1.3x |
| 6 | 1.8x | 1.1x | 1.5x |

### Enemy Types
| Enemy | Faction | Behavior |
|-------|---------|----------|
| Scrap Rat | Scavenger | Standard, confused by flickering gates |
| Buzz Drone | Swarm | Fast, low HP, spawns in large groups |
| Rust Hulk | Brute | Slow tank, breaks switches on contact, resists slow |
| Phase Crawler | Ghost | Phases through gates and nodes |
| Iron Warden | Commander | Elite boss with high HP |
| Forge Overseer | Boss (F3) | 400 HP mini-boss |
| Apex Protocol | Boss (F6) | 1200 HP final boss |

### Corruption Events (AXIS Chaos)
Triggers on Floor 2+ during waves:
- Enemies get +50% HP, armor bonus
- Player gets +1.5x speed, +1.5x attack speed
- Resource drops at 3x rate
- Enemies wander randomly (stop pathing to exit)
- Red lightning VFX, ground shader changes
- Reverts after wave completion

### Meta-Perks (Persistent)
25 nodes across 3 lanes (Network / Player / Harvester), 9 tiers.
Points earned: 3 for Floor 1, 2 for each subsequent floor.
Saved to `user://vine_meta.json`.

### In-Run Perks (13 total)
Pick 1 of 3 random perks after each floor:
- Tower perks: +20% DPS, +1 sensor range, +30% signal speed, +15% slow, +1 turret range
- Economy perks: +2 lives, +15% sell refund, +40 resources
- Player perks: +30 HP, +25% atk speed, +50% materials regen, +40% atk damage, +50 harvester HP

---

## Known Issues to Be Aware Of

- **PushPull node does nothing** — `ActivateEffect` fires but no `UpdatePushPull()` exists
- **TypeSensor triggers on ALL enemies** — No faction filter implemented
- **DifficultyScaler not wired** — JSON data exists but only surge spawn multiplier is read
- **EntityRegistry empty** — Registered but nothing uses it
- **ConversionDome rebuilds meshes every frame** — Performance concern
- **Character editor animation boundaries are wrong** — In-game Run works fine though
- **Dome floor clips through terrain** — Needs Z-offset
- **Planet 2 has no wave data** — Falls back to Planet 1 data
- **No music exists** — Only SFX and ambient
- **Pause resumes to Build phase** — Could be wrong if paused during wave
- **100+ Sonniss WAV files unmapped** — `Assets/Audio/Sonniss/BigMechanical/` has real audio but `Data/audio.json` doesn't reference most of them
- **Floating damage numbers exist** — `VFX/DamageNumber.cs` is implemented (Label3D, color-coded, crit support)
