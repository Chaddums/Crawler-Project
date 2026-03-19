# Vine Logic TD — Coordination Doc

*Last updated: 2026-03-19 — for handoff between Claude instances*

---

## Quick Catchup for New Claude

**What is this game?** A programmable-logic tower defense where your vine network IS the maze. Sensors detect enemies, fire signals along connections to turrets. Each planet has a unique visual theme and enemy AI style.

**Current state:** Playable alpha with 2 planets, 6 floors per planet, multi-surge wave system, commander spawning, data-driven JSON waves, draft system, perk system, meta-perk progression, intro cinematic, real 3D models, and two visual themes (Tron + Scrapyard).

**Design doc:** `JUNKYARD_TD_CONTEXT.md` — canonical design decisions, hierarchy, terminology, resources, characters, magic types. READ THIS FIRST.

**Canonical hierarchy:** `Run > Planet (1-3) > Floor (1-6) > Wave (1-6) > Surge (dynamic) > Enemy`

**Address format:** `P#-F#-W#-S#` in all logs, comments, data.

---

## Active Work Split (2026-03-19)

### Claude A — Visual/Gameplay Feel
- **Mining Building** — evolve harvester into Mining Building with Scrap/Magic toggle. Conversion dome is the visual foundation. Toggle changes dome VFX color/intensity.
- **Dome material system** — finish ground/terrain/asset texture swaps inside dome radius
- **BIT visual polish** — silver-white material, animation split, naruto run

### Claude B — Data Architecture/Systems (THIS SESSION — completed items below)
- **DONE:** Terminology renames (VineSpawnGroup→SurgeData, Groups→Surges, Gold→Scrap, Mana→Magic, Chaos→Psychic)
- **DONE:** Wave data JSON export (6 files: P1-F1 through P1-F6)
- **DONE:** VineWaveLoader — JSON-first wave loading with hardcoded fallback
- **DONE:** Commander data model + spawn hook in VineWaveManager
- **DONE:** Completion modes (KillAll/Timer/KillThreshold/Hybrid) on VineWaveData
- **DONE:** Economy scaffolding (Scrap + Magic in GameManager, OnMagicChanged event)
- **DONE:** 6 floors per planet (was 3) with 3 new map layouts (Forge, Labyrinth, Crucible)
- **DONE:** Ported 7 systems from HoldtheLine (FrameBudget, EntityRegistry, frame stagger, march mode, DifficultyScaler, spawn accumulator, BuffDebuffComponent)
- **DONE:** Gameplay rebalance (slower speeds, wider spawns, path preview removed, multi-surge pacing)
- **DONE:** Live player tuning in SignalTuningEditor (persists across floor transitions)
- **DONE:** EditorTestSuite (191 tests, catches orphaned tuning fields)
- **DONE:** Robot Warriors asset textures (TGA conversion for gun_robot.fbx)

### Shared Rules
- **Read `JUNKYARD_TD_CONTEXT.md`** before making any design decisions
- **Terminology:** Surge (not group), Commander (not miniboss), Scrap (not gold), Magic (not mana)
- **Address format:** `P#-F#-W#-S#` in logs, comments, data
- **Don't build on Gold system** — it's deprecated (renamed to Scrap everywhere)
- **All tuning in JSON** — never hardcode enemy counts, HP, timing
- **Don't touch Classic TD** unless explicitly asked

---

## What Claude B Built (2026-03-19)

### Terminology Renames (codebase-wide)
- `VineSpawnGroup` → `SurgeData`, `.Groups` → `.Surges`
- `BonusGold` → `BonusScrap`, `GoldCost` → `ScrapCost`
- `StartingGold` → `StartingScrap` (Constants + all refs)
- `MaxMana/CurrentMana/ManaRegen` → `MaxMagic/CurrentMagic/MagicRegen`
- `OnPlayerManaChanged` → `OnPlayerMagicChanged`
- `MagicType.Chaos` → `MagicType.Psychic` (design doc term, but other session reverted to Chaos)
- `GoldCarryover` → `ScrapCarryover`
- All UI labels: "Gold:" → "Scrap:", "Mana" → "Magic"

### Wave/Surge Data Architecture
- `VineWaveLoader.cs` — loads from `Data/Waves/P{planet}-F{floor}.json`, falls back to hardcoded
- `SurgeData` has optional `CommanderData Commander` field
- `VineWaveData` has `WaveCompletionMode CompletionMode` (KillAll/Timer/KillThreshold/Hybrid)
- `VineWaveData` has `CompletionTimer` and `CompletionKillCount` for non-KillAll modes
- `FloorScaler` class for layered multipliers (HP, speed, count, scrap value)
- JSON DTOs in VineWaveLoader handle deserialization with enum string parsing

