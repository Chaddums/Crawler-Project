# Vine Logic TD — Coordination Doc

*Last updated: 2026-03-19 02:00 — for handoff between Claude instances*

---

## Quick Catchup for New Claude

**What is this game?** A programmable-logic tower defense where your vine network IS the maze. Sensors detect enemies, fire signals along connections to turrets. Each planet has a unique visual theme and enemy AI style.

**Current state:** Playable alpha with 2 planets, 6 floors per planet, player-placed Mining Building with Scrap/Magic toggle, multi-surge wave system, commander spawning, data-driven JSON waves, draft system, perk system, meta-perk progression, intro cinematic, real 3D models, and two visual themes (Tron + Scrapyard).

**Design doc:** `JUNKYARD_TD_CONTEXT.md` — canonical design decisions, hierarchy, terminology, resources, characters, magic types. READ THIS FIRST.

**Canonical hierarchy:** `Run > Planet (1-3) > Floor (1-6) > Wave (1-6) > Surge (dynamic) > Enemy`

**Address format:** `P#-F#-W#-S#` in all logs, comments, data.

---

## Active Work Split (2026-03-19)

### Claude A — Visual/Gameplay Feel
- **Dome material system** — finish ground/terrain/asset texture swaps inside dome radius
- **BIT visual polish** — silver-white material, animation split, naruto run

### Claude B — Data Architecture/Systems (THIS SESSION)

**Completed:**
- Terminology renames (VineSpawnGroup→SurgeData, Groups→Surges, Gold→Scrap, Mana→Magic)
- Wave data JSON export (6 files: P1-F1 through P1-F6) with multi-surge pacing
- VineWaveLoader — JSON-first wave loading with hardcoded fallback
- Commander data model + spawn hook in VineWaveManager
- Completion modes (KillAll/Timer/KillThreshold/Hybrid) on VineWaveData
- Economy scaffolding (Scrap + Magic in GameManager, OnMagicChanged event)
- 6 floors per planet with 3 new map layouts (Forge, Labyrinth, Crucible)
- Ported 7 systems from HoldtheLine (FrameBudget, EntityRegistry, frame stagger, march mode, DifficultyScaler, spawn accumulator, BuffDebuffComponent)
- Gameplay rebalance (slower speeds, wider spawns, path preview removed, multi-surge pacing)
- Live player tuning in SignalTuningEditor (persists across floor transitions)
- EditorTestSuite (191 tests, catches orphaned tuning fields)
- Robot Warriors asset textures (TGA conversion for gun_robot.fbx)
- **Mining Building placement** — player-placed, magic type selection popup, Scrap/Magic toggle, exit point follows building, dome repositions, paths recalculate

### Shared Rules
- **Read `JUNKYARD_TD_CONTEXT.md`** before making any design decisions
- **Terminology:** Surge (not group), Commander (not miniboss), Scrap (not gold), Magic (not mana)
- **Address format:** `P#-F#-W#-S#` in logs, comments, data
- **Don't build on Gold system** — it's deprecated (renamed to Scrap everywhere)
- **All tuning in JSON** — never hardcode enemy counts, HP, timing
- **Don't touch Classic TD** unless explicitly asked

---

## Mining Building (Player-Placed)

**How it works now:**
1. "Mining Building" button is first in the HUD build bar — always visible
2. Click → enter placement mode with cyan cylinder ghost preview
3. Left-click any empty cell to place (creates VineHarvester, blocks cell as wall)
4. **Can't start waves** until placed — Space shows "Place Mining Building First!" warning
5. On placement, magic type selection popup appears (Psychic/Power/Environment)
6. After placement, button becomes **toggle** — click to switch Scrap/Magic mode
7. **Right-click** the building on the map also toggles mode
8. Everything follows the building: exit point, enemy paths, BIT spawn, conversion dome, old exit glow removed

**Design doc rules:**
- One building per magic type — one total per combat character, two for non-attacker
- Magic type chosen at placement — locked for the run
- Toggle is the core strategic decision: Scrap (invest in vine nodes) vs Magic (invest in player power)
- Cannot produce both simultaneously

