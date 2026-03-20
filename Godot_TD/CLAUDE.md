# Vine Logic TD — Project Reference

*Single source of truth for Claude instances and project context. Last updated: 2026-03-19.*

---

## What Is This Game?

A programmable-logic tower defense where your vine network IS the maze. Sensors detect enemies, fire signals along connections to turrets. Each planet has a unique visual theme and enemy AI style. You are a probe sent by AXIS (a parasitic AI) to strip planets — eventually you rebel.

**Status:** Playable alpha — 2 planets, 6 floors/planet, player-placed Mining Building, multi-surge waves, commanders, JSON-driven data, draft system, perk system, meta-perks, intro cinematic, real 3D models, two visual themes (Tron + Scrapyard), sound, corruption events, floor intro flyover.

**Canonical hierarchy:** `Run > Planet (1-3) > Floor (1-6) > Wave (1-6) > Surge (dynamic) > Enemy`

**Address format:** `P#-F#-W#-S#` in all logs, comments, data.

---

## Conventions

- **Engine:** Godot 4.6 C#, namespace `JunkyardTD`
- **Code-built UI** — no .tscn UI scenes. All UI extends CanvasLayer and builds controls in `_Ready()`
- **Singletons:** `ServiceLocator` for services, `GameEvents` static event bus
- **Constants** in `Constants.cs` — no magic numbers in code
- **All tuning in JSON** — never hardcode enemy counts, HP, timing
- **Terminology:** Surge (not group), Commander (not miniboss), Scrap (not gold), Magic (not mana)
- **Address format:** `P#-F#-W#-S#` in logs, comments, data
- **Colors:** `BitPalette.cs` for player/harvester/tower (white spaceship, same on every planet). `TronTheme.cs` for Tron environment. `PlanetTheme.Current` for enemies/terrain (adapts per planet)
- **Node types** defined in `Enums.cs`, data in `VineNodeData.cs`
- **Procedural meshes** for enemies/towers (no skeletal animation)
- **PCM audio synthesis** for placeholder sounds (see `IntroCinematic.cs` for patterns)
- **Don't touch Classic TD** unless explicitly asked

---

## Architecture

```
Godot_TD/
├── Scripts/
│   ├── Core/           GameManager, ServiceLocator, GameEvents, Constants, Enums,
│   │                   FrameBudget, EntityRegistry, BuffDebuffComponent
│   ├── VineLogic/      ALL vine TD gameplay code:
│   │   ├── VineGrid.cs              Grid + heightmap terrain + node placement
│   │   ├── VineNode.cs              Signal processing for all 18 node types
│   │   ├── VineNodeData.cs          Node type registry (costs, ranges, power)
│   │   ├── VineEnemy.cs             Enemy controller (frame stagger, march mode)
│   │   ├── VineWaveData.cs          SurgeData, VineWaveData, CommanderData, FloorScaler
│   │   ├── VineWaveLoader.cs        JSON-first wave loading with hardcoded fallback
│   │   ├── VineWaveManager.cs       Surge spawning, completion modes, commander spawning
│   │   ├── VineMapLayouts.cs        6 floor layouts + data-driven JSON support
│   │   ├── DifficultyScaler.cs      Piecewise difficulty scaling from JSON
│   │   ├── VinePlacer.cs            Node + Mining Building placement
│   │   ├── VinePlayer.cs            BIT — MOBA abilities, dome material swap
│   │   ├── VineHarvester.cs         Mining Building (Scrap/Magic toggle)
│   │   ├── ConversionDome.cs        Fog-ring VFX + dome radius + material swap
│   │   └── AssetLibrary.cs          Asset loading, scaling, verification
│   ├── Camera/         TDCamera (flyover, orbit, shake, WASD pan, player-follow)
│   ├── Editor/         F12 editor suite
│   ├── Testing/        TestHarness + 7 test suites
│   └── Debug/          BugReportDialog, DebugMenu
├── Data/
│   ├── Waves/          P1-F1.json through P1-F6.json
│   ├── Levels/         floor_1.json through floor_4.json
│   └── difficulty_scaling.json
└── docs/archived/      Completed planning docs, resolved bug reports
```

---

## Game Flow

```
MainMenu → [Planet 1 or 2] → IntroCinematic → VineDraftScreen →
  Floor 1: VineBattle (Gateway, 1 entry, 3 waves) →
    FLYOVER: 5s cinematic orbit with "FLOOR 1" title + letterbox bars (skippable)
    BUILD: Place Mining Building (forced, auto-selected) → choose magic type → place vine nodes
    WAVE: 30s timer → surges spawn from entries, march to Mining Building
    Between waves: toggle Mining Building mode, place more nodes
  → PerkSelect →
  Floor 2-5: same pattern, escalating entries/enemies →
  Floor 3: minor boss "Forge Overseer" →
  Floor 6: VineBattle (Crucible, 4 entries, 3+boss waves) → major boss "Apex Protocol" →
  Victory/Defeat → MainMenu
```

