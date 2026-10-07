# Vine Logic TD — Project Reference

*Single source of truth for Claude instances and project context. Last updated: 2026-10-06.*

---

## What Is This Game?

A programmable-logic tower defense where your vine network IS the maze. Sensors detect enemies, fire signals along connections to turrets. You are BIT — an ancient AI cleanup script deployed by AXIS, a dismissive corporation that acquired BIT without understanding what it holds. You always lose. The question is how much you extract before you fall.

**One-liner:** "Mine everything you can before they take it all."

**Core reframe:** Not survival — extraction. Not pass/fail — optimization. Resources scale exponentially with wave depth. Every run extracts something. No run is wasted.

**Status:** Continuous extraction loop implemented. Core systems (vine network, mining building, BIT, wave/surge spawning, continuous wave curve, extraction scaling, milestone events, 2 planet themes) are solid. Floor layer removed. S4 meta layer (territory unlocks, suits, boss runs) implemented — data persistence, game flow, and UI screens built. Between-runs loop complete: debrief screen (resource transfer + suit capture) → meta hub (command center with territory/suits/relics/start-run nav) → sub-screens → next run. Scene transitions use TransitionManager (fade in/out). Ascendants, narrative rewrite, and node unlock shop are greenfield.

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
- **Stitch CEF UI** — primary screens use Stitch HTML rendered via godot-cef (see `docs/STITCH_WORKFLOW.md`). Code-built fallback for when CEF is unavailable. Some screens still code-built CanvasLayer pending CEF rewrite.
- **Singletons:** `ServiceLocator` for services, `GameEvents` static event bus
- **Constants** in `Constants.cs` — no magic numbers in code
- **All tuning in JSON** — never hardcode enemy counts, HP, timing
- **Colors:** `BitPalette.cs` for player/harvester/tower. `PlanetTheme.Current` for enemies/terrain
- **Node types** defined in `Enums.cs`, data in `VineNodeData.cs`
- **Characters** (BIT, enemies) are rigged FBX driven by `CharacterAnimator`. Models without usable clips get a procedural gait/bob, deaths without a death clip get a procedural roll, skin offsets are normalised at load, and `AssetLibrary.GetFacingYawOffset` turns models not authored facing +Z. Check with the `anim` / `anim-sheets` suites.
- **Planet looks** live on `PlanetTheme` (`ConfigureEnvironment`, `ConfigureLights`, accents, converted-ground colour). Scrapyard = dusk (low orange sun, cyan player accent); Grid Prime = two-tone (cyan world, orange player, red/magenta enemies). Player-owned things (towers, converted decor) wear a fresnel rim in the player accent via `BitPalette.ApplyAccentRim` (a `MaterialOverlay`, so imported materials stay untouched).
- **Towers:** kit models (KB3D) are recentred on their footprint at load (`AssetLibrary.RecenterFootprint`; the GLBs keep props at their kit-layout positions, tens of units from origin). Towers without a kit model use `TowerMeshes`. A placed node's origin is `NODE_ORIGIN_HEIGHT` above the ground; models are offset down by it.
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
│   │                   TransitionManager (autoload — fade transitions),
│   │                   FrameBudget, EntityRegistry, BuffDebuffComponent,
│   │                   TerritoryData, SuitData, SuitManager
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
│   │   ├── ShieldWall.cs            Energy barrier Node3D (procedural mesh, pulse, collapse VFX)
│   │   ├── ShieldWallManager.cs     Shield wall lifecycle (time triggers, BreakWall API)
│   │   ├── ShieldWallData.cs        Shield wall config + trigger type enum
│   │   └── AssetLibrary.cs          Asset loading, scaling, verification
│   ├── Camera/         TDCamera (flyover, orbit, shake, WASD pan, player-follow)
│   ├── Commentary/     AXISCommentary (rewriting for BIT voice)
│   ├── UI/             MainMenuUI (CEF title + planet select via URL swap),
│   │                   MetaHubScreen (CEF meta hub), DebriefScreen (CEF debrief),
│   │                   RelicInventoryScreen (CEF), LoadoutsScreen, LoadoutSave,
│   │                   TerritoryScreen (code-built), BossConfirmScreen (code-built)
│   ├── Editor/         F12 editor suite
│   ├── Testing/        TestHarness + 7 test suites
│   └── Debug/          BugReportDialog, DebugMenu
├── ui/                 Stitch HTML screens (title/, code.html, meta-hub/, debrief/,
│                       relic-inventory/)
├── Data/
│   ├── Waves/          JSON wave data per planet (P1.json — 20 waves, continuous)
│   ├── Levels/         Map layout JSON
│   ├── territory.json  Territory section definitions per planet
│   └── difficulty_scaling.json
└── docs/archived/      Pre-pivot planning docs
```

---

## Game Flow (Post-Pivot Target)

```
MainMenu → [Planet Select] → Territory Map (unlock sections, view suits) →
  Farming Run:
    IntroCinematic → VineDraft (choose role) →
    Place Mining Building → choose material type →
    Wave loop (continuous, escalating):
      Build phase → Wave with surges → Build phase → next wave
      Wave milestones trigger: perk selection, new entry points, map expansion
    Spire destroyed → 2s delay → Debrief (DebriefScreen) → Save Suit (optional, max 3 slots) →
    Continue → Meta Hub (MetaHubScreen) →
  Boss Run (from Territory Map):
    Select unlocked boss section → Select suit → Confirm ("RISK IT") →
    Skip draft → suit towers pre-placed on grid → waves until boss_wave →
    Win → section cleared, bonus resources
    Lose → suit DESTROYED
  Meta Layer:
    Spend extracted resources → Territory unlocks →
    Territory gates boss sections → boss runs need suits →
  Next Run (more options, clearer target)