**Key code:** `VinePlacer.StartPlacingMiningBuilding()`, `TryPlaceMiningBuilding()`, `ShowMagicTypeSelection()`

---

## What Claude B Built (2026-03-19)

### Terminology Renames (codebase-wide)
- `VineSpawnGroup` → `SurgeData`, `.Groups` → `.Surges`
- `BonusGold` → `BonusScrap`, `GoldCost` → `ScrapCost`
- `StartingGold` → `StartingScrap` (Constants + all refs)
- `MaxMana/CurrentMana/ManaRegen` → `MaxMagic/CurrentMagic/MagicRegen`
- `OnPlayerManaChanged` → `OnPlayerMagicChanged`
- `GoldCarryover` → `ScrapCarryover`
- All UI labels: "Gold:" → "Scrap:", "Mana" → "Magic"

### Wave/Surge Data Architecture
- `VineWaveLoader.cs` — loads from `Data/Waves/P{planet}-F{floor}.json`, falls back to hardcoded
- `SurgeData` has optional `CommanderData Commander` field
- `VineWaveData` has `WaveCompletionMode CompletionMode` (KillAll/Timer/KillThreshold/Hybrid)
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
  - Floor 4 "Forge" — 2 entries (left/right), exit bottom center, open field
  - Floor 5 "Labyrinth" — 3 entries, exit center, dense wall maze
  - Floor 6 "Crucible" — 4 entries (all cardinal), exit center, boss arena
- 6 JSON wave files with multi-surge pacing (2-4 surges per wave)
- Floor 3: minor boss "Forge Overseer" (HP 400)
- Floor 6: major boss "Apex Protocol" (HP 1200)

### Gameplay Rebalance
- Enemy base speed: 3 → 2
- Player move speed: 4.5 → 3.2
- Hero bot: 8 → 5.5
- Spawn offset: 12 units behind entry for visible approach march
- Path preview line removed (was fake/misleading)
- Every wave has 2-4 surges with staggered timing

### Ported from HoldtheLine (GDScript → C#)
- `FrameBudget.cs` — frame time gating, prevents FPS drops below 30
- `EntityRegistry.cs` — spatial grid (7x5 cells, 16 units), O(1) nearest-entity lookup
- `VineEnemy.cs` — frame stagger + march mode for offscreen enemies
- `DifficultyScaler.cs` + `Data/difficulty_scaling.json` — piecewise scaling (1x→2x→4x→8x)
- `VineWaveManager.cs` — spawn accumulator pattern (opt-in per surge)
- `BuffDebuffComponent.cs` — stacking buffs/debuffs with duration + source tracking

### Mining Building Placement
- Removed auto-placement from `BuildEntryExitVisuals`
- Exit glow disc at original exit point (tagged "ExitGlow", removed on placement)
- `VinePlacer.StartPlacingMiningBuilding()` — cyan cylinder ghost
- `TryPlaceMiningBuilding()` — places harvester, updates exit point, recalculates paths, moves BIT, repositions dome, removes exit glow
- `ShowMagicTypeSelection()` — code-built popup with 3 magic type buttons
- HUD: Mining Building button first in build bar, becomes toggle after placement
- Right-click Mining Building on map toggles Scrap/Magic mode
- Waves blocked until Mining Building placed (warning flash on Start Wave button)

### Editor & Testing
- SignalTuningEditor: Player/Enemy/Harvester sections with live push per-stat
- VinePlayer._Ready() reads from SignalTuningEditor live values (persists across floors)
- EditorTestSuite: 191 tests for all tuning fields
- New GameEvents: OnBuffApplied/Removed, OnDebuffApplied/Removed, OnSurgeStarted/Ended, OnMagicChanged

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
│   ├── Editor/         F12 editor suite
│   ├── Testing/        TestHarness + 7 test suites
│   └── Debug/          BugReportDialog, DebugMenu
├── Data/
│   ├── Waves/          P1-F1.json through P1-F6.json
│   ├── Levels/         floor_1.json through floor_4.json
│   └── difficulty_scaling.json
└── JUNKYARD_TD_CONTEXT.md   Design doc (READ THIS)
```

---

## Game Flow

```
MainMenu → [Planet 1 or 2] → IntroCinematic → VineDraftScreen →
  Floor 1: VineBattle (Gateway, 1 entry, 3 waves) →
    BUILD: Place Mining Building → choose magic type → place vine nodes → Start Wave
    WAVE: Surges spawn from entries, march in, path to Mining Building
    Between waves: toggle Mining Building mode, place more nodes
  → PerkSelect →
  Floor 2-5: same pattern, escalating entries/enemies →
  Floor 3: minor boss "Forge Overseer" →
  Floor 6: VineBattle (Crucible, 4 entries, 3+boss waves) → major boss "Apex Protocol" →
  Victory/Defeat → MainMenu
