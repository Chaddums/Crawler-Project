# Vine Logic TD — Developer Onboarding

*For developers joining the project. Read this before writing any code.*

---

## Quick Setup

```bash
# Clone and build
git clone https://github.com/Chaddums/Crawler-Project.git
cd Crawler-Project

# Build C# project
cd Godot_TD && dotnet build

# Open in Godot 4.6
# File > Open Project > navigate to Godot_TD/project.godot
# Press F5 to run
```

**Requirements:** Godot 4.6.1 with .NET, .NET 8 SDK, Windows (primary dev platform).

---

## What Is This Game?

A **programmable-logic tower defense** where you build a network of sensors, routing nodes, and turrets. The network IS the maze — enemies pathfind around your nodes.

You are **BIT** — an ancient AI deployed by AXIS (a dismissive corporation) to strip planets. You always lose. The question is how much you extract before you fall.

**Core loop:** Place Mining Building → build sensor-to-turret signal chains → survive escalating waves → extract as much as possible → fall → spend resources in meta layer → run again with more options.

**One-liner:** "Mine everything you can before they take it all."

**Unique mechanic:** Sensors have a **power budget** (3-4). Each effect node (turret, slow field) costs 1 power. Routing nodes (extender, junction, switch) pass signals free. So one sensor can power 3 turrets max.

---

## How to Play

### Starting a Run
1. **Main Menu** → Select Planet
2. **Intro Cinematic** → 30-second AXIS scene (press any key to skip)
3. **Draft Screen** → Choose role (Scrapwright / Arcanist / Bruteforge) — determines your 8 available node types

### The Run (Continuous)
1. **Place Mining Building** — Mandatory first action. Click to place on any empty cell
2. **Choose Material Type** — Popup: Chaos, Power, or Environment (locked for the run)
3. **Build Phase** — Place nodes from the bottom bar. Connect sensors → routing → turrets
4. **Wave Phase** — Press Space (or wait 30s). Enemies spawn in surges, march to your Spire
5. **Between Waves** — Place more nodes, toggle Mining Building between Resources/Materials mode
6. **Wave Milestones** — At key waves: perk selection, new entry points open, map expands
7. **Extraction** — Resources scale exponentially the longer you survive
8. **Fall** — Spire destroyed. Extraction total shown. Resources carry to meta layer.

**There are no floors.** One continuous map per run. Your build compounds over time. No rebuilds, no resets.

### Controls
| Key | Action |
|-----|--------|
| WASD | Pan camera (build phase) / Move player (wave phase) |
| Scroll | Zoom in/out |
| Left-click | Place node / Select |
| Right-click | Cancel placement / Sell node / Toggle Mining Building mode |
| Space | Start wave / Send next wave early |
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
- **Materials Mode**: Accumulates materials (fund player abilities and upgrades)
- Toggle with the HUD button or right-click the building on the map
- **Core strategic decision:** more towers vs more ability power

### Signal Chain Basics
```
Sensor → [optional routing nodes] → Effect Node (turret/slow/etc.)
```
- Sensors fire signals when they detect enemies
- Signals travel along vine connections to reach effect nodes
- Each effect node costs 1 power from the signal
- Routing nodes (Extender, Junction, Switch, Gate) pass signals FREE
- A Proximity Sensor (power 3) can activate up to 3 turrets in a chain

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

## Hierarchy & Terminology

### Game Hierarchy
```
Run
└── Planet   (1-3)
    └── Wave (1-N, continuous)
        └── Surge  (dynamic count)
            └── Enemy
```

### Address Format
`P#-W#-S#` — Example: `P1-W4-S3` = Planet 1, Wave 4, Surge 3

### Terminology (Use These Everywhere)
| Term | NOT this | Meaning |
|------|----------|---------|
| Surge | ~~group, spawn group~~ | A batch of enemies within a wave |
| Commander | ~~miniboss, special~~ | Named enemy with special behavior |
| Resources | ~~Gold, Scrap~~ | Currency for building nodes |
| Materials | ~~Mana, Magic~~ | Resource for player abilities and upgrades |
| Ascendant | ~~god, hero bot~~ | Massively overpowered AI that appears late game |

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
│   ├── Commentary/     AXISCommentary
│   ├── Editor/         F12 editor suite (6 modules)
│   ├── Debug/          DebugMenu (~ console + Insert panel)
│   └── UI/             MainMenuUI
├── Data/
│   ├── Waves/          JSON wave data per planet
│   ├── Levels/         Map layout JSON
│   ├── audio.json      Sound manifest
│   └── difficulty_scaling.json
├── Audio/              WAV files (SFX, Abilities, Ambient — no Music yet)
├── Models/             FBX/GLB models (player, enemies, props)
├── Scenes/             .tscn scene files
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

---

## Developer Tools

### F12 Editor
Press F12 in-game to open the dev editor. Pauses the game.

| Tab | What it does |
|-----|-------------|
| Asset Sandbox | Browse all imported 3D assets, preview with orbit camera, apply themes |
| Node Balance | Tune node costs, ranges, damage values live |
| Wave Editor | Edit wave/surge configurations |
| Signal Tuning | Tune player stats, enemy stats, harvester stats, sell refund |
| Characters | View/test character animations |
| Sound Designer | Browse and test audio system |

### Debug Console (~)
| Command | Effect |
|---------|--------|
| `scrap 500` | Add 500 resources |
| `kill` | Kill all enemies |
| `god` | Toggle god mode |
| `speed 5` | Set game speed to 5x |
| `boss` | Skip to boss wave |
| `chaos` | Trigger AXIS corruption event |
| `hp` | Toggle HP bars |
| `freecam` | Toggle free camera |

### Bug Reporter (Ctrl+Shift+B)
Opens screenshot capture + bug report dialog. Saves to `bugs/` directory.

---

## Known Issues

- **PushPull node does nothing** — `ActivateEffect` fires but no movement logic
- **TypeSensor triggers on ALL enemies** — No faction filter implemented
- **DifficultyScaler not wired** — Only surge spawn multiplier is read
- **EntityRegistry empty** — Registered but nothing uses it
- **ConversionDome rebuilds meshes every frame** — Performance concern
- **Planet 2 has no wave data** — Falls back to P1 data
- **No music exists** — Only SFX and ambient
- **Floor references still in code** — Being removed (see Phase 0 in backlog)
