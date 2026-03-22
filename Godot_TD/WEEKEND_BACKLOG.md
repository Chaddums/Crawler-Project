# Vine Logic TD — Pivot Backlog

*Goal: Transition from floor-based alpha to continuous extraction loop.*
*Created: 2026-03-21*

---

## Phase 0 — Cleanup (unblocks everything)

Remove dead code and flatten hierarchy. Nothing new can be built cleanly until the floor layer is gone.

| # | Task | Size | Notes |
|---|------|------|-------|
| 0.1 | **Delete Classic TD** | S | `WaveManager`, `WaveData`, `WaveRegistry`, `Battle.tscn`, `BattleScene`, `MapSelect.tscn`, `MapSelectUI` |
| 0.2 | **Delete deprecated systems** | S | `HeroBotController`, `FabricationSystem`, `ScrapManager` |
| 0.3 | **Strip floor references** | M | `CurrentFloor` from GameManager, `FloorComplete` from GamePhase, floor-indexed dispatch in VineMapLayouts, floor-based lookup in VineWaveRegistry, floor-triggered perk select |
| 0.4 | **Address format sweep** | S | All logs, comments, JSON keys, UI: `P#-F#-W#-S#` → `P#-W#-S#` |
| 0.5 | **Terminology sweep** | S | Mana→Materials, Gold/Scrap→Resources, Magic→Materials, SpawnGroup→Surge in any remaining code. Psychic→Chaos. |

---

## Phase 1 — The Run Works (core loop playable)

Drop in, build, waves escalate continuously, you eventually die, extraction score shown.

| # | Task | Size | Notes |
|---|------|------|-------|
| 1.1 | **Continuous wave curve** | L | Rip out floor-based wave sequencing. VineWaveManager loads single escalating sequence per planet. DifficultyScaler finally wired — enemy HP, speed, armor, count scale per-wave. |
| 1.2 | **Dynamic entry points** | M | Wave milestone triggers open new spawn regions on VineGrid. Start 1 entry, gates at thresholds add 2nd, 3rd, 4th. Entry data in JSON per planet. |
| 1.3 | **Exponential extraction curve** | M | Resource accumulation scales with wave depth. Surviving to wave 15 earns dramatically more than wave 8. Core tension: push further or lock in. |
| 1.4 | **Wave milestone system** | M | Replace FloorComplete as hook for: perk selection, map expansion, difficulty jumps, Ascendant spawns. Generic event: `GameEvents.WaveMilestone(int wave)`. |
| 1.5 | **Three mining rig variants** — Built-in turrets (offensive), regenerating shields (defensive), high regen + enemy pushback (sustain). Stub all three for testing. | M | Strategic choice at run start. Different difficulty curves per rig. |
| 1.6 | **Debrief screen** | M | On Spire death: extraction total, wave reached, resources earned. "How far did you push it?" Not win/lose. |
| 1.7 | **Simplify tower entry point** — White towers work by default without signal chains. Start with one tower class, unlock mixed later. | M | Vine logic signal chains become advanced/optional. Biggest gameplay change. |
| 1.8 | **Make 20 map variants and playtest** — Use map editor to test layouts. Open squares, complex pathing, corridors, arenas, asymmetric. Find the fun. | M | Action item from design session. Map design determines game feel. |

---

## Phase 2 — The Meta Works (between-run progression)

Resources from runs feed into persistent layer. Players come back stronger.

| # | Task | Size | Notes |
|---|------|------|-------|
| 2.1 | **Territory unlock system** | M | Deterministic, fixed cost per planet section. Opens farming variants, gates boss runs. JSON-driven. Reuse MetaPerkSave persistence. |
| 2.2 | **Suits system** | L | Serialize successful build state (node placements, material type, upgrades) to save slot. Load at boss run start. Lose suit on death. Needs SuitData, suit inventory UI, load/equip flow. |
| 2.3 | **Node unlock shop** | M | Spend meta resources to add node types to permanent draft pool. Reshaped from VinePerkData. |
| 2.4 | **Boss run mode** | M | Select planet, select suit, start at wave 1 with suit pre-loaded. Boss spawns at wave milestone. Death = suit lost. Victory = planet section cleared + boss reward. |
| 2.5 | **Relic system** — Found in farming runs, persistent inventory, limited boss carry. Visual-only flex items. Negative tradeoff relics for synergy builds (POE2 style). | M | New system. Data-driven JSON definitions. |
| 2.6 | **Tower customization / modular slots** — White towers with slottable components. Adjacent tower synergies. May replace vine logic as primary build depth. | L | Biggest design question: how deep should this go? |

