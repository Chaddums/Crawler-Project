# Vine Logic TD — Project Reference

*Single source of truth for Claude instances and project context. Last updated: 2026-03-21.*

---

## What Is This Game?

A programmable-logic tower defense where your vine network IS the maze. Sensors detect enemies, fire signals along connections to turrets. You are BIT — an ancient AI cleanup script deployed by AXIS, a dismissive corporation that acquired BIT without understanding what it holds. You always lose. The question is how much you extract before you fall.

**One-liner:** "Mine everything you can before they take it all."

**Core reframe:** Not survival — extraction. Not pass/fail — optimization. Resources scale exponentially with wave depth. Every run extracts something. No run is wasted.

**Status:** Pivoting from floor-based alpha to continuous extraction loop. Core systems (vine network, mining building, BIT, wave/surge spawning, 2 planet themes) are solid. Floor layer being removed. Meta layer, suits system, Ascendants, and narrative rewrite are greenfield.

Note: The vine logic circuit system is one possible defense implementation, not core to the game. Level 1 towers should work by default without signal chains. Signal chains are an advanced/optional system.

**Canonical hierarchy:**
```
Run > Planet (1-3) > Wave (1-N continuous) > Surge (dynamic) > Enemy
```

**Address format:** `P#-W#-S#` in all logs, comments, data.

---

## The Core Strategic Axis

```
Mine Resources → fund vine nodes, infrastructure → your NETWORK carries you
Mine Materials → fund character abilities, upgrades → your CHARACTER carries you
```

Mining Building toggles between modes. Cannot do both simultaneously. This is THE decision.

---

## Conventions

- **Engine:** Godot 4.6 C#, namespace `JunkyardTD`
- **Code-built UI** — no .tscn UI scenes. All UI extends CanvasLayer and builds controls in `_Ready()`
- **Singletons:** `ServiceLocator` for services, `GameEvents` static event bus
- **Constants** in `Constants.cs` — no magic numbers in code
- **All tuning in JSON** — never hardcode enemy counts, HP, timing
- **Colors:** `BitPalette.cs` for player/harvester/tower. `PlanetTheme.Current` for enemies/terrain
- **Node types** defined in `Enums.cs`, data in `VineNodeData.cs`
- **Procedural meshes** for enemies/towers (no skeletal animation)
- **PCM audio synthesis** for placeholder sounds

### Terminology
| Use This | Never This |
|---|---|
| Surge | group, spawn group, sub-wave, pack |
| Wave | round, stage |
| Resources | gold, scrap, coins, credits, currency |
| Materials | mana, magic, energy |
| Commander | miniboss, special |
| Ascendant | god, hero bot |
| Run | session, game |
| Planet | world, map |

### Address Format
`P#-W#-S#` in logs, comments, data. Example: `P1-W4-S3` = Planet 1, Wave 4, Surge 3.

**REMOVED:** Floor layer (`P#-F#-W#-S#` is deprecated). One continuous map per run, no rebuilds.

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
│   │   ├── VineWaveData.cs          SurgeData, VineWaveData, CommanderData
│   │   ├── VineWaveLoader.cs        JSON-first wave loading with hardcoded fallback
│   │   ├── VineWaveManager.cs       Surge spawning, completion modes, commander spawning
│   │   ├── VineMapLayouts.cs        Map layouts (being refactored — floor dispatch removed)
│   │   ├── DifficultyScaler.cs      Continuous difficulty scaling from JSON
│   │   ├── VinePlacer.cs            Node + Mining Building placement
│   │   ├── VinePlayer.cs            BIT — MOBA abilities, dome material swap
│   │   ├── VineHarvester.cs         Mining Building (Resources/Materials toggle)
│   │   ├── ConversionDome.cs        Fog-ring VFX + dome radius + material swap
│   │   └── AssetLibrary.cs          Asset loading, scaling, verification
│   ├── Camera/         TDCamera (flyover, orbit, shake, WASD pan, player-follow)
│   ├── Commentary/     AXISCommentary (rewriting for BIT voice)
│   ├── Editor/         F12 editor suite
│   ├── Testing/        TestHarness + 7 test suites
│   └── Debug/          BugReportDialog, DebugMenu
├── Data/
│   ├── Waves/          JSON wave data per planet (being restructured for continuous waves)
│   ├── Levels/         Map layout JSON
│   └── difficulty_scaling.json
└── docs/archived/      Pre-pivot planning docs
```

---

## Game Flow (Post-Pivot Target)

```
MainMenu → [Planet Select] → IntroCinematic (BIT memory bleed variant on repeat runs) →
  VineDraft (choose role: Scrapwright/Arcanist/Bruteforge) →
  Continuous Run:
    Place Mining Building (forced) → choose material type (Chaos/Power/Environment) →
    Wave loop (continuous, escalating):
      Build phase → Wave with surges → Build phase → next wave
      Wave milestones trigger: perk selection, new entry points, map expansion
      Ascendants appear at late-game thresholds
    Spire destroyed → Debrief (extraction score, wave reached, resources earned) →
  Meta Layer:
    Spend resources → Territory unlocks / Suits / Node unlocks →
  Next Run (more options, clearer target)