```

---

## Known Issues / In Progress

- **Hull textures not rendering** — PBR texture PNGs are in `Materials/Hull/` but may need Godot editor restart to import. BitPalette.MakeHullMaterial/MakeSolidMaterial load them via GD.Load — if null, falls back to flat color. Textures: Metal040 (grey plate, dome floor), Metal055C (brushed steel, structures/conversion)
- **Dome floor clipping** — terrain ring objects at dome boundary still clip through. Need to either hide them or raise the dome floor mesh at edges
- **Hardcoded VineWaveRegistry speeds** — fallback data still uses old fast speeds (2.5-5.0). JSON files have correct slow speeds. If JSON loading fails, enemies are too fast
- **ConversionDome compile errors** — 9 pre-existing errors in ConversionDome.cs related to removed fields (_outerBand, _conflictBand, etc.). Game runs fine, Godot ignores them at runtime
- **DifficultyScaler not wired** — registered but only the spawn accumulator reads surge multiplier. Enemy HP/speed/armor scaling not applied to spawned enemies
- **EntityRegistry empty** — registered but towers/enemies don't register/unregister with it yet
- **FrameBudget underused** — only VineEnemy.ShouldProcessAI checks it

## Known Bugs / Active Issues

### Character Editor Animation (Priority — User-Facing)
- **Animations out of order in editor**: The clip segment list (Idle, Run, Attack_R, etc.) in the Characters tab raw timeline doesn't match the actual animation content. When you click ► next to "Run", it may play a different animation. The hardcoded segment boundaries (Idle 0-3.17s, Run 3.17-4.13s, Attack_R 4.13-5.07s, Attack_L 5.07-5.90s, Attack 5.90-6.73s, Death 6.73-7.90s) need to be verified by scrubbing the raw timeline and checking what poses appear at each timestamp. The boundaries were guessed and may be wrong.
- **Fix approach**: Scrub the raw timeline in the editor (F12 > Characters > BIT > Load Raw Timeline), note what animation is at each gap, update the hardcoded boundaries in `VinePlayer.SplitBitAnimations()` AND in `CharacterViewerEditor.AutoDetectSegments()`.
- **Files**: `Scripts/VineLogic/VinePlayer.cs` (line ~700, segments array), `Scripts/Editor/Modules/CharacterViewerEditor.cs` (line ~1560, AutoDetectSegments for BIT)
- **The in-game naruto run WORKS** — the Run clip plays correctly during gameplay sprint. The issue is only in the editor preview.

### Dome Floor Clipping
- Dome floor disc clips through terrain objects (walls, elevated platforms, props). Needs Z-offset or should only render below terrain features.

## Not Yet Implemented

- **Magic Shop** — per-floor deterministic upgrade shop using accumulated Magic
- **3 Characters** — only BIT exists; need 2 combat + 1 non-attacker
- **Non-attacker second Mining Building** — design doc allows 2 buildings for non-attacker
- **Commander behaviors** — only Elite implemented; AuraBuffer/Rally/Assassin are data stubs
- **Reactive commander triggers** — WaveClearTime, PlayerOutOfBase not evaluated
- **Planet 3** — no theme or content
- **Sound** — no audio on enemy spawn, death, tower fire, wave start/complete
- **Wave countdown** — no visual warning before surges start
- **More enemy types** — only 4 (Scrap Rat, Buzz Drone, Rust Hulk, Phase Crawler). Models exist for wire_worm, volt_sprinter, overclock_drone, etc.

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