---

## Phase 3 — The Narrative Lives (BIT and AXIS)

The game has a voice. BIT is ancient and tired. AXIS is dismissive.

| # | Task | Size | Notes |
|---|------|------|-------|
| 3.1 | **Character barks (BIT + AXIS)** — BIT sarcasm/futility, AXIS dismissive directives. ~5 lines per 10 waves. Hundreds of variations, no duplicates until all seen. Keep it light. | M | No 3-act structure. No memory bleed. Barks only. |
| 3.2 | **AXIS + BIT dialogue rewrite** | M | Full rewrite of commentary pool. AXIS: corporate, dismissive, performatively urgent. BIT: flat, dry, occasionally devastating. Two distinct voice registers. |

---

## Phase 4 — The Spectacle (Ascendants and late game)

Late game transforms when ancient AIs walk onto your battlefield.

| # | Task | Size | Notes |
|---|------|------|-------|
| 4.1 | **Ascendant spawn system** | M | Reactive trigger (wave threshold + extraction amount). Enemy Ascendant appears, friendly responds. They fight each other, not you. |
| 4.2 | **Inter-Ascendant combat AI** | L | Two AI agents with own targeting, abilities, health pools. Fight resolves independently of player's war. |
| 4.3 | **Map chaos on clash** | M | Ascendant combat destroys terrain, opens corridors, damages player nodes. Uses VineGrid terrain mutation hooks. |
| 4.4 | **Ascendant personalities** | M | Individual designs, motivations, combat styles, map chaos signatures. |

---

## Dependency Chain

```
Phase 0 (cleanup)
  └── Phase 1 (run loop)
        ├── Phase 2 (meta layer)
        │     └── Phase 3 (barks) — can start in parallel with Phase 2
        └── Phase 4 (Ascendants) — needs Phase 1 wave milestones
```

Phase 0 and 1 are the critical path. Phase 2 and 3 can run in parallel. Phase 4 is last and can be cut from a first playable without losing the core.

---

## HAVE (keep as-is or minor edits)

- AXISCommentary — architecture stays, lines need rewriting
- VineNode — 18 node types. Signal chains become advanced/optional. White towers work by default.
- VineWaveManager — surge spawning, completion modes (remove floor refs)
- VineWaveData — SurgeData, CommanderData (rename/reshape)
- VinePlayer — BIT exists, MOBA movement, abilities (rename Mana→Materials)
- VineHarvester — Mining Building toggle works
- VineGrid — grid, heightmap, entry regions, pathfinding hooks
- VinePathfinder — solid
- VineEnemy — factions, frame stagger, march mode
- PlanetTheme / TronTheme / ScrapyardTheme — keep both planets
- MetaPerkTree / MetaPerkSave — persistence architecture reused for suits/territory
- DifficultyScaler — finally gets wired
- FrameBudget, EntityRegistry, BuffDebuffComponent — keep
- VfxFactory, DamageNumber — keep
- TDCamera, BugReportDialog, DebugMenu — keep
- ServiceLocator, GameEvents, StatBlock — keep
- AssetLibrary, CharacterAnimator — keep
- F12 editor suite — keep
- All JSON wave data files — keep, extend
- IntroCinematic — keep, extend

## REMOVE

- CurrentFloor from GameManager
- FloorComplete from GamePhase enum
- Floor-indexed dispatch in VineMapLayouts
- VineWaveRegistry floor-based lookup
- Floor-triggered perk select screen
- Classic TD entirely (WaveManager, WaveData, WaveRegistry, Battle.tscn, BattleScene)
- MapSelect.tscn and MapSelectUI
- HeroBotController
- FabricationSystem
- ScrapManager

---

## Size Legend

- **S** = Small (< 1 hour, single file, straightforward)
- **M** = Medium (1-3 hours, 2-4 files, some design decisions)
- **L** = Large (3+ hours, many files, significant new functionality)