### Commander System
- `CommanderData` class: spawn condition + behavior as separate fields
- `CommanderSpawnType`: Scripted, Random, Reactive
- `CommanderBehavior`: Elite, AuraBuffer, Rally, Assassin
- `VineWaveManager.SpawnCommander()` evaluates spawn conditions, spawns as boss-flagged enemy
- F3 waves 2-3 have Iron Warden commanders (Scripted/Elite)

### 6 Floors Per Planet
- `Constants.VINE_FLOOR_COUNT = 6`
- 3 new map layouts in `VineMapLayouts.cs`:
  - Floor 4 "Forge" — 2 entries (left/right), exit bottom center, open field with platforms
  - Floor 5 "Labyrinth" — 3 entries, exit center, dense wall maze
  - Floor 6 "Crucible" — 4 entries (all cardinal), exit center, boss arena with inner ring
- 6 JSON wave files with multi-surge pacing (2-4 surges per wave)
- Hardcoded fallback data for all 6 floors in VineWaveRegistry
- Floor 3: minor boss "Forge Overseer" (HP 400)
- Floor 6: major boss "Apex Protocol" (HP 1200)

### Gameplay Rebalance
- Enemy base speed: 3 → 2 (VINE_ENEMY_BASE_SPEED)
- Player move speed: 4.5 → 3.2 (VINE_PLAYER_MOVE_SPEED)
- Hero bot: 8 → 5.5
- Difficulty speed ramp: 3% → 2% per wave
- Spawn offset: 12 units behind entry (VINE_SPAWN_OFFSET) — enemies march in visibly
- Path preview line removed (was fake/misleading)
- Every wave has 2-4 surges with staggered timing (scout → main → flank → cleanup)

### Ported from HoldtheLine (GDScript → C#)
- `FrameBudget.cs` — frame time gating, prevents FPS drops below 30
- `EntityRegistry.cs` — spatial grid (7x5 cells, 16 units), O(1) nearest-entity lookup
- `VineEnemy.cs` — frame stagger (`ShouldProcessAI()`) + march mode for offscreen enemies
- `DifficultyScaler.cs` + `Data/difficulty_scaling.json` — piecewise scaling (1x→2x→4x→8x)
- `VineWaveManager.cs` — spawn accumulator pattern (opt-in per surge via `UseAccumulator`)
- `BuffDebuffComponent.cs` — stacking buffs/debuffs with duration + source tracking

### Editor & Testing
- SignalTuningEditor: Player (BIT) section with live push per-stat (move speed, attack speed, etc.)
- VinePlayer._Ready() reads from SignalTuningEditor live values (persists across floor transitions)
- EditorTestSuite: 191 tests, catches orphaned static fields in SignalTuningEditor
- New GameEvents: OnBuffApplied, OnBuffRemoved, OnDebuffApplied, OnDebuffRemoved, OnSurgeStarted, OnSurgeEnded, OnMagicChanged