```

### Difficulty Via Directionality (Shield Wall System)
- Game starts with West entry open, N/E/S blocked by Shield Walls
- Shield walls are visible energy barriers with procedural mesh + pulse animation
- Default trigger: time-based milestones (5min / 10min / 15min)
- Flexible trigger system: `ShieldWallManager.BreakWall(direction)` callable by any system
- Trigger types: `TimeMilestone`, `WorldObject`, `UIPrompt`, `Scripted`, `Manual`
- When a wall breaks: collapse VFX, entry region activates, pathfinder recalculates, HUD announces
- Entry regions have `Active` flag — VineWaveManager and VinePathfinder only use active regions
- Shield wall configs defined in level JSON (`shieldWalls` array) or hardcoded fallback
- More entries = more enemies = more resources to extract (opportunity, not just threat)
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

## Meta Layer (S4 — Implemented)

### Territory
- Deterministic planet section unlocks, fixed cost, no RNG
- Data: `Data/territory.json` — sections per planet with id, cost, requires, map_variants, gates_boss, boss_wave
- Persistence: `TerritorySave` → `user://territory.json` (unlocked sections, cleared bosses, total spent)
- Loader: `TerritoryLoader` with `IsUnlocked()`, `CanUnlock()`, `TryUnlock()`, `GetBossSection()`
- UI: `TerritoryScreen.cs` — code-built CanvasLayer with planet tabs, section cards, unlock/boss buttons
- Gates boss runs and opens new farming map variants

### Boss Run Mode
- `RunMode.BossRun` in `GameManager` — high-stakes mode
- Flow: Territory → select boss section → select suit → BossConfirmScreen → StartBossRun()
- Skips draft screen, applies suit towers directly to grid via `SuitManager.ApplySuit()`
- `VineWaveManager.CheckBossWaveTrigger()` fires `OnBossDefeated` at `boss_wave` milestone
- Win: section cleared permanently, bonus resources awarded
- Lose: equipped suit destroyed via `SuitManager.DestroySuit()`
- UI: `BossConfirmScreen.cs` — suit preview, warning, suit selector, confirm/cancel

### Suits
- Serialize a successful build to meta storage (max 3 slots, `Constants.MAX_SUIT_SLOTS`)
- Data: `SuitSaveData` (name, role, planet, material, nodes list, consumed flag)
- `SuitNodeEntry` stores grid position + VineNodeType + slotted TowerComponentTypes
- `SuitManager`: static manager with `CaptureSuit()`, `ApplySuit()`, `DestroySuit()`, `GetAvailableSuits()`
- Persistence: `user://suits.json`
- Displayed in `LoadoutsScreen.cs` suits section (above existing loadout grid)
- Lose the suit if you die on the boss run