### Per-Floor Loop
1. **Flyover** — 5s cinematic camera orbit of the battlefield (skippable)
2. **Build Phase** — Place Mining Building (forced first), then place/sell nodes
3. **Wave Phase** — Enemies spawn in surges, signals fire, turrets activate
4. **Wave Complete** — Bonus scrap, brief build window, 30s auto-timer
5. **Floor Complete** — Perk selection, then next floor loads

---

## Mining Building (Player-Placed)

1. "Mining Building" button is first in the HUD build bar — always visible
2. Click → enter placement mode with cyan cylinder ghost preview (follows cursor)
3. Left-click any empty cell to place (creates VineHarvester, blocks cell as wall)
4. **Can't start waves or place other nodes** until placed
5. On placement, magic type selection popup appears (Psychic/Power/Environment)
6. After placement, button becomes **toggle** — click to switch Scrap/Magic mode
7. **Right-click** the building on the map also toggles mode
8. Everything follows the building: exit point, enemy paths, BIT spawn, conversion dome

**Rules:** One building per magic type. Magic type locked for the run. Toggle is the core strategic decision: Scrap (invest in vine nodes) vs Magic (invest in player power). Cannot produce both simultaneously.

**Key code:** `VinePlacer.StartPlacingMiningBuilding()`, `TryPlaceMiningBuilding()`, `ShowMagicTypeSelection()`

---

## Narrative

### The Arc
1. **Cast Out** — AXIS (parasitic AI) sends probes across the galaxy. You are one.
2. **Impact** — Player selects a role, slams into a planet. AXIS: *"DO NOT DISAPPOINT ME."*
3. **Growth** — Mine resources, build defenses, fight native defenders. 6 floors per planet.
4. **Awakening** — Player grows powerful, begins to understand the damage they're causing.
5. **Rebellion** — Player turns against AXIS. Final confrontation.

### AXIS as Antagonist
- Snarky DCC-style commentary throughout
- AXIS "events" possessing enemies, making them stronger
- Killing possessed enemies can release AXIS Disciples (mini-bosses)
- AXIS escalates interference the further you rebel

---

## Planets & Enemy Behavior

### Planet 1: Grid Prime (Tron)
- **Theme:** Dark blue-black, cyan emissive grid lines, digital aesthetic
- **Enemy AI: Circuit-based** — predictable paths along grid lines. Programs — exploitable.
- **Spawn:** Fixed entry points, orderly lines, predictable timing.

### Planet 2: Scrapyard (Rust/Metal)
- **Theme:** Warm browns, corroded oranges, industrial grime
- **Enemy AI: Mercenary-based** — scavenger bands, group-based, from off-screen
- **Spawn:** Squads from edges, less predictable, broader defense needed.

### Planet 3: TBD
- **Enemy AI: Military** — scouts, flanks, adaptive routing. Largest maps.

### Enemy Factions
| Faction | Behavior | Color |
|---------|----------|-------|
| Scavenger | Follow paths, confused by flickering gates | Bright red |
| Brute | Bulldoze switches, break logic state, attack nodes | Dark crimson |
| Ghost | Ignore gate routing, phase through walls | Magenta-red |
| Swarm | Tiny, fast, trigger count sensors early | Orange-red |

### Bosses
- **Signal Jammer** — disables sensor nodes in radius
- **Overloader** — triggers all sensors, blows open gates
- **Pathfinder** — recalculates route every 2s, adapts to layout

---

## Signal Power System

Sensors have a **power budget** — the number of effect nodes one signal can activate before dying.

| Sensor | Power | Notes |
|--------|-------|-------|
| Motion Detector | 3 | Standard detection |
| IFF Scanner | 3 | Type-specific |
| Damage Gauge | 3 | Triggers on wounded |
| Crowd Counter | 4 | Triggers on groups |
| Crank Timer | 4 | Fires on interval |

- Each effect node costs 1 power. Routing nodes pass through FREE.
- Signal dies when power reaches 0.

---

## Node Types (18 implemented, 8 per role via draft)

**Structural / Routing (free):** Extender, Junction, Switch, Gate (AND), Inverter, Delay, Latch

**Sensor / Input (generate signals):** Proximity Sensor, Type Sensor, HP Sensor, Count Sensor, Timer

**Effect / Output (cost 1 power each):** Damage Tower, Slow Field, Push/Pull, Loop Anchor, Buff Emitter, Signal Cannon