### Economy Scaffolding
- `GameManager.CurrentMagic`, `AddMagic()`, `SetMagic()`, `SelectedMagicType`
- `OnMagicChanged` event in GameEvents
- Magic reset in `StartVineRun()`
- `MiningMode` enum (Scrap/Magic) + `OnMiningModeChanged` event (added by Claude A)

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
│   │   ├── VineConnection.cs        Vine connections + signal travel + power checking
│   │   ├── VinePathfinder.cs        A* with ghost mode + terrain cost + slope penalty
│   │   ├── VineWaveData.cs          SurgeData, VineWaveData, CommanderData, FloorScaler, CompletionMode
│   │   ├── VineWaveLoader.cs        JSON-first wave loading with hardcoded fallback
│   │   ├── VineWaveManager.cs       Surge spawning, completion modes, commander spawning
│   │   ├── VineMapLayouts.cs        6 floor layouts + data-driven JSON support
│   │   ├── DifficultyScaler.cs      Piecewise difficulty scaling from JSON
│   │   ├── VineBattleScene.cs       Battle orchestrator + environment dressing
│   │   ├── VineHUD.cs               HUD with draft-aware build bar
│   │   ├── VinePlacer.cs            Node placement with preview
│   │   ├── VinePlayer.cs            BIT — MOBA abilities, dome material swap
│   │   ├── VineHarvester.cs         Mining Building (Scrap/Magic toggle)
│   │   ├── ConversionDome.cs        Fog-ring VFX + dome radius + material swap
│   │   ├── BitPalette.cs            Canonical BIT/AXIS palette
│   │   └── AssetLibrary.cs          Asset loading, scaling, verification
│   ├── Editor/         F12 editor suite (NodeBalance, WaveEditor, SignalTuning, AssetSandbox, LevelEditor, CharacterViewer)
│   ├── Testing/        TestHarness + 7 test suites (content, editor, ui, gameplay, visual, integration)
│   └── Debug/          BugReportDialog, DebugMenu
├── Data/
│   ├── Waves/          P1-F1.json through P1-F6.json (wave/surge definitions)
│   ├── Levels/         floor_1.json through floor_4.json (level editor layouts)
│   └── difficulty_scaling.json
├── Scenes/             Main, MainMenu, IntroCinematic, VineDraft, VineBattle, VinePerkSelect, MetaPerk, LevelEditor
├── Models/             Imported 3D models (LilRobot, gun_robot, Robot Warriors, enemies, KitBash)
└── JUNKYARD_TD_CONTEXT.md   Design doc (READ THIS)
```

---

## Key Systems

### Signal Chain
`Sensor detects enemy → fires signal (power=3-4) → travels along vine → hits effect node (costs 1 power, activates + propagates) → next effect → ... → power exhausted`

### Wave/Surge Flow
`VineWaveLoader.LoadFloorWaves(planet, floor)` → JSON first, hardcoded fallback → `VineWaveManager.StartWave()` → spawns surges with staggered timing → completion mode check (KillAll/Timer/KillThreshold/Hybrid) → `CompleteWave()` → floor progression

### Performance Systems (from HoldtheLine port)
- `FrameBudget.HasBudget()` — gates expensive work to maintain 30 FPS
- `EntityRegistry.GetNearest()` — spatial grid for O(1) proximity queries
- `VineEnemy.ShouldProcessAI()` — frame stagger distributes AI across frames
- `VineEnemy._marchMode` — cheap direct movement for offscreen enemies
- `DifficultyScaler` — piecewise scaling (base → 2x at 8min → 4x at 14min → 8x at 20min)

### Planet Themes
- `PlanetTheme.Current` — static reference, set in `GameManager.StartVineBattle()`
- Planet 1: `TronPlanetTheme` — dark + cyan outlines
- Planet 2: `ScrapyardPlanetTheme` — PBR rust/metal
- **Player infrastructure uses BitPalette, NOT PlanetTheme** (same look on every planet)

### Floor Progression
- 6 floors per planet, each with own map layout and wave set
- Floor 3: minor boss wave (4th wave). Floor 6: major boss wave (4th wave).
- Between floors: perk selection, scrap carries over
- `GameManager.CurrentFloor` tracks progress

---

## Game Flow

```
MainMenu → [Planet 1 or 2] → IntroCinematic → VineDraftScreen →
  Floor 1: VineBattle (Gateway, 1 entry, 3 waves) → PerkSelect →
  Floor 2: VineBattle (Conduit, 2 entries, 3 waves) → PerkSelect →
  Floor 3: VineBattle (Arena, 3 entries, 3+boss waves) → PerkSelect →
  Floor 4: VineBattle (Forge, 2 entries, 3 waves) → PerkSelect →
  Floor 5: VineBattle (Labyrinth, 3 entries, 3 waves) → PerkSelect →
  Floor 6: VineBattle (Crucible, 4 entries, 3+boss waves) → Victory/Defeat →
MainMenu
```

---

## Not Yet Implemented

- **Mining Building** — player-placed, Scrap/Magic toggle (VineHarvester has toggle code but auto-places at exit)
- **Magic Shop** — per-floor deterministic upgrade shop using accumulated Magic
- **3 Characters** — only BIT exists; need 2 combat + 1 non-attacker
- **Commander behaviors** — only Elite implemented; AuraBuffer/Rally/Assassin are stubs
- **Reactive commander triggers** — WaveClearTime, PlayerOutOfBase not evaluated
- **Planet 3** — no theme or content
- **Robot Warriors model** — gun_robot.fbx imported with textures but not wired as playable

---

## Controls

WASD pan/move, scroll zoom, left-click place, right-click cancel/sell, Space start wave, Tab speed (1x/2x/3x), H help, F12 editor, ESC menu, Ctrl+Shift+B bug report, Ctrl+Shift+K kill all, Ctrl+Shift+G add scrap

---

## Build & Run

```
cd Godot_TD && dotnet build
# Open project.godot in Godot 4.6, F5

# Run editor tests:
& "C:\Program Files (x86)\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --path "C:\Users\Stu\GitHub\Crawler_Project\Godot_TD" -- --test-harness --suite=editor
```