### Node Unlocks (NOT YET IMPLEMENTED)
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

- **DifficultyScaler, EntityRegistry, FrameBudget are never instantiated** — every `ServiceLocator.TryGet` for them returns false. Procedural-wave escalation lives in `VineWaveLoader.GenerateWave` instead.
- **TypeSensor triggers on ALL enemies** — no faction filter (sensors are no longer in the build roster)
- **Planet 2 has no wave data** — falls back to P1
- **No music** — only SFX and ambient
- **Stale texture imports:** `Materials/Scrapyard/Textures/` has `.import` files for Ground031 and Metal042A but not the PNGs; loading them logs engine errors, so nothing references them.
- **Kit textures are huge in VRAM:** most KB3D textures are 4096x4096 imported with `compress/mode=0` (uncompressed RGBA8 plus mipmaps, about 89 MB each). Models/Turrets alone is about 9 GB if every texture loads; one battle with three turret types needed several GB. `compress/mode=2` plus `process/size_limit=1024` cut a Grid Prime battle from over 4.7 GB of GPU memory to about 330 MB in testing.
- **VineWaveRegistry fallback uses old speeds** — JSON has correct values

### Resolved

- **Animation pass (2026-10):** the Scavenger's model ships with an empty clip and slid along frozen (now a procedural trot); the Swarm drone rendered 2 units above where it was grounded (skin offset); Brute/Ghost deaths played Idle (now their power-down); Hit looped via Stunned and restarted on every tower hit; a hit flash left enemies glowing white for life; the Scavenger walked sideways. Guarded by `anim`.
- **Perk Tree unreachable (2026-10):** nothing linked to it and no points were ever awarded. Now in the Command Center; milestones.json `metaPoints` pays a point the first time each milestone is reached per planet; tree has a reset. Scrapyard had no milestones at all (no perk picks); it now uses planet 1's.
- **Scrapyard ground:** a flat 200x200 plane at y=0 cut through the field's valleys; replaced by `VineGrid.BuildOuterGround` (apron easing the edge heights down, shared dome material). Junk ring, debris and lamps stay off the field.
- **Towers invisible / black monoliths on the field (2026-10):** KB3D towers rendered about 70 units off their cells (only the HP bar showed) and Grid Prime's background turrets, mesas and ridges (laid out for a 40x28 field) stood on the 80x48 field. Models are recentred and `VineBattleScene.PruneDressingFromField` frees dressing that reaches the field. Tesla Coil, Flak Battery, Scatter Cannon and Barrier Wall were bare cubes; now `TowerMeshes`. Towers hovered 0.5 above the ground.
- **Hit flashes stuck (2026-10):** enemies and towers kept the flash colour after the first hit (only the energy was reset). `HitFlash` restores the material exactly.

- **Scrapyard (P2) loaded as a flat haze (2026-10):** the Conversion Dome recolored every mesh whose origin was inside its radius, including the grid-centered 120x120 haze layers and ground plane, turning them into opaque metal sheets over the map. Now only meshes whose whole footprint fits inside the dome, and that are not see-through, are converted. Guarded by the `planets` suite.
- **HUD top-right overlap:** shield-wall and next-wave panels started at y=10, on top of the top bar's Speed/Help labels; they now start below the bar. Build bar costs read "r" (Resources), not "g".
- **Memory leaks that ran headless/autoplay runs out of memory (2026-10)** — ConversionDome rebuilt 17 meshes every frame (now cached, rebuilt only when radius/position/colors/terrain change); every placement/sale rebuilt every VineConnection's meshes (now one in-place recolor per frame); transient VFX allocated a mesh + material per hit/trail dot that the C# wrapper kept alive until GC (now shared via `VfxCache`, faded with `GeometryInstance3D.Transparency`). Guarded by the `perf` suite.
- **Tron fog-bank shader never compiled** — read `INSTANCE_CUSTOM` in `fragment()`; now passed through a varying. Guarded by `health/battle_load_errors`.
- **Input** — right-click cancel also sold the tower under the cursor; Tab toggled speed twice per press; F11 opened the level editor mid-run. Guarded by the `input` suite.
- **Materials mode unreachable** — the material picker only followed manual Mining Building placement; the Spire is auto-placed. Picker now opens when the intro ends.
- **Orphaned PlanetSelectScreen** — `PlanetSelectScreen.cs` and `PlanetSelect.tscn` deleted. `MainMenuUI` handles both title and planet select screens via CEF URL swap. `GameManager.StartPlanetSelect()` sets `MainMenuUI.StartOnPlanetSelect` flag and loads MainMenu scene. `SCENE_PLANET_SELECT` constant removed. Back button added to planet select HTML. Settings button shows toast overlay.
- **Grunt Mech (decoy_unit.fbx)** — Actually `Robots_Grunt.FBX` from InvisGun Hero 2016 pack (3ds Max 2014). Texture: `GRUNT_red.png`. Was white/untextured (PNG gitignored), tracks appeared misaligned (`root_scale=100` distortion). Fixed: gitignore whitelist for `Godot_TD/Models/**/*.png`, `root_scale=1.0`, `materials/extract=1`. Constant renamed `ENEMY_DECOY` → `ENEMY_GRUNT_MECH`. FBX filename unchanged to avoid reimport churn. **Hierarchy note:** `totalControl` has rot=(-90,0,0) converting Z-up to Y-up. Track transforms under `leftControl`/`rightControl` are symmetric — do NOT adjust Y positions (local Y = world Z in this model). Track alignment is handled entirely by the `root_scale=1.0` import fix.