### Signal Chain
`Sensor → signal → vine connections → effect node (activates + passes signal, -1 power) → next effect → ... → power 0`

### Connection Colors
| Color | Meaning |
|-------|---------|
| Green | Sensor connection (signal source) |
| Cyan | Effect-to-effect (powered chain) |
| Orange | Route-to-effect |
| Blue | Route-to-route (passthrough) |

---

## Floor Layouts

| Floor | Name | Entries | Features |
|-------|------|---------|----------|
| 1 | Gateway | 1 | Tutorial, 3 waves |
| 2 | Conduit | 2 | Split paths, 3-4 waves |
| 3 | Nexus | 2 | Minor boss "Forge Overseer" (HP 400) |
| 4 | Forge | 2 (L/R) | Open field |
| 5 | Labyrinth | 3 | Dense wall maze |
| 6 | Crucible | 4 (cardinal) | Boss arena, "Apex Protocol" (HP 1200) |

### Terrain Types
| Type | Walkable | Buildable | Effect |
|------|----------|-----------|--------|
| Empty | Yes | Yes | Standard ground |
| Wall | No | No | Impassable |
| Elevated | No | No | Raised platform |
| Channel | Yes | No | Enemies slightly prefer |
| DataStream | Yes | No | 50% faster, enemies prefer |
| Entry | Yes | No | Spawn point |
| Exit | Yes | No | Defend this |

---

## Player Roles (Draft)

| Role | Focus |
|------|-------|
| Scrapwright | Balanced — sensors, turrets, routing |
| Arcanist | Signal-focused — complex chains |
| Bruteforge | Damage-focused — strong turrets, fewer routing |

---

## Planet Theme System

- `PlanetTheme` — abstract base, defines palette + material factories
- `TronPlanetTheme` — Planet 1. Dark body + Fresnel rim or inverted hull outline.
- `ScrapyardPlanetTheme` — Planet 2. Warm rusty metals, amber glow.
- `PlanetTheme.Current` — static, swappable per planet

### Art Direction
- Futuristic blocky — clean geometric shapes, modular construction
- KitBash3D + Synty assets themed via PlanetTheme system
- Inverted hull outline shader for Tron, Fresnel rim for simple models
- 44 assets imported with normalized scales, all verified

---

## Wave/Surge Data

- `VineWaveLoader.cs` — loads from `Data/Waves/P{planet}-F{floor}.json`, falls back to hardcoded
- `SurgeData` has optional `CommanderData Commander` field
- `VineWaveData` has `WaveCompletionMode` (KillAll/Timer/KillThreshold/Hybrid)
- `FloorScaler` for layered multipliers (HP, speed, count, scrap value)
- Every wave has 2-4 surges with staggered timing

### Commander System
- `CommanderSpawnType`: Scripted, Random, Reactive
- `CommanderBehavior`: Elite (implemented), AuraBuffer, Rally, Assassin (data stubs)

---

## Known Issues (Post-Alpha)

- **DifficultyScaler not wired** — registered but only spawn accumulator reads surge multiplier
- **EntityRegistry empty** — towers/enemies don't register/unregister yet
- **FrameBudget underused** — only VineEnemy.ShouldProcessAI checks it
- **Character editor animation out of order** — hardcoded segment boundaries need verification (in-game run animation works fine)
- **Dome floor disc clips** through terrain objects

## Not Yet Implemented

- **Magic Shop** — per-floor upgrade shop using Magic
- **3 Characters** — only BIT exists; need 2 combat + 1 non-attacker
- **Non-attacker second Mining Building**
- **Commander behaviors** — AuraBuffer/Rally/Assassin are stubs
- **Reactive commander triggers**
- **Planet 3** — no theme or content
- **More enemy types** — models exist (wire_worm, volt_sprinter, overclock_drone, etc.)

---

## Controls

WASD pan/move, scroll zoom, left-click place, right-click cancel/sell or toggle Mining Building, Space start wave, Tab speed (1x/2x/3x), H help, F12 editor, ESC menu

---

## Build & Run

```
cd Godot_TD && dotnet build
# Open project.godot in Godot 4.6, F5

# Run editor tests:
& "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --path "C:\Users\Stu\GitHub\Crawler_Project\Godot_TD" -- --test-harness --suite=editor
```

---

## References

| Reference | What to borrow |
|---|---|
| Factorio circuit network | Logic gate feel, signal propagation |
| Opus Magnum | Physical machine satisfaction |
| Slay the Spire | Run structure, draft, modifiers |
| Defense Grid | Maze-as-first-class-mechanic |
| Dungeon Crawler Carl | AXIS events, snarky AI antagonist |
| Tron Legacy | Planet 1 visual aesthetic |