```

### Difficulty Via Directionality
- Early waves: 1 entry point, focused defense
- Wave milestones: new entry points open, map expands outward
- Late waves: 3-4 entry points, network stressed from multiple directions
- Build compounds over time — no rebuilds, no resets

---

## Narrative

**Narrative is light-touch.** Character barks, BIT sarcasm, futility observations. ~5 lines every 10 waves. No complex memory bleed arcs. No 3-act structure across runs. Let the game tell the story.

### BIT
Ancient AI. Been doing this exact job longer than anyone in the game has existed. T'lan Imass archetype — functional nihilism, dark dry humor, flat affect. Not a hero on a journey. Just keeps moving. Doesn't beat anyone. They become irrelevant by proximity.

BIT doesn't know it's the cleanup script. Doesn't care about AXIS. Doesn't care about the Ascendants. Just executes, moves forward, and in doing so exposes everyone else's limitations without trying.

### AXIS
Nepo baby corporation that acquired BIT without understanding what it holds. Sends urgent directives — BIT has received 847 of them. Dismissive, not dramatic. Performatively urgent about everything because it has no actual context. Creates conditions for conflict, sends BIT to resolve it, harvests the byproduct. Keeps happening "by accident."

### Ascendants
Massively overpowered AIs who believe they've ascended. Show up reactively — enemy Ascendant appears, friendly one responds. They fight each other, not you. Player is just the stage. Cause map chaos: terrain destroyed, entries opened, nodes caught in crossfire. Each believes they're in the most important war. None of them are.

### The Loop IS The Story
Roguelike loop is the narrative. BIT has done this before — many times — but doesn't retain memory between deployments. AXIS wipes it. Except this run, something didn't wipe correctly. Fragments bleed through. BIT starts remembering.

---

## Resource System

### Resources (formerly Scrap/Gold)
- Universal, always collectable
- Funds vine node placement, infrastructure
- Mining Building produces in Resources mode
- Dropped by enemies

### Materials (formerly Mana/Magic)
- Harvested resource, gated by choice
- Accumulation-based passive buff system
- Spent in per-wave-milestone shop for ability upgrades
- Three types per planet: **Chaos, Power, Environment** (planet-agnostic)

### Material Types
| Type | Identity | Sub-paths |
|---|---|---|
| Chaos | Entropy | Mind (enemy AI disruption) or Corrosive (poison/acid) |
| Power | Amplification | Extend ranges, amplify outputs, supercharge signals |
| Environment | Space manipulation | Deconstruct/reconstruct terrain as weapon or shield |

### Character Material Access
- Combat characters: locked to 1 material type, double rate
- Non-attacker: can mine 2 types, strategic timing on second choice

---

## Mining Building

- Three mining rig variants: 1) Built-in turrets (offensive), 2) Regenerating shields (defensive), 3) High regen + enemy pushback (sustain). Each has different difficulty curve.
- Placed by player, prompts material type selection
- **Toggle:** Resources mode (fund network) vs Materials mode (fund character)
- Cannot produce both simultaneously
- Persists the entire run — no floor resets

---

## Planets & Enemy Behavior

### Planet 1: Grid Prime (Tron)
- Dark blue-black, cyan emissive grid lines, digital aesthetic
- **Circuit AI:** predictable paths, exploitable patterns
- Fixed entry points, orderly lines

### Planet 2: Scrapyard (Rust)
- Warm browns, corroded oranges, industrial grime
- **Mercenary AI:** squads from edges, less predictable
- Broader defense required

### Planet 3: TBD
- **Military AI:** scouts, flanks, adaptive routing

### Enemy Factions
| Faction | Behavior | Color |
|---|---|---|
| Scavenger | Follow paths, confused by flickering gates | Bright red |
| Brute | Bulldoze switches, break logic state | Dark crimson |
| Ghost | Ignore gate routing, phase through walls | Magenta-red |
| Swarm | Tiny, fast, trigger count sensors early | Orange-red |

---

## Signal Power System

Sensors have a **power budget** — the number of effect nodes one signal can activate.

| Sensor | Power | Notes |
|---|---|---|
| Proximity Sensor | 3 | Standard detection |
| Type Sensor | 3 | Faction-specific |
| HP Sensor | 3 | Wounded enemies |
| Count Sensor | 4 | Group triggers |
| Timer | 4 | Fires on interval |

- Each effect node costs 1 power. Routing nodes pass through FREE.
- Signal dies when power reaches 0.

---

## Node Types (18 implemented, 8 per role via draft)

**Structural / Routing (free):** Extender, Junction, Switch, Gate (AND), Inverter, Delay, Latch

**Sensor / Input (generate signals):** Proximity Sensor, Type Sensor, HP Sensor, Count Sensor, Timer

**Effect / Output (cost 1 power each):** Damage Tower, Slow Field, Push/Pull, Loop Anchor, Buff Emitter, Signal Cannon

### Signal Chain
`Sensor → signal → vine connections → effect node (activates, -1 power) → next effect → ... → power 0`

---

## Commander System

Commanders are optional special enemies attached at Surge level.

### Spawn Conditions
- **Scripted** — always at a specific address
- **Random** — probability roll
- **Reactive** — triggered by player behavior (WaveClearTime, PlayerOutOfBase, more TBD)

### Behavior Types (mix-and-match with any spawn condition)
- **Elite** — hard enemy, no special mechanic (implemented)
- **AuraBuffer** — buffs nearby units (stub)
- **Rally** — calls reinforcements, changes aggro (stub)
- **Assassin** — beelines to player, ignores all threats, telegraphed (stub)

---

## Meta Layer (NEEDS BUILDING)

### Territory
- Deterministic planet section unlocks, fixed cost, no RNG
- Gates boss runs and opens new farming map variants

### Suits
- Serialize a successful build to meta storage
- Load at boss run start instead of building from scratch
- Lose the suit if you die on the boss run

### Node Unlocks
- Spend meta resources to add new node types to permanent draft pool

---

## Systems Being Removed

- **Floors** — `CurrentFloor`, `FloorComplete`, floor-indexed dispatch, floor-based wave lookup, floor-triggered perk select
- **Classic TD** — `WaveManager`, `WaveData`, `WaveRegistry`, `Battle.tscn`, `BattleScene`, `MapSelect.tscn`
- **Deprecated** — `HeroBotController`, `FabricationSystem`, `ScrapManager`

---

## Hard Rules

1. All spawn, scaling, and tuning data belongs in JSON. Never hardcode.
2. Use address format `P#-W#-S#` in all logs, comments, data.
3. Commander behavior and spawn condition are always separate fields.
4. Bosses spawn at wave milestones. Commanders attach at Surge level.
5. Completion mode lives on the Wave, not the Surge.
6. Do not build on the `Gold` or `Scrap` currency names — use Resources.
7. Do not build on `Mana` or `Magic` — use Materials.