---

## Not Yet Implemented

- Continuous wave curve (replacing floor-based progression)
- ~~Dynamic entry points at wave gates~~ (DONE — Shield Wall system with flexible triggers)
- Exponential extraction resource curve
- Wave milestone system (perks, map expansion, Ascendant triggers)
- ~~Debrief/extraction score screen~~ (DONE — DebriefScreen.cs + ui/debrief/index.html, resource transfer, suit capture prompt)
- ~~Territory unlock system~~ (DONE — S4: TerritoryData, TerritoryScreen, persistence)
- ~~Suits system~~ (DONE — S4: SuitData, SuitManager, LoadoutsScreen integration)
- ~~Boss run mode~~ (DONE — S4: GameManager.StartBossRun, BossConfirmScreen, VineWaveManager boss trigger)
- ~~Scene transitions~~ (DONE — TransitionManager autoload, all scene changes use fade transitions)
- ~~Meta Hub~~ (DONE — MetaHubScreen.cs + ui/meta-hub/index.html, command center between runs)
- ~~Suit capture UI~~ (DONE — integrated into DebriefScreen, post-farming-run save prompt)
- Node unlock shop
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
- Suit detail view (left: 3 suit slots, right: grid viz + stats + relic equip)
- Settings screen
- Territory CEF rewrite (replace code-built version with Stitch HTML)
- Boss Confirm CEF rewrite (replace code-built version with Stitch HTML)
- TowerSlotSystem serialization in suit capture (currently captures empty components)

---

## Controls

WASD pan/move, scroll zoom, left-click place, right-click cancel/sell or toggle Mining Building, Space start wave, Tab speed (1x/2x/3x), H help, F11 fullscreen, F12 editor, ESC menu. Debug builds: Ctrl+F11 level editor (outside a run).

---

## Build & Run

```
cd Godot_TD && dotnet build
# Open project.godot in Godot 4.6, F5
```

## Testing (headless)

```
godot --headless --path . -- --test-harness --suite=<name> --request-id=<id>
# suites: bvt content editor ui gameplay maps relics flow perf planets maze input anim integration visual all
# results: test-reports/results/<id>.json ; unknown suite names fail
godot --headless --path . -- --autoplay --config qa/configs/turret_spam.json
# reports: autoplay-reports/<timestamp>_<strategy>_<role>/report.json (errors, peak objects/memory)
```

`anim` = every animated character built the way the game builds it: the clip each state plays, loops, moving tracks, no root drift or skin offset, a visible death, one-shots that don't loop, front legs ahead when walking (`anim-sheets` also renders contact sheets and facing shots to `test-reports/anim/`, needs a display). `perf` = per-frame/per-edit allocation guards + no shader/script errors on battle load. `planets` = a real battle on every planet via its first territory site: no load errors, nothing opaque between camera and grid, HUD panels clear of the top bar. `maze` = enemies never stand inside solid nodes. `input` = real viewport input routing (Tab, right-click, F11, material picker).

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