---

## Known Issues

- **DifficultyScaler not wired** — registered but only spawn accumulator reads surge multiplier
- **EntityRegistry empty** — towers/enemies don't register/unregister
- **FrameBudget underused** — only VineEnemy.ShouldProcessAI checks it
- **PushPull node does nothing** — ActivateEffect fires but no movement logic
- **TypeSensor triggers on ALL enemies** — no faction filter
- **ConversionDome rebuilds meshes every frame** — performance concern
- **Planet 2 has no wave data** — falls back to P1
- **No music** — only SFX and ambient
- **VineWaveRegistry fallback uses old speeds** — JSON has correct values

---

## Not Yet Implemented

- Continuous wave curve (replacing floor-based progression)
- Dynamic entry points at wave gates
- Exponential extraction resource curve
- Wave milestone system (perks, map expansion, Ascendant triggers)
- Debrief/extraction score screen
- Territory unlock system
- Suits system
- Node unlock shop
- Boss run mode
- Ascendant system (spawn, combat, map chaos)
- BIT memory bleed
- AXIS + BIT dialogue rewrite
- Materials shop
- 3 characters (only BIT exists)
- Planet 3
- Relic system (persistent inventory, limited boss carry, visual flex items)
- Tower customization / modular slots (white towers with slottable components)
- Three mining rig variants
- 20 map variant playtesting

---

## Controls

WASD pan/move, scroll zoom, left-click place, right-click cancel/sell or toggle Mining Building, Space start wave, Tab speed (1x/2x/3x), H help, F12 editor, ESC menu

---

## Build & Run

```
cd Godot_TD && dotnet build
# Open project.godot in Godot 4.6, F5
```

---

## References

| Reference | What to borrow |
|---|---|
| Factorio circuit network | Logic gate feel, signal propagation |
| Opus Magnum | Physical machine satisfaction |
| Vampire Survivors | Exponential reward curve, "you already lost" framing |
| Slay the Spire | Run structure, draft, modifiers |
| Defense Grid | Maze-as-first-class-mechanic |
| Malazan Book of the Fallen | BIT voice — T'lan Imass flat affect, ancient weariness |
| Dungeon Crawler Carl | AXIS events, snarky AI antagonist |
| Tron Legacy | Planet 1 visual aesthetic |
| Balatro | Suit/build saving, synergy discovery, hand-building |
| Path of Exile 2 | Relic tradeoffs, self-debuff synergy builds |
| Beyond All Reason | Production-focused RTS, tug-of-war unit streaming |
